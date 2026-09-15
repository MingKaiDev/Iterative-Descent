using UnityEngine;

/// <summary>
/// Combat DDA controller. Evaluates player combat performance after each encounter
/// (last active enemy killed) and updates CurrentTier for EnemyDirector to consume.
///
/// Starts at Tier 1 (Easy) so new players get a gentler first encounter.
///
/// Signal weights:
///   [35%] Accuracy          — ShotsLanded / ShotsFired, pooled across all three weapons.
///                            2026-09: pistol and rifle each call NotifyShotFired() once
///                            per trigger pull and NotifyShotLanded() once immediately on
///                            a hit (single projectile, binary hit/miss -- see
///                            ShotgunPellet.notifyDDAOnHit). The shotgun also calls
///                            NotifyShotFired() once per trigger pull, but a shot only
///                            counts as NotifyShotLanded() once at least
///                            ShotgunController.pelletHitFractionForDDA (default 40%) of
///                            that shot's pellets connect -- see ShotgunController's
///                            PelletVolley tally, which resolves once every pellet in
///                            the shot has hit or expired.
///   [30%] Normalised health — CurrentHP / MaxHP at encounter end
///   [20%] Combat time       — InverseLerp(fastTime, slowTime, duration), inverted so fast = 1
///   [15%] Normalised ammo   — InverseLerp(minAmmoCount, ammoCombatSoftCap, mag + spare)
///                            at encounter end. 2026-09 revamp: uses a floor
///                            (PlayerCombat.minAmmoCount) instead of raw zero, so a
///                            player sitting on a handful of rounds isn't scored as
///                            meaningfully different from a player with none.
///
/// Partial data: if a signal source is unavailable (e.g. no shots fired yet),
/// that signal is excluded and the remaining weights are re-normalised, matching
/// the puzzle DDA approach.
///
/// Player death (PlayerHealth.OnPlayerDied) is handled separately from the normal
/// per-encounter evaluation above: a death does NOT wait for OnEncounterEnd (which
/// may never fire if the player died before the last enemy was killed). Instead it
/// forces a hard, heavily-weighted low raw score straight into the smoothing step
/// -- see OnPlayerDied() -- so dying is a strong, immediate tier-drop signal rather
/// than something that only shows up diluted inside the next encounter's average.
///
/// Output: CurrentScore [0,1] + CurrentTier [0-4] via Debug.Log.
/// Attach to the same persistent GameManager GameObject as PlayerMetricsTracker.
/// </summary>
public class CombatDDAController : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static CombatDDAController Instance { get; private set; }

    // ── Inspector: Smoothing ───────────────────────────────────────────────
    [Header("Evaluation")]
    [Tooltip("Score smoothing — 0 = instant, 0.9 = very gradual.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float scoreSmoothing = 0.5f;

    // ── Inspector: Signal Weights ──────────────────────────────────────────
    [Header("Signal Weights")]
    [Range(0f, 1f)][SerializeField] private float accuracyWeight = 0.35f;
    [Range(0f, 1f)][SerializeField] private float healthWeight   = 0.30f;
    [Range(0f, 1f)][SerializeField] private float timeWeight     = 0.20f;
    [Range(0f, 1f)][SerializeField] private float ammoWeight     = 0.15f;

    // ── Inspector: Time Thresholds ─────────────────────────────────────────
    [Header("Combat Time Thresholds (seconds)")]
    [Tooltip("Encounter duration considered 'fast' — kills at or under this time score 1.0 on the time signal.")]
    [SerializeField] private float fastCombatTime = 15f;
    [Tooltip("Encounter duration considered 'slow' — kills at or over this time score 0.0 on the time signal.")]
    [SerializeField] private float slowCombatTime = 90f;

    // ── Inspector: Death Penalty ───────────────────────────────────────────
    [Header("Death Penalty")]
    [Tooltip("Raw score forced in on player death, bypassing the weighted signals above entirely " +
             "(a death is a hard failure regardless of how accuracy/health/time/ammo looked up to " +
             "that point). 0 = worst possible score.")]
    [Range(0f, 1f)]
    [SerializeField] private float deathPenaltyScore = 0f;
    [Tooltip("Smoothing applied to the death penalty specifically -- deliberately lower than the " +
             "normal Score Smoothing above so a death lands as an immediate, large tier drop instead " +
             "of being eased in the same way an ordinary encounter result is.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float deathSmoothing = 0.2f;

    // ── Public State ───────────────────────────────────────────────────────
    public float CurrentScore { get; private set; } = 0.30f; // mid Tier 1
    public int   CurrentTier  { get; private set; } = 1;     // Easy (default start)
    public float LastRawScore { get; private set; } = 0.30f;

    public static readonly string[] TierNames =
        { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

    // ── Events ───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever the combat DDA tier changes value (0-4).
    /// Subscribe in OnEnable, unsubscribe in OnDisable to avoid leaks.
    /// </summary>
    public static event System.Action<int> OnCombatTierChanged;

    // ── Cached References ──────────────────────────────────────────────────
    private PlayerHealth _playerHealth;
    private PlayerCombat _playerCombat;

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Cache player components — safe in Start() since persistent GameManager
        // initialises before scene objects. Re-cache if null on evaluate (scene change safety).
        _playerHealth = FindFirstObjectByType<PlayerHealth>();
        _playerCombat = FindFirstObjectByType<PlayerCombat>();
    }

    private void OnEnable()
    {
        PlayerMetricsTracker.OnEncounterEnd += OnEncounterEnd;
        PlayerHealth.OnPlayerDied += OnPlayerDied;
    }

    private void OnDisable()
    {
        PlayerMetricsTracker.OnEncounterEnd -= OnEncounterEnd;
        PlayerHealth.OnPlayerDied -= OnPlayerDied;
    }

    // ── Event Handlers ─────────────────────────────────────────────────────

    private void OnEncounterEnd()
    {
        // Re-cache if null (handles scene reloads)
        if (_playerHealth == null) _playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (_playerCombat == null) _playerCombat = FindFirstObjectByType<PlayerCombat>();

        Evaluate();
    }

    /// <summary>
    /// Player died. This is scored on its own -- it does NOT go through Evaluate()'s
    /// weighted-signal path, because OnEncounterEnd may never fire for this encounter
    /// at all (the player can die before the last enemy is killed, in which case the
    /// normal per-encounter evaluation simply never triggers). A death is treated as
    /// the strongest possible negative signal: deathPenaltyScore is forced straight in
    /// as the raw score, blended with deathSmoothing (deliberately low, so the drop is
    /// large and immediate rather than eased in like a normal encounter result).
    /// </summary>
    private void OnPlayerDied()
    {
        float rawScore = deathPenaltyScore;
        LastRawScore = rawScore;
        CurrentScore = Mathf.Lerp(rawScore, CurrentScore, deathSmoothing);

        int newTier = ScoreToTier(CurrentScore);
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            OnCombatTierChanged?.Invoke(newTier);
        }

        Debug.Log($"[CombatDDA] Player died — forcing raw score {rawScore:F2}. " +
                  $"Smoothed: {CurrentScore:F3} | Tier: {CurrentTier} ({TierNames[CurrentTier]})");

        // The encounter the player just died in may still have enemies alive. Checkpoint
        // respawn (CheckpointManager.RespawnPlayer -> EncounterTrigger.ResetEncounter) calls
        // Activate() again on every enemy in that encounter, dead or alive, which re-fires
        // NotifyEnemyActivated() for survivors that were never actually killed. Zeroing the
        // active-enemy count here first means that re-activation rebuilds the count cleanly
        // from 0 instead of stacking on top of the still-alive count from before death.
        PlayerMetricsTracker.Instance?.ResetActiveEncounter();
    }

    // ── Core Evaluation ────────────────────────────────────────────────────

    private void Evaluate()
    {
        PlayerMetricsTracker m = PlayerMetricsTracker.Instance;
        if (m == null) return;

        float score       = 0f;
        float totalWeight = 0f;

        // ── Accuracy signal [35%] ──────────────────────────────────────────
        // Shots landed / shots fired — higher = player is more skilled
        if (m.LastEncounterShotsFired > 0)
        {
            float accuracy = Mathf.Clamp01(
                (float)m.LastEncounterShotsLanded / m.LastEncounterShotsFired);
            score       += accuracyWeight * accuracy;
            totalWeight += accuracyWeight;
        }

        // ── Health signal [30%] ────────────────────────────────────────────
        // More HP remaining = player took less damage = more skilled
        if (_playerHealth != null)
        {
            float healthSignal = Mathf.Clamp01(_playerHealth.CurrentHealth / _playerHealth.maxHealth);
            score       += healthWeight * healthSignal;
            totalWeight += healthWeight;
        }

        // ── Time signal [20%] ──────────────────────────────────────────────
        // Faster kill = higher score (inverted InverseLerp so fast maps to 1.0)
        if (m.LastEncounterDuration > 0f)
        {
            float timeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(fastCombatTime, slowCombatTime, m.LastEncounterDuration));
            score       += timeWeight * timeSignal;
            totalWeight += timeWeight;
        }

        // ── Ammo signal [15%] ──────────────────────────────────────────────
        // More ammo remaining = player was efficient = more skilled
        if (_playerCombat != null)
        {
            float ammoSignal = _playerCombat.TotalAmmoNormalised;
            score       += ammoWeight * ammoSignal;
            totalWeight += ammoWeight;
        }

        // Nothing available to evaluate — keep current tier
        if (totalWeight <= 0f) return;

        float rawScore = Mathf.Clamp01(score / totalWeight);
        LastRawScore = rawScore;
        CurrentScore = Mathf.Lerp(rawScore, CurrentScore, scoreSmoothing);

        int newTier = ScoreToTier(CurrentScore);
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            OnCombatTierChanged?.Invoke(newTier);
        }
        else
        {
            CurrentTier = newTier;
        }

        float accuracy_log = m.LastEncounterShotsFired > 0
            ? (float)m.LastEncounterShotsLanded / m.LastEncounterShotsFired
            : 0f;

        Debug.Log($"[CombatDDA] Raw: {rawScore:F3} | Smoothed: {CurrentScore:F3} | " +
                  $"Tier: {CurrentTier} ({TierNames[CurrentTier]}) | " +
                  $"Accuracy: {accuracy_log:P0} | " +
                  $"Duration: {m.LastEncounterDuration:F1}s | " +
                  $"HP: {(_playerHealth != null ? _playerHealth.CurrentHealth : 0f):F0} | " +
                  $"Ammo: {(_playerCombat != null ? _playerCombat.TotalAmmoNormalised : 0f):P0}");
    }

    // ── Tier Mapping ───────────────────────────────────────────────────────

    private static int ScoreToTier(float score)
    {
        if (score < 0.20f) return 0;
        if (score < 0.40f) return 1;
        if (score < 0.60f) return 2;
        if (score < 0.80f) return 3;
        return 4;
    }
}

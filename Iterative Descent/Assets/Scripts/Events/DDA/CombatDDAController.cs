using UnityEngine;

/// <summary>
/// Combat DDA controller. Evaluates player combat performance after each encounter
/// (last active enemy killed) and updates CurrentTier for EnemyDirector to consume.
///
/// Starts at Tier 1 (Easy) so new players get a gentler first encounter.
///
/// Signal weights:
///   [35%] Accuracy          — ShotsLanded / ShotsFired
///   [30%] Normalised health — CurrentHP / MaxHP at encounter end
///   [20%] Combat time       — InverseLerp(fastTime, slowTime, duration), inverted so fast = 1
///   [15%] Normalised ammo   — (mag + spare) / ammoCombatSoftCap at encounter end
///
/// Partial data: if a signal source is unavailable (e.g. no shots fired yet),
/// that signal is excluded and the remaining weights are re-normalised, matching
/// the puzzle DDA approach.
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
    }

    private void OnDisable()
    {
        PlayerMetricsTracker.OnEncounterEnd -= OnEncounterEnd;
    }

    // ── Event Handler ──────────────────────────────────────────────────────

    private void OnEncounterEnd()
    {
        // Re-cache if null (handles scene reloads)
        if (_playerHealth == null) _playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (_playerCombat == null) _playerCombat = FindFirstObjectByType<PlayerCombat>();

        Evaluate();
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

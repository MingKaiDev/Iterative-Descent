using UnityEngine;

/// <summary>
/// Reads the current CombatDDA tier and exposes scaled combat parameters
/// for enemies to consume.
///
/// ─── Scene setup ──────────────────────────────────────────────────────────────
///   Attach to the persistent GameManager GameObject.
///   Assign TierProfile ScriptableObject assets (Tier 0 through Tier 4) in the
///   tierProfiles array in the Inspector — index 0 = Very Easy, 4 = Very Hard.
///
/// ─── Usage (enemy side) ────────────────────────────────────────────────────
///   EnemyChaser/EnemyBrute/EnemyRusher.OnActivate() call:
///     _agent.speed = chaseSpeed * EnemyDirector.Instance.SpeedMultiplier;
///     _attack.ApplyDamageMultiplier(EnemyDirector.Instance.DamageMultiplier);
///   EnemyBase.Activate() applies EnemyHealthMultiplier to maxHealth.
///   EnemyBase.TakeDamage() applies DamageMitigation to incoming damage.
///
/// ─── 2026-09 revamp ──────────────────────────────────────────────────────────
///   Enemy-count scaling (formerly GetScaledCount(), reading enemyCountMultiplier)
///   has been removed. EncounterTrigger now always activates every pre-placed
///   enemy in a room; DDA difficulty is expressed entirely through enemy
///   toughness (health + mitigation) and aggression (speed + damage) instead of
///   enemy quantity, so a harder tier costs the player more ammo per enemy
///   rather than throwing more enemies at them.
/// </summary>
public class EnemyDirector : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static EnemyDirector Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Tier Profiles (index = tier number, 0 to 4)")]
    [Tooltip("Assign TierProfile assets in order: index 0 = Very Easy, 4 = Very Hard.")]
    [SerializeField] private TierProfile[] tierProfiles = new TierProfile[5];

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // GameManager already calls DontDestroyOnLoad via PlayerMetricsTracker.
        // Only needed here if EnemyDirector is on a separate GameObject.
    }

    // ── Active Profile ─────────────────────────────────────────────────────

    private TierProfile ActiveProfile
    {
        get
        {
            int tier = CombatDDAController.Instance != null
                ? Mathf.Clamp(CombatDDAController.Instance.CurrentTier, 0, tierProfiles.Length - 1)
                : 1; // fallback to Tier 1 (Easy) if controller is missing

            return tier < tierProfiles.Length ? tierProfiles[tier] : null;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>Speed multiplier from the active tier profile. Default 1.</summary>
    public float SpeedMultiplier => ActiveProfile?.speedMultiplier ?? 1f;

    /// <summary>Damage multiplier from the active tier profile. Default 1.</summary>
    public float DamageMultiplier => ActiveProfile?.damageMultiplier ?? 1f;

    /// <summary>Enemy max-health multiplier from the active tier profile. Default 1.</summary>
    public float EnemyHealthMultiplier => ActiveProfile?.enemyHealthMultiplier ?? 1f;

    /// <summary>
    /// Fraction of incoming damage mitigated (absorbed) by enemies at the active
    /// tier. 0 = no mitigation, 0.25 = enemies only take 75% of a hit's damage.
    /// Default 0.
    /// </summary>
    public float DamageMitigation => ActiveProfile?.damageMitigation ?? 0f;

    /// <summary>Ammo spawn multiplier from the active tier profile. Default 1. (Future use)</summary>
    public float AmmoSpawnMultiplier => ActiveProfile?.ammoSpawnMultiplier ?? 1f;

    /// <summary>Health spawn multiplier from the active tier profile. Default 1. (Future use)</summary>
    public float HealthSpawnMultiplier => ActiveProfile?.healthSpawnMultiplier ?? 1f;

    /// <summary>
    /// Debug helper — logs the current tier and all active multipliers, including
    /// the combined effective-HP figure (health multiplier vs. mitigation).
    /// </summary>
    public void LogCurrentProfile()
    {
        int tier = CombatDDAController.Instance?.CurrentTier ?? 1;
        TierProfile p = ActiveProfile;
        if (p == null)
        {
            Debug.LogWarning("[EnemyDirector] No TierProfile assigned for tier " + tier);
            return;
        }

        float effectiveHp = p.enemyHealthMultiplier / Mathf.Max(0.05f, 1f - p.damageMitigation);
        Debug.Log($"[EnemyDirector] Tier {tier} | Speed x{p.speedMultiplier} | " +
                  $"Dmg x{p.damageMultiplier} | Health x{p.enemyHealthMultiplier} | " +
                  $"Mitigation {p.damageMitigation:P0} (effective HP x{effectiveHp:F2}) | " +
                  $"Ammo x{p.ammoSpawnMultiplier} | HP-drop x{p.healthSpawnMultiplier}");
    }
}

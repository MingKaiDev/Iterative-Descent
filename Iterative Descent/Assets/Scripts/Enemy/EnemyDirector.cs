using UnityEngine;

/// <summary>
/// Reads the current CombatDDA tier and exposes scaled combat parameters
/// for enemies and encounter spawners to consume.
///
/// ─── Scene setup ──────────────────────────────────────────────────────────────
///   Attach to the persistent GameManager GameObject.
///   Assign TierProfile ScriptableObject assets (Tier 0 through Tier 4) in the
///   tierProfiles array in the Inspector — index 0 = Very Easy, 4 = Very Hard.
///
/// ─── Usage (enemy side) ──────────────────────────────────────────────────────
///   EnemyChaser.OnActivate() calls:
///     _agent.speed = chaseSpeed * EnemyDirector.Instance.SpeedMultiplier;
///     _attack.ApplyDamageMultiplier(EnemyDirector.Instance.DamageMultiplier);
///
/// ─── Usage (spawner side) ────────────────────────────────────────────────────
///   int spawnCount = EnemyDirector.Instance.GetScaledCount(baseCount);
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

    /// <summary>Ammo spawn multiplier from the active tier profile. Default 1. (Future use)</summary>
    public float AmmoSpawnMultiplier => ActiveProfile?.ammoSpawnMultiplier ?? 1f;

    /// <summary>Health spawn multiplier from the active tier profile. Default 1. (Future use)</summary>
    public float HealthSpawnMultiplier => ActiveProfile?.healthSpawnMultiplier ?? 1f;

    /// <summary>
    /// Returns the DDA-scaled enemy count for an encounter with the given base count.
    /// Floors the result to an integer and enforces a minimum of 1.
    /// </summary>
    /// <param name="baseCount">Designer-intended enemy count for this encounter.</param>
    public int GetScaledCount(int baseCount)
    {
        if (ActiveProfile == null) return Mathf.Max(1, baseCount);
        int scaled = Mathf.FloorToInt(baseCount * ActiveProfile.enemyCountMultiplier);
        return Mathf.Max(1, scaled);
    }

    /// <summary>
    /// Debug helper — logs the current tier and all active multipliers.
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
        Debug.Log($"[EnemyDirector] Tier {tier} | Speed x{p.speedMultiplier} | " +
                  $"Dmg x{p.damageMultiplier} | Count x{p.enemyCountMultiplier} | " +
                  $"Ammo x{p.ammoSpawnMultiplier} | HP x{p.healthSpawnMultiplier}");
    }
}

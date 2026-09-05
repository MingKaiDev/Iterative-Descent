using UnityEngine;

/// <summary>
/// ScriptableObject defining the combat difficulty parameters for one DDA tier.
/// Create one asset per tier (Tier 0 through Tier 4) via:
///   Right-click in Project window -> Create -> DDA -> Tier Profile
///
/// Assign all five in order to EnemyDirector.tierProfiles in the Inspector.
///
/// Tier table reference (2026-09 revamp -- enemy-count scaling has been removed;
/// higher tiers now make enemies tougher instead of more numerous, so a harder
/// tier takes more ammo per enemy rather than throwing more enemies at once):
///   Tier 0 (Very Easy) : speed 0.90, dmg 0.80, health 0.80x, mitigation  0%  -> effective HP 0.80x
///   Tier 1 (Easy)      : speed 1.00, dmg 1.00, health 1.00x, mitigation  0%  -> effective HP 1.00x  [default start]
///   Tier 2 (Normal)    : speed 1.05, dmg 1.05, health 1.15x, mitigation  8%  -> effective HP 1.25x
///   Tier 3 (Hard)      : speed 1.10, dmg 1.10, health 1.30x, mitigation 15%  -> effective HP 1.53x
///   Tier 4 (Very Hard) : speed 1.20, dmg 1.15, health 1.50x, mitigation 25%  -> effective HP 2.00x
///
/// Effective HP = enemyHealthMultiplier / (1 - damageMitigation) -- this is the
/// real "shots needed to kill" multiplier once mitigation is factored in on top
/// of the raw health increase. Placeholder starting values -- expect to retune
/// alongside the rest of Story 9's combat DDA balance pass.
/// </summary>
[CreateAssetMenu(fileName = "TierProfile", menuName = "DDA/Tier Profile")]
public class TierProfile : ScriptableObject
{
    [Tooltip("Multiplier applied to EnemyChaser's base chase speed.")]
    public float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to EnemyAttack's base melee damage.")]
    public float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to the enemy's base max health, applied fresh at " +
             "every Activate() call. Combines with damageMitigation below -- see " +
             "the effective-HP formula in the class doc comment above.")]
    public float enemyHealthMultiplier = 1f;

    [Range(0f, 0.95f)]
    [Tooltip("Fraction of incoming damage absorbed before it reaches the enemy's " +
             "health, e.g. 0.25 = enemy only takes 75% of a hit's damage. Applied in " +
             "EnemyBase.TakeDamage(). Capped below 1 so a hit can never be fully negated.")]
    public float damageMitigation = 0f;

    [Tooltip("Multiplier applied to ammo pickup quantity or spawn rate. (Future use)")]
    public float ammoSpawnMultiplier = 1f;

    [Tooltip("Multiplier applied to health pack spawn rate or quantity. (Future use)")]
    public float healthSpawnMultiplier = 1f;
}

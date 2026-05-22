using UnityEngine;

/// <summary>
/// ScriptableObject defining the combat difficulty parameters for one DDA tier.
/// Create one asset per tier (Tier 0 through Tier 4) via:
///   Right-click in Project window -> Create -> DDA -> Tier Profile
///
/// Assign all five in order to EnemyDirector.tierProfiles in the Inspector.
///
/// Tier table reference:
///   Tier 0 (Very Easy) : count 0.8, speed 0.90, dmg 0.80, ammo 1.4, hp 1.2
///   Tier 1 (Easy)      : count 1.0, speed 1.00, dmg 1.00, ammo 1.0, hp 1.0  [default start]
///   Tier 2 (Normal)    : count 1.2, speed 1.05, dmg 1.05, ammo 0.9, hp 0.8
///   Tier 3 (Hard)      : count 1.5, speed 1.10, dmg 1.10, ammo 0.8, hp 0.7
///   Tier 4 (Very Hard) : count 2.0, speed 1.20, dmg 1.15, ammo 0.7, hp 0.6
/// </summary>
[CreateAssetMenu(fileName = "TierProfile", menuName = "DDA/Tier Profile")]
public class TierProfile : ScriptableObject
{
    [Tooltip("Multiplier applied to the base enemy count for an encounter. " +
             "Result is floored to int with a minimum of 1.")]
    public float enemyCountMultiplier = 1f;

    [Tooltip("Multiplier applied to EnemyChaser's base chase speed.")]
    public float speedMultiplier = 1f;

    [Tooltip("Multiplier applied to EnemyAttack's base melee damage.")]
    public float damageMultiplier = 1f;

    [Tooltip("Multiplier applied to ammo pickup quantity or spawn rate. (Future use)")]
    public float ammoSpawnMultiplier = 1f;

    [Tooltip("Multiplier applied to health pack spawn rate or quantity. (Future use)")]
    public float healthSpawnMultiplier = 1f;
}

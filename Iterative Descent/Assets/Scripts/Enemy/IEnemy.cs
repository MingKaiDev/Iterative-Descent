using UnityEngine;

/// <summary>
/// Contract that every enemy type must fulfil.
///
/// Why an interface instead of a base class alone?
/// Because some enemies may need to extend other MonoBehaviours
/// (e.g. a StateMachineBehaviour) — interfaces let them still
/// participate in the enemy system without inheritance conflicts.
///
/// Typical usage:
///   EnemyChaser : EnemyBase, IEnemy   ← most enemies
///   TurretEnemy : EnemyBase, IEnemy   ← stationary shooter
///   BossEnemy   : EnemyBase, IEnemy   ← big boy with phases
///
/// EnemyBase provides a default implementation; override per type.
/// </summary>
public interface IEnemy
{
    /// <summary>
    /// Wake the enemy up and tell it who to pursue.
    /// Called by EnemyEventBridge after the door breaks.
    /// </summary>
    void Activate(Transform target);

    /// <summary>
    /// Pause AI without destroying (e.g. during a cutscene).
    /// Call Activate() again to resume.
    /// </summary>
    void Deactivate();

    /// <summary>
    /// Kill the enemy — plays death sequence then removes from scene.
    /// Safe to call multiple times; second call is a no-op.
    /// </summary>
    void Die();

    /// <summary>True once Die() has been called and processed.</summary>
    bool IsDead { get; }
}

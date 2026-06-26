using UnityEngine;

/// <summary>
/// DROID-7 Left Punch attack.
///
/// Animation Event setup (add to the triggerLeftPunch clip in DROIDAnimator):
///   - "OnHitboxOpen"    -- frame where the fist connects (deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done)
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same as Animator).
///   - Assign playerLayer in the Inspector.
/// </summary>
public class LeftPunchAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Left Punch Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 20f;

    [Tooltip("Radius of the hit check sphere around the boss.")]
    public float hitRange = 2f;

    [Tooltip("Half-angle of the hit cone in front of the boss (degrees).")]
    public float hitHalfAngle = 60f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerLeftPunch = Animator.StringToHash("triggerLeftPunch");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[LeftPunchAttack] No Animator found on this GameObject.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerLeftPunch);
        Debug.Log("[LeftPunchAttack] Left Punch triggered.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;

        var hits = Physics.OverlapSphere(transform.position, hitRange, playerLayer);
        foreach (var hit in hits)
        {
            var dir = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dir) > hitHalfAngle) continue;

            var damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                var hitPoint = hit.ClosestPoint(transform.position);
                damageable.TakeDamage(damage, hitPoint);
                Debug.Log($"[LeftPunchAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

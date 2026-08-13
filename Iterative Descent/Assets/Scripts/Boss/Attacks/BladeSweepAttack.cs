using UnityEngine;

/// <summary>
/// DROID-7 Blade Sweep attack -- wide horizontal arc, more damage than Slash.
///
/// Animation Event setup (add to the triggerBladeSweep clip in DROIDAnimator):
///   - "OnHitboxOpen"    -- frame where the sweep connects (deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done)
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same as Animator).
///   - Assign playerLayer in the Inspector.
/// </summary>
public class BladeSweepAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Blade Sweep Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 40f;

    [Tooltip("Radius of the hit check sphere around the boss.")]
    public float hitRange = 3.5f;

    [Tooltip("Half-angle of the sweep arc in front of the boss (degrees). " +
             "Wider than Slash to reward spacing.")]
    public float hitHalfAngle = 100f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerBladeSweep = Animator.StringToHash("triggerBladeSweep");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[BladeSweepAttack] No Animator found on this GameObject.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerBladeSweep);
        Debug.Log("[BladeSweepAttack] Blade Sweep triggered.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip);
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
                Debug.Log($"[BladeSweepAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

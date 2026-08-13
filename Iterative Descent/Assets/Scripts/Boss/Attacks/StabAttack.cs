using UnityEngine;

/// <summary>
/// DROID-7 Stab attack -- narrow forward thrust.
///
/// Animation Event setup (add to the triggerStab clip in DROIDAnimator):
///   - "OnHitboxOpen"    -- frame where the blade connects (deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done)
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same as Animator).
///   - Assign playerLayer in the Inspector.
/// </summary>
public class StabAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Stab Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 30f;

    [Tooltip("Range of the thrust hit check.")]
    public float hitRange = 2f;

    [Tooltip("Half-angle of the stab cone (degrees). Narrow -- player must be directly in front.")]
    public float hitHalfAngle = 30f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerStab = Animator.StringToHash("triggerStab");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[StabAttack] No Animator found on this GameObject.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerStab);
        Debug.Log("[StabAttack] Stab triggered.");
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
                Debug.Log($"[StabAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.2f, 0.8f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

using UnityEngine;

/// <summary>
/// DROID-7 Slash attack -- basic close-range melee swing.
///
/// Animation Event setup (add these to the triggerSlash clip in DROIDAnimator):
///   - "OnHitboxOpen"   -- frame where the blade connects (deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done)
///
/// If animation events are not yet set up, the attack will play visually
/// but deal no damage, and will end via the attackTimeoutDuration safety net.
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same as Animator).
///   - Assign playerLayer in the Inspector (the layer your Player GameObject is on).
///   - Tune hitRange and damage in the Inspector.
/// </summary>
public class SlashAttack : BossAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Slash Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 25f;

    [Tooltip("Radius of the hit check sphere around the boss.")]
    public float hitRange = 2.5f;

    [Tooltip("Half-angle of the hit cone in front of the boss (degrees). " +
             "90 = 180 degree arc, 45 = 90 degree arc.")]
    public float hitHalfAngle = 70f;

    [Tooltip("Layer the player is on. Set in Inspector to avoid hitting other colliders.")]
    public LayerMask playerLayer;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerSlash = Animator.StringToHash("triggerSlash");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[SlashAttack] No Animator found on this GameObject.");
    }

    // ─── BossAttackBase Implementation ───────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerSlash);
        Debug.Log("[SlashAttack] Slash triggered.");
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    /// <summary>Called by Animation Event at the damage frame of the Slash clip.</summary>
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
                Debug.Log($"[SlashAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

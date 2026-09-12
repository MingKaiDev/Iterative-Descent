using UnityEngine;

/// <summary>
/// Deimos's Dropkick attack -- Move 3 of the heuristic build-up, second attack after MMAKick.
/// Same shape as MMAKickAttack.cs (a stationary close-range melee strike, no leap/root-motion
/// handling -- Chimera.controller's clip hasn't been confirmed to bake forward drift into the
/// hip, and OnAnimatorMove() suppresses root motion anyway; revisit if Ming Kai wants an actual
/// forward lunge once the clip's motion is checked in-Editor), retargeted at the pre-existing
/// "DropKick" trigger parameter (state is named "Dropkick", the trigger parameter is "DropKick"
/// -- both already declared in Chimera.controller from Ming Kai's own Animator-window build).
///
/// Animator wiring (patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> Dropkick   [trigger: DropKick, no exit time, cannot transition to self]
///   Dropkick -> Idle        [exit time 0.9, no condition]
/// Same "Any State" reasoning as MMAKick: DeimosAttackRegistry only ever fires the trigger
/// while CurrentState == Combat AND IsLocomotionReady, so in practice this only ever fires from
/// Idle or Walk, never interrupting Typing/Sit To Stand/Death. No DeimosAttackRegistry changes
/// were needed to add this -- it already finds every DeimosAttackBase on the GameObject via
/// GetComponents(), MMAKick included.
///
/// Knockback: a dropkick is a shove-forward attack by nature, so this carries a stronger push
/// than MMAKick's out of the box (tune both in the Inspector). Uses the same
/// DeimosAttackBase.ApplyKnockbackTo() helper as MMAKickAttack -- see that class's doc comment
/// for why this is the pattern (BruteAttack's swipe push), not GroundSlamAttack's (no actual
/// knockback there despite the name).
///
/// Hit-landed audio: PlaySound(hitLandedClip) fires only inside the "if (damageable != null)"
/// branch, i.e. only when the kick actually connects -- distinct from PlaySound(hitboxOpenClip)
/// at the top of OnHitboxOpen(), which fires every time the hitbox opens regardless of whether
/// anything was in range. Assign hitboxOpenClip to a whoosh/swing sound and hitLandedClip to an
/// impact/thud in the Inspector.
///
/// Animation Event setup still needed on the Dropkick clip itself (Ming Kai, in-Editor):
///   - "OnHitboxOpen"    -- frame where the kick connects (deals damage + knockback)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done, returns to Combat)
/// Until those events exist, the kick will play visually but deal no damage, and will end via
/// attackTimeoutDuration instead (logs a warning when that happens -- see DeimosAttackBase).
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject (same as Animator/DeimosStateMachine/MMAKickAttack).
///   - Assign playerLayer in the Inspector.
///   - maxRange (inherited, DeimosAttackBase) should be >= hitRange with a little slack so the
///     attack doesn't get selected by DeimosAttackRegistry from a distance the kick can't reach.
/// </summary>
public class DropkickAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Dropkick Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 25f;

    [Tooltip("Radius of the hit check sphere around Deimos.")]
    public float hitRange = 2.2f;

    [Tooltip("Half-angle of the hit cone in front of Deimos (degrees).")]
    public float hitHalfAngle = 50f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("Knockback")]
    [Tooltip("Horizontal speed (m/s) given to the player, pushed straight away from Deimos, " +
             "the instant the kick connects. Stronger than MMAKick's by default -- a dropkick " +
             "reads as a shove. Hand-off to PlayerMovement.ApplyKnockback -- that component " +
             "owns the actual decay (see its knockbackRecoverySpeed field).")]
    public float knockbackForce = 12f;

    [Tooltip("Small upward component added on top of the horizontal push, purely for stagger " +
             "feel. 0 = perfectly horizontal.")]
    public float knockbackUpwardKick = 1.5f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    // Trigger parameter is "DropKick" (capital K) -- the Animator STATE is named "Dropkick"
    // (lowercase k). Both pre-existing, Ming Kai's own naming from the Animator window.
    private static readonly int TriggerDropKick = Animator.StringToHash("DropKick");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[DropkickAttack] No Animator found on this GameObject.");
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerDropKick);
        Debug.Log("[DropkickAttack] Dropkick triggered.");
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
                PlaySound(hitLandedClip);
                Debug.Log($"[DropkickAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }

            ApplyKnockbackTo(hit, knockbackForce, knockbackUpwardKick);
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

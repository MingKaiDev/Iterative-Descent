using UnityEngine;

/// <summary>
/// Deimos's MMA Kick attack -- basic close-range melee strike, and the first attack in Deimos's
/// heuristic moveset (Move 2, after the core Idle/Combat/Walk FSM in Move 1). Mirrors
/// LeftPunchAttack.cs's (ARES/DROID-7) pattern, retargeted at DeimosAttackBase and Chimera.
/// controller's existing "MMAKick" trigger parameter (already declared, PascalCase, matching
/// Ming Kai's naming convention for this controller -- NOT ARES's lowercase "triggerLeftPunch").
///
/// Animator wiring (already patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> MMAKick   [trigger: MMAKick, no exit time, cannot transition to self]
///   MMAKick -> Idle        [exit time 0.9, no condition]
/// "Any State" is used instead of wiring Idle->MMAKick and Walk->MMAKick separately so any
/// future locomotion state doesn't need its own copy of the same transition. This is safe
/// because DeimosAttackRegistry only ever fires the trigger while CurrentState == Combat AND
/// IsLocomotionReady is true (see DeimosAttackRegistry.Update()) -- so in practice it only ever
/// fires from Idle or Walk, never interrupting Typing/Sit To Stand/Death.
///
/// Animation Event setup still needed on the MMAKick clip itself (Ming Kai, in-Editor):
///   - "OnHitboxOpen"    -- frame where the kick connects (deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done, returns to Combat)
/// Until those events exist, the kick will play visually but deal no damage, and will end via
/// attackTimeoutDuration instead (logs a warning when that happens -- see DeimosAttackBase).
///
/// Knockback (added 2026-09-11, after Ming Kai asked for "knockback similar to how ground slam
/// does it"): turns out ARES's GroundSlamAttack.cs has no actual knockback at all -- it's AOE
/// damage plus a purely visual shockwave ring (GroundSlamVFX), nothing pushes the player via
/// PlayerMovement.ApplyKnockback. The real (and only) precedent for that in this codebase is
/// BruteAttack's swipe-hit push, so that's the pattern mirrored here -- via
/// DeimosAttackBase.ApplyKnockbackTo(), shared with future Deimos melee attacks (Dropkick, and
/// likely Flying Knee) instead of re-duplicated per attack.
///
/// Hit-landed audio (added 2026-09-11, same pass): PlaySound(hitLandedClip) fires only inside
/// the "if (damageable != null)" branch, i.e. only when the kick actually connects -- distinct
/// from PlaySound(hitboxOpenClip) at the top of OnHitboxOpen(), which fires every time the
/// hitbox opens regardless of whether anything was in range. Assign hitboxOpenClip to a
/// whoosh/swing sound and hitLandedClip to an impact/thud in the Inspector.
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject (same as Animator/DeimosStateMachine).
///   - Assign playerLayer in the Inspector.
///   - maxRange (inherited, DeimosAttackBase) should be >= hitRange with a little slack so the
///     attack doesn't get selected by DeimosAttackRegistry from a distance the kick can't reach.
/// </summary>
public class MMAKickAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("MMA Kick Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 20f;

    [Tooltip("Radius of the hit check sphere around Deimos.")]
    public float hitRange = 2f;

    [Tooltip("Half-angle of the hit cone in front of Deimos (degrees).")]
    public float hitHalfAngle = 60f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("Knockback")]
    [Tooltip("Horizontal speed (m/s) given to the player, pushed straight away from Deimos, " +
             "the instant the kick connects. Hand-off to PlayerMovement.ApplyKnockback -- that " +
             "component owns the actual decay (see its knockbackRecoverySpeed field).")]
    public float knockbackForce = 8f;

    [Tooltip("Small upward component added on top of the horizontal push, purely for stagger " +
             "feel. 0 = perfectly horizontal.")]
    public float knockbackUpwardKick = 1f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerMMAKick = Animator.StringToHash("MMAKick");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[MMAKickAttack] No Animator found on this GameObject.");
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerMMAKick);
        Debug.Log("[MMAKickAttack] MMA Kick triggered.");
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
                Debug.Log($"[MMAKickAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }

            ApplyKnockbackTo(hit, knockbackForce, knockbackUpwardKick);
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

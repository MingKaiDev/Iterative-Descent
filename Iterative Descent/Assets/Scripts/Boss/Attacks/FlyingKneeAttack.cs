using UnityEngine;

/// <summary>
/// Deimos's Flying Knee attack -- Move 4 of the heuristic build-up, third attack after MMAKick
/// and Dropkick. Same shape as DropkickAttack.cs/MMAKickAttack.cs (a stationary close-range
/// melee strike, no leap/root-motion handling -- the source clip is "Flying Knee Punch
/// Combo.fbx", and exactly like Dropkick's clip, it hasn't been confirmed in-Editor whether it
/// bakes forward drift into the hip; OnAnimatorMove() suppresses root motion regardless, so
/// Deimos stays planted either way until that's checked and this is revisited for an actual
/// lunge), retargeted at the pre-existing "FlyingKnee" trigger parameter and "Flying Knee"
/// state (both already declared in Chimera.controller from Ming Kai's own Animator-window
/// build, same as Dropkick/MMAKick).
///
/// Animator wiring (patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> Flying Knee   [trigger: FlyingKnee, no exit time, cannot transition to self]
///   Flying Knee -> Idle        [exit time 0.9, no condition]
/// Same "Any State" reasoning as MMAKick/Dropkick: DeimosAttackRegistry only ever fires the
/// trigger while CurrentState == Combat AND IsLocomotionReady, so in practice this only ever
/// fires from Idle or Walk, never interrupting Typing/Sit To Stand/Death. No
/// DeimosAttackRegistry changes were needed to add this -- it already finds every
/// DeimosAttackBase on the GameObject via GetComponents(), MMAKick and Dropkick included.
///
/// Knockback: a knee strike to the chest/face reads as a harder stagger than either MMAKick or
/// Dropkick, so this carries the strongest push of the three out of the box (tune in the
/// Inspector) with extra upward kick for the "lifted off their feet" feel. Uses the same
/// DeimosAttackBase.ApplyKnockbackTo() helper as MMAKickAttack/DropkickAttack -- see
/// MMAKickAttack's doc comment for why this (BruteAttack's swipe push), not GroundSlamAttack's
/// (no actual knockback there despite the name), is the pattern mirrored here.
///
/// Animation Event setup still needed on the Flying Knee clip itself (Ming Kai, in-Editor):
///   - "OnHitboxOpen"    -- frame where the knee connects (deals damage + knockback)
///   - "OnAttackAnimEnd" -- last frame of the clip (signals attack done, returns to Combat)
/// Until those events exist, the attack will play visually but deal no damage, and will end via
/// attackTimeoutDuration instead (logs a warning when that happens -- see DeimosAttackBase).
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject (same as Animator/DeimosStateMachine/MMAKickAttack/
///     DropkickAttack).
///   - Assign playerLayer in the Inspector.
///   - maxRange (inherited, DeimosAttackBase) should be >= hitRange with a little slack so the
///     attack doesn't get selected by DeimosAttackRegistry from a distance the knee can't reach.
///   - windupClip/hitboxOpenClip (inherited, DeimosAttackBase) -- same per-attack audio fields
///     every Deimos attack already carries, see FYP/setup-guides/DeimosAudio_UnitySetup.md.
/// </summary>
public class FlyingKneeAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Flying Knee Settings")]
    [Tooltip("Damage dealt to the player on hit.")]
    public float damage = 30f;

    [Tooltip("Radius of the hit check sphere around Deimos.")]
    public float hitRange = 2.5f;

    [Tooltip("Half-angle of the hit cone in front of Deimos (degrees).")]
    public float hitHalfAngle = 45f;

    [Tooltip("Layer the player is on.")]
    public LayerMask playerLayer;

    [Header("Knockback")]
    [Tooltip("Horizontal speed (m/s) given to the player, pushed straight away from Deimos, " +
             "the instant the knee connects. Strongest of Deimos's three attacks by default -- " +
             "a knee to the chest reads as the hardest stagger. Hand-off to PlayerMovement." +
             "ApplyKnockback -- that component owns the actual decay (see its " +
             "knockbackRecoverySpeed field).")]
    public float knockbackForce = 15f;

    [Tooltip("Upward component added on top of the horizontal push -- higher than MMAKick/" +
             "Dropkick's, for a 'lifted off their feet' stagger feel. 0 = perfectly horizontal.")]
    public float knockbackUpwardKick = 2.5f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerFlyingKnee = Animator.StringToHash("FlyingKnee");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator _animator;

    void Start()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError("[FlyingKneeAttack] No Animator found on this GameObject.");
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null) return;
        _animator.SetTrigger(TriggerFlyingKnee);
        Debug.Log("[FlyingKneeAttack] Flying Knee triggered.");
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
                Debug.Log($"[FlyingKneeAttack] Hit {hit.gameObject.name} for {damage} damage.");
            }

            ApplyKnockbackTo(hit, knockbackForce, knockbackUpwardKick);
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.2f, 0.9f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hitRange);
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Deimos's Jump Forward -- Move 6, a pure gap-closer with no damage. Ming Kai asked for this to
/// work "similar to ground slam from ARES but without the damage" -- GroundSlamAttack.cs (ARES)
/// is the leap-movement precedent this borrows: disconnect the NavMeshAgent, arc toward the
/// player's captured XZ position over leapDuration (Lerp for XZ + a Sin arc for height), snap to
/// the target and reconnect the agent when the clip signals the landing. This class copies
/// exactly that movement model and drops everything else GroundSlamAttack does on landing -- no
/// AOE damage, no slamRadius, no VFX, no playerLayer even. OnHitboxOpen() here only plays
/// hitboxOpenClip as a landing thud and snaps position, nothing more.
///
/// Selection: this is Deimos's only "too far to melee" move. DeimosAttackBase gained a new
/// minRange field (default 0, so every other attack is unaffected) specifically for this class --
/// DeimosAttackRegistry now gates candidate attacks on
/// "distToPlayer >= attack.minRange && distToPlayer <= attack.maxRange" instead of just maxRange,
/// so Jump Forward only enters the random-pick pool once the player is further away than Deimos's
/// other attacks can reach (minRange defaults to 6, just past Flying Knee's 2.5 hitRange with
/// slack -- tune against whatever the widest melee reach ends up being in Play mode), and
/// maxRange caps how far it'll actually try to close (defaults to 20, matching/exceeding
/// DeimosStateMachine.detectionRadius's default 15 so it can fire near the edge of detection).
///
/// Animator wiring (patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> JumpForward   [trigger: JumpForward, no exit time, cannot transition to self]
///   JumpForward -> Idle        [exit time 0.9, no condition]
/// Same "Any State" reasoning as every other Deimos attack -- DeimosAttackRegistry only ever
/// fires while CurrentState == Combat && IsLocomotionReady.
///
/// Animation Event setup still needed on the JumpForward clip itself (Ming Kai, in-Editor):
///   - "OnHitboxOpen"    -- frame the feet land (snaps position, plays landing sound, NO damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (re-enables the NavMeshAgent, signals done)
/// Until those exist, the leap plays but Deimos never snaps to the target or hands the
/// NavMeshAgent back, and ends via attackTimeoutDuration instead (logs a warning) -- unlike the
/// swipe attacks, a missing OnAttackAnimEnd here is worse than usual, since CancelAttack() is the
/// only other path that restores agent.updatePosition/updateRotation. Make sure
/// attackTimeoutDuration is comfortably longer than leapDuration.
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject (same as the other five attacks).
///   - No playerLayer to assign -- this attack never does a hit check, so there's nothing to hit.
///   - Set minRange (Inspector, inherited from DeimosAttackBase, defaults to 0 like every other
///     attack) to something past Deimos's melee reach -- e.g. 6 -- so this only gets picked when
///     the player is genuinely too far for anything else.
///   - Tune leapDuration/leapHeight/maxRange once playable.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class JumpForwardAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Jump Forward Settings")]
    [Tooltip("How long (seconds) Deimos spends in the air, closing the gap toward the player's " +
             "captured position. Tune to match the animation's air-time.")]
    public float leapDuration = 0.6f;

    [Tooltip("Peak height of the arc above the start/end Y.")]
    public float leapHeight = 2.5f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerJumpForward = Animator.StringToHash("JumpForward");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator     _animator;
    private NavMeshAgent _agent;
    private Transform    _player;

    // ─── Private State ───────────────────────────────────────────────────────────
    private Vector3 _targetPosition;
    private bool    _hasLanded;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _agent    = GetComponent<NavMeshAgent>();

        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) _player = ph.transform;

        if (_animator == null) Debug.LogError("[JumpForwardAttack] No Animator found on this GameObject.");
        if (_agent    == null) Debug.LogError("[JumpForwardAttack] No NavMeshAgent found on this GameObject.");
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null || _player == null) return;

        // Capture player XZ at the moment the attack starts -- same snapshot approach as
        // GroundSlamAttack, so a player who moves mid-leap doesn't retarget Deimos in the air.
        _targetPosition = new Vector3(_player.position.x, transform.position.y, _player.position.z);
        _hasLanded      = false;

        // Disconnect NavMeshAgent so we can drive position manually during the arc.
        if (_agent != null)
        {
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }

        _animator.SetTrigger(TriggerJumpForward);
        StartCoroutine(LeapRoutine());
        Debug.Log($"[JumpForwardAttack] Jumping toward {_targetPosition}.");
    }

    // ─── Leap Coroutine ──────────────────────────────────────────────────────────

    IEnumerator LeapRoutine()
    {
        Vector3 startPos = transform.position;

        // Face target immediately.
        Vector3 dir = _targetPosition - startPos;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        float elapsed = 0f;

        while (elapsed < leapDuration && !_hasLanded)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / leapDuration);

            float x    = Mathf.Lerp(startPos.x, _targetPosition.x, t);
            float z    = Mathf.Lerp(startPos.z, _targetPosition.z, t);
            float arcY = startPos.y + Mathf.Sin(t * Mathf.PI) * leapHeight;

            transform.position = new Vector3(x, arcY, z);

            if (_agent != null)
                _agent.nextPosition = transform.position;

            yield return null;
        }
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    /// <summary>Landing -- snaps to the target and plays a landing sound. Deliberately no damage,
    /// no hit check at all, unlike GroundSlamAttack's own OnHitboxOpen() -- Ming Kai asked for
    /// this move specifically "without the damage."</summary>
    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip); // landing thud -- no damage, this attack never hits anything

        _hasLanded = true; // stops LeapRoutine if still running

        // Snap to target XZ so the landing reads cleanly even if the arc undershot/overshot.
        transform.position = _targetPosition;
        if (_agent != null)
            _agent.Warp(_targetPosition);

        Debug.Log("[JumpForwardAttack] Landed.");
    }

    public override void OnAttackAnimEnd()
    {
        // Re-enable NavMeshAgent before ending so DeimosStateMachine can resume pathing.
        if (_agent != null)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }

        EndAttack();
    }

    /// <summary>See GroundSlamAttack.CancelAttack() for the full story -- if the leap is cut
    /// short mid-flight (StopAllCoroutines() in the base call above already killed LeapRoutine
    /// before OnHitboxOpen/OnAttackAnimEnd ran), this restores the NavMeshAgent handoff those
    /// callbacks would otherwise have done, so the agent isn't left permanently disconnected.
    /// </summary>
    public override void CancelAttack()
    {
        bool wasActive = IsActive;
        base.CancelAttack();
        if (!wasActive) return;

        if (_agent != null)
        {
            _agent.updatePosition = true;
            _agent.updateRotation = true;
        }
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, minRange);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, maxRange);
    }
}

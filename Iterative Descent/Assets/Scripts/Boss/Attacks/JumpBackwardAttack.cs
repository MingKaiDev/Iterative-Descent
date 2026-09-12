using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Deimos's Jump Back -- Move 7. Ming Kai's spec: it fires either (a) the instant the player
/// gets directly behind Deimos via encirclement (circle-strafing), or (b) opportunistically
/// whenever it's simply off cooldown, same as every other attack. Correction, 2026-09-11 (Ming
/// Kai, after the initial "hop 3 units away from Deimos" build): the landing point isn't just
/// "away from Deimos" -- it's "behind the player," the same tactical shape as ARES's
/// PounceAttack.cs teleport (reappear in the target's blind spot). So this now mirrors
/// PounceAttack's arrival-point math (behindPlayer = player.position - player.forward *
/// behindPlayerOffset, NavMesh.SamplePosition-validated with the same player-position fallback)
/// instead of the pre-correction "away from Deimos's own position" vector. Unlike Pounce, this
/// keeps the visible leap/arc already wired for Jump Forward's movement model rather than
/// Pounce's vanish-and-reappear (hidden renderers + teleport VFX) -- Deimos has no VFX
/// infrastructure for that yet, and Ming Kai confirmed a visible hop to a point behind the
/// player is the right scope for now. There's a nice thematic symmetry to keep in mind reading
/// this: the player tries to flank Deimos from behind (the encirclement trigger, path 1 below),
/// and Deimos's counter is to reappear behind THEM instead (the landing target, described here).
///
/// Two trigger paths:
///   1. Encirclement watchdog (new -- Update() override, polled every encirclementCheckInterval
///      seconds rather than every frame): while Combat + IsLocomotionReady + off cooldown + not
///      already attacking, does an OverlapSphere + REVERSE cone check against playerLayer (angle
///      from transform.forward >= behindAngleThreshold means the player is roughly in the rear
///      arc, not the forward cone every other attack's hit-check uses). The instant that's true,
///      this SELF-triggers: calls DeimosStateMachine.EnterAttacking() then Execute() directly,
///      bypassing DeimosAttackRegistry's normal attackDecisionInterval poll entirely. This is the
///      first Deimos attack that triggers itself rather than waiting to be picked -- safe because
///      EnterAttacking()/ExitAttacking() are already public API on DeimosStateMachine (the
///      registry just happened to be the only caller so far), and DeimosAttackRegistry's own
///      Update() already no-ops while CurrentState == Attacking, so there's no risk of the
///      registry also picking this attack in the same frame -- see DeimosAttackRegistry's own
///      doc comment note on this.
///   2. Normal registry pick: this is still just another DeimosAttackBase sitting in
///      DeimosAttackRegistry's GetComponents<DeimosAttackBase>() list, so it can also be chosen
///      randomly through the ordinary off-cooldown/in-range flow like every other move, with no
///      encirclement required -- covering Ming Kai's "or when it is off cooldown" half of the
///      spec. maxRange (default 8) keeps it from being picked as a repositioning move when the
///      player is already far away, where landing behind them accomplishes nothing tactically;
///      minRange is left at DeimosAttackBase's default (0) since, unlike Jump Forward, this isn't
///      a "too far" move.
///
/// Movement model: same disconnect-NavMeshAgent-and-arc pattern as GroundSlamAttack/
/// JumpForwardAttack, just much shorter (hopDuration/hopHeight both default lower than either).
/// The target point is NavMesh-sampled with PounceAttack's own two-stage fallback (try the
/// behind-player point, then the player's own position if that's not walkable) so this can't
/// land Deimos inside a wall or off the floor -- if BOTH samples fail, the hop target collapses
/// to Deimos's own start position (a no-op hop in place) rather than risking an out-of-bounds
/// Warp(). On landing, Deimos also rotates to face the player (mirrors PounceAttack's own
/// "face the player on arrival" step) -- it just repositioned behind them, so it should be
/// looking at them immediately rather than wherever the hop's start rotation left it facing.
///
/// Animator wiring (patched into Chimera.controller alongside this script, 2026-09-11):
///   Any State -> JumpBack   [trigger: JumpBackwards, no exit time, cannot transition to self]
///   JumpBack -> Idle        [exit time 0.9, no condition]
/// Note the trigger parameter is "JumpBackwards" (plural) while the state and this class are
/// "JumpBack"/"JumpBackwardAttack" -- Ming Kai's own Animator-window build already had the
/// trigger named that way (same "state name vs trigger name differ slightly" pattern already
/// seen on Dropkick/DropKick and Flying Knee/FlyingKnee), so this class matches it rather than
/// renaming anything in the .controller.
///
/// Animation Event setup still needed on the JumpBack clip itself (Ming Kai, in-Editor):
///   - "OnHitboxOpen"    -- frame the feet land (snaps position, NO damage -- same as Jump
///                          Forward, this attack never deals damage)
///   - "OnAttackAnimEnd" -- last frame of the clip (re-enables the NavMeshAgent, signals done)
/// Until those exist, ends via attackTimeoutDuration instead (logs a warning) -- same caution as
/// JumpForwardAttack: make sure attackTimeoutDuration is comfortably longer than hopDuration, or
/// the NavMeshAgent handoff never gets restored until the timeout fires.
///
/// Unity Setup:
///   - Attach to the Deimos root GameObject.
///   - Assign playerLayer (used for the encirclement OverlapSphere check).
///   - Tune hopDuration/hopHeight/behindAngleThreshold/encirclementCheckInterval/maxRange once
///     playable -- especially behindAngleThreshold, which decides how strict "directly behind"
///     needs to be before this preempts the normal attack cycle.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class JumpBackwardAttack : DeimosAttackBase
{
    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Jump Back Settings")]
    [Tooltip("How far behind the player (in the player's own -forward direction) Deimos lands. " +
             "Mirrors PounceAttack.behindPlayerOffset exactly -- 3 units is Ming Kai's spec'd " +
             "hop distance, now measured from the player's facing rather than Deimos's own " +
             "position (see class doc comment's Correction note).")]
    public float behindPlayerOffset = 3f;

    [Tooltip("How long (seconds) the hop takes. Much shorter than Jump Forward's leapDuration -- " +
             "this is a quick sidestep, not a committed gap-closer.")]
    public float hopDuration = 0.35f;

    [Tooltip("Peak height of the hop arc above the start/end Y.")]
    public float hopHeight = 1f;

    [Tooltip("Search radius passed to NavMesh.SamplePosition when validating the hop-back point.")]
    public float navMeshSampleRadius = 1.5f;

    [Header("Encirclement Detection")]
    [Tooltip("Layer the player is on -- used only for the rear-cone overlap check below, this " +
             "attack has no forward hitbox of its own.")]
    public LayerMask playerLayer;

    [Tooltip("Radius of the OverlapSphere used to detect the player for the encirclement check.")]
    public float detectionCheckRadius = 6f;

    [Tooltip("Angle (degrees) from Deimos's forward beyond which the player counts as " +
             "'directly behind.' 180 = exactly behind; lower is more lenient (a wider rear arc " +
             "counts as encirclement).")]
    public float behindAngleThreshold = 135f;

    [Tooltip("Seconds between encirclement checks -- polled on a timer rather than every frame.")]
    public float encirclementCheckInterval = 0.2f;

    // ─── Animator Parameter ──────────────────────────────────────────────────────
    private static readonly int TriggerJumpBackwards = Animator.StringToHash("JumpBackwards");

    // ─── Component References ────────────────────────────────────────────────────
    private Animator           _animator;
    private NavMeshAgent       _agent;
    private Transform          _player;
    private DeimosStateMachine _stateMachine;

    // ─── Private State ───────────────────────────────────────────────────────────
    private Vector3 _targetPosition;
    private bool    _hasLanded;
    private float   _encirclementTimer;

    void Start()
    {
        _animator     = GetComponent<Animator>();
        _agent        = GetComponent<NavMeshAgent>();
        _stateMachine = GetComponent<DeimosStateMachine>();

        var ph = FindFirstObjectByType<PlayerHealth>();
        if (ph != null) _player = ph.transform;

        if (_animator     == null) Debug.LogError("[JumpBackwardAttack] No Animator found on this GameObject.");
        if (_agent        == null) Debug.LogError("[JumpBackwardAttack] No NavMeshAgent found on this GameObject.");
        if (_stateMachine == null) Debug.LogError("[JumpBackwardAttack] No DeimosStateMachine found on this GameObject.");
    }

    // ─── Encirclement Watchdog ─────────────────────────────────────────────────────

    /// <summary>Overrides DeimosAttackBase.Update() to add the self-trigger check on top of the
    /// cooldown/variety ticking every attack already does -- base.Update() is called first so
    /// that ticking never gets skipped.</summary>
    protected override void Update()
    {
        base.Update();

        if (IsActive || IsOnCooldown) return;
        if (_stateMachine == null || _stateMachine.CurrentState != DeimosStateMachine.DeimosState.Combat) return;
        if (!_stateMachine.IsLocomotionReady) return;

        _encirclementTimer -= Time.deltaTime;
        if (_encirclementTimer > 0f) return;
        _encirclementTimer = encirclementCheckInterval;

        if (PlayerIsBehind())
        {
            Debug.Log("[JumpBackwardAttack] Encirclement detected -- self-triggering Jump Back.");
            _stateMachine.EnterAttacking();
            Execute();
        }
    }

    bool PlayerIsBehind()
    {
        var hits = Physics.OverlapSphere(transform.position, detectionCheckRadius, playerLayer);
        foreach (var hit in hits)
        {
            Vector3 dir = hit.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) continue; // degenerate: same position, skip

            if (Vector3.Angle(transform.forward, dir.normalized) >= behindAngleThreshold)
                return true;
        }
        return false;
    }

    // ─── DeimosAttackBase Implementation ──────────────────────────────────────────

    protected override void PerformAttack()
    {
        if (_animator == null || _player == null) return;

        // Behind the PLAYER's own facing, not Deimos's -- mirrors PounceAttack.TeleportRoutine's
        // arrival-point math exactly. See class doc comment's Correction note for why this
        // replaced the earlier "away from Deimos's own position" vector.
        Vector3 behindPlayer = _player.position - _player.forward * behindPlayerOffset;

        if (NavMesh.SamplePosition(behindPlayer, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
        {
            _targetPosition = hit.position;
        }
        else if (NavMesh.SamplePosition(_player.position, out NavMeshHit fallback, navMeshSampleRadius, NavMesh.AllAreas))
        {
            // Mirrors PounceAttack's own fallback: land at the player's feet if the behind-point
            // isn't walkable (e.g. the player is backed into a wall).
            _targetPosition = fallback.position;
            Debug.LogWarning("[JumpBackwardAttack] Behind-player NavMesh sample failed -- falling back to player position.");
        }
        else
        {
            // Both samples failed -- no-op hop in place rather than an out-of-bounds Warp().
            _targetPosition = transform.position;
            Debug.LogWarning("[JumpBackwardAttack] NavMesh.SamplePosition failed entirely -- hopping in place.");
        }

        _hasLanded = false;

        if (_agent != null)
        {
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }

        _animator.SetTrigger(TriggerJumpBackwards);
        StartCoroutine(HopRoutine());
        Debug.Log($"[JumpBackwardAttack] Hopping behind player to {_targetPosition}.");
    }

    // ─── Hop Coroutine ───────────────────────────────────────────────────────────

    IEnumerator HopRoutine()
    {
        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < hopDuration && !_hasLanded)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hopDuration);

            float x    = Mathf.Lerp(startPos.x, _targetPosition.x, t);
            float z    = Mathf.Lerp(startPos.z, _targetPosition.z, t);
            float arcY = startPos.y + Mathf.Sin(t * Mathf.PI) * hopHeight;

            transform.position = new Vector3(x, arcY, z);

            if (_agent != null)
                _agent.nextPosition = transform.position;

            yield return null;
        }
    }

    // ─── Animation Event Receivers ───────────────────────────────────────────────

    /// <summary>Landing -- snaps to the target, faces the player, and plays a landing sound. No
    /// damage, no hit check -- this is a repositioning move, not an attack that hits the player.
    /// </summary>
    public override void OnHitboxOpen()
    {
        if (!IsActive) return;
        PlaySound(hitboxOpenClip); // landing thud -- no damage, this attack never hits anything

        _hasLanded = true; // stops HopRoutine if still running

        transform.position = _targetPosition;
        if (_agent != null)
            _agent.Warp(_targetPosition);

        // Face the player on arrival, mirroring PounceAttack -- Deimos just repositioned behind
        // them, so it should be looking at them the instant it lands.
        if (_player != null)
        {
            Vector3 toPlayer = _player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
        }

        Debug.Log("[JumpBackwardAttack] Landed behind the player.");
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

    /// <summary>See GroundSlamAttack.CancelAttack() for the full story -- if the hop is cut short
    /// mid-flight (StopAllCoroutines() in the base call above already killed HopRoutine before
    /// OnHitboxOpen/OnAttackAnimEnd ran), this restores the NavMeshAgent handoff those callbacks
    /// would otherwise have done, so the agent isn't left permanently disconnected.</summary>
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
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, detectionCheckRadius);

        // Rear-cone indicator -- two lines at behindAngleThreshold degrees off forward, mirrored
        // left/right, so the "counts as encirclement" arc is visible when the GameObject is
        // selected.
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Quaternion left  = Quaternion.AngleAxis(-behindAngleThreshold, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(behindAngleThreshold, Vector3.up);
        Gizmos.DrawLine(transform.position, transform.position + (left  * transform.forward) * detectionCheckRadius);
        Gizmos.DrawLine(transform.position, transform.position + (right * transform.forward) * detectionCheckRadius);
    }
}

using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// FSM controller for Deimos -- Move 1 of the heuristic build-up: Idle detection + NavMesh
/// chase (Walk) only. Mirrors BossStateMachine.cs's (ARES/DROID-7) architecture and its
/// already-learned NavMeshAgent gotcha (see UpdateCombat() below), but is a deliberately
/// separate class rather than a shared one, matching this project's existing pattern of one
/// dedicated script per enemy/boss (EnemyChaser/EnemyBrute/EnemyRusher each have their own
/// class despite sharing an FSM shape) -- and because BossHealth's events are static, so
/// reusing it directly for a second boss would collide with ARES's HUD/commentator hookups.
///
/// States (this pass):
///   Idle    -- waiting for player to enter detection radius (Animator plays Typing, its
///              default state, the whole time -- game-logic Idle is not the same thing as
///              the Animator's own "Idle" state, see the animator graph note below)
///   Combat  -- chasing player via NavMeshAgent; fires "FightStarted" once on entry, then the
///              Animator graph itself handles Sit To Stand -> Idle -> Walk from there
///   Dead    -- triggered by DeimosHealth.OnBossDeath; agent disabled permanently
///
/// Move 2 adds:
///   Attacking -- movement paused (_agent.ResetPath()); DeimosAttackRegistry (mirrors
///                BossAttackRegistry) calls EnterAttacking()/ExitAttacking() around a
///                DeimosAttackBase.Execute() the same way ARES's registry does. First
///                (and currently only) attack is MMAKickAttack.
///
/// Added 2026-09-11 (flinch + death wiring, wrapping up Deimos):
///   Staggered -- DeimosHealth's ported EnemyBase-style DPS-threshold "flinch check"
///                (see DeimosHealth.cs's class doc comment) fires OnStaggerStart, this
///                class self-subscribes and reacts: cancels whatever DeimosAttackBase is
///                currently active (CancelAttack() -- see HandleStaggerStart() below for
///                why that alone doesn't already move CurrentState off Attacking),
///                stops the NavMeshAgent, and fires the "Hit" Animator trigger (plays the
///                Hit To Body clip). OnStaggerEnd -- fired by DeimosHealth once
///                staggerDuration elapses, skipped if Deimos died mid-stagger -- resumes
///                Combat. This is a distinct top-level state (not just an IsStaggered
///                bool layered on top of Combat) specifically so DeimosAttackRegistry's
///                existing "CurrentState != Combat -> don't pick a new attack" gate
///                blocks new attacks during the flinch for free, the same way it already
///                does during Attacking -- no registry changes needed.
///   Dead      -- now actually reachable in the Animator: "IsDead" was set every death
///                already, but Chimera.controller had no Any State -> Death transition
///                consuming it (see the old note below, now stale) -- that transition is
///                wired now (fires the Standing Death Forward 02 clip via the pre-existing
///                Death state). No C# change was needed for this half; EnterState(Dead)
///                already sets the bool.
///
/// Audio (added alongside DeimosAudio.cs, Assets/Scripts/Audio/DeimosAudio.cs): a new static
/// OnCombatStart event, fired once from EnterState() the instant Combat begins, right next to
/// the existing "FightStarted" Animator trigger. Unlike ARES -- which fires its encounter
/// stinger/music from an external BossEncounterTrigger (an arena-gate collider Deimos has no
/// equivalent of, since Deimos self-detects the player via detectionRadius rather than the
/// player walking through a scripted arena entrance) -- DeimosAudio just self-subscribes to
/// this event, same "many independent listeners, no central coordinator" shape already used
/// for DeimosHealth.OnPhaseTwo/OnBossDeath. No Inspector wiring needed for that hookup.
///
/// NOT built yet, intentionally (future moves, one at a time, same as ARES's Step 3 attacks
/// were added after its core FSM existed):
///   - PhaseTransition state, if Deimos ends up needing one.
///   - Root-motion handling for attacks that bake XZ movement into the hip (Jump Forward/Back,
///     Superhuman Choke Lift all look like gap-closers/grabs from their clip names -- revisit
///     BossStateMachine's OnAnimatorMove() comment when those get wired).
///
/// Unity Setup:
///   - Add this component to the Deimos prefab/GameObject (same object as its Animator).
///     [RequireComponent] auto-adds NavMeshAgent and DeimosHealth if not already present.
///   - NavMeshAgent: set Speed, Angular Speed, Stopping Distance in Inspector once added.
///   - A NavMesh must be baked covering the room Deimos stands in, or SetDestination() will
///     never succeed (same class of issue documented at length in BossStateMachine.cs/
///     project-boss-fight.md for ARES).
///   - Animator (Chimera.controller), current graph (built in-Editor by Ming Kai, transition
///     wiring/conditions patched in alongside this script 2026-09-11):
///       Entry -> Typing (default state)
///       Typing -> Sit To Stand   [trigger: FightStarted]
///       Sit To Stand -> Idle     [exit time only, plays the stand-up clip through once]
///       Idle -> Walk             [Speed > 0.1]              (tagged "Locomotion")
///       Walk -> Idle             [Speed < 0.05]              (tagged "Locomotion")
///       Any State -> MMAKick     [trigger: MMAKick]          (added 2026-09-11, Move 2)
///       MMAKick -> Idle          [exit time 0.9]             (added 2026-09-11, Move 2)
///       Any State -> Hit         [trigger: Hit, no exit time, cannot transition to self]
///       Hit -> Idle              [exit time 0.9]              (Hit To Body clip)
///       Any State -> Death       [bool: IsDead, no exit time, cannot transition to self]
///       (Death has no outgoing transition -- it's terminal, same as BossStateMachine's
///       Dead state has no Animator equivalent to return from.)
///     This script fires "FightStarted" exactly once, on entering Combat -- everything after
///     that (stand up, then the Idle/Walk blend) is driven by the Animator graph itself from
///     the "Speed" float this script sets every frame. The one thing the graph can't enforce on
///     its own is NavMeshAgent movement: UpdateCombat() checks the "Locomotion" tag on Idle/Walk
///     and holds the agent still (no SetDestination()) until the stand-up animation has actually
///     finished playing, so Deimos doesn't slide toward the player mid-Sit-To-Stand. The same
///     "Locomotion" tag is exposed publicly as IsLocomotionReady so DeimosAttackRegistry can
///     hold off on triggering MMAKick until Deimos has actually finished standing up, even
///     though the Animator's own "Any State" transition would technically allow interrupting
///     Sit To Stand -- we don't want it to.
///
/// Animator parameters required (all present in Chimera.controller as of 2026-09-11):
///   float   "Speed"        -- driven from agent velocity magnitude each frame.
///   trigger "FightStarted" -- fired once when Combat begins (see EnterState()).
///   bool    "IsDead"       -- set true on death; consumed by Any State -> Death (wired
///                             2026-09-11 alongside the Staggered state -- see above).
///   trigger "Hit"          -- fired once per stagger (see EnterState()'s Staggered case).
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(DeimosHealth))]
public class DeimosStateMachine : MonoBehaviour
{
    // ─── Events ─────────────────────────────────────────────────────────────────

    /// <summary>Fired once from EnterState() the instant Deimos transitions into Combat --
    /// the same moment the "FightStarted" Animator trigger fires. DeimosAudio self-subscribes
    /// to this for its combat-start stinger/music, since Deimos has no external arena-entry
    /// trigger the way ARES does (see class doc comment).</summary>
    public static event Action OnCombatStart;

    // ─── State Enum ─────────────────────────────────────────────────────────────

    public enum DeimosState { Idle, Combat, Attacking, Staggered, Dead }

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Radius at which Deimos detects the player and enters Combat.")]
    public float detectionRadius = 15f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public DeimosState CurrentState { get; private set; }

    /// <summary>True once Deimos is actually standing in Idle or Walk (Animator "Locomotion"
    /// tag), false while Typing/Sit To Stand/etc. are still playing. DeimosAttackRegistry
    /// gates attack decisions on this so MMAKick can't fire mid-stand-up -- same reasoning as
    /// UpdateCombat()'s own check below, just shared publicly instead of duplicated.</summary>
    public bool IsLocomotionReady => _animator.GetCurrentAnimatorStateInfo(0).IsTag(LocomotionTag);

    // ─── Animator Parameter Hashes ──────────────────────────────────────────────
    // Names must match Chimera.controller's actual parameters exactly (Ming Kai's naming,
    // PascalCase) -- NOT BossStateMachine.cs's lowercase "speed"/"isDead" convention for ARES.
    private static readonly int SpeedHash        = Animator.StringToHash("Speed");
    private static readonly int FightStartedHash = Animator.StringToHash("FightStarted");
    private static readonly int IsDeadHash       = Animator.StringToHash("IsDead");
    private static readonly int HitHash          = Animator.StringToHash("Hit");

    // AnimatorStateInfo.IsTag() only has a string overload (no StringToHash fast path) --
    // tagged onto Idle and Walk in Chimera.controller, see UpdateCombat().
    private const string LocomotionTag = "Locomotion";

    // ─── Component References ────────────────────────────────────────────────────
    private NavMeshAgent  _agent;
    private Animator      _animator;
    private DeimosHealth  _health;
    private Transform     _player;

    // ─── Checkpoint respawn (captured for future use, not wired yet) ────────────
    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _health   = GetComponent<DeimosHealth>();

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        DeimosHealth.OnBossDeath    += HandleBossDeath;
        DeimosHealth.OnStaggerStart += HandleStaggerStart;
        DeimosHealth.OnStaggerEnd   += HandleStaggerEnd;
    }

    void OnDestroy()
    {
        DeimosHealth.OnBossDeath    -= HandleBossDeath;
        DeimosHealth.OnStaggerStart -= HandleStaggerStart;
        DeimosHealth.OnStaggerEnd   -= HandleStaggerEnd;
    }

    void Start()
    {
        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;
        else
            Debug.LogWarning("[DeimosStateMachine] No PlayerHealth found in scene.");

        EnterState(DeimosState.Idle);
    }

    void Update()
    {
        // Animator speed driven from agent velocity -- never set manually in state enter.
        _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);

        switch (CurrentState)
        {
            case DeimosState.Idle:   UpdateIdle();   break;
            case DeimosState.Combat: UpdateCombat(); break;
        }
    }

    /// <summary>
    /// Empty OnAnimatorMove suppresses automatic root motion while keeping applyRootMotion =
    /// true (so Unity keeps calling this method) -- NavMeshAgent owns all position in Idle/
    /// Combat. Same convention as BossStateMachine.cs. A future gap-closer move that bakes XZ
    /// drift into the hip (Jump Forward/Back, Superhuman Choke Lift) will selectively apply
    /// animator.deltaPosition here via a flag, same as that class's doc comment describes.
    /// </summary>
    void OnAnimatorMove() { }

    // ─── State Updates ───────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        if (_player == null) return;
        if (Vector3.Distance(transform.position, _player.position) <= detectionRadius)
            EnterState(DeimosState.Combat);
    }

    void UpdateCombat()
    {
        if (_player == null) return;

        // Don't let the NavMeshAgent move Deimos while it's still standing up. FightStarted
        // fires Typing -> Sit To Stand the instant Combat begins, but the stand-up animation
        // takes real time to play (plus the Sit To Stand -> Idle crossfade), and OnAnimatorMove()
        // below suppresses root motion -- meaning without this guard the agent would immediately
        // start sliding Deimos toward the player while it's still visually getting up. "Idle"
        // and "Walk" are tagged "Locomotion" in Chimera.controller specifically so this check
        // doesn't have to hardcode either state's name -- any future replacement/addition to
        // the locomotion set just needs the same tag.
        if (!IsLocomotionReady)
        {
            _agent.ResetPath();
            return;
        }

        // Same fix already discovered and confirmed for ARES (BossStateMachine.UpdateCombat()):
        // the player's collider pivot sits above the actual walkable NavMesh surface, and
        // NavMeshAgent.SetDestination() silently fails when the raw target is off-mesh by more
        // than its (strict) internal tolerance. Always ground-project the target first.
        Vector3 destination = _player.position;
        if (NavMesh.SamplePosition(_player.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            destination = hit.position;

        _agent.SetDestination(destination);
    }

    // ─── State Transitions ───────────────────────────────────────────────────────

    void EnterState(DeimosState next)
    {
        DeimosState previous = CurrentState;
        CurrentState = next;
        Debug.Log($"[DeimosStateMachine] -> {next}");

        switch (next)
        {
            case DeimosState.Idle:
                _agent.ResetPath();
                break;

            case DeimosState.Combat:
                // Only treat this as an actual fight-start (Animator "Sit To Stand" cue +
                // OnCombatStart) when Combat is being entered fresh -- from Idle (real
                // detection), Dead/whatever-else (ResetForNewEpisode/ResetForCheckpointRespawn),
                // etc. Attacking -> Combat (ExitAttacking(), fired after every single attack)
                // and Staggered -> Combat (HandleStaggerEnd(), fired after every flinch) are
                // both just resuming a fight already in progress, not starting one -- Deimos is
                // already standing, so re-firing "FightStarted" would be a harmless no-op
                // Animator-wise, but OnCombatStart is NOT harmless to re-fire: DeimosAudio's
                // HandleCombatStart() calls GameAudioManager.PlayBossMusic(), which hard
                // Stop()s + Play()s the BGM source with no "already playing this clip" guard --
                // so without this check, combat music restarted from the top after every single
                // attack (reported bug, 2026-09-11: see stack trace ending in
                // JumpBackwardAttack.OnAttackAnimEnd -> ... -> ExitAttacking ->
                // EnterState(Combat)).
                bool isFreshEncounterStart =
                    previous != DeimosState.Attacking && previous != DeimosState.Staggered;
                if (isFreshEncounterStart)
                {
                    _animator.SetTrigger(FightStartedHash);
                    OnCombatStart?.Invoke();
                }
                break;

            case DeimosState.Attacking:
                // Movement stops while an attack plays out -- DeimosAttackRegistry calls
                // EnterAttacking() right before Execute()-ing the chosen DeimosAttackBase, and
                // ExitAttacking() (-> back to Combat) once that attack's OnAttackEnded fires.
                // The Animator's own "Any State -> MMAKick" transition is what actually starts
                // the clip; this just makes sure the NavMeshAgent doesn't keep dragging Deimos
                // toward the player while the kick plays.
                _agent.ResetPath();
                break;

            case DeimosState.Staggered:
                // HandleStaggerStart() below already cancelled any in-progress attack before
                // calling EnterState(Staggered) -- this just makes sure the agent is actually
                // stopped (it might not have been mid-Combat-chase) and plays the reaction.
                _agent.ResetPath();
                _animator.SetTrigger(HitHash);
                break;

            case DeimosState.Dead:
                _agent.ResetPath();
                // Safe to fully disable on death -- we never re-enable after this.
                _agent.enabled = false;
                _animator.SetBool(IsDeadHash, true);
                // Any State -> Death (Standing Death Forward 02) is wired in Chimera.controller
                // as of 2026-09-11 -- this bool is what actually drives it now.
                break;
        }
    }

    // ─── Public API (called by DeimosAttackRegistry) ─────────────────────────────

    public void EnterAttacking() => EnterState(DeimosState.Attacking);
    public void ExitAttacking()  => EnterState(DeimosState.Combat);

    // ─── Public API (RL/training only, once Deimos gets a training env -- unused for now) ──

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Not called anywhere yet -- kept for parity with BossStateMachine.SetTarget() in case a
    /// training environment is built for Deimos later, same as ARES's.</summary>
    public void SetTarget(Transform target) => _player = target;

    /// <summary>Re-arms the state machine after a health reset: re-enables the NavMeshAgent,
    /// clears the IsDead animator flag, and re-enters Combat immediately (skips Idle's
    /// proximity wait). Mirrors BossStateMachine.ResetForNewEpisode() -- kept as its own method
    /// (rather than inlined into ResetForCheckpointRespawn() below) for the same reason ARES's
    /// is: a future Deimos training env would call this after DeimosHealth.ResetHealth() too.</summary>
    public void ResetForNewEpisode()
    {
        _agent.enabled = true;
        _animator.SetBool(IsDeadHash, false);
        EnterState(DeimosState.Combat);
    }

    // ─── Public API (live-game checkpoint respawn) ───────────────────────────────

    /// <summary>
    /// Full "refight from scratch" reset for a checkpoint respawn while Deimos is still alive
    /// (the player died mid-fight, not to Deimos's own death). Mirrors
    /// BossStateMachine.ResetForCheckpointRespawn() exactly:
    ///   1. Cancels any attack currently mid-execution (e.g. SuperhumanChokeLiftAttack's grabbed
    ///      player, JumpForwardAttack/JumpBackwardAttack's disconnected NavMeshAgent) so a
    ///      coroutine holding manual control can't keep running on stale pre-reset state --
    ///      see DeimosAttackBase.CancelAttack()'s own doc comment, same reasoning
    ///      HandleStaggerStart() above already relies on.
    ///   2. Warps the agent back to Deimos's original spawn position/rotation (captured in
    ///      Awake()). Agent is enabled first -- Warp() is a silent no-op on a disabled agent.
    ///   3. Resets health (and stagger state) via DeimosHealth.ResetHealth().
    ///   4. Re-arms the state machine via ResetForNewEpisode() so combat resumes immediately --
    ///      the player just respawned right there, no need to wait through Idle's detection
    ///      check.
    /// No-op if Deimos has already died -- CheckpointManager should never reach this case in
    /// practice (killing a boss is permanent, same as ARES).
    /// </summary>
    public void ResetForCheckpointRespawn()
    {
        if (_health != null && !_health.IsAlive) return;

        foreach (var attack in GetComponents<DeimosAttackBase>())
            attack.CancelAttack();

        _agent.enabled = true;
        _agent.Warp(_spawnPosition);
        transform.rotation = _spawnRotation;

        _health?.ResetHealth();
        ResetForNewEpisode();
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────────

    void HandleBossDeath() => EnterState(DeimosState.Dead);

    /// <summary>
    /// Reacts to DeimosHealth.OnStaggerStart. Cancels whatever DeimosAttackBase is currently
    /// active FIRST, then enters Staggered -- order matters: DeimosAttackBase.CancelAttack()
    /// deliberately does NOT invoke OnAttackEnded (see that method's own doc comment), so if
    /// CurrentState was Attacking, DeimosAttackRegistry's HandleAttackEnded() will never fire
    /// to move CurrentState off Attacking on its own. EnterState(Staggered) here is what
    /// actually does that -- same as it would need to for any other interruption of an
    /// in-progress attack. Safe to call this every time regardless of what Deimos was doing
    /// (Combat, Attacking, or even Idle) -- CancelAttack() itself no-ops on an attack that
    /// isn't currently active.
    /// </summary>
    void HandleStaggerStart()
    {
        if (CurrentState == DeimosState.Dead) return;

        foreach (var attack in GetComponents<DeimosAttackBase>())
            attack.CancelAttack();

        EnterState(DeimosState.Staggered);
    }

    /// <summary>
    /// Reacts to DeimosHealth.OnStaggerEnd (not fired at all if Deimos died mid-stagger --
    /// see DeimosHealth.StaggerRoutine()). Guards against CurrentState having moved on for
    /// some other reason in the meantime (defensive, mirrors HandleAttackEnded's own guard
    /// in DeimosAttackRegistry) before resuming Combat.
    /// </summary>
    void HandleStaggerEnd()
    {
        if (CurrentState != DeimosState.Staggered) return;
        EnterState(DeimosState.Combat);
    }

    // ─── Debug ───────────────────────────────────────────────────────────────────

    [ContextMenu("Test: Force Combat")]
    void Debug_ForceCombat() => EnterState(DeimosState.Combat);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

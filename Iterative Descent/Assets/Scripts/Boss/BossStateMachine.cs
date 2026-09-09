using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// FSM controller for DROID-7.
///
/// States:
///   Idle            -- waiting for player to enter detection radius
///   Combat          -- chasing player via NavMeshAgent
///   Attacking       -- stub; movement paused. BossAttackRegistry (Step 3) calls EnterAttacking / ExitAttacking.
///   PhaseTransition -- fires on BossHealth.OnPhaseTwo; pauses briefly, then resumes Combat
///   Dead            -- triggered by BossHealth.OnBossDeath; agent disabled permanently
///
/// Unity Setup:
///   - Attach to the DROID-7 root GameObject (same object as Animator and NavMeshAgent).
///   - NavMeshAgent: set Speed, Angular Speed, Stopping Distance in Inspector.
///   - Animator: applyRootMotion must be TRUE. This script defines OnAnimatorMove (empty)
///     which suppresses automatic root motion so NavMeshAgent owns all position.
///     For Mixamo leap clips that bake XZ into the hip, manually apply animator.deltaPosition
///     during those attacks in Step 3.
///
/// Animator parameters required:
///   float "speed"     -- driven from agent velocity magnitude each frame
///   bool  "isPhase2"  -- set true on PhaseTransition enter
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BossHealth))]
public class BossStateMachine : MonoBehaviour
{
    // ─── State Enum ─────────────────────────────────────────────────────────────

    public enum BossState { Idle, Combat, Attacking, PhaseTransition, Dead }

    // ─── Inspector ──────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Radius at which the boss detects the player and enters Combat.")]
    public float detectionRadius = 15f;

    [Header("Phase Transition")]
    [Tooltip("How long the boss pauses at 50% HP before resuming Combat.")]
    public float phaseTransitionDuration = 2f;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public BossState CurrentState { get; private set; }

    // ─── Animator Parameter Hashes ──────────────────────────────────────────────
    private static readonly int SpeedHash    = Animator.StringToHash("speed");
    private static readonly int IsPhase2Hash = Animator.StringToHash("isPhase2");
    private static readonly int IsDeadHash   = Animator.StringToHash("isDead");

    // ─── Component References ────────────────────────────────────────────────────
    private NavMeshAgent _agent;
    private Animator     _animator;
    private BossHealth   _health;
    private Transform    _player;

    // ─── Checkpoint respawn ─────────────────────────────────────────────────────
    // Captured once in Awake() -- the boss can wander well away from its original
    // position mid-fight, so ResetForCheckpointRespawn() below needs somewhere concrete
    // to warp it back to.
    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _health   = GetComponent<BossHealth>();

        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        BossHealth.OnPhaseTwo  += HandlePhaseTwo;
        BossHealth.OnBossDeath += HandleBossDeath;
    }

    void OnDestroy()
    {
        BossHealth.OnPhaseTwo  -= HandlePhaseTwo;
        BossHealth.OnBossDeath -= HandleBossDeath;
    }

    void Start()
    {
        var playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            _player = playerHealth.transform;
        else
            Debug.LogWarning("[BossStateMachine] No PlayerHealth found in scene.");

        EnterState(BossState.Idle);
    }

    void Update()
    {
        // Animator speed driven from agent velocity -- never set manually in state enter.
        _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);

        switch (CurrentState)
        {
            case BossState.Idle:   UpdateIdle();   break;
            case BossState.Combat: UpdateCombat(); break;
        }
    }

    /// <summary>
    /// Empty OnAnimatorMove suppresses automatic root motion application
    /// while keeping applyRootMotion = true so Unity keeps calling this method.
    /// NavMeshAgent owns all position in Idle / Combat.
    /// Step 3 attacks that use Mixamo hip drift (GroundSlam, Pounce) will
    /// selectively apply animator.deltaPosition here via a flag.
    /// </summary>
    void OnAnimatorMove() { }

    // ─── State Updates ───────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        if (_player == null) return;
        if (Vector3.Distance(transform.position, _player.position) <= detectionRadius)
            EnterState(BossState.Combat);
    }

    void UpdateCombat()
    {
        if (_player == null) return;

        // 2026-08-28 fix: the scripted opponents' capsule pivot sits ~1 unit above the actual
        // walkable NavMesh surface (their transform.position.y is the capsule center, not
        // ground contact). Feeding that raw, elevated position straight into SetDestination()
        // silently fails every time -- NavMeshAgent.SetDestination() has a much stricter
        // internal tolerance for how far off-mesh a target can be than NavMesh.SamplePosition()
        // does, and a full 1-unit vertical gap exceeds it. Confirmed via direct testing: the
        // raw call returned false with hasPath staying false forever (the boss never chased at
        // all, only gap-closer attacks that move it manually ever looked like movement), while
        // ground-projecting the exact same target first made it succeed immediately. So: always
        // sample the nearest point on the NavMesh before handing it to SetDestination().
        Vector3 destination = _player.position;
        if (NavMesh.SamplePosition(_player.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            destination = hit.position;

        _agent.SetDestination(destination);
    }

    // ─── State Transitions ───────────────────────────────────────────────────────

    void EnterState(BossState next)
    {
        CurrentState = next;
        Debug.Log($"[BossStateMachine] -> {next}");

        switch (next)
        {
            case BossState.Idle:
                _agent.ResetPath();
                break;

            case BossState.Combat:
                // Agent resumes chasing normally -- no speed override here.
                break;

            case BossState.Attacking:
                // Stop movement while an attack plays out.
                // BossAttackRegistry (Step 3) will call EnterAttacking / ExitAttacking.
                _agent.ResetPath();
                break;

            case BossState.PhaseTransition:
                _agent.ResetPath();
                _animator.SetBool(IsPhase2Hash, true);
                Invoke(nameof(EndPhaseTransition), phaseTransitionDuration);
                break;

            case BossState.Dead:
                _agent.ResetPath();
                _animator.SetBool(IsDeadHash, true);
                // Safe to fully disable on death -- we never re-enable after this.
                _agent.enabled = false;
                break;
        }
    }

    void EndPhaseTransition()
    {
        // Guard in case the boss died during the transition pause.
        if (CurrentState != BossState.PhaseTransition) return;
        EnterState(BossState.Combat);
    }

    // ─── Public API (called by BossAttackRegistry in Step 3) ────────────────────

    public void EnterAttacking() => EnterState(BossState.Attacking);
    public void ExitAttacking()  => EnterState(BossState.Combat);

    // ─── Public API (RL/training only, called by BossTrainingEnv) ───────────────

    /// <summary>Overrides the auto-found FindFirstObjectByType&lt;PlayerHealth&gt;() target.
    /// Called once per episode by BossTrainingEnv (Training.unity) to point the boss at
    /// whichever scripted opponent is active this episode. Never called in the live game.</summary>
    public void SetTarget(Transform target) => _player = target;

    /// <summary>Re-arms the state machine for a new training episode after a boss death:
    /// re-enables the NavMeshAgent, clears the isDead/isPhase2 animator flags, and re-enters
    /// Combat immediately (skips Idle's proximity wait -- the training opponent's position is
    /// already known). Call AFTER BossHealth.ResetHealth(). Never called in the live game.</summary>
    public void ResetForNewEpisode()
    {
        _agent.enabled = true;
        _animator.SetBool(IsDeadHash, false);
        _animator.SetBool(IsPhase2Hash, false);
        EnterState(BossState.Combat);
    }

    // ─── Public API (live-game checkpoint respawn) ───────────────────────────────

    /// <summary>
    /// Full "refight from scratch" reset for a checkpoint respawn while the boss is still
    /// alive (the player died mid-fight, not to the boss's own death). Distinct from
    /// ResetForNewEpisode() above -- that one is training-only and assumes the caller
    /// (BossTrainingEnv) already repositioned/reset everything else itself. This one owns
    /// the whole sequence:
    ///   1. Cancels any attack currently mid-execution -- a coroutine that seized manual
    ///      control (e.g. SprintChargeAttack's NavMeshAgent takeover, PounceAttack's hidden
    ///      renderers -- see BossAttackBase.CancelAttack()'s own doc comment) would otherwise
    ///      keep running on stale pre-reset state and clobber what this method sets next.
    ///   2. Warps the agent back to the boss's original arena spawn position/rotation
    ///      (captured in Awake()). Agent is enabled first -- Warp()/SetDestination() are
    ///      silent no-ops on a disabled agent.
    ///   3. Resets health via BossHealth.ResetHealth().
    ///   4. Re-arms the state machine via ResetForNewEpisode() so combat resumes immediately
    ///      -- the player just respawned right there, no need to wait through Idle's
    ///      detection-radius check.
    /// No-op if the boss has already died -- killing it ends the demo, there's nothing to
    /// reset, and CheckpointManager should never reach this case in practice.
    /// </summary>
    public void ResetForCheckpointRespawn()
    {
        if (_health != null && !_health.IsAlive) return;

        foreach (var attack in GetComponents<BossAttackBase>())
            attack.CancelAttack();

        _agent.enabled = true;
        _agent.Warp(_spawnPosition);
        transform.rotation = _spawnRotation;

        _health?.ResetHealth();
        ResetForNewEpisode();
    }

    // ─── Event Handlers ──────────────────────────────────────────────────────────

    void HandlePhaseTwo()  => EnterState(BossState.PhaseTransition);
    void HandleBossDeath() => EnterState(BossState.Dead);

    // ─── Debug ───────────────────────────────────────────────────────────────────

    [ContextMenu("Test: Force Combat")]
    void Debug_ForceCombat() => EnterState(BossState.Combat);

    [ContextMenu("Test: Force PhaseTransition")]
    void Debug_ForcePhaseTransition() => EnterState(BossState.PhaseTransition);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}

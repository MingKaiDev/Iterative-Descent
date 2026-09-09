using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Rusher enemy variant -- distinct animator from EnemyChaser.
/// Uses three discrete Bool params for movement and alternating Trigger params
/// for left/right swipe attacks (right-first).
///
/// ─── State machine ────────────────────────────────────────────────────────
///
///   [Idle] ──Activate()──► [Sprinting] ──within 12m──► [Walking]
///                              ▲                             │
///                              └───── beyond 12m ───────────┘
///                                                           │
///                                              within 6m    │
///                                                    ▼      │
///                                              [Mauling] ───┘
///                                              (walk + attack simultaneously)
///
///   [Dead] ◄── health == 0 (from any state, handled by EnemyBase)
///
/// ─── Distance thresholds ─────────────────────────────────────────────────
///   rushDistance  (default 12m) : beyond = sprint, within = walk
///   maulDistance  (default  6m) : within = walk AND attack simultaneously
///
/// ─── Speeds ──────────────────────────────────────────────────────────────
///   Sprint : walkSpeed * rushMultiplier  (default 5 * 3 = 15)
///   Walk   : walkSpeed                   (default 5)
///   Maul   : walkSpeed                   (same as walk -- closes in while swinging)
///
/// ─── DDA integration ─────────────────────────────────────────────────────
///   EnemyDirector.SpeedMultiplier applies to walkSpeed at Activate().
///   The rush speed scales with it automatically (walkSpeed * rushMultiplier),
///   so the 3x sprint ratio is always preserved.
///   EnemyDirector.DamageMultiplier is passed to RusherAttack.ApplyDamageMultiplier().
///
/// ─── Required components (on same GameObject) ────────────────────────────
///   NavMeshAgent  (auto-required by EnemyBase)
///   RusherAttack  (auto-required here)
///
/// ─── Animator parameters to create ───────────────────────────────────────
///   Bool  "Fast Run" -- true while sprinting (beyond 12m)
///   Bool  "Walking"  -- true while walking or mauling (within 12m)
///   Bool  "IsDead"   -- set true on death
///
///   Movement states are mutually exclusive:
///     Sprinting  -> Fast Run=true,  Walking=false
///     Walking    -> Fast Run=false, Walking=true
///     Mauling    -> Fast Run=false, Walking=true  (agent still moving while swinging)
///     Idle/Dead  -> Fast Run=false, Walking=false
///
///   Attack triggers (Right Swipe / Left Swipe) are configured and fired
///   by RusherAttack -- not this script.
/// </summary>
[RequireComponent(typeof(RusherAttack))]
public class EnemyRusher : EnemyBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Movement")]
    [Tooltip("Base walking speed (within 12m). Sprint = this * rushMultiplier.")]
    [SerializeField] private float walkSpeed       = 5f;
    [Tooltip("Speed multiplier applied when sprinting (beyond rushDistance). Default 3x.")]
    [SerializeField] private float rushMultiplier  = 3f;
    [Tooltip("Beyond this distance the Rusher sprints. Within it, the Rusher walks.")]
    [SerializeField] private float rushDistance    = 12f;
    [Tooltip("Within this distance the Rusher walks AND attacks simultaneously.")]
    [SerializeField] private float maulDistance    = 6f;
    [Tooltip("Seconds between NavMesh path recalculations.")]
    [SerializeField] private float pathUpdateRate  = 0.2f;
    [Tooltip("Distance at which the Rusher stops advancing and just faces+attacks. " +
             "Prevents the agent from pushing the player. Should be slightly less than maulDistance.")]
    [SerializeField] private float stopDistance    = 1.8f;

    [Header("Rotation")]
    [Tooltip("Rotation speed while walking (deg/s). Higher = snappier turn.")]
    [SerializeField] private float walkTurnSpeed   = 240f;
    [Tooltip("Rotation speed while sprinting (deg/s). Fast enough to handle sharp turns at speed.")]
    [SerializeField] private float sprintTurnSpeed = 480f;
    [Tooltip("Rotation speed while mauling (deg/s). Very fast to face the player during swings.")]
    [SerializeField] private float maulTurnSpeed   = 720f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string fastRunParam = "Fast Run";
    [SerializeField] private string walkingParam = "Walking";
    [SerializeField] private string deadParam    = "IsDead";
    // Right Swipe and Left Swipe triggers are configured in RusherAttack, not here.

    // ─── Private ──────────────────────────────────────────────────────────────

    private enum State { Idle, Sprinting, Walking, Mauling, Dead }
    private State _state = State.Idle;

    private RusherAttack _rusherAttack;
    private float        _pathTimer;

    // Cached at Awake -- DDA multiplier is applied as a ratio on top of this
    private float _baseWalkSpeed;

    // Animator hashes -- built once in Awake from the serialised param names
    private int _fastRunHash;
    private int _walkingHash;
    private int _deadHash;

    // ─── EnemyBase overrides ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();

        _rusherAttack  = GetComponent<RusherAttack>();
        _baseWalkSpeed = walkSpeed;

        _agent.stoppingDistance = 0f;
        _agent.speed            = walkSpeed;
        // Take full manual control of rotation so it never fights the agent.
        // RotateToward() drives turning in every state tick.
        _agent.updateRotation   = false;

        _fastRunHash = Animator.StringToHash(fastRunParam);
        _walkingHash = Animator.StringToHash(walkingParam);
        _deadHash    = Animator.StringToHash(deadParam);

        _state = State.Idle;
    }

    protected override void OnActivate()
    {
        if (EnemyDirector.Instance != null)
        {
            walkSpeed = _baseWalkSpeed * EnemyDirector.Instance.SpeedMultiplier;
            _rusherAttack.ApplyDamageMultiplier(EnemyDirector.Instance.DamageMultiplier);
        }

        // Reset swipe sequence so every encounter starts with a right swipe
        _rusherAttack.ResetSwipeSequence();

        // Choose initial state based on current distance
        if (_target != null)
        {
            float dist = Vector3.Distance(transform.position, _target.position);
            _state = ChooseStateForDistance(dist);
            ApplyMovementBools(_state);
        }
        else
        {
            _state = State.Walking;
            ApplyMovementBools(_state);
        }

        _pathTimer = 0f;
    }

    protected override void OnDeactivate()
    {
        _state = State.Idle;
        ApplyMovementBools(State.Idle);
    }

    protected override void OnDie()
    {
        _state = State.Dead;
        ApplyMovementBools(State.Dead);
        SetAnimBool(_deadHash, true);
        HandleCorpseDespawn(3f);
    }

    protected override void OnResetForRetry()
    {
        _state = State.Idle;
        ApplyMovementBools(State.Idle);
        SetAnimBool(_deadHash, false);
    }

    protected override void OnHit(float amount, Vector3 hitPoint)
    {
        Debug.Log($"[EnemyRusher] Hit for {amount} at {hitPoint}. HP: {_currentHealth}");
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Dead || _target == null) return;

        float dist = Vector3.Distance(transform.position, _target.position);

        State prevState = _state;

        switch (_state)
        {
            case State.Sprinting: TickSprint(dist); break;
            case State.Walking:   TickWalk(dist);   break;
            case State.Mauling:   TickMaul(dist);   break;
        }

        // Only push bool changes to the animator when the state actually changed
        // to avoid calling SetBool every frame unnecessarily.
        if (_state != prevState)
            ApplyMovementBools(_state);
    }

    // ─── State ticks ──────────────────────────────────────────────────────────

    private void TickSprint(float dist)
    {
        _agent.speed            = walkSpeed * rushMultiplier;
        _agent.stoppingDistance = 0f;
        RotateToward(sprintTurnSpeed);
        UpdatePath();

        if (dist <= maulDistance)
            EnterMaul();
        else if (dist <= rushDistance)
            _state = State.Walking;
    }

    private void TickWalk(float dist)
    {
        _agent.speed            = walkSpeed;
        _agent.stoppingDistance = 0f;
        RotateToward(walkTurnSpeed);
        UpdatePath();

        if (dist > rushDistance)
            _state = State.Sprinting;
        else if (dist <= maulDistance)
            EnterMaul();
    }

    private void TickMaul(float dist)
    {
        RotateToward(maulTurnSpeed);
        _rusherAttack.TryAttack();

        if (dist > stopDistance)
        {
            // Not yet in striking range -- close in slowly
            _agent.speed            = walkSpeed;
            _agent.stoppingDistance = stopDistance;
            if (_agent.isStopped) _agent.isStopped = false;
            UpdatePath();
        }
        else
        {
            // Close enough -- freeze the agent so it stops pushing the player.
            // Only rotate and attack from here.
            if (!_agent.isStopped) _agent.isStopped = true;
        }

        if (dist > rushDistance)
            _state = State.Sprinting;
        else if (dist > maulDistance)
        {
            if (_agent.isStopped) _agent.isStopped = false;
            _agent.stoppingDistance = 0f;
            _state = State.Walking;
        }
    }

    // ─── Animator ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets Fast Run and Walking bools to match the current state.
    /// Called only when the state changes -- not every frame.
    /// </summary>
    private void ApplyMovementBools(State state)
    {
        bool fastRun = state == State.Sprinting;
        bool walking = state == State.Walking || state == State.Mauling;

        SetAnimBool(_fastRunHash, fastRun);
        SetAnimBool(_walkingHash, walking);
    }

    // ─── State helpers ────────────────────────────────────────────────────────

    private void EnterMaul()
    {
        if (_agent.isOnNavMesh && _agent.isStopped)
            _agent.isStopped = false;
        _state = State.Mauling;
    }

    private State ChooseStateForDistance(float dist)
    {
        if (dist <= maulDistance)  return State.Mauling;
        if (dist <= rushDistance)  return State.Walking;
        return State.Sprinting;
    }

    // ─── Path update ──────────────────────────────────────────────────────────

    private void UpdatePath()
    {
        _pathTimer -= Time.deltaTime;
        if (_pathTimer > 0f) return;

        _pathTimer = pathUpdateRate;
        if (_agent.isOnNavMesh && _target != null)
            _agent.SetDestination(_target.position);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Rotates toward the player at a given degrees-per-second speed.
    /// Always called manually -- NavMeshAgent.updateRotation is false,
    /// so the agent never fights this rotation.
    /// </summary>
    private void RotateToward(float degreesPerSecond)
    {
        if (_target == null) return;
        Vector3 dir = _target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, target, degreesPerSecond * Time.deltaTime);
    }

    private void SetAnimBool(int hash, bool value)
    {
        if (_animator != null) _animator.SetBool(hash, value);
    }

    // ─── Scene gizmos ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Yellow sphere = maul range (walk + attack)
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, maulDistance);

        // Orange sphere = rush threshold (sprint beyond, walk within)
        Gizmos.color = new Color(1f, 0.45f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, rushDistance);
    }
}

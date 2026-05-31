using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Concrete enemy — chases the player via NavMeshAgent, transitions to
/// attacking when close enough, and hands back to chasing if the player
/// runs away. Extends EnemyBase so health, death, and IDamageable are free.
///
/// ─── State machine ────────────────────────────────────────────────────────
///
///   [Idle] ──Activate()──► [Chasing] ──in range──► [Attacking]
///                              ▲                         │
///                              └─── player moves away ───┘
///                              [Dead] ◄── health == 0 (from any state)
///
/// ─── Required components (on same GameObject) ─────────────────────────────
///   • NavMeshAgent   (auto-required by EnemyBase)
///   • EnemyAttack    (auto-required here)
///
/// ─── Animator parameters to create ───────────────────────────────────────
///   Float  "Speed"    — driven by agent velocity magnitude
///   Trigger "Attack"  — fired by EnemyAttack.TryAttack()
///   Bool   "IsDead"   — set true on death
/// </summary>
[RequireComponent(typeof(EnemyAttack))]
public class EnemyChaser : EnemyBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Chase")]
    [SerializeField] private float chaseSpeed     = 4f;
    [Tooltip("Distance at which the enemy stops chasing and starts attacking.")]
    [SerializeField] private float attackRange    = 2f;
    [Tooltip("Seconds between NavMesh path recalculations. Lower = more responsive, higher = cheaper.")]
    [SerializeField] private float pathUpdateRate = 0.2f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParamName  = "Speed";
    [SerializeField] private string deadParamName   = "IsDead";

    // ─── Private ──────────────────────────────────────────────────────────────

    private enum State { Idle, Chasing, Attacking, Dead }
    private State _state = State.Idle;

    private EnemyAttack      _attack;
    private EnemyChaserAudio _audio;
    private float            _pathTimer;

    // Cache animator hashes once — slightly faster than string lookups each frame
    private int _speedHash;
    private int _deadHash;

    // ─── EnemyBase overrides ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake(); // sets up _agent, _animator, _currentHealth

        _attack = GetComponent<EnemyAttack>();
        _audio  = GetComponent<EnemyChaserAudio>(); // null-safe; optional component

        _agent.speed            = chaseSpeed;
        // Stop slightly before the attack threshold so the agent doesn't
        // overshoot into the player's mesh. A small margin (0.1) keeps it
        // within range without pushing the bodies together visually.
        _agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);

        // Build hashes after serialised names are available
        _speedHash = Animator.StringToHash(speedParamName);
        _deadHash  = Animator.StringToHash(deadParamName);

        _state = State.Idle;
        // Base Awake already sets agent.enabled = false and this.enabled = false
    }

    protected override void OnActivate()
    {
        _state = State.Chasing;
        // _agent.isStopped is already set to false by EnemyBase.Activate()

        // Apply EnemyDirector DDA multipliers at activation time so the correct
        // tier is always used, even if the director was not ready at Awake.
        if (EnemyDirector.Instance != null)
        {
            _agent.speed = chaseSpeed * EnemyDirector.Instance.SpeedMultiplier;
            _attack.ApplyDamageMultiplier(EnemyDirector.Instance.DamageMultiplier);
        }
    }

    protected override void OnDeactivate()
    {
        _state = State.Idle;
        SetAnimFloat(_speedHash, 0f);
    }

    protected override void OnDie()
    {
        _state = State.Dead;
        SetAnimBool(_deadHash, true);
        SetAnimFloat(_speedHash, 0f);

        _audio?.PlayDeath();

        // TODO: ragdoll, death VFX, score reward, notify GameManager
        Destroy(gameObject, 3f);
    }

    protected override void OnHit(float amount, Vector3 hitPoint)
    {
        // TODO: play hit-stagger animation, spawn blood VFX at hitPoint
        Debug.Log($"[EnemyChaser] Hit for {amount} at {hitPoint}. HP remaining: {_currentHealth}");
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Dead || _target == null) return;

        float dist = Vector3.Distance(transform.position, _target.position);

        switch (_state)
        {
            case State.Chasing:   TickChase(dist);   break;
            case State.Attacking: TickAttack(dist);  break;
        }

        // Drive animator blend tree with current movement speed
        SetAnimFloat(_speedHash, _agent.velocity.magnitude);
    }

    // ─── State ticks ──────────────────────────────────────────────────────────

    private void TickChase(float dist)
    {
        // Throttle path recalculation — SetDestination every frame is expensive
        _pathTimer -= Time.deltaTime;
        if (_pathTimer <= 0f)
        {
            _pathTimer = pathUpdateRate;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(_target.position);
        }

        // Enter attack state when close enough — fully stop the agent
        if (dist <= attackRange)
        {
            StopAgent();
            _state = State.Attacking;
        }
    }

    private void TickAttack(float dist)
    {
        FaceTarget();

        // Delegate actual hit timing and animation to EnemyAttack
        _attack.TryAttack();

        // Player escaped — immediately resume chase with a fresh path
        if (dist > attackRange * 1.25f)
        {
            ResumeChase();
        }
    }

    // ─── Agent stop / resume helpers ──────────────────────────────────────────

    private void StopAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();         // clears cached path so the agent doesn't drift
        _state = State.Attacking;
    }

    private void ResumeChase()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(_target.position); // start pathing immediately
        _pathTimer = 0f;            // force next throttled update to refresh path
        _state = State.Chasing;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void FaceTarget()
    {
        Vector3 dir = (_target.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 8f);
    }

    private void SetAnimFloat(int hash, float value)
    {
        if (_animator != null) _animator.SetFloat(hash, value);
    }

    private void SetAnimBool(int hash, bool value)
    {
        if (_animator != null) _animator.SetBool(hash, value);
    }

    // ─── Scene gizmos ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Red sphere = attack range
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

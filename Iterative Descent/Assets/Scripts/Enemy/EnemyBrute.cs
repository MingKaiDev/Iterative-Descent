using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Brute enemy variant -- heavy melee chaser with a wakeup roar and a rare
/// instant-kill grab, on top of a two-hit swipe combo. Extends EnemyBase so
/// health, death, and IDamageable are free (same convention as EnemyChaser /
/// EnemyRusher). Attack selection/damage/instant-kill logic lives entirely in
/// BruteAttack -- this script only owns movement/state and the wakeup scream.
///
/// ─── State machine ────────────────────────────────────────────────────────
///
///   [Idle] ──Activate()──► [WakingUp] ──wakeupLockDuration──► [Chasing] ──in range──► [Attacking]
///                                                                 ▲                        │
///                                                                 └── player moves away ───┘
///                                    [Dead] ◄── health == 0 (from any state)
///
/// WakingUp fires the Scream trigger and freezes the agent for wakeupLockDuration
/// seconds before the Brute starts moving. This fires every time Activate() is
/// called, not just the first time ever -- a Brute that goes dormant (Deactivate)
/// and is later reactivated re-plays the scream, same as a real "waking up" beat.
///
/// ─── Required components (on same GameObject) ─────────────────────────────
///   • NavMeshAgent  (auto-required by EnemyBase)
///   • BruteAttack   (auto-required here)
///
/// ─── Animator parameters to create ───────────────────────────────────────
///   Float   "Speed"  — driven by agent velocity magnitude
///   Trigger "Scream" — Zombie Scream clip, fired once per Activate()
///   Bool    "IsDead" — set true on death
///   (BruteAttack configures its own five swipe/grab triggers separately --
///   see BruteAttack.cs's header comment for the full Animator wiring list,
///   including the "wire from Any State" lesson learned on CyberSoldier.)
/// </summary>
[RequireComponent(typeof(BruteAttack))]
public class EnemyBrute : EnemyBase
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Chase")]
    [SerializeField] private float chaseSpeed     = 3.2f;
    [Tooltip("Distance at which the Brute stops chasing and starts attacking.")]
    [SerializeField] private float attackRange    = 2.4f;
    [Tooltip("Seconds between NavMesh path recalculations.")]
    [SerializeField] private float pathUpdateRate = 0.2f;

    [Header("Animator Parameter Names")]
    [SerializeField] private string speedParamName  = "Speed";
    [SerializeField] private string deadParamName   = "IsDead";
    [SerializeField] private string screamParamName = "Scream";

    [Header("Wakeup Scream")]
    [Tooltip("Seconds the Brute stands still and roars (Zombie Scream) before it starts " +
             "chasing. Match this to the length of the scream clip so movement doesn't cut " +
             "it off. Fires every time this Brute is Activate()'d, including re-activations " +
             "after Deactivate(), not just the first time ever.")]
    [SerializeField] private float wakeupLockDuration = 1.6f;

    // ─── Private ──────────────────────────────────────────────────────────────

    private enum State { Idle, WakingUp, Chasing, Attacking, Dead }
    private State _state = State.Idle;

    private BruteAttack _attack;
    private float        _pathTimer;
    private Coroutine    _wakeupRoutine;

    private int _speedHash;
    private int _deadHash;
    private int _screamHash;

    // ─── EnemyBase overrides ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake(); // sets up _agent, _animator, _currentHealth

        _attack = GetComponent<BruteAttack>();

        _agent.speed            = chaseSpeed;
        _agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);

        _speedHash  = Animator.StringToHash(speedParamName);
        _deadHash   = Animator.StringToHash(deadParamName);
        _screamHash = Animator.StringToHash(screamParamName);

        _state = State.Idle;
        // Base Awake already sets agent.enabled = false and this.enabled = false
    }

    protected override void OnActivate()
    {
        // Apply EnemyDirector DDA multipliers at activation time, same convention as
        // EnemyChaser/EnemyRusher. Grab stays an instant kill regardless of tier -- see
        // BruteAttack.ApplyDamageMultiplier's doc comment.
        if (EnemyDirector.Instance != null)
        {
            _agent.speed = chaseSpeed * EnemyDirector.Instance.SpeedMultiplier;
            _attack.ApplyDamageMultiplier(EnemyDirector.Instance.DamageMultiplier);
        }

        if (_wakeupRoutine != null) StopCoroutine(_wakeupRoutine);
        _wakeupRoutine = StartCoroutine(WakeUpRoutine());
    }

    protected override void OnDeactivate()
    {
        _state = State.Idle;
        if (_wakeupRoutine != null)
        {
            StopCoroutine(_wakeupRoutine);
            _wakeupRoutine = null;
        }
        SetAnimFloat(_speedHash, 0f);
    }

    protected override void OnDie()
    {
        _state = State.Dead;
        if (_wakeupRoutine != null)
        {
            StopCoroutine(_wakeupRoutine);
            _wakeupRoutine = null;
        }
        SetAnimBool(_deadHash, true);
        SetAnimFloat(_speedHash, 0f);

        // TODO: ragdoll, death VFX, score reward, notify GameManager -- same as EnemyChaser.
        Destroy(gameObject, 3f);
    }

    protected override void OnHit(float amount, Vector3 hitPoint)
    {
        // TODO: hit-stagger animation, blood VFX -- same as EnemyChaser/EnemyRusher.
        Debug.Log($"[EnemyBrute] Hit for {amount} at {hitPoint}. HP remaining: {_currentHealth}");
    }

    // ─── Wakeup ───────────────────────────────────────────────────────────────

    private IEnumerator WakeUpRoutine()
    {
        _state = State.WakingUp;

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath(); // matches EnemyChaser.StopAgent's rationale -- isStopped
                                 // alone leaves a cached path that can cause drift
        }

        if (_animator != null)
            _animator.SetTrigger(_screamHash);

        yield return new WaitForSeconds(wakeupLockDuration);

        if (_agent.isOnNavMesh)
            _agent.isStopped = false;

        _wakeupRoutine = null;
        _state = State.Chasing;
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        // WakingUp is handled entirely by the coroutine above -- Update() just waits it out.
        if (_state == State.Dead || _state == State.WakingUp || _target == null)
        {
            SetAnimFloat(_speedHash, _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f);
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        switch (_state)
        {
            case State.Chasing:   TickChase(dist);   break;
            case State.Attacking: TickAttack(dist);  break;
        }

        SetAnimFloat(_speedHash, _agent.velocity.magnitude);
    }

    // ─── State ticks ──────────────────────────────────────────────────────────

    private void TickChase(float dist)
    {
        _pathTimer -= Time.deltaTime;
        if (_pathTimer <= 0f)
        {
            _pathTimer = pathUpdateRate;
            if (_agent.isOnNavMesh)
                _agent.SetDestination(_target.position);
        }

        if (dist <= attackRange)
        {
            StopAgent();
            _state = State.Attacking;
        }
    }

    private void TickAttack(float dist)
    {
        FaceTarget();

        // Delegate the Grab-vs-swipe decision and all hit timing to BruteAttack.
        _attack.TryAttack();

        // Only break off to chase once the current swing/grab has actually
        // finished (BruteAttack.IsAttacking false) -- otherwise a player who
        // steps out of range mid-swing yanks the agent back into a walk while
        // the attack animation is still playing, which reads as the animation
        // getting cut off. Letting it resolve first is what makes the swipe/
        // grab "carry through" instead of snapping. TryAttack() above is a
        // no-op while an attack is already in progress, so calling it every
        // frame here is harmless.
        if (dist > attackRange * 1.25f && !_attack.IsAttacking)
            ResumeChase();
    }

    // ─── Agent stop / resume helpers ──────────────────────────────────────────

    private void StopAgent()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
        _state = State.Attacking;
    }

    private void ResumeChase()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = false;
        _agent.SetDestination(_target.position);
        _pathTimer = 0f;
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
        // Red sphere = attack range (both swipe combos and Grab trigger from here)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

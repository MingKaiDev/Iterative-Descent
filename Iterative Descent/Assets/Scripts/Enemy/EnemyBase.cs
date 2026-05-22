using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base class for all enemies.
/// Owns: health, death, and shared component references.
/// Subclasses own: movement patterns, attack logic, state transitions.
///
/// Implements both IEnemy (AI lifecycle) and IDamageable (hit response)
/// so any code that knows about either interface can interact with an enemy.
///
/// ─── How to create a new enemy type ───────────────────────────────────────
///   1. Create a new class that extends EnemyBase.
///   2. Override the four abstract hooks (OnActivate, OnDeactivate, OnDie, OnHit).
///   3. Add your movement / attack logic in Update() or separate components.
///   4. Put EnemyEventBridge in the scene and assign your enemy to it.
/// ──────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour, IEnemy, IDamageable
{
    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;

    // ─── Protected state (accessible to all subclasses) ───────────────────────

    protected float      _currentHealth;
    protected bool       _isDead;
    protected Transform  _target;          // the player transform
    protected NavMeshAgent _agent;
    protected Animator     _animator;      // found in children (your character rig)

    // ─── IEnemy ───────────────────────────────────────────────────────────────

    public bool IsDead => _isDead;

    public virtual void Activate(Transform target)
    {
        if (_isDead) return;
        _target = target;
        _agent.enabled = true;          // ensure agent is on before touching isStopped
        if (_agent.isOnNavMesh)
            _agent.isStopped = false;
        enabled = true;
        OnActivate();
        // Notify metrics tracker — resets per-encounter counters on first enemy of each encounter
        PlayerMetricsTracker.Instance?.NotifyEnemyActivated();
    }

    public virtual void Deactivate()
    {
        if (_agent.isOnNavMesh)
            _agent.isStopped = true;
        enabled = false;
        OnDeactivate();
    }

    public virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;
        if (_agent.isOnNavMesh)
            _agent.isStopped = true;
        _agent.enabled = false;         // prevents NavMesh errors after death
        enabled = false;
        OnDie();
        // Notify metrics tracker — fires OnEncounterEnd when last active enemy dies
        PlayerMetricsTracker.Instance?.NotifyEnemyKilled();
    }

    // ─── IDamageable ──────────────────────────────────────────────────────────

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (_isDead) return;
        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        OnHit(amount, hitPoint);
        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _currentHealth = maxHealth;

        // Disable root motion — NavMeshAgent drives position entirely.
        // Root motion + NavMeshAgent fight each other and cause walking-on-spot.
        if (_animator != null)
            _animator.applyRootMotion = false;

        // Start dormant — EnemyEventBridge calls Activate() to wake the enemy.
        // Use agent.enabled = false instead of isStopped because isStopped
        // throws if the agent hasn't been placed on a NavMesh yet (Awake runs
        // before the first physics frame bakes the agent's position).
        _agent.enabled = false;
        enabled = false;
    }

    // ─── Abstract hooks ── subclasses MUST override all four ──────────────────

    /// <summary>Called immediately after Activate(). Start your state machine here.</summary>
    protected abstract void OnActivate();

    /// <summary>Called immediately after Deactivate(). Pause states / animations.</summary>
    protected abstract void OnDeactivate();

    /// <summary>Called once when health reaches zero. Play death anim, cleanup, etc.</summary>
    protected abstract void OnDie();

    /// <summary>Called every time TakeDamage is called (before death check). Play hit react.</summary>
    protected abstract void OnHit(float amount, Vector3 hitPoint);
}

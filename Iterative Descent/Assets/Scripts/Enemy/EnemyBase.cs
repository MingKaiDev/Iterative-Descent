using System;
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

    [Header("Death")]
    [Tooltip("Small upward nudge applied after the death-time NavMesh ground-snap. " +
             "NavMesh height data is voxelized (a simplified approximation of the real " +
             "floor mesh), so a raw snap can land a few cm below the visible floor and " +
             "read as the corpse clipping into the ground. Increase if it still clips, " +
             "decrease (or set to 0) if it now floats instead.")]
    [SerializeField] protected float deathGroundSnapOffset = 0.03f;

    // ─── Protected state (accessible to all subclasses) ───────────────────────

    protected float      _currentHealth;
    protected bool       _isDead;
    protected Transform  _target;          // the player transform
    protected NavMeshAgent _agent;
    protected Animator     _animator;      // found in children (your character rig)

    // ─── Static Events ────────────────────────────────────────────────────────

    /// <summary>
    /// Fired once per enemy death, carrying the world position of the corpse.
    /// ItemSpawner and other systems subscribe to this for drop/reward logic.
    /// </summary>
    public static event Action<Vector3> OnAnyEnemyDied;

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

        // Safety net against corpses freezing at the wrong height (e.g. an
        // agent that ended up slightly off-height, a bad death-animation
        // import setting, or any other cause) -- snap to the nearest real
        // NavMesh surface point before the agent stops correcting position.
        // 2f search radius is generous enough to find the floor under normal
        // circumstances without snapping to an unrelated NavMesh area.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit groundHit, 2f, NavMesh.AllAreas))
        {
            Vector3 snapped = groundHit.position;
            snapped.y += deathGroundSnapOffset;
            transform.position = snapped;
        }

        _agent.enabled = false;         // prevents NavMesh errors after death
        enabled = false;
        OnDie();
        // Notify metrics tracker — fires OnEncounterEnd when last active enemy dies
        PlayerMetricsTracker.Instance?.NotifyEnemyKilled();
        // Broadcast death position for drop/reward systems
        OnAnyEnemyDied?.Invoke(transform.position);
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

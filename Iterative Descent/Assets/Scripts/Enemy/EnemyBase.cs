using System;
using System.Collections;
using System.Collections.Generic;
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

    [Header("Stagger (DPS Threshold)")]
    [Tooltip("If the damage taken within staggerWindowSeconds implies a damage-per-second " +
             "rate at or above this value, the enemy staggers: NavMesh movement pauses and " +
             "OnStaggerStart() fires. Tune per enemy type -- e.g. a Brute's higher max HP " +
             "usually wants a higher threshold than a normal chaser.")]
    [SerializeField] protected float staggerDpsThreshold = 40f;
    [Tooltip("Rolling time window (seconds) used to compute recent DPS for the stagger check. " +
             "A shotgun blast landing 3 pellets within this window sums their damage together.")]
    [SerializeField] protected float staggerWindowSeconds = 1f;
    [Tooltip("Seconds the enemy stays staggered -- NavMeshAgent is stopped (isStopped = true, " +
             "path cleared) for this long before movement resumes.")]
    [SerializeField] protected float staggerDuration = 1f;
    [Tooltip("Minimum seconds between the END of one stagger and the next one being allowed " +
             "to trigger. Prevents a couple of stray hits right after recovering from " +
             "immediately re-triggering another stagger.")]
    [SerializeField] protected float staggerCooldown = 1.5f;
    [Tooltip("Each successive stagger during the same activation multiplies the effective " +
             "staggerDpsThreshold by this factor -- e.g. 1.5 means the 2nd stagger needs 1.5x " +
             "the DPS, the 3rd needs 2.25x, and so on, so a sustained beam/full-auto weapon " +
             "can't chain-stunlock an enemy forever. Fully resets back to 1x on (re)Activate() " +
             "(new encounter), and also decays gradually mid-encounter -- see " +
             "staggerResistanceDecayDelay/Rate below -- so a long fight with natural lulls " +
             "(reloads, repositioning) isn't limited to a single stagger ever.")]
    [SerializeField] protected float staggerResistanceGrowth = 1.5f;
    [Tooltip("Seconds since the resistance multiplier last changed before it starts decaying " +
             "back toward 1x. Represents the enemy \"catching its breath\" -- if the player " +
             "keeps landing hits inside this window the resistance stays at full strength " +
             "(still can't be chain-stunlocked), but a lull of this long lets a later burst " +
             "stagger it again without needing an ever-escalating amount of DPS.")]
    [SerializeField] protected float staggerResistanceDecayDelay = 4f;
    [Tooltip("Exponential decay rate (1/seconds) applied to the multiplier's excess above 1x " +
             "once staggerResistanceDecayDelay has elapsed with no change. Higher = faster " +
             "recovery. ~0.35 roughly halves the excess every 2 seconds.")]
    [SerializeField] protected float staggerResistanceDecayRate = 0.35f;
    [Tooltip("A SINGLE hit dealing at least this much raw damage instantly staggers, " +
             "bypassing the rolling-DPS window check entirely -- lets one heavy weapon " +
             "(e.g. a bolt-action rifle round) reliably stagger by itself without needing " +
             "to land multiple hits inside staggerWindowSeconds, unlike a spread/chip " +
             "weapon (e.g. shotgun pellets, each a separate small hit) which still has to " +
             "clear staggerDpsThreshold the normal way. Still scaled by " +
             "_staggerResistanceMultiplier like the windowed check, so it can't be spammed " +
             "into a permanent flinch-lock either. Leave very high (default) to disable and " +
             "rely purely on the windowed DPS check.")]
    [SerializeField] protected float staggerBigHitDamage = 99999f;

    // ─── Protected state (accessible to all subclasses) ───────────────────────

    protected float      _currentHealth;
    private   float      _baseMaxHealth;   // maxHealth as set in the Inspector, before EnemyDirector scaling
    protected bool       _isDead;
    protected Transform  _target;          // the player transform
    protected NavMeshAgent _agent;
    protected Animator     _animator;      // found in children (your character rig)

    // ─── Stagger runtime state ─────────────────────────────────────────────────

    private readonly List<(float time, float amount)> _recentHits = new();
    private float     _staggerLockedUntil;  // Time.time value; no new stagger check while Time.time < this
    private Coroutine _staggerRoutine;
    private float     _staggerResistanceMultiplier = 1f;  // grows by staggerResistanceGrowth per stagger, resets on Activate()
    private float     _lastResistanceChangeTime;          // Time.time the multiplier was last set/decayed -- anchors the decay calc

    /// <summary>True while a DPS-triggered stagger is in progress (NavMesh paused).</summary>
    public bool IsStaggered { get; private set; }

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
        // Fresh encounter -- stagger resistance built up from a previous fight (or an earlier
        // stretch of this same one, if this enemy was deactivated and reactivated) shouldn't
        // carry over silently.
        _staggerResistanceMultiplier = 1f;
        _lastResistanceChangeTime = Time.time;

        // DDA toughness scaling -- rescale from the original Inspector value every
        // Activate() call (never compounding an already-scaled maxHealth), and heal
        // to the newly scaled max so a tier change always grants this enemy its full
        // scaled health pool for the encounter it's entering.
        float healthMult = EnemyDirector.Instance != null ? EnemyDirector.Instance.EnemyHealthMultiplier : 1f;
        maxHealth      = _baseMaxHealth * healthMult;
        _currentHealth = maxHealth;

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

        // DDA mitigation -- flat percentage reduction applied before anything else
        // touches the damage number, so health loss, hit reactions, and the stagger
        // DPS check all agree on what actually landed.
        float mitigation      = EnemyDirector.Instance != null ? EnemyDirector.Instance.DamageMitigation : 0f;
        float effectiveDamage = amount * (1f - mitigation);

        _currentHealth = Mathf.Max(0f, _currentHealth - effectiveDamage);
        OnHit(effectiveDamage, hitPoint);
        CheckStaggerOnHit(effectiveDamage);
        if (_currentHealth <= 0f)
            Die();
    }

    // ─── Stagger (DPS threshold) ────────────────────────────────────────────────

    /// <summary>
    /// Records this hit's damage/timestamp, prunes anything older than
    /// staggerWindowSeconds, and triggers a stagger if EITHER the resulting rolling DPS
    /// meets staggerDpsThreshold, OR this single hit alone meets staggerBigHitDamage (see
    /// that field's tooltip -- lets a heavy single-shot weapon skip the windowed combo
    /// requirement). No-op while dead, already staggered, or still on staggerCooldown from
    /// a previous stagger.
    /// </summary>
    private void CheckStaggerOnHit(float amount)
    {
        if (_isDead || IsStaggered) return;
        if (Time.time < _staggerLockedUntil) return;

        DecayStaggerResistance();

        _recentHits.Add((Time.time, amount));
        _recentHits.RemoveAll(hit => Time.time - hit.time > staggerWindowSeconds);

        float windowDamage = 0f;
        foreach (var hit in _recentHits)
            windowDamage += hit.amount;

        float currentDps = windowDamage / staggerWindowSeconds;
        float effectiveThreshold = staggerDpsThreshold * _staggerResistanceMultiplier;
        float effectiveBigHit = staggerBigHitDamage * _staggerResistanceMultiplier;
        bool bigHitOverride = amount >= effectiveBigHit;
        if (!bigHitOverride && currentDps < effectiveThreshold) return;

        _recentHits.Clear();
        // Locked for the full stagger + cooldown so a fresh burst of hits landing
        // while already staggered can't queue up an immediate second stagger the
        // instant this one ends.
        _staggerLockedUntil = Time.time + staggerDuration + staggerCooldown;

        // Next stagger needs proportionally more DPS -- diminishing returns so one
        // weapon can't permanently lock the enemy in a flinch loop. Fully resets in
        // Activate() (new encounter); also decays mid-encounter -- see
        // DecayStaggerResistance() -- so a long fight isn't limited to a single stagger.
        _staggerResistanceMultiplier *= staggerResistanceGrowth;
        _lastResistanceChangeTime = Time.time;

        if (_staggerRoutine != null) StopCoroutine(_staggerRoutine);
        _staggerRoutine = StartCoroutine(StaggerRoutine());
    }

    /// <summary>
    /// Eases staggerResistanceMultiplier back toward 1x once staggerResistanceDecayDelay
    /// seconds have passed since it last changed. Called at the top of CheckStaggerOnHit
    /// (i.e. lazily, only when a new hit actually needs an up-to-date effective threshold)
    /// rather than every frame, so no Update() polling is needed here.
    /// </summary>
    private void DecayStaggerResistance()
    {
        if (_staggerResistanceMultiplier <= 1f) return;

        float elapsedSinceChange = Time.time - _lastResistanceChangeTime;
        if (elapsedSinceChange <= staggerResistanceDecayDelay) return;

        float decaySeconds = elapsedSinceChange - staggerResistanceDecayDelay;
        float decayFactor  = Mathf.Exp(-staggerResistanceDecayRate * decaySeconds);
        _staggerResistanceMultiplier = 1f + (_staggerResistanceMultiplier - 1f) * decayFactor;
        _lastResistanceChangeTime = Time.time; // restart the clock from now
    }

    private IEnumerator StaggerRoutine()
    {
        IsStaggered = true;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        OnStaggerStart();

        yield return new WaitForSeconds(staggerDuration);

        IsStaggered = false;
        _staggerRoutine = null;

        if (_isDead) yield break; // died mid-stagger -- nothing left to resume

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;

        OnStaggerEnd();
    }

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _baseMaxHealth = maxHealth;   // snapshot the designer-set value before any DDA scaling
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

    // ─── Stagger hooks (virtual -- override only if you need to react) ────────────

    /// <summary>
    /// Called once when a DPS-triggered stagger begins, right after the NavMeshAgent
    /// is stopped. Default no-op. Override to interrupt an in-progress attack windup,
    /// play a "Stagger" animator trigger, silence movement audio, etc. -- see
    /// EnemyChaser/EnemyBrute for examples.
    /// </summary>
    protected virtual void OnStaggerStart() { }

    /// <summary>
    /// Called once staggerDuration has elapsed and the NavMeshAgent has been resumed
    /// (skipped entirely if the enemy died mid-stagger). Default no-op. Override to
    /// restore whatever state machine value OnStaggerStart interrupted -- e.g. force
    /// back into a Chasing state and re-issue SetDestination.
    /// </summary>
    protected virtual void OnStaggerEnd() { }
}

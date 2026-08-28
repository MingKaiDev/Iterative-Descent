using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Abstract base for scripted (non-RL) training opponents that fight DROID-7 inside
/// Training.unity. Concrete subclasses (PlayerAgentAggressive, PlayerAgentCircler,
/// PlayerAgentKiter) only implement DecideMovement() -- everything shared (simulated
/// ranged attack, decision-interval ticking, move/fire timing exposed for BossAgent's
/// observation vector in Step 5) lives here.
///
/// These are heuristic stand-ins for a real player during BossAgent PPO training --
/// NOT used anywhere in the live game (Level 1.unity has no PlayerAgent* components).
///
/// Unity Setup:
///   - Attach alongside a TrainingDummyHealth (auto-added via RequireComponent) and a
///     NavMeshAgent on the same GameObject (one of Training.unity's 3 Agent slots).
///   - Assign bossHealth / bossTransform in the Inspector, or leave null and have
///     BossTrainingEnv (Step 4) wire them at episode start -- both are null-guarded.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(TrainingDummyHealth))]
public abstract class PlayerAgentBase : MonoBehaviour
{
    // ─── Inspector: References ───────────────────────────────────────────────────
    [Header("References")]
    [Tooltip("Optional. Lets this opponent react to boss HP/phase later. Null-guarded if unassigned.")]
    public BossHealth bossHealth;
    [Tooltip("Boss transform to move/aim relative to. Null-guarded -- agent stays idle if unassigned.")]
    public Transform bossTransform;

    // ─── Inspector: Combat ────────────────────────────────────────────────────────
    [Header("Combat")]
    public float shotDamage = 25f;
    public float fireInterval = 1f;
    [Range(0f, 1f)]
    public float missChance = 0.15f;
    public float shootRange = 30f;

    // ─── Inspector: Decision Timing ─────────────────────────────────────────────
    [Header("Decision Timing")]
    [Tooltip("How often (seconds) this opponent re-evaluates its movement decision.")]
    public float decisionInterval = 0.3f;
    [Tooltip("Random +/- jitter applied to decisionInterval each tick so multiple opponents don't sync up.")]
    public float decisionJitter = 0.1f;

    // ─── Public Read-Only State (feeds BossAgent's observation vector, Step 5) ──
    /// <summary>Seconds since this opponent last attempted a shot (hit or miss).</summary>
    public float TimeSinceLastFired => Time.time - _lastFireTime;
    /// <summary>Seconds since this opponent was last moving (NavMeshAgent velocity above threshold).</summary>
    public float TimeSinceLastMoved => Time.time - _lastMoveTime;
    /// <summary>Current NavMeshAgent velocity (world space). Zero if the agent isn't ready yet.
    /// Exposed for BossAgent's observation vector (Step 5) -- relative velocity / angular velocity around the boss.</summary>
    public Vector3 Velocity => Agent != null ? Agent.velocity : Vector3.zero;

    // ─── Protected (available to subclasses) ────────────────────────────────────
    protected NavMeshAgent        Agent  { get; private set; }
    protected TrainingDummyHealth Health { get; private set; }

    // ─── Private ────────────────────────────────────────────────────────────────
    private float _decisionTimer;
    private float _fireTimer;
    private float _lastFireTime;
    private float _lastMoveTime;
    private const float MoveVelocityThreshold = 0.1f;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        Agent  = GetComponent<NavMeshAgent>();
        Health = GetComponent<TrainingDummyHealth>();
        _lastFireTime = Time.time;
        _lastMoveTime = Time.time;
    }

    protected virtual void Update()
    {
        if (Health != null && !Health.IsAlive) return;

        TickDecision();
        TickFire();
        TrackMovement();
    }

    // ─── Decision / Fire Ticking ─────────────────────────────────────────────────

    void TickDecision()
    {
        _decisionTimer -= Time.deltaTime;
        if (_decisionTimer > 0f) return;

        _decisionTimer = Mathf.Max(0.05f, decisionInterval + Random.Range(-decisionJitter, decisionJitter));
        if (bossTransform != null)
            DecideMovement();
    }

    void TickFire()
    {
        if (bossTransform == null) return;

        _fireTimer -= Time.deltaTime;
        if (_fireTimer > 0f) return;
        _fireTimer = fireInterval;

        float dist = Vector3.Distance(transform.position, bossTransform.position);
        if (dist > shootRange) return;

        _lastFireTime = Time.time;
        if (Random.value < missChance) return; // simulated miss -- still counts as "fired" for timing purposes

        var damageable = bossTransform.GetComponent<IDamageable>();
        damageable?.TakeDamage(shotDamage, bossTransform.position);
    }

    void TrackMovement()
    {
        if (Agent != null && Agent.velocity.magnitude > MoveVelocityThreshold)
            _lastMoveTime = Time.time;
    }

    // ─── Subclass Contract ────────────────────────────────────────────────────────

    /// <summary>
    /// Called every decisionInterval (+/- jitter) while bossTransform is assigned.
    /// Implementations should call Agent.SetDestination(...) (or leave it alone to hold
    /// position) to express this archetype's movement style.
    /// </summary>
    protected abstract void DecideMovement();
}

using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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
///   Float   "Speed"   — driven by agent velocity magnitude
///   Trigger "Punch1"  — fired on odd swings (see HandleAttackFired)
///   Trigger "Punch2"  — fired on even swings
///   Bool    "IsDead"  — set true on death
///   (The "Attack" trigger EnemyAttack fires by default is unused here —
///   HandleAttackFired() subscribes to OnAttackFired, which suppresses it.
///   "Attack" can stay in the Animator Controller unused, or be removed.)
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
    [Tooltip("Animator trigger fired once when a DPS-triggered stagger begins " +
             "(see EnemyBase.OnStaggerStart). Must exist as a Trigger parameter -- " +
             "safe to leave the Animator Controller without one, SetAnimFloat/trigger " +
             "calls are all null-guarded via _animator == null checks.")]
    [SerializeField] private string staggerParamName = "Stagger";

    [Header("Attack Animation")]
    [Tooltip("Animator trigger fired on odd swings (1st, 3rd, ...). Must exist " +
             "as a Trigger parameter in the Animator Controller, wired to the " +
             "'Zombie Punch 1' clip's state -- see FYP/setup-guides for wiring steps.")]
    [SerializeField] private string punch1TriggerName = "Punch1";
    [Tooltip("Animator trigger fired on even swings (2nd, 4th, ...). Wired to " +
             "the 'Zombie Punch 2' clip's state.")]
    [SerializeField] private string punch2TriggerName = "Punch2";
    [Tooltip("Seconds until the hit check fires for a Punch1 swing. Zombie " +
             "Punch 1 and Punch 2 are different-length clips (84 vs 31 frames) " +
             "so they cannot share one delay -- tune each separately by " +
             "watching the [EnemyAttack] HIT CHECK ACTIVE log against the swing.")]
    [SerializeField] private float punch1HitboxDelay = 0.4f;
    [Tooltip("Seconds until the hit check fires for a Punch2 swing.")]
    [SerializeField] private float punch2HitboxDelay = 0.4f;

    [Header("Attack Windup")]
    [Tooltip("Degrees to rotate away from facing the player during the attack " +
             "windup, before the swing resolves. Sign is viewed from above -- " +
             "if it visually rotates the wrong way (clockwise instead of " +
             "anti-clockwise), flip the sign.")]
    [SerializeField] private float windupRotationDegrees = -30f;

    [Header("Auto-Activation (independent of EncounterTrigger / EnemyEventBridge)")]
    [Tooltip("If the player gets this close to a dormant (never-yet-activated) EnemyChaser, " +
             "it wakes up and starts chasing on its own. Checked on a timer, not every frame, " +
             "because this enemy's Update() does not run while dormant. Set to 0 to disable " +
             "proximity auto-activation for this specific enemy. Deliberately bypasses " +
             "EncounterTrigger's DDA-scaled activation count -- this fires from a direct " +
             "player action (walking up) rather than a scripted room-wide encounter start.")]
    [SerializeField] private float autoActivateRadius = 8f;
    [Tooltip("Seconds between proximity checks while dormant. Lower = more responsive, " +
             "higher = cheaper. Irrelevant once activated (the check cancels itself).")]
    [SerializeField] private float autoActivateCheckInterval = 0.25f;
    [Tooltip("Tag used to find the player for auto-activation purposes before this enemy has " +
             "ever been given an explicit target via Activate(). Same convention as EncounterTrigger.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Height (metres, local up) at which the line-of-sight check is cast between enemy " +
             "and player for proximity auto-activation, so a dormant enemy in the next room " +
             "doesn't wake up just because it's within radius through a wall. Roughly chest " +
             "height by default -- raise/lower if rooms have unusual floor offsets.")]
    [SerializeField] private float losHeight = 1f;

    // ─── Private ──────────────────────────────────────────────────────────────

    private enum State { Idle, Chasing, Attacking, Dead }
    private State _state = State.Idle;

    private EnemyAttack      _attack;
    private EnemyChaserAudio _audio;
    private float            _pathTimer;

    private bool       _isWindingUp;
    private Coroutine  _windupRoutine;

    private bool _nextPunchIsOne = true;

    // Cache animator hashes once — slightly faster than string lookups each frame
    private int _speedHash;
    private int _deadHash;
    private int _punch1Hash;
    private int _punch2Hash;
    private int _staggerHash;

    // Lazily-resolved player reference used only for auto-activation, before
    // this enemy has ever received an explicit target via Activate().
    private Transform _autoActivateTarget;

    // Obstruction layer used for the proximity line-of-sight check. Same layer
    // name the Boss attack scripts already use for wall checks (e.g.
    // SprintChargeAttack, CoreOverloadAttack) -- see LayerMask.GetMask calls there.
    private int _wallLayer;

    // ─── EnemyBase overrides ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake(); // sets up _agent, _animator, _currentHealth

        _attack = GetComponent<EnemyAttack>();
        _audio  = GetComponent<EnemyChaserAudio>(); // null-safe; optional component

        _attack.OnAttackWindupStart += HandleWindupStart;
        _attack.OnAttackWindupEnd   += HandleWindupEnd;
        _attack.OnAttackFired       += HandleAttackFired;

        _agent.speed            = chaseSpeed;
        // Stop slightly before the attack threshold so the agent doesn't
        // overshoot into the player's mesh. A small margin (0.1) keeps it
        // within range without pushing the bodies together visually.
        _agent.stoppingDistance = Mathf.Max(0f, attackRange - 0.1f);

        // Build hashes after serialised names are available
        _speedHash   = Animator.StringToHash(speedParamName);
        _deadHash    = Animator.StringToHash(deadParamName);
        _punch1Hash  = Animator.StringToHash(punch1TriggerName);
        _punch2Hash  = Animator.StringToHash(punch2TriggerName);
        _staggerHash = Animator.StringToHash(staggerParamName);

        _state = State.Idle;
        // Base Awake already sets agent.enabled = false and this.enabled = false

        _wallLayer = LayerMask.GetMask("Level 1 Obstacle");

        // Start the proximity auto-activation poll. InvokeRepeating keeps firing
        // even while this component is disabled (enabled = false), unlike Update(),
        // which is exactly what's needed to detect the player approaching a
        // dormant enemy that no EncounterTrigger/EnemyEventBridge has woken yet.
        if (autoActivateRadius > 0f)
            InvokeRepeating(nameof(CheckProximityAutoActivate), autoActivateCheckInterval, autoActivateCheckInterval);
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

    private void OnDestroy()
    {
        if (_attack != null)
        {
            _attack.OnAttackWindupStart -= HandleWindupStart;
            _attack.OnAttackWindupEnd   -= HandleWindupEnd;
            _attack.OnAttackFired       -= HandleAttackFired;
        }
    }

    /// <summary>
    /// Supplies EnemyAttack's custom-animation extension point (see EnemyAttack.OnAttackFired
    /// doc comment). Assigning this suppresses EnemyAttack's built-in "Attack" trigger entirely,
    /// so this is now the only thing that fires an attack animation on CyberSoldier -- alternates
    /// Punch1/Punch2 each swing, same alternation pattern RusherAttack uses for its swipes.
    /// </summary>
    private void HandleAttackFired()
    {
        bool isOne      = _nextPunchIsOne;
        _nextPunchIsOne = !_nextPunchIsOne;

        // Set the correct per-clip delay BEFORE the trigger fires -- EnemyAttack
        // reads hitboxDelay right after this call returns (for the windup
        // rotation duration) and again when its hit-check coroutine resolves.
        _attack.SetHitboxDelay(isOne ? punch1HitboxDelay : punch2HitboxDelay);

        if (_animator == null) return;
        _animator.SetTrigger(isOne ? _punch1Hash : _punch2Hash);
    }

    protected override void OnDeactivate()
    {
        _state = State.Idle;
        SetAnimFloat(_speedHash, 0f);

        // Not currently called anywhere in the project, but if something ever
        // puts this enemy back to sleep, re-arm the proximity poll so it can
        // still be woken by the player again later.
        if (autoActivateRadius > 0f)
            InvokeRepeating(nameof(CheckProximityAutoActivate), autoActivateCheckInterval, autoActivateCheckInterval);
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

        // Suppress the hit sound on the killing blow -- EnemyBase.TakeDamage()
        // has already subtracted damage from _currentHealth by the time OnHit
        // runs, so _currentHealth <= 0 here means this hit is lethal and
        // Die() (which plays the death sounds) is about to fire right after.
        if (_currentHealth > 0f)
            _audio?.PlayHit();

        // A dormant enemy that gets shot wakes up and starts chasing, on top
        // of (not instead of) the normal EncounterTrigger / EnemyEventBridge
        // scripted activation. No range limit -- any hit that connects means
        // the player is already engaging this enemy. Skipped on the killing
        // blow (_currentHealth <= 0f) since there is no point activating an
        // enemy that is about to die in the same frame.
        if (_state == State.Idle && _currentHealth > 0f)
        {
            Transform player = ResolveAutoActivateTarget();
            if (player != null)
                AutoActivate(player, "shot");
        }
    }

    // ─── Stagger (EnemyBase hooks) ─────────────────────────────────────────────

    protected override void OnStaggerStart()
    {
        // Cancel any in-progress windup pose so the enemy doesn't hold a half-turned
        // stance through the stun -- TickAttack()'s FaceTarget()/WindupRotate() are
        // both skipped while IsStaggered (see Update() below), so this just cleans up
        // whatever was mid-flight the instant stagger hit.
        _isWindingUp = false;
        if (_windupRoutine != null)
        {
            StopCoroutine(_windupRoutine);
            _windupRoutine = null;
        }

        if (_animator != null)
            _animator.SetTrigger(_staggerHash);

        SetAnimFloat(_speedHash, 0f);
    }

    protected override void OnStaggerEnd()
    {
        if (_state == State.Dead) return;

        // Same convention as ResumeChase() -- force back into Chasing with a fresh
        // path rather than trying to guess whether we were chasing or mid-attack
        // when the stagger interrupted us.
        _state = State.Chasing;
        if (_agent.isOnNavMesh && _target != null)
            _agent.SetDestination(_target.position);
        _pathTimer = 0f;
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Dead || _target == null) return;

        // While staggered, EnemyBase has already stopped the NavMeshAgent -- just
        // hold still and skip all chase/attack ticks until OnStaggerEnd() fires.
        if (IsStaggered)
        {
            SetAnimFloat(_speedHash, 0f);
            return;
        }

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
        // Skip the normal every-frame face-the-player tracking while a windup
        // rotation is in progress -- WindupRotate() owns transform.rotation
        // during that window, and FaceTarget() would fight it back to
        // dead-on-facing every frame otherwise.
        if (!_isWindingUp)
            FaceTarget();

        // Delegate actual hit timing and animation to EnemyAttack
        _attack.TryAttack();

        // Player escaped — immediately resume chase with a fresh path
        if (dist > attackRange * 1.25f)
        {
            ResumeChase();
        }
    }

    // ─── Attack windup rotation ─────────────────────────────────────────────────

    private void HandleWindupStart(float delay)
    {
        if (_windupRoutine != null) StopCoroutine(_windupRoutine);
        _windupRoutine = StartCoroutine(WindupRotate(Mathf.Max(delay, 0.01f)));
    }

    private void HandleWindupEnd()
    {
        _isWindingUp = false;
        if (_windupRoutine != null)
        {
            StopCoroutine(_windupRoutine);
            _windupRoutine = null;
        }
        // No explicit "rotate back" needed -- TickAttack() resumes calling
        // FaceTarget() next frame, which Slerps back toward the player at the
        // same rate it always does.
    }

    /// <summary>
    /// Rotates from the current facing toward (facing-the-player + windupRotationDegrees),
    /// timed to complete exactly at `duration` seconds -- the same hitboxDelay EnemyAttack
    /// uses to resolve the swing, passed in via OnAttackWindupStart. Uses a normalized-time
    /// Slerp (t/duration from a captured start rotation) rather than a fixed-rate Slerp, so
    /// it reliably reaches the pose even when duration is short, instead of asymptotically
    /// approaching it. Holds the wound-up pose (does not rotate back on its own) until
    /// HandleWindupEnd() releases it, so the enemy visibly commits to the swing.
    /// </summary>
    private IEnumerator WindupRotate(float duration)
    {
        _isWindingUp = true;
        Quaternion startRot = transform.rotation;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float normalizedT = Mathf.Clamp01(t / duration);

            Vector3 dir = (_target.position - transform.position);
            dir.y = 0f;
            Quaternion faceTarget = dir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(dir.normalized)
                : startRot;

            Quaternion windupTarget = faceTarget * Quaternion.Euler(0f, windupRotationDegrees, 0f);
            transform.rotation = Quaternion.Slerp(startRot, windupTarget, normalizedT);

            yield return null;
        }

        _windupRoutine = null;
        // _isWindingUp stays true -- the pose holds until HandleWindupEnd() fires.
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

    // ─── Auto-activation (proximity or shot) ───────────────────────────────────

    /// <summary>
    /// Runs on a timer via InvokeRepeating -- NOT Update() -- because Update()
    /// does not run while this enemy is dormant (enabled = false). Self-cancels
    /// once no longer Idle (activated by any means, or dead) since there is
    /// nothing left to check for.
    /// </summary>
    private void CheckProximityAutoActivate()
    {
        if (_isDead || _state != State.Idle)
        {
            CancelInvoke(nameof(CheckProximityAutoActivate));
            return;
        }

        Transform player = ResolveAutoActivateTarget();
        if (player == null) return;

        if (Vector3.Distance(transform.position, player.position) > autoActivateRadius) return;

        // Within radius, but only wake up if there's actually a clear line of
        // sight -- otherwise a dormant enemy in the next room over would wake
        // through the wall just because it happens to be within range.
        // Doesn't cancel the poll on a blocked check -- keeps retrying so the
        // enemy still wakes the moment the player rounds the corner.
        if (HasLineOfSight(player))
            AutoActivate(player, "proximity");
    }

    /// <summary>
    /// Raycasts between this enemy and the player at losHeight, against the
    /// same wall/obstruction layer the Boss attack scripts use for their own
    /// wall checks. Returns true (clear) if nothing on that layer is between
    /// them, false if blocked.
    /// </summary>
    private bool HasLineOfSight(Transform player)
    {
        Vector3 from = transform.position + Vector3.up * losHeight;
        Vector3 to   = player.position    + Vector3.up * losHeight;

        // Linecast returns true if it HIT something -- i.e. blocked -- so a
        // clear line of sight is the inverse of that.
        return !Physics.Linecast(from, to, _wallLayer, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Lazily resolves and caches the player transform for auto-activation
    /// purposes only. Separate from _target, which EnemyBase only sets once
    /// Activate() has actually been called (by anything, scripted or auto).
    /// </summary>
    private Transform ResolveAutoActivateTarget()
    {
        if (_autoActivateTarget != null) return _autoActivateTarget;

        GameObject player = GameObject.FindWithTag(playerTag);
        if (player != null)
            _autoActivateTarget = player.transform;
        return _autoActivateTarget;
    }

    /// <summary>
    /// Wakes this enemy outside of the normal EncounterTrigger / EnemyEventBridge
    /// scripted flow -- the player either got close enough (proximity, from
    /// CheckProximityAutoActivate) or shot it while it was still dormant (see
    /// OnHit above). Deliberately bypasses EncounterTrigger's DDA-scaled
    /// activation count: this is a direct player action against this specific
    /// enemy, not a scripted room-wide encounter start, so it activates
    /// regardless of whether EncounterTrigger already left it dormant for the
    /// current tier.
    /// </summary>
    private void AutoActivate(Transform player, string reason)
    {
        if (_isDead || _state != State.Idle) return;
        CancelInvoke(nameof(CheckProximityAutoActivate));
        Debug.Log($"[EnemyChaser] Auto-activated ({reason}).");
        Activate(player);
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

        // Cyan sphere = auto-activation proximity radius (dormant enemies only)
        if (autoActivateRadius > 0f)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, autoActivateRadius);
        }
    }
}

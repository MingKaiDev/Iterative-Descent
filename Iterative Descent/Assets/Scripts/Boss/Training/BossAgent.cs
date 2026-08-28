using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// PPO-driven attack director for DROID-7, trained inside Training.unity against the
/// 3 scripted opponents (PlayerAgentAggressive/Circler/Kiter). Replaces
/// BossAttackRegistry.TryExecuteRandomAttack()'s uniform-random picker with a learned
/// policy while training/running -- BossStateMachine still owns all movement/chasing
/// (Combat state's NavMeshAgent chase logic is untouched; this Agent only decides
/// WHICH attack fires, never where the boss moves).
///
/// NOT wired into Level 1.unity -- Training.unity only. Deploying a trained model onto
/// the live boss is Step 7, deliberately deferred until training converges.
///
/// Unity Setup (Training.unity, DROID_Boss GameObject):
///   - BehaviorParameters: Behavior Name "BossAgent" (must match Assets/ML/boss_config.yaml's
///     top-level behaviors key), Vector Observation Space Size 16, Discrete Branches [9].
///   - DecisionRequester: attached alongside (RequireComponent), Decision Period tuned in Inspector.
///   - Assign trainingEnv / bossHealth / bossStateMachine / bossAttackRegistry, and the 8
///     attacks array in the FIXED order documented below.
///
/// Observation vector (16 floats, locked design -- see project-boss-fight.md):
///   0  distance to opponent
///   1-2 opponent velocity relative to boss, X/Z (world space, boss doesn't rotate obs frame)
///   3  opponent angular velocity around the boss (signed, rad/s -- tangential speed / distance)
///   4  time since opponent last fired, normalized against a 10s horizon
///   5  time since opponent last moved, normalized against a 10s horizon
///   6  boss HP normalized [0,1]
///   7  phase-2 flag [0/1]
///   8-15 time-since-last-used per attack, normalized [0,1] (TimeSinceUsedNormalized), fixed order below
///
/// Action space (discrete, 9 values, single branch):
///   0 = Idle (always unmasked -- ML-Agents requires >=1 enabled action per branch)
///   1-8 = Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload
///   (fixed order -- do not reorder without updating boss_config.yaml / any trained model's
///   action semantics, since index meaning is baked into the checkpoint)
///
/// Masking: an attack is only selectable while BossStateMachine.CurrentState == Combat
/// (mirrors BossAttackRegistry.Update()'s own "only in Combat state" rule -- prevents
/// picking a new attack while one is still mid-swing, since IsOnCooldown only becomes
/// true once the current attack ENDS, not while it's active), AND !IsOnCooldown,
/// AND !IsOnVarietyCooldown, AND within maxRange.
///
/// Reward v1 (simple, tunable in Inspector): terminal +1 win / -1 lose / +timeout on
/// BossTrainingEnv.OnEpisodeEnded; small shaping reward for landing a hit (scaled by
/// damage dealt / opponent max HP), small shaping penalty for taking damage (scaled by
/// damage taken / boss max HP), plus a flat bonus for landing a hit with an attack that
/// hadn't been used recently (encourages variety, the whole reason v2 exists -- v1 spammed
/// one move repeatedly).
/// </summary>
[RequireComponent(typeof(BehaviorParameters))]
[RequireComponent(typeof(DecisionRequester))]
public class BossAgent : Agent
{
    // ─── Inspector: Core References ─────────────────────────────────────────────
    [Header("Core References")]
    public BossTrainingEnv    trainingEnv;
    public BossHealth         bossHealth;
    public BossStateMachine   bossStateMachine;
    [Tooltip("Disabled automatically in Initialize() -- BossAgent owns attack selection " +
             "during training, so the registry's own random-heuristic decision timer must " +
             "not run at the same time. Its OnAttackEnded subscription (set up in its own " +
             "Awake(), unaffected by disabling) still fires BossStateMachine.ExitAttacking() " +
             "for us when an attack completes, so nothing else needs to duplicate that.")]
    public BossAttackRegistry bossAttackRegistry;

    [Header("Attacks -- FIXED ORDER, index 0-7 maps to discrete action 1-8")]
    [Tooltip("Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload. " +
             "Do not reorder without updating boss_config.yaml / any trained checkpoint's action semantics.")]
    public BossAttackBase[] attacks = new BossAttackBase[8];

    // ─── Inspector: Reward Tuning (v1) ──────────────────────────────────────────
    [Header("Reward Tuning (v1)")]
    public float terminalWinReward     = 1f;
    public float terminalLoseReward    = -1f;
    [Tooltip("Applied on TimedOut -- boss survived the full episode without dying, but didn't " +
             "land a clean kill either. Not a strong signal either way, so kept small and positive " +
             "rather than neutral; revisit if training shows the agent stalling to farm this.")]
    public float terminalTimeoutReward = 0.3f;

    [Tooltip("Reward per hit landed = this * (damage dealt / opponent max HP).")]
    public float hitLandedRewardScale = 0.5f;
    [Tooltip("Penalty per hit taken = -this * (damage taken / boss max HP).")]
    public float damageTakenPenaltyScale = 0.5f;
    [Tooltip("Flat bonus added on top of the hit-landed reward when the attack that landed " +
             "was not used recently (see varietyBonusFreshnessThreshold below).")]
    public float varietyBonusReward = 0.1f;
    [Range(0f, 1f)]
    [Tooltip("An attack counts as 'not used recently' for the variety bonus if its " +
             "TimeSinceUsedNormalized was at or above this threshold at the moment it was executed.")]
    public float varietyBonusFreshnessThreshold = 0.5f;

    [Header("Observation Normalization")]
    [Tooltip("Horizon (seconds) TimeSinceLastFired / TimeSinceLastMoved are clamped/normalized against.")]
    public float playerTimingNormalizationHorizon = 10f;

    // ─── Internal State ───────────────────────────────────────────────────────────
    private TrainingDummyHealth _activeOpponentHealth;
    private float _lastOpponentHealth;
    private float _lastBossHealth;
    private int   _lastExecutedAttackIndex = -1;
    private bool  _lastExecutedWasFresh;

    // ─── ML-Agents Lifecycle ──────────────────────────────────────────────────────

    public override void Initialize()
    {
        if (bossAttackRegistry != null)
            bossAttackRegistry.enabled = false;
    }

    // Added 2026-08-27: these MUST be real overrides that chain to base.OnEnable()/
    // base.OnDisable() -- Agent.OnEnable() is what calls LazyInitialize() (which calls our own
    // Initialize(), which disables bossAttackRegistry so the old random-heuristic fallback stops
    // competing with real decisions), and Agent.OnDisable() unsubscribes this agent from Academy's
    // step events and cleans up sensors/the brain. Without `override`, these were plain,
    // unrelated methods that HID the base class's -- Unity's message dispatch only found and
    // called the most-derived one, so Agent.OnEnable()/OnDisable() never ran at all. Confirmed
    // via live Play testing: bossAttackRegistry stayed enabled the entire session, meaning
    // Initialize() never fired and BossAttackRegistry's OWN attackDecisionInterval-based random
    // picker was driving 100% of boss attacks the whole time -- not BossAgent's policy. (This
    // was investigated once before during the revival-bug fix and incorrectly ruled out on the
    // reasoning "attacks are firing, so initialization must be fine" -- that reasoning was
    // circular, since the attacks firing were never actually coming from this Agent.)
    protected override void OnEnable()
    {
        base.OnEnable();

        if (trainingEnv != null)
            trainingEnv.OnEpisodeEnded += HandleEpisodeEnded;
        BossHealth.OnHealthChanged          += HandleBossHealthChanged;
        TrainingDummyHealth.OnHealthChanged += HandleOpponentHealthChanged;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (trainingEnv != null)
            trainingEnv.OnEpisodeEnded -= HandleEpisodeEnded;
        BossHealth.OnHealthChanged          -= HandleBossHealthChanged;
        TrainingDummyHealth.OnHealthChanged -= HandleOpponentHealthChanged;
    }

    public override void OnEpisodeBegin()
    {
        _lastExecutedAttackIndex = -1;
        _lastExecutedWasFresh    = false;

        // Does NOT call trainingEnv.ResetEpisode() itself (removed 2026-08-27) -- the world is
        // already reset by the time this runs. BossTrainingEnv.EndEpisode() now calls
        // ResetEpisode() directly and unconditionally, rather than waiting for this callback,
        // because this callback is only reachable if ML-Agents' own EndEpisode()/NotifyAgentDone()
        // bookkeeping completes without throwing -- observed 2026-08-27 that it can throw
        // (NullReferenceException in Agent.UpdateSensors(), root cause not fully pinned down),
        // which used to silently skip boss revival entirely. All this method needs to do now is
        // re-sync its OWN reward-tracking fields from whatever state already exists.
        var opponent = trainingEnv != null ? trainingEnv.ActiveOpponent : null;
        _activeOpponentHealth = opponent != null ? opponent.GetComponent<TrainingDummyHealth>() : null;

        _lastOpponentHealth = _activeOpponentHealth != null ? _activeOpponentHealth.CurrentHealth : 0f;
        _lastBossHealth     = bossHealth != null ? bossHealth.CurrentHealth : 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        var opponent = trainingEnv != null ? trainingEnv.ActiveOpponent : null;

        if (opponent == null || bossHealth == null)
        {
            // Shouldn't happen mid-episode (BossTrainingEnv always picks an opponent in
            // ResetEpisode()), but keeps the 16-float vector length correct if it ever does --
            // ML-Agents throws if CollectObservations doesn't add exactly VectorObservationSize floats.
            for (int i = 0; i < 16; i++)
                sensor.AddObservation(0f);
            return;
        }

        Vector3 toOpponent = opponent.transform.position - transform.position;
        float distance = toOpponent.magnitude;
        sensor.AddObservation(distance);                                              // 0

        Vector3 opponentVelocity = opponent.Velocity;
        sensor.AddObservation(opponentVelocity.x);                                    // 1
        sensor.AddObservation(opponentVelocity.z);                                    // 2

        sensor.AddObservation(AngularVelocityAroundBoss(opponentVelocity, toOpponent, distance)); // 3

        float horizon = Mathf.Max(0.01f, playerTimingNormalizationHorizon);
        sensor.AddObservation(Mathf.Clamp01(opponent.TimeSinceLastFired / horizon));  // 4
        sensor.AddObservation(Mathf.Clamp01(opponent.TimeSinceLastMoved / horizon));  // 5

        sensor.AddObservation(bossHealth.CurrentHealth / Mathf.Max(1f, bossHealth.maxHealth)); // 6
        sensor.AddObservation(bossHealth.IsPhaseTwo ? 1f : 0f);                       // 7

        for (int i = 0; i < 8; i++)                                                   // 8-15
            sensor.AddObservation(attacks[i] != null ? attacks[i].TimeSinceUsedNormalized : 1f);
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        bool canAttack = bossStateMachine != null &&
                          bossStateMachine.CurrentState == BossStateMachine.BossState.Combat;

        var opponent = trainingEnv != null ? trainingEnv.ActiveOpponent : null;
        float distance = opponent != null
            ? Vector3.Distance(transform.position, opponent.transform.position)
            : Mathf.Infinity;

        for (int i = 0; i < attacks.Length; i++)
        {
            var attack = attacks[i];
            bool allowed = canAttack && attack != null &&
                           !attack.IsOnCooldown &&
                           !attack.IsOnVarietyCooldown &&
                           distance <= attack.maxRange;

            actionMask.SetActionEnabled(0, i + 1, allowed);
        }
        // Action 0 (Idle) is never masked here -- ML-Agents requires at least one enabled
        // action per branch, and Idle is always a safe fallback (boss keeps chasing via
        // BossStateMachine.Combat regardless of what this Agent picks).
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int choice = actions.DiscreteActions[0];
        _lastExecutedAttackIndex = -1;

        if (choice == 0) return; // Idle -- no-op.

        int idx = choice - 1;
        if (idx < 0 || idx >= attacks.Length || attacks[idx] == null) return;

        var attack = attacks[idx];

        // Defensive re-check -- WriteDiscreteActionMask should already guarantee this is a
        // legal choice, but this guards against a stale mask (e.g. a manually-driven Heuristic
        // action during testing) silently corrupting attack/state-machine state.
        if (attack.IsOnCooldown || attack.IsOnVarietyCooldown) return;

        _lastExecutedWasFresh    = attack.TimeSinceUsedNormalized >= varietyBonusFreshnessThreshold;
        _lastExecutedAttackIndex = idx;

        bossStateMachine.EnterAttacking();
        attack.Execute();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Safe manual-testing default (Behavior Type = Heuristic Only in the Inspector).
        // Always Idle -- this is for sanity-checking observations/masking in Play mode,
        // not a stand-in policy, so it deliberately never picks an attack itself.
        // (DiscreteActions returns an ActionSegment<int> by value -- indexing straight off
        // the property on an `in` parameter doesn't compile (CS1612), so grab it into a
        // local first; ActionSegment wraps the caller's underlying array, so writing through
        // the local still mutates the real buffer.)
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;
    }

    // ─── Reward Hooks ─────────────────────────────────────────────────────────────

    void HandleOpponentHealthChanged(TrainingDummyHealth instance, float current, float max)
    {
        if (_activeOpponentHealth == null || instance != _activeOpponentHealth) return;

        float damageDealt = _lastOpponentHealth - current;
        _lastOpponentHealth = current;
        if (damageDealt <= 0f) return;

        float reward = hitLandedRewardScale * (damageDealt / Mathf.Max(1f, max));

        if (_lastExecutedAttackIndex >= 0)
        {
            var attack = attacks[_lastExecutedAttackIndex];
            if (attack != null && attack.IsActive && _lastExecutedWasFresh)
                reward += varietyBonusReward;
        }

        AddReward(reward);
    }

    void HandleBossHealthChanged(float current, float max)
    {
        // BossHealth is a single scene instance (static event, no sender arg) so this always
        // concerns our own boss -- unlike the opponent handler above, no identity check needed.
        float damageTaken = _lastBossHealth - current;
        _lastBossHealth = current;
        if (damageTaken > 0f)
            AddReward(-damageTakenPenaltyScale * (damageTaken / Mathf.Max(1f, max)));
    }

    void HandleEpisodeEnded(BossTrainingEnv.EpisodeResult result)
    {
        switch (result)
        {
            case BossTrainingEnv.EpisodeResult.BossWon:  AddReward(terminalWinReward);     break;
            case BossTrainingEnv.EpisodeResult.BossLost: AddReward(terminalLoseReward);    break;
            case BossTrainingEnv.EpisodeResult.TimedOut: AddReward(terminalTimeoutReward); break;
        }
        EndEpisode();
    }

    // ─── Internal Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Signed angular velocity (rad/s) of the opponent around the boss: the component of
    /// the opponent's velocity tangential to the line connecting boss->opponent, divided by
    /// distance. Positive = orbiting counter-clockwise (viewed from above), negative = clockwise.
    /// XZ-plane only (vertical motion ignored, matching every other horizontal-only signal here).
    /// This is my interpretation of the locked design's "player angular velocity around boss"
    /// spec -- flag if a different convention (e.g. degrees/sec, or unsigned) is wanted instead.
    /// </summary>
    float AngularVelocityAroundBoss(Vector3 opponentVelocity, Vector3 toOpponent, float distance)
    {
        if (distance < 0.01f) return 0f;

        Vector3 radialFlat = new Vector3(toOpponent.x, 0f, toOpponent.z).normalized;
        Vector3 tangent = new Vector3(-radialFlat.z, 0f, radialFlat.x);
        float tangentialSpeed = Vector3.Dot(opponentVelocity, tangent);
        return tangentialSpeed / distance;
    }
}

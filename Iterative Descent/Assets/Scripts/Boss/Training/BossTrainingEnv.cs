using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Orchestrates BossAgent PPO training episodes inside Training.unity: resets the boss and
/// picks/repositions one of the 3 scripted opponents (Aggressive/Circler/Kiter) at the start
/// of each episode, then reports how the episode ended so BossAgent (Step 5) can apply reward
/// and call EndEpisode(). This class only detects/reports outcomes -- it does not score them.
///
/// Not used anywhere in the live game -- Level 1.unity has no BossTrainingEnv.
///
/// Unity Setup:
///   - Attach to a "BossTrainingEnv" GameObject at the root of Training.unity.
///   - Wire bossHealth / bossStateMachine / bossAttackRegistry / bossNavAgent / bossSpawnPoint
///     to the DROID_Boss instance and its Boss_Spawn marker.
///   - Wire opponents[0..2] to each Agent's PlayerAgentBase-derived component + its matching
///     marker under SpawnPoint.
///   - trainingHUD is optional (leave null to skip HUD resets).
///   - BossAgent (Step 5) should hold a reference to this component and subscribe to
///     OnEpisodeEnded to know when/why an episode ended and apply reward. As of 2026-08-27
///     it does NOT need to call ResetEpisode() itself -- EndEpisode() below now calls it
///     directly, so the world always resets even if BossAgent's own ML-Agents bookkeeping
///     throws (see EndEpisode() for why that matters).
/// </summary>
public class BossTrainingEnv : MonoBehaviour
{
    public enum EpisodeResult { BossWon, BossLost, TimedOut }

    [Serializable]
    public struct OpponentSlot
    {
        [Tooltip("The PlayerAgentAggressive/Circler/Kiter component on this opponent's GameObject.")]
        public PlayerAgentBase agent;
        [Tooltip("Where this opponent is warped to at the start of an episode (a marker under SpawnPoint).")]
        public Transform spawnPoint;
    }

    // ─── Inspector: Boss ────────────────────────────────────────────────────────
    [Header("Boss")]
    public BossHealth         bossHealth;
    public BossStateMachine   bossStateMachine;
    public BossAttackRegistry bossAttackRegistry;
    public NavMeshAgent       bossNavAgent;
    public Transform          bossSpawnPoint;

    // ─── Inspector: Opponents ────────────────────────────────────────────────────
    [Header("Opponents (one slot per archetype)")]
    public OpponentSlot[] opponents = new OpponentSlot[3];

    // ─── Inspector: Optional ─────────────────────────────────────────────────────
    [Header("Optional")]
    public TrainingBossHUD trainingHUD;

    [Header("Episode")]
    [Tooltip("Episode auto-ends as TimedOut if neither side dies within this many seconds.")]
    public float maxEpisodeDuration = 90f;

    // ─── Events ─────────────────────────────────────────────────────────────────
    /// <summary>Fired once when an episode ends. BossAgent (Step 5) subscribes to apply reward
    /// and call EndEpisode() -- this class only detects/reports the outcome, it does not score it.</summary>
    public event Action<EpisodeResult> OnEpisodeEnded;

    // ─── Public Read-Only State ─────────────────────────────────────────────────
    public int             ActiveOpponentIndex { get; private set; } = -1;
    public PlayerAgentBase ActiveOpponent => ActiveOpponentIndex >= 0 ? opponents[ActiveOpponentIndex].agent : null;
    public float           EpisodeTime    { get; private set; }

    // ─── Private ────────────────────────────────────────────────────────────────
    private bool _episodeActive;

    // Set by HandleBossDeath()/HandleDummyDied() below instead of ending the episode
    // immediately -- see the big comment on RequestEpisodeEnd() for why. Consumed and
    // cleared at the top of our own Update() on the next frame.
    private EpisodeResult? _pendingResult;

    // ─── Unity Lifecycle ────────────────────────────────────────────────────────

    void OnEnable()
    {
        BossHealth.OnBossDeath          += HandleBossDeath;
        TrainingDummyHealth.OnDummyDied += HandleDummyDied;
    }

    void OnDisable()
    {
        BossHealth.OnBossDeath          -= HandleBossDeath;
        TrainingDummyHealth.OnDummyDied -= HandleDummyDied;
    }

    void Start()
    {
        // Lets Steps 1-4 be sanity-checked in the Editor by pressing Play, before BossAgent
        // (Step 5) exists to drive episodes itself. Calling this again from BossAgent later
        // is harmless -- ResetEpisode() is idempotent.
        ResetEpisode();
    }

    void Update()
    {
        // Process a deferred result from RequestEpisodeEnd() first, on our own normal
        // frame -- never from inside the nested call stack that detected it.
        if (_pendingResult.HasValue)
        {
            var result = _pendingResult.Value;
            _pendingResult = null;
            EndEpisode(result);
            return;
        }

        if (!_episodeActive) return;

        EpisodeTime += Time.deltaTime;
        if (EpisodeTime >= maxEpisodeDuration)
            EndEpisode(EpisodeResult.TimedOut);
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Resets the boss, picks a random opponent archetype, and starts a fresh episode.
    /// Called by Start() for the very first episode, and by EndEpisode() below at the end of
    /// every subsequent one -- BossAgent no longer needs to (and no longer does) call this
    /// itself.</summary>
    public void ResetEpisode()
    {
        _episodeActive = false; // guard against a stray end-event firing mid-reset
        EpisodeTime = 0f;

        // --- Cancel any attack still mid-flight, BEFORE touching boss health/state/position ---
        // Added 2026-08-27: an attack that seizes manual control of the NavMeshAgent or other
        // state mid-coroutine (SprintChargeAttack/GroundSlamAttack disconnecting the agent,
        // PounceAttack hiding renderers, CoreOverloadAttack's beam/telegraph) has no way to know
        // the boss just died -- its coroutine would otherwise keep running to completion using
        // stale pre-death data, silently overwriting the reset below (in the NavMeshAgent case,
        // potentially dragging the boss off the baked NavMesh and making Warp()/SetDestination()
        // fail silently for the rest of the session). Must run FIRST, before the Warp below, so
        // no cancelled coroutine can stomp on it. Safe/harmless for the 4 attacks that don't hold
        // any such state (SlashAttack, LeftPunchAttack, StabAttack, BladeSweepAttack) -- their
        // CancelAttack() is just the base no-op-when-inactive implementation.
        if (bossAttackRegistry != null)
        {
            foreach (var attack in bossAttackRegistry.GetComponents<BossAttackBase>())
                attack.CancelAttack();
        }

        // --- Reset boss ---
        if (bossHealth != null) bossHealth.ResetHealth();
        if (bossStateMachine != null) bossStateMachine.ResetForNewEpisode();
        if (bossNavAgent != null && bossSpawnPoint != null)
            bossNavAgent.Warp(bossSpawnPoint.position);

        // --- Pick one opponent, disable the other two ---
        int chosen = opponents.Length > 0 ? UnityEngine.Random.Range(0, opponents.Length) : -1;
        for (int i = 0; i < opponents.Length; i++)
        {
            var slot = opponents[i];
            if (slot.agent == null) continue;

            bool active = i == chosen;
            slot.agent.gameObject.SetActive(active);

            if (active)
            {
                var dummyHealth = slot.agent.GetComponent<TrainingDummyHealth>();
                if (dummyHealth != null) dummyHealth.ResetHealth();

                var opponentNavAgent = slot.agent.GetComponent<NavMeshAgent>();
                if (opponentNavAgent != null && slot.spawnPoint != null)
                    opponentNavAgent.Warp(slot.spawnPoint.position);
            }
        }
        ActiveOpponentIndex = chosen;

        // --- Point the boss at the chosen opponent ---
        if (chosen >= 0 && bossStateMachine != null && bossAttackRegistry != null)
        {
            var targetTransform = opponents[chosen].agent.transform;
            bossStateMachine.SetTarget(targetTransform);
            bossAttackRegistry.SetTarget(targetTransform);

            // Added 2026-08-27: these four attacks resolve their own player-position target
            // independently (FindFirstObjectByType<PlayerHealth>() in their own Start()), which
            // always finds nothing in Training.unity (opponents use TrainingDummyHealth, not
            // PlayerHealth) -- unlike bossStateMachine/bossAttackRegistry above, they never had
            // an override path at all. Without this, SprintCharge charges toward transform.forward
            // instead of the opponent, GroundSlam/Pounce silently no-op (their PerformAttack()
            // early-returns on a null _player), and CoreOverload's beam aims at
            // laserOrigin.forward instead of tracking. The other 4 attacks (Slash/LeftPunch/Stab/
            // BladeSweep) are pure hitbox/melee and never reference a player transform at all, so
            // they need no override.
            var sprintCharge = bossAttackRegistry.GetComponent<SprintChargeAttack>();
            if (sprintCharge != null) sprintCharge.SetTarget(targetTransform);

            var groundSlam = bossAttackRegistry.GetComponent<GroundSlamAttack>();
            if (groundSlam != null) groundSlam.SetTarget(targetTransform);

            var pounce = bossAttackRegistry.GetComponent<PounceAttack>();
            if (pounce != null) pounce.SetTarget(targetTransform);

            var coreOverload = bossAttackRegistry.GetComponent<CoreOverloadAttack>();
            if (coreOverload != null) coreOverload.SetTarget(targetTransform);
        }

        // --- Reset HUD ---
        if (trainingHUD != null)
            trainingHUD.ResetVisual();

        _episodeActive = true;
    }

    // ─── Internal ────────────────────────────────────────────────────────────────

    void EndEpisode(EpisodeResult result)
    {
        if (!_episodeActive) return;
        _episodeActive = false;

        // Reset the world ourselves, right here -- do NOT rely on BossAgent's OnEpisodeBegin()
        // callback to do it (that used to be the only path). Found 2026-08-27: ML-Agents'
        // Agent.EndEpisode() -- called synchronously by BossAgent.HandleEpisodeEnded() below,
        // itself called synchronously from OnEpisodeEnded -- used to throw a
        // NullReferenceException inside Agent.UpdateSensors() whenever this ran reentrantly
        // mid-stack (e.g. PlayerAgentBase.TickFire() -> BossHealth.TakeDamage() -> Die()).
        //
        // 2026-08-28: root-caused and fixed properly. HandleBossDeath()/HandleDummyDied() no
        // longer call this method directly -- they call RequestEpisodeEnd(), which just
        // records the result and lets our own Update() call EndEpisode() on a clean, non-
        // reentrant frame. So by the time we get here, Agent.EndEpisode() is no longer being
        // invoked from inside another component's call stack, and the NRE should no longer
        // occur at all. The try/catch below stays only as a defensive fallback -- if this
        // warning ever fires now, that means something NEW is wrong, not the old known quirk.
        try
        {
            OnEpisodeEnded?.Invoke(result);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[BossTrainingEnv] OnEpisodeEnded subscriber threw -- this used to be an " +
                $"expected reentrancy quirk but should no longer happen after the 2026-08-28 " +
                $"deferred-EndEpisode fix; investigate if you see this. Boss will still " +
                $"revive normally because ResetEpisode() below runs unconditionally: {ex}");
        }

        ResetEpisode();
    }

    void HandleBossDeath() => RequestEpisodeEnd(EpisodeResult.BossLost);

    void HandleDummyDied(TrainingDummyHealth dummy)
    {
        if (ActiveOpponentIndex < 0) return;
        var activeAgent = opponents[ActiveOpponentIndex].agent;
        if (activeAgent != null && dummy.gameObject == activeAgent.gameObject)
            RequestEpisodeEnd(EpisodeResult.BossWon);
    }

    /// <summary>
    /// Called from HandleBossDeath()/HandleDummyDied() above -- both of which fire from deep,
    /// reentrant call stacks (a PlayerAgent's own TickFire()/Update(), or a boss attack's hit
    /// callback), not from our own Update(). Calling Agent.EndEpisode() synchronously from
    /// there used to throw inside ML-Agents' internals (see EndEpisode() above) and very
    /// likely corrupted terminal-step bookkeeping for the trainer on every such episode, even
    /// though the try/catch kept the game itself working. So this method does the minimum
    /// possible work -- just latch the result -- and Update() does the actual EndEpisode()
    /// call on a normal, non-reentrant frame.
    /// </summary>
    void RequestEpisodeEnd(EpisodeResult result)
    {
        if (!_episodeActive || _pendingResult.HasValue) return;
        _pendingResult = result;
    }

    // ─── Debug ───────────────────────────────────────────────────────────────────

    [ContextMenu("Test: Reset Episode")]
    void Debug_ResetEpisode() => ResetEpisode();
}

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
///   - BossAgent (Step 5) should hold a reference to this component, call ResetEpisode() from
///     OnEpisodeBegin(), and subscribe to OnEpisodeEnded to know when/why an episode ended.
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
        if (!_episodeActive) return;

        EpisodeTime += Time.deltaTime;
        if (EpisodeTime >= maxEpisodeDuration)
            EndEpisode(EpisodeResult.TimedOut);
    }

    // ─── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Resets the boss, picks a random opponent archetype, and starts a fresh episode.
    /// Call from BossAgent.OnEpisodeBegin() (Step 5).</summary>
    public void ResetEpisode()
    {
        _episodeActive = false; // guard against a stray end-event firing mid-reset
        EpisodeTime = 0f;

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
        OnEpisodeEnded?.Invoke(result);
    }

    void HandleBossDeath() => EndEpisode(EpisodeResult.BossLost);

    void HandleDummyDied(TrainingDummyHealth dummy)
    {
        if (ActiveOpponentIndex < 0) return;
        var activeAgent = opponents[ActiveOpponentIndex].agent;
        if (activeAgent != null && dummy.gameObject == activeAgent.gameObject)
            EndEpisode(EpisodeResult.BossWon);
    }

    // ─── Debug ───────────────────────────────────────────────────────────────────

    [ContextMenu("Test: Reset Episode")]
    void Debug_ResetEpisode() => ResetEpisode();
}

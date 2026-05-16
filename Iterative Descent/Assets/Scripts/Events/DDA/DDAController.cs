using UnityEngine;

/// <summary>
/// Quiz-only Dynamic Difficulty Adjustment controller.
///
/// Reads PlayerMetricsTracker every <evaluationInterval> seconds.
/// Computes a difficulty score from three quiz signals:
///   [55%] Average quiz score across the session
///   [25%] Last quiz completion time (fast = confident = harder)
///   [20%] Session pass rate (consistency signal)
///
/// Output: CurrentScore [0,1] and CurrentTier [0-4] consumed by DDADisplayHUD.
///         No side effects on gameplay yet — display only for sprint showcase.
///
/// PPO handoff: assign AgentScoreOverride from DirectorAgent.cs when ready.
/// Attach to the same persistent GameManager GameObject as PlayerMetricsTracker.
/// </summary>
public class DDAController : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static DDAController Instance { get; private set; }

    // ── Inspector ──────────────────────────────────────────────────────────
    [Header("Evaluation")]
    [Tooltip("How often (seconds) the DDA re-evaluates.")]
    [SerializeField] private float evaluationInterval = 10f;

    [Tooltip("Score smoothing — 0 = instant updates, 0.9 = very gradual.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float scoreSmoothing = 0.5f;

    [Header("Quiz Signal Weights")]
    [Tooltip("Weight for average session quiz score. Should be largest.")]
    [Range(0f, 1f)]
    [SerializeField] private float avgScoreWeight = 0.55f;

    [Tooltip("Weight for how quickly the player finished their last quiz.")]
    [Range(0f, 1f)]
    [SerializeField] private float lastTimeWeight = 0.25f;

    [Tooltip("Weight for pass rate across all quiz attempts this session.")]
    [Range(0f, 1f)]
    [SerializeField] private float passRateWeight = 0.20f;

    [Header("Quiz Time Thresholds (seconds)")]
    [Tooltip("Finishing a quiz under this time is considered fast → pushes difficulty up.")]
    [SerializeField] private float fastQuizTime = 20f;

    [Tooltip("Finishing a quiz over this time is considered slow → pushes difficulty down.")]
    [SerializeField] private float slowQuizTime = 90f;

    // ── Public State (read by DDADisplayHUD) ───────────────────────────────
    /// <summary>Smoothed difficulty score in [0, 1]. 0 = easiest, 1 = hardest.</summary>
    public float CurrentScore { get; private set; } = 0.5f;

    /// <summary>Discrete tier index [0–4].</summary>
    public int CurrentTier { get; private set; } = 2;

    /// <summary>Human-readable tier labels.</summary>
    public static readonly string[] TierNames = { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

    /// <summary>Last computed raw (unsmoothed) score. Shown in HUD for transparency.</summary>
    public float LastRawScore { get; private set; } = 0.5f;

    /// <summary>Breakdown of individual signal contributions. Shown in HUD.</summary>
    public float DbgAvgScoreSignal { get; private set; }
    public float DbgLastTimeSignal { get; private set; }
    public float DbgPassRateSignal { get; private set; }

    // ── PPO Override Hook ─────────────────────────────────────────────────
    /// <summary>
    /// Set this from DirectorAgent.cs to hand control to the PPO agent.
    /// Signature:  float Decide(float[] observations)  →  score in [0, 1]
    /// Leave null to run the heuristic.
    /// </summary>
    public System.Func<float[], float> AgentScoreOverride { get; set; } = null;

    // ── Private ────────────────────────────────────────────────────────────
    private float _evalTimer = 0f;

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // Evaluate immediately whenever a quiz finishes — don't wait for the timer
        PuzzleUI.OnPuzzleFinished += OnQuizFinished;
    }

    private void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished -= OnQuizFinished;
    }

    private void OnQuizFinished(int correct, int total)
    {
        // Tracker processes this event first (subscribed in its own OnEnable),
        // so by the time we get here the metrics are already updated.
        // Delay one frame to guarantee tracker has written its values.
        StartCoroutine(EvaluateNextFrame());
    }

    private System.Collections.IEnumerator EvaluateNextFrame()
    {
        yield return null;
        Evaluate();
        _evalTimer = evaluationInterval; // reset periodic timer so it doesn't double-fire
    }

    private void Update()
    {
        _evalTimer -= Time.deltaTime;
        if (_evalTimer <= 0f)
        {
            _evalTimer = evaluationInterval;
            Evaluate();
        }
    }

    // ── Core Evaluation ────────────────────────────────────────────────────
    private void Evaluate()
    {
        PlayerMetricsTracker m = PlayerMetricsTracker.Instance;
        if (m == null) return;

        // No quiz data yet — hold at neutral, skip evaluation
        if (m.TotalQuizAttempts == 0) return;

        float rawScore;
        if (AgentScoreOverride != null)
        {
            rawScore = Mathf.Clamp01(AgentScoreOverride(m.GetObservations()));
        }
        else
        {
            rawScore = ComputeScore(m);
        }

        LastRawScore = rawScore;
        CurrentScore = Mathf.Lerp(rawScore, CurrentScore, scoreSmoothing);
        CurrentTier = ScoreToTier(CurrentScore);

        Debug.Log($"[DDA] Raw: {rawScore:F3} | Smoothed: {CurrentScore:F3} | " +
                  $"Tier: {CurrentTier} ({TierNames[CurrentTier]}) | " +
                  $"Mode: {(AgentScoreOverride != null ? "PPO Agent" : "Heuristic")}");
    }

    private float ComputeScore(PlayerMetricsTracker m)
    {
        // Signal 1: average quiz score across session — already [0,1]
        float avgScoreSignal = m.AverageQuizScore;

        // Signal 2: last quiz time — fast → 1.0 (harder), slow → 0.0 (easier)
        float lastTimeSignal = 1f - Mathf.InverseLerp(fastQuizTime, slowQuizTime, m.LastQuizTime);
        lastTimeSignal = Mathf.Clamp01(lastTimeSignal);

        // Signal 3: session pass rate
        float passRate = m.TotalQuizAttempts > 0
            ? (float)m.TotalQuizPassed / m.TotalQuizAttempts
            : 0.5f;

        // Store for HUD debug breakdown
        DbgAvgScoreSignal = avgScoreSignal;
        DbgLastTimeSignal = lastTimeSignal;
        DbgPassRateSignal = passRate;

        return Mathf.Clamp01(
            avgScoreWeight * avgScoreSignal +
            lastTimeWeight * lastTimeSignal +
            passRateWeight * passRate);
    }

    private static int ScoreToTier(float score)
    {
        if (score < 0.20f) return 0;
        if (score < 0.40f) return 1;
        if (score < 0.60f) return 2;
        if (score < 0.80f) return 3;
        return 4;
    }
}
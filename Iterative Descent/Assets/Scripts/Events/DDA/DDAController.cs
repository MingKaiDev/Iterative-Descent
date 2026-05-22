using UnityEngine;

/// <summary>
/// DDA controller driven by quiz AND linked list puzzle signals.
///
/// Signal weights (Inspector-tunable):
///   QUIZ GROUP     (default 60% combined)
///     [35%] Average quiz score across session
///     [15%] Last quiz completion time
///     [10%] Session pass rate
///   LINKED LIST GROUP  (default 40% combined)
///     [25%] Average wrong attempts before solving  (fewer = harder)
///     [15%] Average solve time                     (faster = harder)
///
/// Evaluates immediately after any puzzle completes (event-driven),
/// plus on a periodic timer as a fallback.
///
/// Output: CurrentScore [0,1] + CurrentTier [0-4] read by DDADisplayHUD.
/// No gameplay side effects yet — display only for sprint showcase.
///
/// PPO handoff: assign AgentScoreOverride from DirectorAgent.cs when ready.
/// Attach to the same persistent GameManager GameObject as PlayerMetricsTracker.
/// </summary>
public class DDAController : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static DDAController Instance { get; private set; }

    // ── Inspector: Evaluation ─────────────────────────────────────────────
    [Header("Evaluation")]
    [Tooltip("Fallback re-evaluation interval in seconds.")]
    [SerializeField] private float evaluationInterval = 10f;

    [Tooltip("Score smoothing — 0 = instant, 0.9 = very gradual.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float scoreSmoothing = 0.5f;

    // ── Inspector: Quiz Weights ────────────────────────────────────────────
    [Header("Quiz Signal Weights")]
    [Range(0f, 1f)][SerializeField] private float avgScoreWeight = 0.35f;
    [Range(0f, 1f)][SerializeField] private float lastTimeWeight = 0.15f;
    [Range(0f, 1f)][SerializeField] private float passRateWeight = 0.10f;

    [Header("Quiz Time Thresholds (seconds)")]
    [SerializeField] private float fastQuizTime = 20f;
    [SerializeField] private float slowQuizTime = 90f;

    // ── Inspector: Linked List Weights ────────────────────────────────────
    [Header("Linked List Signal Weights")]
    [Range(0f, 1f)][SerializeField] private float llWrongAttemptsWeight = 0.20f;
    [Range(0f, 1f)][SerializeField] private float llSolveTimeWeight     = 0.10f;

    [Header("Linked List Thresholds")]
    [Tooltip("Wrong attempts considered 'zero struggle'. 0 wrongs → hardest.")]
    [SerializeField] private float llMaxWrongAttempts = 8f;

    [Tooltip("Solve time considered fast (seconds). Under this → harder.")]
    [SerializeField] private float llFastSolveTime = 20f;

    [Tooltip("Solve time considered slow (seconds). Over this → easier.")]
    [SerializeField] private float llSlowSolveTime = 120f;

    // ── Inspector: Scheduling Weights ─────────────────────────────────────
    [Header("Scheduling Puzzle Signal Weights")]
    [Range(0f, 1f)][SerializeField] private float schedWrongAttemptsWeight = 0.20f;
    [Range(0f, 1f)][SerializeField] private float schedSolveTimeWeight     = 0.10f;

    [Header("Scheduling Thresholds")]
    [Tooltip("Wrong submissions considered 'zero struggle'. 0 wrongs → hardest.")]
    [SerializeField] private float schedMaxWrongAttempts = 8f;

    [Tooltip("Solve time considered fast (seconds). Under this → harder.")]
    [SerializeField] private float schedFastSolveTime = 20f;

    [Tooltip("Solve time considered slow (seconds). Over this → easier.")]
    [SerializeField] private float schedSlowSolveTime = 120f;

    // ── Public State (read by DDADisplayHUD) ───────────────────────────────
    public float CurrentScore { get; private set; } = 0.5f;
    public int CurrentTier { get; private set; } = 2;
    public float LastRawScore { get; private set; } = 0.5f;

    public static readonly string[] TierNames =
        { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

    // Signal debug values exposed for HUD breakdown
    public float DbgAvgScoreSignal   { get; private set; }
    public float DbgLastTimeSignal   { get; private set; }
    public float DbgPassRateSignal   { get; private set; }
    public float DbgLLWrongSignal    { get; private set; }
    public float DbgLLTimeSignal     { get; private set; }
    public float DbgSchedWrongSignal { get; private set; }
    public float DbgSchedTimeSignal  { get; private set; }
    public bool  HasQuizData         { get; private set; }
    public bool  HasLinkedListData   { get; private set; }
    public bool  HasSchedulingData   { get; private set; }

    // ── PPO Override Hook ─────────────────────────────────────────────────
    /// <summary>
    /// Assign from DirectorAgent.cs to replace heuristic with PPO output.
    /// Signature: float Decide(float[] observations) → score in [0,1]
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
        PuzzleUI.OnPuzzleFinished             += OnQuizFinished;
        LinkedListPuzzleUI.OnLinkedListSolved += OnLinkedListSolved;
        SchedulingPuzzleUI.OnSchedulingSolved += OnSchedulingSolved;
    }

    private void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished             -= OnQuizFinished;
        LinkedListPuzzleUI.OnLinkedListSolved -= OnLinkedListSolved;
        SchedulingPuzzleUI.OnSchedulingSolved -= OnSchedulingSolved;
    }

    // Event handlers — delay one frame so the tracker writes its state first
    private void OnQuizFinished(int correct, int total)  => StartCoroutine(EvaluateNextFrame());
    private void OnLinkedListSolved(int wrongAttempts)   => StartCoroutine(EvaluateNextFrame());
    private void OnSchedulingSolved(int wrongAttempts)   => StartCoroutine(EvaluateNextFrame());

    private System.Collections.IEnumerator EvaluateNextFrame()
    {
        yield return null;
        Evaluate();
        _evalTimer = evaluationInterval; // prevent timer double-firing
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

        HasQuizData       = m.TotalQuizAttempts   > 0;
        HasLinkedListData = m.TotalLinkedListSolved > 0;
        HasSchedulingData = m.TotalSchedulingSolved > 0;

        // Nothing to evaluate yet
        if (!HasQuizData && !HasLinkedListData && !HasSchedulingData) return;

        float rawScore = AgentScoreOverride != null
            ? Mathf.Clamp01(AgentScoreOverride(m.GetObservations()))
            : ComputeScore(m);

        LastRawScore = rawScore;
        CurrentScore = Mathf.Lerp(rawScore, CurrentScore, scoreSmoothing);
        CurrentTier = ScoreToTier(CurrentScore);

        Debug.Log($"[DDA] Raw: {rawScore:F3} | Smoothed: {CurrentScore:F3} | " +
                  $"Tier: {CurrentTier} ({TierNames[CurrentTier]}) | " +
                  $"Mode: {(AgentScoreOverride != null ? "PPO Agent" : "Heuristic")}");
    }

    private float ComputeScore(PlayerMetricsTracker m)
    {
        float score = 0f;
        float totalWeight = 0f;

        // ── Quiz signals ───────────────────────────────────────────────────
        if (HasQuizData)
        {
            // Avg score: high → harder
            DbgAvgScoreSignal = m.AverageQuizScore;

            // Last quiz time: fast → 1.0 (harder), slow → 0.0 (easier)
            DbgLastTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(fastQuizTime, slowQuizTime, m.LastQuizTime));

            // Pass rate
            DbgPassRateSignal = (float)m.TotalQuizPassed / m.TotalQuizAttempts;

            score += avgScoreWeight * DbgAvgScoreSignal
                         + lastTimeWeight * DbgLastTimeSignal
                         + passRateWeight * DbgPassRateSignal;
            totalWeight += avgScoreWeight + lastTimeWeight + passRateWeight;
        }
        else
        {
            DbgAvgScoreSignal = DbgLastTimeSignal = DbgPassRateSignal = 0f;
        }

        // ── Linked list signals ────────────────────────────────────────────
        if (HasLinkedListData)
        {
            // Fewer wrong attempts → player is better → harder (signal → 1)
            DbgLLWrongSignal = Mathf.Clamp01(
                1f - (m.AverageLinkedListWrongAttempts / llMaxWrongAttempts));

            // Faster solve → harder (signal → 1)
            DbgLLTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(llFastSolveTime, llSlowSolveTime, m.AverageLinkedListTime));

            score       += llWrongAttemptsWeight * DbgLLWrongSignal
                         + llSolveTimeWeight     * DbgLLTimeSignal;
            totalWeight += llWrongAttemptsWeight + llSolveTimeWeight;
        }
        else
        {
            DbgLLWrongSignal = DbgLLTimeSignal = 0f;
        }

        // ── Scheduling signals ─────────────────────────────────────────────
        if (HasSchedulingData)
        {
            // Fewer wrong submissions → harder (signal → 1)
            DbgSchedWrongSignal = Mathf.Clamp01(
                1f - (m.AverageSchedulingWrongAttempts / schedMaxWrongAttempts));

            // Faster solve → harder (signal → 1)
            DbgSchedTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(schedFastSolveTime, schedSlowSolveTime, m.AverageSchedulingTime));

            score       += schedWrongAttemptsWeight * DbgSchedWrongSignal
                         + schedSolveTimeWeight     * DbgSchedTimeSignal;
            totalWeight += schedWrongAttemptsWeight + schedSolveTimeWeight;
        }
        else
        {
            DbgSchedWrongSignal = DbgSchedTimeSignal = 0f;
        }

        // Normalise by active weight so partial data doesn't artificially pull score down
        return totalWeight > 0f ? Mathf.Clamp01(score / totalWeight) : 0.5f;
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
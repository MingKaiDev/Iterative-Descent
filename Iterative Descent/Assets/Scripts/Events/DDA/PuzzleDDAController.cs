using UnityEngine;

/// <summary>
/// Puzzle DDA controller driven by quiz AND linked list puzzle signals.
/// Renamed from DDAController to PuzzleDDAController.
///
/// NOTE: This file should be renamed to PuzzleDDAController.cs in the Unity Editor
///       (right-click in Project window -> Rename). Unity will remap scene references.
///       Inspector-serialized field values will need to be re-entered after the rename.
///
/// Signal weights (Inspector-tunable):
///   QUIZ GROUP     (default 60% combined)
///     [35%] Average quiz score across session
///     [15%] Last quiz completion time
///     [10%] Session pass rate
///   LINKED LIST GROUP  (default 30% combined)
///     [20%] Average wrong attempts before solving  (fewer = harder)
///     [10%] Average solve time                     (faster = harder)
///   SCHEDULING GROUP   (default ~30% combined)
///     [20%] Average wrong submissions              (fewer = harder)
///     [10%] Average solve time                     (faster = harder)
///
/// Evaluates immediately after any puzzle completes (event-driven),
/// plus on a periodic timer as a fallback.
///
/// Output: CurrentScore [0,1] + CurrentTier [0-4] via Debug.Log.
///
/// PPO handoff: assign AgentScoreOverride from DirectorAgent.cs when ready.
/// Attach to the same persistent GameManager GameObject as PlayerMetricsTracker.
/// </summary>
public class PuzzleDDAController : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static PuzzleDDAController Instance { get; private set; }

    // ── Inspector: Evaluation ─────────────────────────────────────────────
    [Header("Evaluation")]
    [Tooltip("Fallback re-evaluation interval in seconds.")]
    [SerializeField] private float evaluationInterval = 10f;

    [Tooltip("Score smoothing -- 0 = instant, 0.9 = very gradual.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float scoreSmoothing = 0.5f;

    [Header("BKT Blend")]
    [Tooltip("How much weight BKT global P(knows) has vs. the heuristic signal " +
             "when computing CurrentTier. 0 = heuristic only, 1 = BKT only. " +
             "Only applied when BKT has observed at least one concept.")]
    [Range(0f, 1f)]
    [SerializeField] private float bktBlendWeight = 0.5f;

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
    [SerializeField] private float llMaxWrongAttempts = 8f;
    [SerializeField] private float llFastSolveTime    = 20f;
    [SerializeField] private float llSlowSolveTime    = 120f;

    // ── Inspector: Scheduling Weights ─────────────────────────────────────
    [Header("Scheduling Puzzle Signal Weights")]
    [Range(0f, 1f)][SerializeField] private float schedWrongAttemptsWeight = 0.20f;
    [Range(0f, 1f)][SerializeField] private float schedSolveTimeWeight     = 0.10f;

    [Header("Scheduling Thresholds")]
    [SerializeField] private float schedMaxWrongAttempts = 8f;
    [SerializeField] private float schedFastSolveTime    = 20f;
    [SerializeField] private float schedSlowSolveTime    = 120f;

    // ── Public State ───────────────────────────────────────────────────────
    public float CurrentScore { get; private set; } = 0.5f;
    public int   CurrentTier  { get; private set; } = 2;
    public float LastRawScore { get; private set; } = 0.5f;

    public static readonly string[] TierNames =
        { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

    public bool HasQuizData       { get; private set; }
    public bool HasLinkedListData { get; private set; }
    public bool HasSchedulingData { get; private set; }

    // ── Per-Concept BKT Tier Access ───────────────────────────────────────
    /// <summary>
    /// Returns the difficulty tier (0-4) for a specific CS concept based on the
    /// player's BKT knowledge state. Falls back to CurrentTier if BKT has no data
    /// for that concept (e.g. first encounter, or concept not yet introduced).
    ///
    /// Use this to tune individual puzzle parameters:
    ///   int tier = PuzzleDDAController.Instance.GetTierForConcept(
    ///       BayesianKnowledgeTracker.LinkedLists);
    /// </summary>
    public int GetTierForConcept(string concept)
    {
        BayesianKnowledgeTracker bkt = PlayerMetricsTracker.Instance?.BKT;
        if (bkt == null || !bkt.HasConcept(concept)) return CurrentTier;
        return bkt.GetTierForConcept(concept);
    }

    // ── PPO Override Hook ─────────────────────────────────────────────────
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

    private void OnQuizFinished(int correct, int total) => StartCoroutine(EvaluateNextFrame());
    private void OnLinkedListSolved(int wrongAttempts)  => StartCoroutine(EvaluateNextFrame());
    private void OnSchedulingSolved(int wrongAttempts)  => StartCoroutine(EvaluateNextFrame());

    private System.Collections.IEnumerator EvaluateNextFrame()
    {
        yield return null;
        Evaluate();
        _evalTimer = evaluationInterval;
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

        HasQuizData       = m.TotalQuizAttempts    > 0;
        HasLinkedListData = m.TotalLinkedListSolved > 0;
        HasSchedulingData = m.TotalSchedulingSolved > 0;

        if (!HasQuizData && !HasLinkedListData && !HasSchedulingData) return;

        float heuristicScore = AgentScoreOverride != null
            ? Mathf.Clamp01(AgentScoreOverride(m.GetObservations()))
            : ComputeScore(m);

        // Blend heuristic with BKT global P(knows) when BKT has real data.
        // Both signals are [0,1]: higher = player is performing well = harder tier.
        float rawScore = heuristicScore;
        BayesianKnowledgeTracker bkt = m.BKT;
        if (bkt != null && bkt.HasAnyData())
            rawScore = Mathf.Lerp(heuristicScore, bkt.GetGlobalPKnows(), bktBlendWeight);

        LastRawScore = rawScore;
        CurrentScore = Mathf.Lerp(rawScore, CurrentScore, scoreSmoothing);
        CurrentTier  = ScoreToTier(CurrentScore);

        Debug.Log($"[PuzzleDDA] Heuristic: {heuristicScore:F3} | " +
                  $"BKT Global: {(bkt != null ? bkt.GetGlobalPKnows().ToString("F3") : "n/a")} | " +
                  $"Blended: {rawScore:F3} | Smoothed: {CurrentScore:F3} | " +
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
            float avgScoreSignal = m.AverageQuizScore;
            float lastTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(fastQuizTime, slowQuizTime, m.LastQuizTime));
            float passRateSignal = (float)m.TotalQuizPassed / m.TotalQuizAttempts;

            score += avgScoreWeight * avgScoreSignal
                   + lastTimeWeight * lastTimeSignal
                   + passRateWeight * passRateSignal;
            totalWeight += avgScoreWeight + lastTimeWeight + passRateWeight;
        }

        // ── Linked list signals ────────────────────────────────────────────
        if (HasLinkedListData)
        {
            float llWrongSignal = Mathf.Clamp01(
                1f - (m.AverageLinkedListWrongAttempts / llMaxWrongAttempts));
            float llTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(llFastSolveTime, llSlowSolveTime, m.AverageLinkedListTime));

            score       += llWrongAttemptsWeight * llWrongSignal
                         + llSolveTimeWeight     * llTimeSignal;
            totalWeight += llWrongAttemptsWeight + llSolveTimeWeight;
        }

        // ── Scheduling signals ─────────────────────────────────────────────
        if (HasSchedulingData)
        {
            float schedWrongSignal = Mathf.Clamp01(
                1f - (m.AverageSchedulingWrongAttempts / schedMaxWrongAttempts));
            float schedTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(schedFastSolveTime, schedSlowSolveTime, m.AverageSchedulingTime));

            score       += schedWrongAttemptsWeight * schedWrongSignal
                         + schedSolveTimeWeight     * schedTimeSignal;
            totalWeight += schedWrongAttemptsWeight + schedSolveTimeWeight;
        }

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

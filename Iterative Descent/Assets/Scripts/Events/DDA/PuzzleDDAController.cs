using UnityEngine;

/// <summary>
/// Puzzle DDA controller. Two signal groups:
///
///   QUIZ GROUP     (default 60% combined)
///     [35%] Average quiz score across session  (MCQ Quiz + Exam Paper)
///     [15%] Last quiz completion time
///     [10%] Session pass rate
///
///   GENERAL PUZZLE GROUP  (default 30% combined)
///     [20%] Average wrong attempts before solving  (fewer = harder)
///     [10%] Average solve time                     (faster = harder)
///     Pools: LinkedList, Scheduling, Stack, Drain, Matching, Subnet,
///            PacketFilter + any future puzzle types.
///
/// Evaluates immediately after any puzzle completes (event-driven, no timer fallback).
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
    [Tooltip("Score smoothing -- 0 = instant, 0.9 = very gradual.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float scoreSmoothing = 0.3f;

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

    // ── Inspector: General Puzzle Weights ──────────────────────────────────
    // Pools ALL non-quiz puzzle types: LinkedList, Scheduling, Stack, Drain,
    // Matching, Subnet, PacketFilter + any future puzzles.
    // To add a new puzzle: subscribe its solved event to OnGeneralPuzzleSolved below
    // AND call UpdateGeneralPool() in its PlayerMetricsTracker handler.
    [Header("General Puzzle Signal Weights (all non-quiz puzzles)")]
    [Range(0f, 1f)][SerializeField] private float generalWrongAttemptsWeight = 0.20f;
    [Range(0f, 1f)][SerializeField] private float generalSolveTimeWeight     = 0.10f;

    [Header("General Puzzle Thresholds")]
    [SerializeField] private float generalMaxWrongAttempts = 8f;
    [SerializeField] private float generalFastSolveTime    = 30f;
    [SerializeField] private float generalSlowSolveTime    = 180f;

    // ── Public State ───────────────────────────────────────────────────────
    public float CurrentScore { get; private set; } = 0.5f;
    public int   CurrentTier  { get; private set; } = 2;
    public float LastRawScore { get; private set; } = 0.5f;

    public static readonly string[] TierNames =
        { "Very Easy", "Easy", "Normal", "Hard", "Very Hard" };

    public bool HasQuizData    { get; private set; }
    public bool HasGeneralData { get; private set; }

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

    // ── Events ───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Fired whenever the puzzle DDA tier changes value (0-4).
    /// Subscribe in OnEnable, unsubscribe in OnDisable to avoid leaks.
    /// </summary>
    public static event System.Action<int> OnPuzzleTierChanged;

    // ── PPO Override Hook ─────────────────────────────────────────────────
    public System.Func<float[], float> AgentScoreOverride { get; set; } = null;

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        PuzzleUI.OnPuzzleFinished                 += OnQuizFinished;
        ExamPaperPuzzleUI.OnPuzzleFinished        += OnQuizFinished;
        // General pool -- add new puzzle types here only; no other file changes needed.
        LinkedListPuzzleUI.OnLinkedListSolved     += OnGeneralPuzzleSolved;
        SchedulingPuzzleUI.OnSchedulingSolved     += OnGeneralPuzzleSolved;
        StackPuzzleUI.OnStackSolved               += OnGeneralPuzzleSolved;
        HashTablePuzzleUI.OnHashTableSolved       += OnGeneralPuzzleSolved;
        DrainPuzzleUI.OnDrainSolved               += OnGeneralPuzzleSolved;
        MatchingPuzzleUI.OnMatchingSolved         += OnGeneralPuzzleSolved;
        SubnetPuzzleUI.OnSubnetSolved             += OnGeneralPuzzleSolved;
        PacketFilterPuzzleUI.OnPacketFilterSolved += OnGeneralPuzzleSolved;
        PaintingPuzzleManager.OnPaintingSolved    += OnGeneralPuzzleSolved;
        ReagentRoutingUI.OnReagentRoutingSolved   += OnGeneralPuzzleSolved;
    }

    private void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished                 -= OnQuizFinished;
        ExamPaperPuzzleUI.OnPuzzleFinished        -= OnQuizFinished;
        LinkedListPuzzleUI.OnLinkedListSolved     -= OnGeneralPuzzleSolved;
        SchedulingPuzzleUI.OnSchedulingSolved     -= OnGeneralPuzzleSolved;
        StackPuzzleUI.OnStackSolved               -= OnGeneralPuzzleSolved;
        HashTablePuzzleUI.OnHashTableSolved       -= OnGeneralPuzzleSolved;
        DrainPuzzleUI.OnDrainSolved               -= OnGeneralPuzzleSolved;
        MatchingPuzzleUI.OnMatchingSolved         -= OnGeneralPuzzleSolved;
        SubnetPuzzleUI.OnSubnetSolved             -= OnGeneralPuzzleSolved;
        PacketFilterPuzzleUI.OnPacketFilterSolved -= OnGeneralPuzzleSolved;
        PaintingPuzzleManager.OnPaintingSolved    -= OnGeneralPuzzleSolved;
        ReagentRoutingUI.OnReagentRoutingSolved   -= OnGeneralPuzzleSolved;
    }

    private void OnQuizFinished(int correct, int total)   => StartCoroutine(EvaluateNextFrame());
    private void OnGeneralPuzzleSolved(int wrongAttempts) => StartCoroutine(EvaluateNextFrame());

    private System.Collections.IEnumerator EvaluateNextFrame()
    {
        yield return null;
        Evaluate();
    }

    // ── Core Evaluation ────────────────────────────────────────────────────
    private void Evaluate()
    {
        PlayerMetricsTracker m = PlayerMetricsTracker.Instance;
        if (m == null) return;

        HasQuizData    = m.TotalQuizAttempts       > 0;
        HasGeneralData = m.TotalGeneralPuzzleSolved > 0;

        if (!HasQuizData && !HasGeneralData) return;

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

        int newTier = ScoreToTier(CurrentScore);
        if (newTier != CurrentTier)
        {
            CurrentTier = newTier;
            OnPuzzleTierChanged?.Invoke(newTier);
        }
        else
        {
            CurrentTier = newTier;
        }

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

        // ── General puzzle signals ─────────────────────────────────────────────
        // All non-quiz puzzles pooled: LinkedList, Scheduling, Stack, Drain,
        // Matching, Subnet, PacketFilter + future puzzles.
        if (HasGeneralData)
        {
            float generalWrongSignal = Mathf.Clamp01(
                1f - (m.AverageGeneralPuzzleWrongAttempts / generalMaxWrongAttempts));
            float generalTimeSignal = Mathf.Clamp01(
                1f - Mathf.InverseLerp(generalFastSolveTime, generalSlowSolveTime, m.AverageGeneralPuzzleTime));

            score       += generalWrongAttemptsWeight * generalWrongSignal
                         + generalSolveTimeWeight     * generalTimeSignal;
            totalWeight += generalWrongAttemptsWeight + generalSolveTimeWeight;
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

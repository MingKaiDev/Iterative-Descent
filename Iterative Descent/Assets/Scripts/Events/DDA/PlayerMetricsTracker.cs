using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks player metrics for the PPO Director's DDA observation space.
/// Attach this to a persistent GameManager GameObject.
///
/// Metrics collected:
///   - Total session time elapsed
///   - Time spent in each room (by room ID)
///   - Quiz performance (score, time taken, attempts, pass/fail)
///   - Linked List puzzle performance (wrong attempts, time taken, sessions)
/// </summary>
public class PlayerMetricsTracker : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────
    public static PlayerMetricsTracker Instance { get; private set; }

    // ── BKT ────────────────────────────────────────────────────────────────
    [Header("Bayesian Knowledge Tracker")]
    [Tooltip("One ConceptProfile asset per CS concept. Create via " +
             "Assets > Create > ARBITEX > Concept Profile.")]
    [SerializeField] private ConceptProfile[] _conceptProfiles;

    /// <summary>
    /// The live BKT model. Persists across scenes because this MonoBehaviour
    /// is DontDestroyOnLoad. Read per-concept tiers via BKT.GetTierForConcept().
    /// </summary>
    public BayesianKnowledgeTracker BKT { get; private set; }

    // ── Inspector Config ───────────────────────────────────────────────────
    [Header("Normalisation Caps (seconds)")]
    [SerializeField] private float maxSessionTime = 1800f;  // 30 min
    [SerializeField] private float maxRoomTime = 300f;   // 5 min
    [SerializeField] private float maxAvgRoomTime = 120f;   // 2 min

    [Header("Normalisation Caps (quiz)")]
    [SerializeField] private float maxQuizTime = 120f;  // 2 min per quiz
    [SerializeField] private int maxQuizAttempts = 5;

    [Header("Normalisation Caps (linked list)")]
    [SerializeField] private float maxLinkedListTime    = 180f; // 3 min per puzzle
    [SerializeField] private int   maxLinkedListAttempts = 10;  // wrong attempts before solving

    [Header("Normalisation Caps (scheduling)")]
    [SerializeField] private float maxSchedulingTime    = 180f; // 3 min per puzzle
    [SerializeField] private int   maxSchedulingAttempts = 10;  // wrong submissions before solving

    [Header("Normalisation Caps (stack)")]
    [SerializeField] private float maxStackTime     = 180f; // 3 min per puzzle
    [SerializeField] private int   maxStackAttempts = 10;   // wrong attempts before solving

    [Header("Normalisation Caps (combat)")]
    [Tooltip("Enemy encounter duration considered 'fast' -- used by CombatDDAController.")]
    [SerializeField] public float combatFastTime = 15f;
    [Tooltip("Enemy encounter duration considered 'slow' -- used by CombatDDAController.")]
    [SerializeField] public float combatSlowTime = 90f;

    // ── Room State ─────────────────────────────────────────────────────────
    public float TotalSessionTime { get; private set; }
    public string CurrentRoomID { get; private set; }
    public float CurrentRoomTime { get; private set; }
    public int RoomsVisited { get; private set; }

    private readonly Dictionary<string, float> _roomTimeLedger = new();
    private const int HistoryCapacity = 10;
    private readonly Queue<(string id, float time)> _recentRooms = new();
    private bool _inRoom;

    // ── Quiz State ─────────────────────────────────────────────────────────

    /// <summary>Score of the most recently completed quiz (0.0 - 1.0).</summary>
    public float LastQuizScore { get; private set; }

    /// <summary>Seconds taken to complete the most recently completed quiz.</summary>
    public float LastQuizTime { get; private set; }

    /// <summary>Whether the player passed the most recent quiz (all correct).</summary>
    public bool LastQuizPassed { get; private set; }

    /// <summary>True once any quiz terminal has been opened this session. Used by QuestionSelector to determine first-session globally rather than per-prop.</summary>
    public bool HasStartedAnyQuiz { get; private set; }

    /// <summary>Total quiz attempts this session (including retries).</summary>
    public int TotalQuizAttempts { get; private set; }

    /// <summary>Total quizzes passed this session.</summary>
    public int TotalQuizPassed { get; private set; }

    /// <summary>Running average score across all quiz attempts this session.</summary>
    public float AverageQuizScore { get; private set; }

    private float _quizStartTime;      // realtimeSinceStartup — immune to timeScale
    private bool _quizInProgress;

    // ── Linked List Puzzle State ───────────────────────────────────────────

    /// <summary>
    /// Number of wrong submissions on the most recently solved linked list puzzle.
    /// This is the raw value from OnLinkedListSolved(attempts).
    /// A value of 0 means the player solved it on the first try.
    /// </summary>
    public int LastLinkedListWrongAttempts { get; private set; }

    /// <summary>
    /// Total submissions on the last puzzle (wrong attempts + 1 for the correct solve).
    /// </summary>
    public int LastLinkedListTotalSubmits => LastLinkedListWrongAttempts + 1;

    /// <summary>Seconds taken to solve the most recently completed linked list puzzle.</summary>
    public float LastLinkedListTime { get; private set; }

    /// <summary>Total linked list puzzles solved this session.</summary>
    public int TotalLinkedListSolved { get; private set; }

    /// <summary>
    /// Running average of wrong attempts per linked list puzzle this session.
    /// Lower = player is improving.
    /// </summary>
    public float AverageLinkedListWrongAttempts { get; private set; }

    /// <summary>
    /// Running average time to solve linked list puzzles this session.
    /// </summary>
    public float AverageLinkedListTime { get; private set; }

    private float _linkedListStartTime; // realtimeSinceStartup — immune to timeScale
    private bool _linkedListInProgress;

    // ── Scheduling Puzzle State ────────────────────────────────────────────

    /// <summary>Wrong submissions on the most recently solved scheduling puzzle.</summary>
    public int   LastSchedulingWrongAttempts   { get; private set; }

    /// <summary>Seconds taken to solve the most recently completed scheduling puzzle.</summary>
    public float LastSchedulingTime            { get; private set; }

    /// <summary>Total scheduling puzzles solved this session.</summary>
    public int   TotalSchedulingSolved         { get; private set; }

    /// <summary>Running average wrong attempts per scheduling puzzle this session.</summary>
    public float AverageSchedulingWrongAttempts { get; private set; }

    /// <summary>Running average solve time for scheduling puzzles this session.</summary>
    public float AverageSchedulingTime         { get; private set; }

    private float _schedulingStartTime;  // realtimeSinceStartup — immune to timeScale
    private bool  _schedulingInProgress;

    // ── Stack Puzzle State ────────────────────────────────────────────────────

    /// <summary>Wrong submissions on the most recently solved stack puzzle.</summary>
    public int   LastStackWrongAttempts    { get; private set; }

    /// <summary>Seconds taken to solve the most recently completed stack puzzle.</summary>
    public float LastStackTime             { get; private set; }

    /// <summary>Total stack puzzles solved this session.</summary>
    public int   TotalStackSolved          { get; private set; }

    /// <summary>Running average wrong attempts per stack puzzle this session.</summary>
    public float AverageStackWrongAttempts { get; private set; }

    /// <summary>Running average solve time for stack puzzles this session.</summary>
    public float AverageStackTime          { get; private set; }

    private float _stackStartTime;   // realtimeSinceStartup -- immune to timeScale
    private bool  _stackInProgress;

    // ── Hash Table Puzzle State ────────────────────────────────────────────────

    /// <summary>Crates/orders spilled (wrong basket at arrival) on the most recently solved hash-table puzzle.</summary>
    public int   LastHashTableWrongAttempts    { get; private set; }

    /// <summary>Seconds taken to clear the most recently completed hash-table puzzle.</summary>
    public float LastHashTableTime             { get; private set; }

    /// <summary>Total hash-table puzzles solved this session.</summary>
    public int   TotalHashTableSolved          { get; private set; }

    /// <summary>Running average spills per hash-table puzzle this session.</summary>
    public float AverageHashTableWrongAttempts { get; private set; }

    /// <summary>Running average clear time for hash-table puzzles this session.</summary>
    public float AverageHashTableTime          { get; private set; }

    private float _hashTableStartTime;   // realtimeSinceStartup -- immune to timeScale
    private bool  _hashTableInProgress;

    // ── General Puzzle Pool State ─────────────────────────────────────────────
    // Pools: Stack, Drain, Matching, Subnet, PacketFilter + any future puzzle types.
    // Add a call to UpdateGeneralPool(wrongAttempts, timeTaken) in each new puzzle handler.

    /// <summary>Total general-pool puzzles solved this session.</summary>
    public int   TotalGeneralPuzzleSolved            { get; private set; }
    /// <summary>Wrong attempts on the most recently solved general-pool puzzle.</summary>
    public int   LastGeneralPuzzleWrongAttempts      { get; private set; }
    /// <summary>Solve time for the most recently solved general-pool puzzle (seconds).</summary>
    public float LastGeneralPuzzleTime               { get; private set; }
    /// <summary>Running average wrong attempts per general-pool puzzle this session.</summary>
    public float AverageGeneralPuzzleWrongAttempts   { get; private set; }
    /// <summary>Running average solve time per general-pool puzzle this session.</summary>
    public float AverageGeneralPuzzleTime            { get; private set; }

    // ── Combat / Encounter State ───────────────────────────────────────────────

    /// <summary>
    /// Fired when the last active enemy in an encounter dies.
    /// CombatDDAController subscribes to this to trigger evaluation.
    /// </summary>
    public static event System.Action OnEncounterEnd;

    /// <summary>Running count of enemies currently active in the encounter.</summary>
    private int _activeEnemyCount;

    /// <summary>realtimeSinceStartup when the first enemy of this encounter activated.</summary>
    private float _encounterStartTime;

    /// <summary>Shots fired by the player during the current encounter (resets each encounter start).</summary>
    public int ShotsFired { get; private set; }

    /// <summary>Shots that connected with an IDamageable during the current encounter.</summary>
    public int ShotsLanded { get; private set; }

    /// <summary>Elapsed seconds from first enemy activation to last enemy death in the most recent encounter.</summary>
    public float LastEncounterDuration { get; private set; }

    /// <summary>ShotsFired snapshot captured at the end of the most recent encounter.</summary>
    public int LastEncounterShotsFired { get; private set; }

    /// <summary>ShotsLanded snapshot captured at the end of the most recent encounter.</summary>
    public int LastEncounterShotsLanded { get; private set; }

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BKT = new BayesianKnowledgeTracker(_conceptProfiles);
    }

    private void OnEnable()
    {
        PuzzleUI.OnPuzzleFinished               += HandleQuizFinished;
        PuzzleUI.OnQuestionAnswered             += HandleQuestionAnswered;
        ExamPaperPuzzleUI.OnPuzzleFinished      += HandleQuizFinished;
        ExamPaperPuzzleUI.OnQuestionAnswered    += HandleQuestionAnswered;
        LinkedListPuzzleUI.OnLinkedListSolved   += HandleLinkedListSolved;
        SchedulingPuzzleUI.OnSchedulingSolved   += HandleSchedulingSolved;
        StackPuzzleUI.OnStackSolved             += HandleStackSolved;
        HashTablePuzzleUI.OnHashTableSolved     += HandleHashTableSolved;
        DrainPuzzleUI.OnDrainSolved             += HandleDrainSolved;
        MatchingPuzzleUI.OnMatchingSolved       += HandleMatchingSolved;
        SubnetPuzzleUI.OnSubnetSolved           += HandleSubnetSolved;
        PacketFilterPuzzleUI.OnPacketFilterSolved += HandlePacketFilterSolved;
        PaintingPuzzleManager.OnPaintingSolved    += HandlePaintingSolved;
        ReagentRoutingUI.OnReagentRoutingSolved   += HandleReagentRoutingSolved;
    }

    private void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished               -= HandleQuizFinished;
        PuzzleUI.OnQuestionAnswered             -= HandleQuestionAnswered;
        ExamPaperPuzzleUI.OnPuzzleFinished      -= HandleQuizFinished;
        ExamPaperPuzzleUI.OnQuestionAnswered    -= HandleQuestionAnswered;
        LinkedListPuzzleUI.OnLinkedListSolved   -= HandleLinkedListSolved;
        SchedulingPuzzleUI.OnSchedulingSolved   -= HandleSchedulingSolved;
        StackPuzzleUI.OnStackSolved             -= HandleStackSolved;
        HashTablePuzzleUI.OnHashTableSolved     -= HandleHashTableSolved;
        DrainPuzzleUI.OnDrainSolved             -= HandleDrainSolved;
        MatchingPuzzleUI.OnMatchingSolved       -= HandleMatchingSolved;
        SubnetPuzzleUI.OnSubnetSolved           -= HandleSubnetSolved;
        PacketFilterPuzzleUI.OnPacketFilterSolved -= HandlePacketFilterSolved;
        PaintingPuzzleManager.OnPaintingSolved    -= HandlePaintingSolved;
        ReagentRoutingUI.OnReagentRoutingSolved   -= HandleReagentRoutingSolved;
    }

    private void Update()
    {
        TotalSessionTime += Time.deltaTime;
        if (_inRoom) CurrentRoomTime += Time.deltaTime;
    }

    // ── Room API ───────────────────────────────────────────────────────────

    public void EnterRoom(string roomID)
    {
        if (_inRoom) CloseCurrentRoom();
        CurrentRoomID = roomID;
        CurrentRoomTime = 0f;
        _inRoom = true;
        RoomsVisited++;
        Debug.Log($"[Metrics] Entered room '{roomID}' | Session: {TotalSessionTime:F1}s");
    }

    public void ExitRoom()
    {
        if (!_inRoom) return;
        CloseCurrentRoom();
    }

    // ── Quiz API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens a quiz so we can time it.
    /// Hook this wherever you call puzzleUI.Setup().
    /// </summary>
    public void NotifyQuizStarted()
    {
        _quizStartTime = Time.realtimeSinceStartup;
        _quizInProgress = true;
        HasStartedAnyQuiz = true;
        TotalQuizAttempts++;
        Debug.Log($"[Metrics] Quiz started (attempt #{TotalQuizAttempts})");
    }

    private void HandleQuizFinished(int correct, int total)
    {
        float score = total > 0 ? (float)correct / total : 0f;
        float timeTaken = _quizInProgress ? Time.realtimeSinceStartup - _quizStartTime : 0f;
        bool passed = correct == total;

        LastQuizScore = score;
        LastQuizTime = timeTaken;
        LastQuizPassed = passed;
        _quizInProgress = false;

        if (passed) TotalQuizPassed++;

        AverageQuizScore = TotalQuizAttempts <= 1
            ? score
            : (AverageQuizScore * (TotalQuizAttempts - 1) + score) / TotalQuizAttempts;

        Debug.Log($"[Metrics] Quiz finished | Score: {correct}/{total} ({score:P0}) | " +
                  $"Time: {timeTaken:F1}s | Passed: {passed} | Avg: {AverageQuizScore:P0}");
    }

    /// <summary>
    /// Called per MCQ question the moment the player answers.
    /// Routes BKT update for the question's concept.
    /// Empty concept tags are silently ignored (question has no BKT mapping).
    /// </summary>
    private void HandleQuestionAnswered(string concept, bool correct)
    {
        BKT?.UpdateAfterAttempt(concept, correct);
    }

    // ── Scheduling Puzzle API ──────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens the scheduling puzzle so we can time it.
    /// Called inside SchedulingPuzzleUI.InitPuzzle() — do NOT call it from the prop too.
    /// </summary>
    public void NotifySchedulingStarted()
    {
        _schedulingStartTime   = Time.realtimeSinceStartup;
        _schedulingInProgress  = true;
        Debug.Log("[Metrics] Scheduling puzzle started.");
    }

    private void HandleSchedulingSolved(int wrongAttempts)
    {
        float timeTaken = _schedulingInProgress
            ? Time.realtimeSinceStartup - _schedulingStartTime
            : 0f;

        LastSchedulingWrongAttempts = wrongAttempts;
        LastSchedulingTime          = timeTaken;
        _schedulingInProgress       = false;
        TotalSchedulingSolved++;

        AverageSchedulingWrongAttempts = TotalSchedulingSolved <= 1
            ? wrongAttempts
            : (AverageSchedulingWrongAttempts * (TotalSchedulingSolved - 1) + wrongAttempts)
              / TotalSchedulingSolved;

        AverageSchedulingTime = TotalSchedulingSolved <= 1
            ? timeTaken
            : (AverageSchedulingTime * (TotalSchedulingSolved - 1) + timeTaken)
              / TotalSchedulingSolved;

        // BKT update — replay each wrong submission then the correct solve
        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.CpuScheduling, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.CpuScheduling, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Scheduling solved | Wrong attempts: {wrongAttempts} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageSchedulingWrongAttempts:F1}");
    }

    // ── Stack Puzzle API ───────────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens the stack puzzle so we can time it.
    /// Called inside StackPuzzleUI.InitPuzzle() -- do NOT call it from the prop too.
    /// </summary>
    public void NotifyStackStarted()
    {
        _stackStartTime   = Time.realtimeSinceStartup;
        _stackInProgress  = true;
        Debug.Log("[Metrics] Stack puzzle started.");
    }

    private void HandleStackSolved(int wrongAttempts)
    {
        float timeTaken = _stackInProgress
            ? Time.realtimeSinceStartup - _stackStartTime
            : 0f;

        LastStackWrongAttempts = wrongAttempts;
        LastStackTime          = timeTaken;
        _stackInProgress       = false;
        TotalStackSolved++;

        AverageStackWrongAttempts = TotalStackSolved <= 1
            ? wrongAttempts
            : (AverageStackWrongAttempts * (TotalStackSolved - 1) + wrongAttempts)
              / TotalStackSolved;

        AverageStackTime = TotalStackSolved <= 1
            ? timeTaken
            : (AverageStackTime * (TotalStackSolved - 1) + timeTaken)
              / TotalStackSolved;

        // BKT update -- replay each wrong submission then the correct solve
        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.StacksAndQueues, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.StacksAndQueues, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Stack puzzle solved | Wrong attempts: {wrongAttempts} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageStackWrongAttempts:F1}");
    }

    // ── Hash Table Puzzle API ────────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens the hash-table conveyor puzzle so we
    /// can time it. Called inside HashTablePuzzleUI.InitPuzzle() -- do NOT
    /// call it from the prop too.
    /// </summary>
    public void NotifyHashTableStarted()
    {
        _hashTableStartTime  = Time.realtimeSinceStartup;
        _hashTableInProgress = true;
        Debug.Log("[Metrics] Hash table puzzle started.");
    }

    private void HandleHashTableSolved(int spilled)
    {
        float timeTaken = _hashTableInProgress
            ? Time.realtimeSinceStartup - _hashTableStartTime
            : 0f;

        LastHashTableWrongAttempts = spilled;
        LastHashTableTime          = timeTaken;
        _hashTableInProgress       = false;
        TotalHashTableSolved++;

        AverageHashTableWrongAttempts = TotalHashTableSolved <= 1
            ? spilled
            : (AverageHashTableWrongAttempts * (TotalHashTableSolved - 1) + spilled)
              / TotalHashTableSolved;

        AverageHashTableTime = TotalHashTableSolved <= 1
            ? timeTaken
            : (AverageHashTableTime * (TotalHashTableSolved - 1) + timeTaken)
              / TotalHashTableSolved;

        // BKT update -- replay each spilled crate as a wrong attempt, then the clear
        for (int i = 0; i < spilled; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.HashTables, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.HashTables, true);

        UpdateGeneralPool(spilled, timeTaken);

        Debug.Log($"[Metrics] Hash table puzzle solved | Spilled: {spilled} | " +
                  $"Time: {timeTaken:F1}s | Avg spilled: {AverageHashTableWrongAttempts:F1}");
    }

    // ── Drain Puzzle API ───────────────────────────────────────────────────

    /// <summary>Last drain puzzle wrong attempts this session.</summary>
    public int   LastDrainWrongAttempts  { get; private set; }
    /// <summary>Last drain puzzle solve time in seconds.</summary>
    public float LastDrainTime           { get; private set; }
    /// <summary>Total drain puzzles solved this session.</summary>
    public int   TotalDrainSolved        { get; private set; }
    /// <summary>Running average wrong attempts per drain puzzle.</summary>
    public float AverageDrainWrongAttempts { get; private set; }
    /// <summary>Running average solve time per drain puzzle.</summary>
    public float AverageDrainTime        { get; private set; }

    private float _drainStartTime;
    private bool  _drainInProgress;

    /// <summary>Call from DrainPuzzleUI.InitPuzzle() -- do NOT call from the prop too.</summary>
    public void NotifyDrainStarted()
    {
        _drainStartTime   = Time.realtimeSinceStartup;
        _drainInProgress  = true;
        Debug.Log("[Metrics] Drain puzzle started.");
    }

    private void HandleDrainSolved(int wrongAttempts)
    {
        float timeTaken = _drainInProgress
            ? Time.realtimeSinceStartup - _drainStartTime
            : 0f;

        LastDrainWrongAttempts = wrongAttempts;
        LastDrainTime          = timeTaken;
        _drainInProgress       = false;
        TotalDrainSolved++;

        AverageDrainWrongAttempts = TotalDrainSolved <= 1
            ? wrongAttempts
            : (AverageDrainWrongAttempts * (TotalDrainSolved - 1) + wrongAttempts)
              / TotalDrainSolved;

        AverageDrainTime = TotalDrainSolved <= 1
            ? timeTaken
            : (AverageDrainTime * (TotalDrainSolved - 1) + timeTaken)
              / TotalDrainSolved;

        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.BfsDfs, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.BfsDfs, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Drain puzzle solved | Wrong attempts: {wrongAttempts} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageDrainWrongAttempts:F1}");
    }

    // ── Reagent Routing Puzzle API ─────────────────────────────────────────

    /// <summary>Last Reagent Routing puzzle wrong attempts this session.</summary>
    public int   LastReagentRoutingWrongAttempts  { get; private set; }
    /// <summary>Last Reagent Routing puzzle solve time in seconds.</summary>
    public float LastReagentRoutingTime           { get; private set; }
    /// <summary>Total Reagent Routing puzzles solved this session.</summary>
    public int   TotalReagentRoutingSolved        { get; private set; }
    /// <summary>Running average wrong attempts per Reagent Routing puzzle.</summary>
    public float AverageReagentRoutingWrongAttempts { get; private set; }
    /// <summary>Running average solve time per Reagent Routing puzzle.</summary>
    public float AverageReagentRoutingTime        { get; private set; }

    private float _reagentRoutingStartTime;
    private bool  _reagentRoutingInProgress;

    /// <summary>Call from ReagentRoutingUI.InitPuzzle() -- do NOT call from the prop too.</summary>
    public void NotifyReagentRoutingStarted()
    {
        _reagentRoutingStartTime   = Time.realtimeSinceStartup;
        _reagentRoutingInProgress  = true;
        Debug.Log("[Metrics] Reagent Routing puzzle started.");
    }

    private void HandleReagentRoutingSolved(int wrongAttempts)
    {
        float timeTaken = _reagentRoutingInProgress
            ? Time.realtimeSinceStartup - _reagentRoutingStartTime
            : 0f;

        LastReagentRoutingWrongAttempts = wrongAttempts;
        LastReagentRoutingTime          = timeTaken;
        _reagentRoutingInProgress       = false;
        TotalReagentRoutingSolved++;

        AverageReagentRoutingWrongAttempts = TotalReagentRoutingSolved <= 1
            ? wrongAttempts
            : (AverageReagentRoutingWrongAttempts * (TotalReagentRoutingSolved - 1) + wrongAttempts)
              / TotalReagentRoutingSolved;

        AverageReagentRoutingTime = TotalReagentRoutingSolved <= 1
            ? timeTaken
            : (AverageReagentRoutingTime * (TotalReagentRoutingSolved - 1) + timeTaken)
              / TotalReagentRoutingSolved;

        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.Dijkstra, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.Dijkstra, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Reagent Routing puzzle solved | Wrong attempts: {wrongAttempts} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageReagentRoutingWrongAttempts:F1}");
    }

    // ── Linked List API ────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens the linked list puzzle so we can time it.
    /// Hook this wherever you call linkedListPuzzleUI.InitPuzzle().
    /// </summary>
    public void NotifyLinkedListStarted()
    {
        _linkedListStartTime = Time.realtimeSinceStartup;
        _linkedListInProgress = true;
        Debug.Log("[Metrics] Linked list puzzle started.");
    }

    /// <summary>
    /// Automatically called via LinkedListPuzzleUI.OnLinkedListSolved event.
    /// 
    /// 'wrongAttempts' = number of incorrect submissions before the correct one.
    /// 0 = solved first try, 1 = one wrong then solved, etc.
    /// </summary>
    private void HandleLinkedListSolved(int wrongAttempts)
    {
        float timeTaken = _linkedListInProgress
            ? Time.realtimeSinceStartup - _linkedListStartTime
            : 0f;

        LastLinkedListWrongAttempts = wrongAttempts;
        LastLinkedListTime = timeTaken;
        _linkedListInProgress = false;
        TotalLinkedListSolved++;

        // Rolling averages
        AverageLinkedListWrongAttempts = TotalLinkedListSolved <= 1
            ? wrongAttempts
            : (AverageLinkedListWrongAttempts * (TotalLinkedListSolved - 1) + wrongAttempts)
              / TotalLinkedListSolved;

        AverageLinkedListTime = TotalLinkedListSolved <= 1
            ? timeTaken
            : (AverageLinkedListTime * (TotalLinkedListSolved - 1) + timeTaken)
              / TotalLinkedListSolved;

        // BKT update — replay each wrong submission then the correct solve
        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.LinkedLists, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.LinkedLists, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Linked list solved | Wrong attempts: {wrongAttempts} | " +
                  $"Total submits: {LastLinkedListTotalSubmits} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageLinkedListWrongAttempts:F1}");
    }

    // ── Matching Puzzle API ───────────────────────────────────────────────────

    /// <summary>Total matching puzzles solved this session.</summary>
    public int   TotalMatchingSolved           { get; private set; }
    /// <summary>Wrong submissions on the last matching puzzle.</summary>
    public int   LastMatchingWrongSubmissions   { get; private set; }
    /// <summary>Solve time for the last matching puzzle (seconds).</summary>
    public float LastMatchingTime              { get; private set; }
    /// <summary>Running average wrong submissions per matching puzzle.</summary>
    public float AverageMatchingWrongSubmissions { get; private set; }

    private float _matchingStartTime;
    private bool  _matchingInProgress;

    /// <summary>
    /// Called from MatchingPuzzleUI.InitPuzzle() -- do NOT call from the prop too.
    /// Time.timeScale == 0 while the puzzle is open, so we use realtimeSinceStartup.
    /// </summary>
    public void NotifyMatchingStarted()
    {
        _matchingStartTime   = Time.realtimeSinceStartup;
        _matchingInProgress  = true;
        Debug.Log("[Metrics] Matching puzzle started.");
    }

    private void HandleMatchingSolved(int wrongSubmissions)
    {
        float timeTaken = _matchingInProgress
            ? Time.realtimeSinceStartup - _matchingStartTime
            : 0f;

        LastMatchingWrongSubmissions = wrongSubmissions;
        LastMatchingTime             = timeTaken;
        _matchingInProgress          = false;
        TotalMatchingSolved++;

        AverageMatchingWrongSubmissions = TotalMatchingSolved <= 1
            ? wrongSubmissions
            : (AverageMatchingWrongSubmissions * (TotalMatchingSolved - 1) + wrongSubmissions)
              / TotalMatchingSolved;

        // BKT update -- replay each wrong submission then the correct solve
        for (int i = 0; i < wrongSubmissions; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.NetworkingPorts, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.NetworkingPorts, true);

        UpdateGeneralPool(wrongSubmissions, timeTaken);

        Debug.Log($"[Metrics] Matching puzzle solved | Wrong submissions: {wrongSubmissions} | " +
                  $"Time: {timeTaken:F1}s | Avg submissions: {AverageMatchingWrongSubmissions:F1}");
    }

    // ── Subnet Puzzle API ─────────────────────────────────────────────────────

    public int   TotalSubnetSolved             { get; private set; }
    public int   LastSubnetWrongSubmissions    { get; private set; }
    public float LastSubnetTime               { get; private set; }
    public float AverageSubnetWrongSubmissions { get; private set; }

    private float _subnetStartTime;
    private bool  _subnetInProgress;

    /// <summary>
    /// Called from SubnetPuzzleUI.InitPuzzle() -- do NOT call from the prop too.
    /// Time.timeScale == 0 while the puzzle is open, so we use realtimeSinceStartup.
    /// </summary>
    public void NotifySubnetStarted()
    {
        _subnetStartTime   = Time.realtimeSinceStartup;
        _subnetInProgress  = true;
        Debug.Log("[Metrics] Subnet puzzle started.");
    }

    private void HandleSubnetSolved(int wrongSubmissions)
    {
        float timeTaken = _subnetInProgress
            ? Time.realtimeSinceStartup - _subnetStartTime
            : 0f;

        LastSubnetWrongSubmissions = wrongSubmissions;
        LastSubnetTime             = timeTaken;
        _subnetInProgress          = false;
        TotalSubnetSolved++;

        AverageSubnetWrongSubmissions = TotalSubnetSolved <= 1
            ? wrongSubmissions
            : (AverageSubnetWrongSubmissions * (TotalSubnetSolved - 1) + wrongSubmissions)
              / TotalSubnetSolved;

        // BKT update -- replay each wrong submission then the correct solve
        for (int i = 0; i < wrongSubmissions; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.ComputerNetworks, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.ComputerNetworks, true);

        UpdateGeneralPool(wrongSubmissions, timeTaken);

        Debug.Log($"[Metrics] Subnet puzzle solved | Wrong submissions: {wrongSubmissions} | " +
                  $"Time: {timeTaken:F1}s | Avg submissions: {AverageSubnetWrongSubmissions:F1}");
    }

    // ── Packet Filter Puzzle API ──────────────────────────────────────────────

    /// <summary>Total Packet Filter puzzles solved this session.</summary>
    public int   TotalPacketFilterSolved             { get; private set; }
    /// <summary>Wrong commit attempts on the last Packet Filter puzzle.</summary>
    public int   LastPacketFilterWrongSubmissions    { get; private set; }
    /// <summary>Solve time for the last Packet Filter puzzle (seconds).</summary>
    public float LastPacketFilterTime               { get; private set; }
    /// <summary>Running average wrong submissions per Packet Filter puzzle.</summary>
    public float AveragePacketFilterWrongSubmissions { get; private set; }

    private float _packetFilterStartTime;
    private bool  _packetFilterInProgress;

    [Header("Normalisation Caps (packet filter)")]
    [SerializeField] private float maxPacketFilterTime     = 300f; // 5 min
    [SerializeField] private int   maxPacketFilterAttempts = 10;

    /// <summary>
    /// Called from PacketFilterPuzzleUI.InitPuzzle() -- do NOT call from the prop too.
    /// Time.timeScale == 0 while the puzzle is open, so we use realtimeSinceStartup.
    /// </summary>
    public void NotifyPacketFilterStarted()
    {
        _packetFilterStartTime   = Time.realtimeSinceStartup;
        _packetFilterInProgress  = true;
        Debug.Log("[Metrics] Packet Filter puzzle started.");
    }

    /// <summary>
    /// Called from PacketFilterPuzzleUI on solve (via NotifyPacketFilterSolved).
    /// Also available as a direct call from PacketFilterPuzzleUI.
    /// </summary>
    public void NotifyPacketFilterSolved(int wrongSubmissions)
    {
        HandlePacketFilterSolved(wrongSubmissions);
    }

    private void HandlePacketFilterSolved(int wrongSubmissions)
    {
        float timeTaken = _packetFilterInProgress
            ? Time.realtimeSinceStartup - _packetFilterStartTime
            : 0f;

        LastPacketFilterWrongSubmissions = wrongSubmissions;
        LastPacketFilterTime             = timeTaken;
        _packetFilterInProgress          = false;
        TotalPacketFilterSolved++;

        AveragePacketFilterWrongSubmissions = TotalPacketFilterSolved <= 1
            ? wrongSubmissions
            : (AveragePacketFilterWrongSubmissions * (TotalPacketFilterSolved - 1) + wrongSubmissions)
              / TotalPacketFilterSolved;

        // BKT update -- replay each wrong commit then the correct solve
        for (int i = 0; i < wrongSubmissions; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.NetworkSecurity, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.NetworkSecurity, true);

        UpdateGeneralPool(wrongSubmissions, timeTaken);

        Debug.Log($"[Metrics] Packet Filter solved | Wrong commits: {wrongSubmissions} | " +
                  $"Time: {timeTaken:F1}s | Avg submissions: {AveragePacketFilterWrongSubmissions:F1}");
    }

    // ── Painting Puzzle API ───────────────────────────────────────────────────

    public int   TotalPaintingSolved             { get; private set; }
    public int   LastPaintingWrongAttempts       { get; private set; }
    public float LastPaintingTime                { get; private set; }
    public float AveragePaintingWrongAttempts    { get; private set; }

    private void HandlePaintingSolved(int wrongAttempts)
    {
        // Painting puzzle runs in world-space (no timeScale pause), so deltaTime
        // clocks are fine. We read elapsed time from the manager itself.
        float timeTaken = PaintingPuzzleManager.Instance != null
            ? PaintingPuzzleManager.Instance.ElapsedTime
            : 0f;

        LastPaintingWrongAttempts = wrongAttempts;
        LastPaintingTime          = timeTaken;
        TotalPaintingSolved++;

        AveragePaintingWrongAttempts = TotalPaintingSolved <= 1
            ? wrongAttempts
            : (AveragePaintingWrongAttempts * (TotalPaintingSolved - 1) + wrongAttempts)
              / TotalPaintingSolved;

        // BKT update -- each wrong confirm is a failed attempt on processes_threads
        for (int i = 0; i < wrongAttempts; i++)
            BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.ProcessesThreads, false);
        BKT?.UpdateAfterAttempt(BayesianKnowledgeTracker.ProcessesThreads, true);

        UpdateGeneralPool(wrongAttempts, timeTaken);

        Debug.Log($"[Metrics] Painting puzzle solved | Wrong confirms: {wrongAttempts} | " +
                  $"Time: {timeTaken:F1}s");
    }

    // ── General Puzzle Pool API ────────────────────────────────────────────────

    /// <summary>
    /// Call at the end of any puzzle handler that belongs to the general pool
    /// (Stack, Drain, Matching, Subnet, PacketFilter, Painting, and future puzzles).
    /// Maintains running averages used by PuzzleDDAController's general signal group.
    /// </summary>
    private void UpdateGeneralPool(int wrongAttempts, float timeTaken)
    {
        LastGeneralPuzzleWrongAttempts = wrongAttempts;
        LastGeneralPuzzleTime          = timeTaken;
        TotalGeneralPuzzleSolved++;

        AverageGeneralPuzzleWrongAttempts = TotalGeneralPuzzleSolved <= 1
            ? wrongAttempts
            : (AverageGeneralPuzzleWrongAttempts * (TotalGeneralPuzzleSolved - 1) + wrongAttempts)
              / TotalGeneralPuzzleSolved;

        AverageGeneralPuzzleTime = TotalGeneralPuzzleSolved <= 1
            ? timeTaken
            : (AverageGeneralPuzzleTime * (TotalGeneralPuzzleSolved - 1) + timeTaken)
              / TotalGeneralPuzzleSolved;
    }

    // ── Combat API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call when an enemy activates (starts chasing). If this is the first enemy
    /// of a fresh encounter, resets per-encounter counters and starts the timer.
    /// Handles multi-enemy encounters — only the transition from 0 to 1 resets counters.
    /// Called from EnemyBase.Activate().
    /// </summary>
    public void NotifyEnemyActivated()
    {
        _activeEnemyCount++;
        if (_activeEnemyCount == 1)
        {
            // First enemy of a new encounter — reset per-encounter state
            ShotsFired = 0;
            ShotsLanded = 0;
            _encounterStartTime = Time.realtimeSinceStartup;
            Debug.Log("[Metrics] Encounter started.");
        }
    }

    /// <summary>
    /// Call when an enemy dies. When the last active enemy is killed, snapshots
    /// encounter metrics and fires OnEncounterEnd for CombatDDAController.
    /// Called from EnemyBase.Die().
    /// </summary>
    public void NotifyEnemyKilled()
    {
        _activeEnemyCount = Mathf.Max(0, _activeEnemyCount - 1);
        if (_activeEnemyCount > 0) return;

        // Last enemy dead — close out the encounter
        LastEncounterDuration    = Time.realtimeSinceStartup - _encounterStartTime;
        LastEncounterShotsFired  = ShotsFired;
        LastEncounterShotsLanded = ShotsLanded;

        float accuracy = LastEncounterShotsFired > 0
            ? (float)LastEncounterShotsLanded / LastEncounterShotsFired
            : 0f;

        Debug.Log($"[Metrics] Encounter ended | Duration: {LastEncounterDuration:F1}s | " +
                  $"Accuracy: {LastEncounterShotsLanded}/{LastEncounterShotsFired} ({accuracy:P0})");

        OnEncounterEnd?.Invoke();
    }

    /// <summary>
    /// Call when the player fires a shot (not a dry fire).
    /// Called from PlayerCombat.HandleFiring().
    /// </summary>
    public void NotifyShotFired()
    {
        ShotsFired++;
    }

    /// <summary>
    /// Call when the player's shot connects with an IDamageable (enemy).
    /// Called from PlayerCombat.HandleFiring() after a successful raycast hit.
    /// </summary>
    public void NotifyShotLanded()
    {
        ShotsLanded++;
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    public void ResetMetrics()
    {
        // Room
        TotalSessionTime = 0f;
        CurrentRoomTime = 0f;
        CurrentRoomID = null;
        RoomsVisited = 0;
        _inRoom = false;
        _roomTimeLedger.Clear();
        _recentRooms.Clear();

        // Quiz
        HasStartedAnyQuiz = false;
        LastQuizScore = 0f;
        LastQuizTime = 0f;
        LastQuizPassed = false;
        TotalQuizAttempts = 0;
        TotalQuizPassed = 0;
        AverageQuizScore = 0f;
        _quizInProgress = false;

        // Linked list
        LastLinkedListWrongAttempts = 0;
        LastLinkedListTime = 0f;
        TotalLinkedListSolved = 0;
        AverageLinkedListWrongAttempts = 0f;
        AverageLinkedListTime = 0f;
        _linkedListInProgress = false;

        // Scheduling
        LastSchedulingWrongAttempts    = 0;
        LastSchedulingTime             = 0f;
        TotalSchedulingSolved          = 0;
        AverageSchedulingWrongAttempts = 0f;
        AverageSchedulingTime          = 0f;
        _schedulingInProgress          = false;

        // Stack
        LastStackWrongAttempts    = 0;
        LastStackTime             = 0f;
        TotalStackSolved          = 0;
        AverageStackWrongAttempts = 0f;
        AverageStackTime          = 0f;
        _stackInProgress          = false;

        // Hash Table
        LastHashTableWrongAttempts    = 0;
        LastHashTableTime             = 0f;
        TotalHashTableSolved          = 0;
        AverageHashTableWrongAttempts = 0f;
        AverageHashTableTime          = 0f;
        _hashTableInProgress          = false;

        // Drain
        LastDrainWrongAttempts    = 0;
        LastDrainTime             = 0f;
        TotalDrainSolved          = 0;
        AverageDrainWrongAttempts = 0f;
        AverageDrainTime          = 0f;
        _drainInProgress          = false;

        // Reagent Routing
        LastReagentRoutingWrongAttempts    = 0;
        LastReagentRoutingTime             = 0f;
        TotalReagentRoutingSolved          = 0;
        AverageReagentRoutingWrongAttempts = 0f;
        AverageReagentRoutingTime          = 0f;
        _reagentRoutingInProgress          = false;

        // Packet filter
        LastPacketFilterWrongSubmissions    = 0;
        LastPacketFilterTime               = 0f;
        TotalPacketFilterSolved            = 0;
        AveragePacketFilterWrongSubmissions = 0f;
        _packetFilterInProgress            = false;

        // Painting puzzle
        LastPaintingWrongAttempts    = 0;
        LastPaintingTime             = 0f;
        TotalPaintingSolved          = 0;
        AveragePaintingWrongAttempts = 0f;

        // General puzzle pool
        LastGeneralPuzzleWrongAttempts    = 0;
        LastGeneralPuzzleTime             = 0f;
        TotalGeneralPuzzleSolved          = 0;
        AverageGeneralPuzzleWrongAttempts = 0f;
        AverageGeneralPuzzleTime          = 0f;

        // Combat
        ShotsFired               = 0;
        ShotsLanded              = 0;
        LastEncounterDuration    = 0f;
        LastEncounterShotsFired  = 0;
        LastEncounterShotsLanded = 0;
        _activeEnemyCount        = 0;

        Debug.Log("[Metrics] All metrics reset.");
    }

    // ── Director Observation Vector ────────────────────────────────────────

    /// <summary>
    /// Normalised observation vector for the PPO Director.
    /// All values clamped to [0, 1].
    ///
    /// Layout (15 values):
    ///   -- Room / Time --
    ///   [0]  Total session time
    ///   [1]  Current room time
    ///   [2]  Average completed room time
    ///   [3]  Previous room time
    ///   [4]  Rooms visited (cap 20)
    ///   -- Quiz --
    ///   [5]  Last quiz score
    ///   [6]  Last quiz time taken
    ///   [7]  Last quiz passed (0 or 1)
    ///   [8]  Average quiz score this session
    ///   [9]  Total quiz attempts (cap = maxQuizAttempts)
    ///   -- Linked List --
    ///   [10] Last linked list wrong attempts
    ///   [11] Last linked list time taken
    ///   [12] Total linked list puzzles solved (cap 5)
    ///   [13] Average wrong attempts per linked list puzzle
    ///   [14] Average time per linked list puzzle
    ///   -- Scheduling --
    ///   [15] Last scheduling wrong attempts
    ///   [16] Last scheduling time taken
    ///   [17] Total scheduling puzzles solved (cap 5)
    ///   [18] Average wrong attempts per scheduling puzzle
    ///   [19] Average time per scheduling puzzle
    ///   -- Stack --
    ///   [20] Last stack wrong attempts
    ///   [21] Last stack time taken
    ///   [22] Total stack puzzles solved (cap 5)
    ///   [23] Average wrong attempts per stack puzzle
    ///   [24] Average time per stack puzzle
    /// </summary>
    public float[] GetObservations()
    {
        float avgRoomTime  = GetAverageCompletedRoomTime();
        float prevRoomTime = 0f;
        foreach (var entry in _recentRooms) prevRoomTime = entry.time;

        return new float[]
        {
            // Room / time
            Mathf.Clamp01(TotalSessionTime / maxSessionTime),
            Mathf.Clamp01(CurrentRoomTime  / maxRoomTime),
            Mathf.Clamp01(avgRoomTime      / maxAvgRoomTime),
            Mathf.Clamp01(prevRoomTime     / maxRoomTime),
            Mathf.Clamp01(RoomsVisited     / 20f),
            // Quiz
            Mathf.Clamp01(LastQuizScore),
            Mathf.Clamp01(LastQuizTime     / maxQuizTime),
            LastQuizPassed ? 1f : 0f,
            Mathf.Clamp01(AverageQuizScore),
            Mathf.Clamp01(TotalQuizAttempts / (float)maxQuizAttempts),
            // Linked list
            Mathf.Clamp01(LastLinkedListWrongAttempts    / (float)maxLinkedListAttempts),
            Mathf.Clamp01(LastLinkedListTime             / maxLinkedListTime),
            Mathf.Clamp01(TotalLinkedListSolved          / 5f),
            Mathf.Clamp01(AverageLinkedListWrongAttempts / maxLinkedListAttempts),
            Mathf.Clamp01(AverageLinkedListTime          / maxLinkedListTime),
            // Scheduling
            Mathf.Clamp01(LastSchedulingWrongAttempts    / (float)maxSchedulingAttempts),
            Mathf.Clamp01(LastSchedulingTime             / maxSchedulingTime),
            Mathf.Clamp01(TotalSchedulingSolved          / 5f),
            Mathf.Clamp01(AverageSchedulingWrongAttempts / maxSchedulingAttempts),
            Mathf.Clamp01(AverageSchedulingTime          / maxSchedulingTime),
            // Stack
            Mathf.Clamp01(LastStackWrongAttempts    / (float)maxStackAttempts),
            Mathf.Clamp01(LastStackTime             / maxStackTime),
            Mathf.Clamp01(TotalStackSolved          / 5f),
            Mathf.Clamp01(AverageStackWrongAttempts / maxStackAttempts),
            Mathf.Clamp01(AverageStackTime          / maxStackTime),
        };
    }

    /// <summary>Total observation vector size. Update Space Size in DirectorAgent to 25.</summary>
    public const int ObservationSize = 25;

    // ── Helpers ────────────────────────────────────────────────────────────

    public float GetTotalTimeInRoom(string roomID) =>
        _roomTimeLedger.TryGetValue(roomID, out float t) ? t : 0f;

    public IReadOnlyDictionary<string, float> GetRoomLedger() => _roomTimeLedger;

    private void CloseCurrentRoom()
    {
        if (!_inRoom) return;

        if (!_roomTimeLedger.ContainsKey(CurrentRoomID))
            _roomTimeLedger[CurrentRoomID] = 0f;
        _roomTimeLedger[CurrentRoomID] += CurrentRoomTime;

        _recentRooms.Enqueue((CurrentRoomID, CurrentRoomTime));
        if (_recentRooms.Count > HistoryCapacity) _recentRooms.Dequeue();

        Debug.Log($"[Metrics] Exited '{CurrentRoomID}' after {CurrentRoomTime:F1}s");

        _inRoom = false;
        CurrentRoomTime = 0f;
    }

    private float GetAverageCompletedRoomTime()
    {
        if (_recentRooms.Count == 0) return 0f;
        float sum = 0f;
        foreach (var entry in _recentRooms) sum += entry.time;
        return sum / _recentRooms.Count;
    }

    // ── Debug HUD ──────────────────────────────────────────────────────────
//    private void OnGUI()
//    {
//#if UNITY_EDITOR
//        GUILayout.BeginArea(new Rect(10, 10, 310, 330));
//        GUILayout.Label("[Metrics Debug]");
//        GUILayout.Label($"Session Time       : {TotalSessionTime:F1}s");
//        GUILayout.Label($"Current Room       : {CurrentRoomID ?? "None"}");
//        GUILayout.Label($"Room Time          : {CurrentRoomTime:F1}s");
//        GUILayout.Label($"Avg Room Time      : {GetAverageCompletedRoomTime():F1}s");
//        GUILayout.Label($"Rooms Visited      : {RoomsVisited}");
//        GUILayout.Label("── Quiz ──────────────────────────────");
//        GUILayout.Label($"Last Score         : {LastQuizScore:P0}  Passed: {LastQuizPassed}");
//        GUILayout.Label($"Last Quiz Time     : {LastQuizTime:F1}s");
//        GUILayout.Label($"Avg Score          : {AverageQuizScore:P0}");
//        GUILayout.Label($"Attempts           : {TotalQuizAttempts}  Passed: {TotalQuizPassed}");
//        GUILayout.Label("── Linked List ───────────────────────");
//        GUILayout.Label($"Last Wrong Attempts: {LastLinkedListWrongAttempts}  (Total submits: {LastLinkedListTotalSubmits})");
//        GUILayout.Label($"Last Puzzle Time   : {LastLinkedListTime:F1}s");
//        GUILayout.Label($"Puzzles Solved     : {TotalLinkedListSolved}");
//        GUILayout.Label($"Avg Wrong Attempts : {AverageLinkedListWrongAttempts:F1}");
//        GUILayout.Label($"Avg Solve Time     : {AverageLinkedListTime:F1}s");
//        GUILayout.EndArea();
//#endif
//    }
}
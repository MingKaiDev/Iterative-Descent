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

    // ── Inspector Config ───────────────────────────────────────────────────
    [Header("Normalisation Caps (seconds)")]
    [SerializeField] private float maxSessionTime = 1800f;  // 30 min
    [SerializeField] private float maxRoomTime = 300f;   // 5 min
    [SerializeField] private float maxAvgRoomTime = 120f;   // 2 min

    [Header("Normalisation Caps (quiz)")]
    [SerializeField] private float maxQuizTime = 120f;  // 2 min per quiz
    [SerializeField] private int maxQuizAttempts = 5;

    [Header("Normalisation Caps (linked list)")]
    [SerializeField] private float maxLinkedListTime = 180f; // 3 min per puzzle
    [SerializeField] private int maxLinkedListAttempts = 10;   // wrong attempts before solving

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

    /// <summary>Total quiz attempts this session (including retries).</summary>
    public int TotalQuizAttempts { get; private set; }

    /// <summary>Total quizzes passed this session.</summary>
    public int TotalQuizPassed { get; private set; }

    /// <summary>Running average score across all quiz attempts this session.</summary>
    public float AverageQuizScore { get; private set; }

    private float _quizStartTime;
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

    private float _linkedListStartTime;
    private bool _linkedListInProgress;

    // ── Unity Lifecycle ────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        PuzzleUI.OnPuzzleFinished += HandleQuizFinished;
        LinkedListPuzzleUI.OnLinkedListSolved += HandleLinkedListSolved;
    }

    private void OnDisable()
    {
        PuzzleUI.OnPuzzleFinished -= HandleQuizFinished;
        LinkedListPuzzleUI.OnLinkedListSolved -= HandleLinkedListSolved;
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
        _quizStartTime = TotalSessionTime;
        _quizInProgress = true;
        TotalQuizAttempts++;
        Debug.Log($"[Metrics] Quiz started (attempt #{TotalQuizAttempts})");
    }

    private void HandleQuizFinished(int correct, int total)
    {
        float score = total > 0 ? (float)correct / total : 0f;
        float timeTaken = _quizInProgress ? TotalSessionTime - _quizStartTime : 0f;
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

    // ── Linked List API ────────────────────────────────────────────────────

    /// <summary>
    /// Call this when the player opens the linked list puzzle so we can time it.
    /// Hook this wherever you call linkedListPuzzleUI.InitPuzzle().
    /// </summary>
    public void NotifyLinkedListStarted()
    {
        _linkedListStartTime = TotalSessionTime;
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
            ? TotalSessionTime - _linkedListStartTime
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

        Debug.Log($"[Metrics] Linked list solved | Wrong attempts: {wrongAttempts} | " +
                  $"Total submits: {LastLinkedListTotalSubmits} | " +
                  $"Time: {timeTaken:F1}s | Avg attempts: {AverageLinkedListWrongAttempts:F1}");
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
    ///   [10] Last linked list wrong attempts (cap = maxLinkedListAttempts)
    ///   [11] Last linked list time taken
    ///   [12] Total linked list puzzles solved (cap 5)
    ///   [13] Average wrong attempts per puzzle this session
    ///   [14] Average time per puzzle this session
    /// </summary>
    public float[] GetObservations()
    {
        float avgRoomTime = GetAverageCompletedRoomTime();
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
            Mathf.Clamp01(AverageLinkedListTime          / maxLinkedListTime)
        };
    }

    /// <summary>Total observation vector size. Set Space Size to this in DirectorAgent.</summary>
    public const int ObservationSize = 15;

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
// PuzzleProp.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("Puzzle Content")]
    [Tooltip("Fallback questions used when Load From JSON is disabled.")]
    public QuestionData[] questions = new QuestionData[5];

    [Header("JSON Source (optional)")]
    [Tooltip("If enabled, questions are loaded from StreamingAssets instead of the Inspector array above.")]
    public bool loadFromJson = false;
    [Tooltip("File name inside StreamingAssets, e.g. quiz_bank.json")]
    public string jsonFileName = "quiz_bank.json";

    [Header("Question Selection")]
    [Tooltip("How many questions to show per quiz session.")]
    public int questionsPerSession = 5;

    [Tooltip("If set, this terminal always draws from this concept tag only, " +
             "bypassing BKT targeting and the first-session plan. " +
             "Use for contextual terminals (e.g. 'bfs_dfs' before the Drain Puzzle). " +
             "Leave blank to use normal BKT-driven selection.")]
    public string pinnedConceptTag = "";

    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- the Puzzle Panel is resolved at runtime via " +
             "PuzzleUI.Instance instead, same cross-scene-safe pattern as every other puzzle " +
             "prop in this project (see BstPuzzleProp.ResolvePuzzleUI()). A plain serialized " +
             "reference here goes stale the moment Level 1 is unloaded and reloaded (returning " +
             "from Level 2), because the fresh Canvas instantiated on reload is immediately " +
             "destroyed as a duplicate by PersistentUIRoot -- this prop's own fresh copy would " +
             "still point at that now-destroyed panel. Only assign this if the Puzzle Panel " +
             "happens to be placed in THIS SAME scene and you want to force a specific instance.")]
    public GameObject puzzleOverlay;

    [Header("Pre-Quiz Briefing (optional)")]
    [Tooltip("If set, shown as a notice every time this quiz opens, before any concept " +
             "tutorial or the quiz itself, explaining what the quiz is for (e.g. why the " +
             "player should bother answering). Leave blank to skip straight to the quiz. " +
             "Requires a QuizBriefingUI in the scene -- see FYP/setup-guides/QuizBriefing_UnitySetup.md.")]
    [TextArea(2, 4)]
    [SerializeField] private string briefingMessage = "";

    [Header("Password Reveal (optional)")]
    [Tooltip("If assigned, passing this quiz (60% or higher) can reveal this PasswordScreenUI's " +
             "correctPassword on the completion screen -- use the {password} placeholder in " +
             "Success Message below to show it. The password string itself always comes from " +
             "the PasswordScreenUI component, never duplicated here, so the two can never drift " +
             "out of sync. Leave blank to fall back to PasswordScreenUI.Instance (the normal " +
             "case -- there's only one password screen), or if this quiz has nothing to do with " +
             "the password puzzle at all.")]
    [SerializeField] private PasswordScreenUI passwordScreenToReveal;

    [Header("Completion Message (optional)")]
    [Tooltip("Shown on the completion screen when the player passes (60% or higher). " +
             "Every generic MCQ terminal in the project shares the same PuzzleUI panel, so " +
             "leaving this blank on a terminal that isn't the password reward prop used to " +
             "always show 'Computer Password is X' -- this field is what lets each prop say " +
             "something that actually fits it (e.g. 'Terminal cleared.'). " +
             "Placeholders: {password} (from Password Reveal above, blank if unset), " +
             "{correct} and {total} (score for this session). " +
             "Leave blank to fall back to the old default: the password-reveal line if " +
             "Password Reveal is assigned, otherwise a generic pass/fail summary.")]
    [TextArea(2, 4)]
    [SerializeField] private string successMessage = "";

    [Header("Outcome Events")]
    [Tooltip("Fired when the player completes the quiz (clicks Close after all questions). Wire up door unlocks, enemy spawns, etc. here.")]
    [SerializeField] private UnityEvent onCompleted;

    // IInteractable
    public string InteractLabel => "Take Quiz";

    public void Interact(GameObject interactor)
    {
        // _questionsReady guards against interacting before the (async, on
        // WebGL/Android an HTTP-backed) JSON load finishes -- see Start().
        if (_puzzleOpen || !_questionsReady) return;
        OpenPuzzle();
    }

    // Puzzle Logic
    private bool _puzzleOpen;
    private bool _questionsReady;
    private QuestionData[] _resolvedQuestions;  // full bank, loaded once

    void Awake()
    {
        if (!loadFromJson)
        {
            _resolvedQuestions = questions;
            _questionsReady    = true;
        }
    }

    void Start()
    {
        // Loading must happen in a coroutine, not Awake(), because
        // QuizDataLoader now goes through UnityWebRequest (StreamingAssets
        // is served over HTTP on WebGL / packed in the APK on Android, so it
        // can't be read synchronously with System.IO there -- it only
        // appeared to work before because Editor Play Mode reads the real
        // Assets/StreamingAssets folder directly).
        if (loadFromJson)
            StartCoroutine(LoadQuestionsFromJson());
    }

    private IEnumerator LoadQuestionsFromJson()
    {
        yield return QuizDataLoader.LoadCoroutine(jsonFileName, result =>
        {
            _resolvedQuestions = result;
            if (_resolvedQuestions == null)
            {
                Debug.LogWarning($"[PuzzleProp] '{gameObject.name}' JSON load failed — falling back to Inspector questions.");
                _resolvedQuestions = questions;
            }
        });
        _questionsReady = true;
    }

    // Holds the selected questions between OpenPuzzle() and OpenPuzzleUI() when
    // a concept primer plays in between (see ConceptPrimers.cs).
    private QuestionData[] _pendingSessionQuestions;

    public void OpenPuzzle()
    {
        _puzzleOpen = true;
        PlayerInteractor.Pause();

        // Falls back to the shared PromptPanelUI when this prop's own promptPanel
        // is unassigned -- same helper every other puzzle prop uses (see
        // InteractableBase.HidePrompt()).
        GetComponent<InteractableBase>()?.HidePrompt();

        // isFirst is global: true only before the player has opened any quiz terminal
        // this session. Per-prop tracking caused every new terminal to always give
        // the same first-session questions.
        bool isFirst = !(PlayerMetricsTracker.Instance?.HasStartedAnyQuiz ?? false);
        _pendingSessionQuestions = QuestionSelector.SelectQuestions(
            _resolvedQuestions, isFirst, questionsPerSession, pinnedConceptTag);

        // A single session can mix questions from more than one concept tag
        // (e.g. the first session mixes arrays_and_lists + complexity_big_o --
        // see QuestionSelector.FirstSessionPlan). Collect every distinct tag
        // present, in the order each first appears in the session, so a
        // tutorial can be shown for each one the player hasn't seen yet --
        // not just whichever tag happens to land on question 1.
        var sessionConcepts = new List<string>();
        if (_pendingSessionQuestions != null)
        {
            foreach (QuestionData q in _pendingSessionQuestions)
            {
                if (!string.IsNullOrEmpty(q.conceptTag) && !sessionConcepts.Contains(q.conceptTag))
                    sessionConcepts.Add(q.conceptTag);
            }
        }

        // First time the player sees each of these concepts, a graphical
        // tutorial panel explains it before the quiz opens -- shown one after
        // another if more than one is unseen. Every subsequent time (or if no
        // tutorial exists yet for a tag) that tag is skipped. OpenPuzzleUI is
        // always eventually called once the queue is empty, even if it was
        // empty to begin with.
        //
        // The briefing notice (if configured) runs first, ahead of the concept
        // tutorial queue -- it explains why the player is doing this quiz at
        // all, before any per-concept teaching content.
        void ContinueToConceptTutorials()
            => ConceptTutorials.ShowAllUnseenThenContinue(sessionConcepts, OpenPuzzleUI);

        if (QuizBriefingUI.Instance != null && !string.IsNullOrEmpty(briefingMessage))
            QuizBriefingUI.Instance.Show(briefingMessage, ContinueToConceptTutorials);
        else
            ContinueToConceptTutorials();
    }

    /// <summary>
    /// Same-scene override first, otherwise the cross-scene-safe Instance lookup --
    /// identical shape to BstPuzzleProp.ResolvePuzzleUI(). Needed because a plain
    /// serialized reference to the Puzzle Panel goes stale across a Level 1 ->
    /// Level 2 -> Level 1 reload: the freshly-reloaded Canvas (and every panel
    /// under it) is immediately destroyed as a duplicate by PersistentUIRoot,
    /// leaving this prop's own freshly-reloaded copy pointing at a dead object.
    /// FindFirstObjectByType(..., FindObjectsInactive.Include) also covers the
    /// very first open of the session, before PuzzleUI.Awake() has necessarily
    /// run (the panel starts inactive in the hierarchy).
    /// </summary>
    private PuzzleUI ResolvePuzzleUI()
    {
        if (puzzleOverlay != null)
        {
            var local = puzzleOverlay.GetComponent<PuzzleUI>();
            if (local != null) return local;
        }

        if (PuzzleUI.Instance != null) return PuzzleUI.Instance;

        return FindFirstObjectByType<PuzzleUI>(FindObjectsInactive.Include);
    }

    private PasswordScreenUI ResolvePasswordScreen()
    {
        if (passwordScreenToReveal != null) return passwordScreenToReveal;
        return PasswordScreenUI.Instance;
    }

    private void OpenPuzzleUI()
    {
        var puzzle = ResolvePuzzleUI();
        if (puzzle == null)
        {
            Debug.LogError("[PuzzleProp] No PuzzleUI found. Is the Puzzle Panel present under " +
                            "the persisted Canvas, and did you enter play mode via Level 1 (so " +
                            "the Canvas has actually loaded and persisted forward)?", this);
            ClosePuzzle(false);
            return;
        }

        puzzle.gameObject.SetActive(true);

        string reveal = ResolvePasswordScreen()?.correctPassword;

        // NotifyQuizStarted() is called inside PuzzleUI.Setup() -- don't call it
        // here too or TotalQuizAttempts gets incremented twice per quiz.
        puzzle.Setup(_pendingSessionQuestions, ClosePuzzle, reveal, successMessage);

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // ICloseable -- called by PlayerInteractor when ESC is pressed while paused.
    public void Close() => ClosePuzzle(false);

    public void ClosePuzzle(bool completed)
    {
        _puzzleOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume(); // re-enables interactor

        var puzzle = ResolvePuzzleUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (completed)
        {
            Debug.Log($"[PuzzleProp] '{gameObject.name}' puzzle completed!");
            onCompleted?.Invoke();
        }
    }
}

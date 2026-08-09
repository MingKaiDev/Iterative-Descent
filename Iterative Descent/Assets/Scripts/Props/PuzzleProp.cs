// PuzzleProp.cs
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

    [Header("UI")]
    [Tooltip("Assign the Puzzle Panel GameObject in the Canvas.")]
    public GameObject puzzleOverlay;

    [Header("Pre-Quiz Briefing (optional)")]
    [Tooltip("If set, shown as a notice every time this quiz opens, before any concept " +
             "tutorial or the quiz itself, explaining what the quiz is for (e.g. why the " +
             "player should bother answering). Leave blank to skip straight to the quiz. " +
             "Requires a QuizBriefingUI in the scene -- see FYP/setup-guides/QuizBriefing_UnitySetup.md.")]
    [TextArea(2, 4)]
    [SerializeField] private string briefingMessage = "";

    [Header("Password Reveal (optional)")]
    [Tooltip("If assigned, passing this quiz (60% or higher) reveals this PasswordScreenUI's " +
             "correctPassword directly on the completion screen, e.g. 'Access granted. " +
             "Password: X'. The password string itself always comes from the PasswordScreenUI " +
             "component, never duplicated here, so the two can never drift out of sync. " +
             "Leave blank for a normal quiz with no password reward text.")]
    [SerializeField] private PasswordScreenUI passwordScreenToReveal;

    [Header("Outcome Events")]
    [Tooltip("Fired when the player completes the quiz (clicks Close after all questions). Wire up door unlocks, enemy spawns, etc. here.")]
    [SerializeField] private UnityEvent onCompleted;

    // IInteractable
    public string InteractLabel => "Take Quiz";

    public void Interact(GameObject interactor)
    {
        if (_puzzleOpen) return;
        OpenPuzzle();
    }

    // Puzzle Logic
    private bool _puzzleOpen;
    private QuestionData[] _resolvedQuestions;  // full bank, loaded once

    void Awake()
    {
        if (puzzleOverlay != null) puzzleOverlay.SetActive(false);

        // Resolve questions once at startup so the file isn't re-read on every interaction.
        if (loadFromJson)
        {
            _resolvedQuestions = QuizDataLoader.Load(jsonFileName);
            if (_resolvedQuestions == null)
            {
                Debug.LogWarning($"[PuzzleProp] '{gameObject.name}' JSON load failed — falling back to Inspector questions.");
                _resolvedQuestions = questions;
            }
        }
        else
        {
            _resolvedQuestions = questions;
        }
    }

    // Holds the selected questions between OpenPuzzle() and OpenPuzzleUI() when
    // a concept primer plays in between (see ConceptPrimers.cs).
    private QuestionData[] _pendingSessionQuestions;

    public void OpenPuzzle()
    {
        _puzzleOpen = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

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

    private void OpenPuzzleUI()
    {
        if (puzzleOverlay != null)
        {
            puzzleOverlay.SetActive(true);

            string reveal = passwordScreenToReveal != null ? passwordScreenToReveal.correctPassword : null;

            // NotifyQuizStarted() is called inside PuzzleUI.Setup() -- don't call it
            // here too or TotalQuizAttempts gets incremented twice per quiz.
            puzzleOverlay.GetComponent<PuzzleUI>().Setup(_pendingSessionQuestions, ClosePuzzle, reveal);
        }
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

        if (puzzleOverlay != null) puzzleOverlay.SetActive(false);
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
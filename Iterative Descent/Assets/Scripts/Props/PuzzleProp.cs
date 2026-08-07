// PuzzleProp.cs
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

    [Header("Outcome Events")]
    [Tooltip("Fired when the player completes the quiz (clicks Close after all questions). Wire up door unlocks, enemy spawns, etc. here.")]
    [SerializeField] private UnityEvent onCompleted;

    // IInteractable
    public string InteractLabel => "Read Note";

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

        string primaryConcept = (_pendingSessionQuestions != null && _pendingSessionQuestions.Length > 0)
            ? _pendingSessionQuestions[0].conceptTag
            : "";

        // First time the player sees this concept, a graphical tutorial panel
        // explains it before the quiz opens. Every subsequent time (or if no
        // tutorial exists yet for this tag) the puzzle opens immediately, same
        // as before. OpenPuzzleUI is always eventually called either way.
        ConceptTutorials.ShowIfUnseenThenContinue(primaryConcept, OpenPuzzleUI);
    }

    private void OpenPuzzleUI()
    {
        if (puzzleOverlay != null)
        {
            puzzleOverlay.SetActive(true);

            // NotifyQuizStarted() is called inside PuzzleUI.Setup() -- don't call it
            // here too or TotalQuizAttempts gets incremented twice per quiz.
            puzzleOverlay.GetComponent<PuzzleUI>().Setup(_pendingSessionQuestions, ClosePuzzle);
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
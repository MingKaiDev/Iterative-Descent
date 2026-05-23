// PuzzleProp.cs
using UnityEngine;
using UnityEngine.Events;

public class PuzzleProp : MonoBehaviour, IInteractable
{
    [Header("Puzzle Content")]
    [Tooltip("Fallback questions used when Load From JSON is disabled.")]
    public QuestionData[] questions = new QuestionData[5];

    [Header("JSON Source (optional)")]
    [Tooltip("If enabled, questions are loaded from StreamingAssets instead of the Inspector array above.")]
    public bool loadFromJson = false;
    [Tooltip("File name inside StreamingAssets, e.g. quiz_data.json")]
    public string jsonFileName = "quiz_data.json";

    [Header("Question Selection")]
    [Tooltip("How many questions to show per quiz session.")]
    public int questionsPerSession = 5;

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
    private int  _sessionCount = 0;             // increments each time the quiz opens

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

    public void OpenPuzzle()
    {
        _puzzleOpen = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        if (puzzleOverlay != null)
        {
            puzzleOverlay.SetActive(true);

            // Select a session-appropriate subset from the full bank.
            // Session 0 = first time: predetermined foundational concepts for BKT baseline.
            // Session 1+ : BKT picks the weakest concept.
            bool isFirst = _sessionCount == 0;
            QuestionData[] sessionQuestions = QuestionSelector.SelectQuestions(
                _resolvedQuestions, isFirst, questionsPerSession);
            _sessionCount++;

            // NotifyQuizStarted() is called inside PuzzleUI.Setup() -- don't call it
            // here too or TotalQuizAttempts gets incremented twice per quiz.
            puzzleOverlay.GetComponent<PuzzleUI>().Setup(sessionQuestions, ClosePuzzle);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void ClosePuzzle(bool completed)
    {
        _puzzleOpen = false;
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
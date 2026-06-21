// ExamPaperProp.cs
// IInteractable prop that opens the exam-paper styled quiz overlay.
// Mirrors PuzzleProp exactly -- same BKT question selection, same DDA hooks.
// Use this on any desk / clipboard prop you want to trigger Quiz 2.
using UnityEngine;
using UnityEngine.Events;

public class ExamPaperProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("Puzzle Content")]
    [Tooltip("Fallback questions used when Load From JSON is disabled.")]
    public QuestionData[] questions = new QuestionData[5];

    [Header("JSON Source (optional)")]
    public bool   loadFromJson   = false;
    public string jsonFileName   = "quiz2_data.json";

    [Header("Question Selection")]
    public int questionsPerSession = 5;

    [Header("UI")]
    [Tooltip("Assign the ExamPaperPanel GameObject in the Canvas.")]
    public GameObject examPaperOverlay;

    [Header("Outcome Events")]
    [SerializeField] private UnityEvent onCompleted;

    // IInteractable
    public string InteractLabel => "Pick Up Paper";

    public void Interact(GameObject interactor)
    {
        if (_open) return;
        OpenPuzzle();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private bool            _open;
    private QuestionData[]  _resolvedQuestions;
    private int             _sessionCount;

    void Awake()
    {
        if (examPaperOverlay != null) examPaperOverlay.SetActive(false);

        if (loadFromJson)
        {
            _resolvedQuestions = QuizDataLoader.Load(jsonFileName);
            if (_resolvedQuestions == null)
            {
                Debug.LogWarning($"[ExamPaperProp] '{gameObject.name}' JSON load failed -- falling back to Inspector questions.");
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
        _open = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        if (examPaperOverlay != null)
        {
            examPaperOverlay.SetActive(true);

            bool isFirst = _sessionCount == 0;
            QuestionData[] sessionQuestions = QuestionSelector.SelectQuestions(
                _resolvedQuestions, isFirst, questionsPerSession);
            _sessionCount++;

            // NotifyQuizStarted() is called inside ExamPaperPuzzleUI.Setup()
            // Do NOT call it here -- same rule as PuzzleProp.
            examPaperOverlay.GetComponent<ExamPaperPuzzleUI>().Setup(sessionQuestions, ClosePuzzle);
        }

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    // ICloseable -- ESC exits without credit
    public void Close() => ClosePuzzle(false);

    public void ClosePuzzle(bool completed)
    {
        _open = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        if (examPaperOverlay != null) examPaperOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;

        if (completed)
        {
            Debug.Log($"[ExamPaperProp] '{gameObject.name}' quiz completed!");
            onCompleted?.Invoke();
        }
    }
}

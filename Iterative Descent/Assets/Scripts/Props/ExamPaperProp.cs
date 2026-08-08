// ExamPaperProp.cs
// IInteractable prop that opens the exam-paper styled quiz overlay.
// Mirrors PuzzleProp exactly -- same BKT question selection, same DDA hooks,
// and (as of 2026-08-08) the same concept-tutorial wiring. Use this on any
// desk / clipboard prop you want to trigger Quiz 2.
using System.Collections.Generic;
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
    public int    questionsPerSession = 5;
    [Tooltip("If set, always draws from this concept tag only, bypassing BKT and first-session logic. Leave blank for normal BKT-driven selection.")]
    public string pinnedConceptTag   = "";

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

    private bool           _open;
    private QuestionData[] _resolvedQuestions;

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

    // Holds the selected questions between OpenPuzzle() and OpenExamPaperUI()
    // when a concept tutorial plays in between. See PuzzleProp.cs for the
    // same pattern.
    private QuestionData[] _pendingSessionQuestions;

    public void OpenPuzzle()
    {
        _open = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        bool isFirst = !(PlayerMetricsTracker.Instance?.HasStartedAnyQuiz ?? false);
        _pendingSessionQuestions = QuestionSelector.SelectQuestions(
            _resolvedQuestions, isFirst, questionsPerSession, pinnedConceptTag);

        // Same multi-concept tutorial handling as PuzzleProp.OpenPuzzle() --
        // collect every distinct concept tag in this session (not just the
        // first question's), show a tutorial for each one the player hasn't
        // seen yet, then open the exam paper overlay once the queue is empty.
        var sessionConcepts = new List<string>();
        if (_pendingSessionQuestions != null)
        {
            foreach (QuestionData q in _pendingSessionQuestions)
            {
                if (!string.IsNullOrEmpty(q.conceptTag) && !sessionConcepts.Contains(q.conceptTag))
                    sessionConcepts.Add(q.conceptTag);
            }
        }
        ConceptTutorials.ShowAllUnseenThenContinue(sessionConcepts, OpenExamPaperUI);
    }

    private void OpenExamPaperUI()
    {
        if (examPaperOverlay != null)
        {
            examPaperOverlay.SetActive(true);

            // NotifyQuizStarted() is called inside ExamPaperPuzzleUI.Setup()
            // Do NOT call it here -- same rule as PuzzleProp.
            examPaperOverlay.GetComponent<ExamPaperPuzzleUI>().Setup(_pendingSessionQuestions, ClosePuzzle);
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

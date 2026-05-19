// PuzzleProp.cs
using UnityEngine;

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

    [Header("UI")]
    [Tooltip("Assign the Puzzle Panel GameObject in the Canvas.")]
    public GameObject puzzleOverlay;

    // IInteractable
    public string InteractLabel => "Read Note";

    public void Interact(GameObject interactor)
    {
        if (_puzzleOpen) return;
        OpenPuzzle();
    }

    // Puzzle Logic
    private bool _puzzleOpen;
    private QuestionData[] _resolvedQuestions;

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
            // NotifyQuizStarted() is called inside PuzzleUI.Setup() — don't call it
            // here too or TotalQuizAttempts gets incremented twice per quiz.
            puzzleOverlay.GetComponent<PuzzleUI>().Setup(_resolvedQuestions, ClosePuzzle);
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
            Debug.Log($"[PuzzleProp] '{gameObject.name}' puzzle completed!");
    }
}
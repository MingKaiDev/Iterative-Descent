// PuzzleProp.cs
using UnityEngine;

public class PuzzleProp : MonoBehaviour, IInteractable
{
    [Header("Puzzle Content")]
    public QuestionData[] questions = new QuestionData[5];

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

    void Awake()
    {
        if (puzzleOverlay != null) puzzleOverlay.SetActive(false);
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
            puzzleOverlay.GetComponent<PuzzleUI>().Setup(questions, ClosePuzzle);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void ClosePuzzle(bool completed)
    {
        _puzzleOpen = false;
        PlayerInteractor.Resume(); // Å© re-enables interactor

        if (puzzleOverlay != null) puzzleOverlay.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;

        if (completed)
            Debug.Log($"[PuzzleProp] '{gameObject.name}' puzzle completed!");
    }
}
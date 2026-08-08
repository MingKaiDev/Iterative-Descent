using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Matching Puzzle overlay.
///
/// Mirrors DrainPuzzleProp exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the matching puzzle terminal prop (or a child trigger collider).
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. Set 'matchingOverlay' to the MatchingPanel in the Canvas (starts inactive).
///   4. The Canvas needs a GraphicRaycaster; scene needs an EventSystem.
///
/// NOTE: NotifyMatchingStarted() is called inside MatchingPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class MatchingPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("MatchingPanel GameObject in the Canvas (inactive by default).")]
    public GameObject matchingOverlay;

    // IInteractable
    public string InteractLabel => "Access Network Terminal";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (matchingOverlay != null)
            matchingOverlay.SetActive(false);
    }

    public void Interact(GameObject interactor)
    {
        if (_puzzleOpen) return;
        OpenPuzzle();
    }

    public void OpenPuzzle()
    {
        _puzzleOpen = true;
        PlayerInteractor.Pause();

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase?.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        // First time the player sees this concept, show the tutorial panel
        // before the puzzle overlay opens. See ConceptTutorials.cs.
        ConceptTutorials.ShowIfUnseenThenContinue("networking_ports", OpenMatchingUI);
    }

    private void OpenMatchingUI()
    {
        if (matchingOverlay != null)
        {
            matchingOverlay.SetActive(true);
            matchingOverlay.GetComponent<MatchingPuzzleUI>().InitPuzzle(ClosePuzzle);
        }

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    // ICloseable
    public void Close() => ClosePuzzle();

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        if (matchingOverlay != null)
            matchingOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

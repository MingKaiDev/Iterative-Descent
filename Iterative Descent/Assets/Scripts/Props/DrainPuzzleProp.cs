using UnityEngine;

/// <summary>
/// IInteractable prop that opens the BFS/DFS Drain puzzle overlay.
///
/// Mirrors StackPuzzleProp exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the DrainTerminal prop GameObject (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. Set 'drainOverlay' to the Drain Panel in the Canvas (starts inactive).
///   4. The Canvas Drain Panel needs: DrainPuzzleUI, EventSystem, GraphicRaycaster.
///
/// NOTE: NotifyDrainStarted() is called inside DrainPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class DrainPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("Drain Panel GameObject in the Canvas (inactive by default).")]
    public GameObject drainOverlay;

    // IInteractable
    public string InteractLabel => "Access Drain Terminal";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (drainOverlay != null)
            drainOverlay.SetActive(false);
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
        ConceptTutorials.ShowIfUnseenThenContinue("bfs_dfs", OpenDrainUI);
    }

    private void OpenDrainUI()
    {
        if (drainOverlay != null)
        {
            drainOverlay.SetActive(true);
            drainOverlay.GetComponent<DrainPuzzleUI>().InitPuzzle(ClosePuzzle);
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

        if (drainOverlay != null)
            drainOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

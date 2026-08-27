using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Reagent Routing (Dijkstra) puzzle overlay.
///
/// Mirrors DrainPuzzleProp exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the Chemistry Lab reagent terminal prop GameObject
///      (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. Set 'reagentRoutingOverlay' to the Reagent Routing Panel in the Canvas
///      (starts inactive).
///   4. The Canvas panel needs: ReagentRoutingUI, EventSystem, GraphicRaycaster.
///
/// NOTE: NotifyReagentRoutingStarted() is called inside ReagentRoutingUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker,
/// same rule as every other puzzle prop in this project).
///
/// CONCEPT TUTORIAL: this calls ConceptTutorials.ShowIfUnseenThenContinue("dijkstra", ...).
/// ConceptTutorials gracefully no-ops (invokes onDone immediately) if no caption
/// text or diagram exists yet for "dijkstra" -- see the setup guide for the
/// optional caption text to add to ConceptTutorials.cs, and note a diagram still
/// needs to be authored separately in ConceptTutorialUI for the tutorial panel
/// to actually display.
/// </summary>
public class ReagentRoutingProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("Reagent Routing Panel GameObject in the Canvas (inactive by default).")]
    public GameObject reagentRoutingOverlay;

    // IInteractable
    public string InteractLabel => "Access Reagent Terminal";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (reagentRoutingOverlay != null)
            reagentRoutingOverlay.SetActive(false);
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
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        ConceptTutorials.ShowIfUnseenThenContinue("dijkstra", OpenReagentRoutingUI);
    }

    private void OpenReagentRoutingUI()
    {
        if (reagentRoutingOverlay != null)
        {
            reagentRoutingOverlay.SetActive(true);
            reagentRoutingOverlay.GetComponent<ReagentRoutingUI>().InitPuzzle(ClosePuzzle);
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

        if (reagentRoutingOverlay != null)
            reagentRoutingOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

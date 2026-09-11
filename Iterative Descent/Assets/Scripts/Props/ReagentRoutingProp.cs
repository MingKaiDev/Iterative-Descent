using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Reagent Routing (Dijkstra) puzzle overlay.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via ReagentRoutingUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the Chemistry Lab reagent terminal prop GameObject
///      (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. 'reagentRoutingOverlay' is optional -- only needed to force a specific same-scene instance.
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
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via ReagentRoutingUI.Instance.")]
    public GameObject reagentRoutingOverlay;

    // IInteractable
    public string InteractLabel => "Access Reagent Terminal";

    private bool _puzzleOpen;

    public void Interact(GameObject interactor)
    {
        if (_puzzleOpen) return;
        OpenPuzzle();
    }

    public void OpenPuzzle()
    {
        _puzzleOpen = true;
        PlayerInteractor.Pause();

        GetComponent<InteractableBase>()?.HidePrompt();

        ConceptTutorials.ShowIfUnseenThenContinue("dijkstra", OpenReagentRoutingUI);
    }

    private ReagentRoutingUI ResolveReagentRoutingUI()
    {
        if (reagentRoutingOverlay != null)
        {
            var local = reagentRoutingOverlay.GetComponent<ReagentRoutingUI>();
            if (local != null) return local;
        }

        if (ReagentRoutingUI.Instance != null) return ReagentRoutingUI.Instance;

        return FindFirstObjectByType<ReagentRoutingUI>(FindObjectsInactive.Include);
    }

    private void OpenReagentRoutingUI()
    {
        var puzzle = ResolveReagentRoutingUI();
        if (puzzle == null)
        {
            Debug.LogError("[ReagentRoutingProp] No ReagentRoutingUI found. Is the Reagent Routing " +
                            "Panel present under the persisted Canvas, and did you enter play mode via Level 1?", this);
            ClosePuzzle();
            return;
        }

        puzzle.gameObject.SetActive(true);
        puzzle.InitPuzzle(ClosePuzzle);

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

        var puzzle = ResolveReagentRoutingUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

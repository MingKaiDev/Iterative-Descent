using UnityEngine;

/// <summary>
/// IInteractable prop that opens the CPU Scheduling (SJF) puzzle overlay.
///
/// Mirrors every other puzzle prop in this project exactly:
///   • Pauses PlayerInteractor
///   • Unlocks cursor
///   • Sets Time.timeScale = 0
///   • Restores all of the above on close
///   • Resolves the overlay at runtime via SchedulingPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// ───────────
///   1. Attach this script to Prop_Whiteboard (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. 'schedulingOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifySchedulingStarted() is called inside SchedulingPuzzleUI.InitPuzzle()
/// — do NOT call it here too (would double-count attempts in PlayerMetricsTracker).
/// </summary>
public class SchedulingPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via SchedulingPuzzleUI.Instance.")]
    public GameObject schedulingOverlay;

    // IInteractable
    public string InteractLabel => "Examine Interface";

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

        // Hide interaction prompt while puzzle is open
        GetComponent<InteractableBase>()?.HidePrompt();

        // First time the player sees this concept, show the tutorial panel
        // before the puzzle overlay opens. See ConceptTutorials.cs.
        ConceptTutorials.ShowIfUnseenThenContinue("cpu_scheduling", OpenSchedulingUI);
    }

    private SchedulingPuzzleUI ResolveSchedulingUI()
    {
        if (schedulingOverlay != null)
        {
            var local = schedulingOverlay.GetComponent<SchedulingPuzzleUI>();
            if (local != null) return local;
        }

        if (SchedulingPuzzleUI.Instance != null) return SchedulingPuzzleUI.Instance;

        return FindFirstObjectByType<SchedulingPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenSchedulingUI()
    {
        var puzzle = ResolveSchedulingUI();
        if (puzzle == null)
        {
            Debug.LogError("[SchedulingPuzzleProp] No SchedulingPuzzleUI found. Is the Scheduling " +
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

        var puzzle = ResolveSchedulingUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

using UnityEngine;

/// <summary>
/// IInteractable prop that opens the BFS/DFS Drain puzzle overlay.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via DrainPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the DrainTerminal prop GameObject (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. 'drainOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifyDrainStarted() is called inside DrainPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class DrainPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via DrainPuzzleUI.Instance.")]
    public GameObject drainOverlay;

    // IInteractable
    public string InteractLabel => "Access Drain Terminal";

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

        // First time the player sees this concept, show the tutorial panel
        // before the puzzle overlay opens. See ConceptTutorials.cs.
        ConceptTutorials.ShowIfUnseenThenContinue("bfs_dfs", OpenDrainUI);
    }

    private DrainPuzzleUI ResolveDrainUI()
    {
        if (drainOverlay != null)
        {
            var local = drainOverlay.GetComponent<DrainPuzzleUI>();
            if (local != null) return local;
        }

        if (DrainPuzzleUI.Instance != null) return DrainPuzzleUI.Instance;

        return FindFirstObjectByType<DrainPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenDrainUI()
    {
        var puzzle = ResolveDrainUI();
        if (puzzle == null)
        {
            Debug.LogError("[DrainPuzzleProp] No DrainPuzzleUI found. Is the Drain Panel present " +
                            "under the persisted Canvas, and did you enter play mode via Level 1?", this);
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

        var puzzle = ResolveDrainUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

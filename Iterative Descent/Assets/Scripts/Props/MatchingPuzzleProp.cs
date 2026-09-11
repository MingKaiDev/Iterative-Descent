using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Matching Puzzle overlay.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via MatchingPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the matching puzzle terminal prop (or a child trigger collider).
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. 'matchingOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifyMatchingStarted() is called inside MatchingPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class MatchingPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via MatchingPuzzleUI.Instance.")]
    public GameObject matchingOverlay;

    // IInteractable
    public string InteractLabel => "Access Network Terminal";

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
        ConceptTutorials.ShowIfUnseenThenContinue("networking_ports", OpenMatchingUI);
    }

    private MatchingPuzzleUI ResolveMatchingUI()
    {
        if (matchingOverlay != null)
        {
            var local = matchingOverlay.GetComponent<MatchingPuzzleUI>();
            if (local != null) return local;
        }

        if (MatchingPuzzleUI.Instance != null) return MatchingPuzzleUI.Instance;

        return FindFirstObjectByType<MatchingPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenMatchingUI()
    {
        var puzzle = ResolveMatchingUI();
        if (puzzle == null)
        {
            Debug.LogError("[MatchingPuzzleProp] No MatchingPuzzleUI found. Is the MatchingPanel " +
                            "present under the persisted Canvas, and did you enter play mode via Level 1?", this);
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

        var puzzle = ResolveMatchingUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

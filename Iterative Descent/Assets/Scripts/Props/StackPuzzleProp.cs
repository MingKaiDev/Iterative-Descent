using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Stack / Memory Corruption puzzle overlay.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via StackPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// ───────────
///   1. Attach this script to the terminal prop GameObject (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. 'stackOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifyStackStarted() is called inside StackPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class StackPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via StackPuzzleUI.Instance.")]
    public GameObject stackOverlay;

    // IInteractable
    public string InteractLabel => "Access PDU Panel";

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

        // First time the player sees this concept, a graphical tutorial panel
        // explains it before the puzzle opens. Every subsequent time (or if no
        // tutorial exists yet for this tag) the puzzle opens immediately, same
        // as before. OpenStackUI is always eventually called either way.
        // Mirrors PuzzleProp.OpenPuzzle -- see ConceptTutorials.cs.
        ConceptTutorials.ShowIfUnseenThenContinue("stacks_and_queues", OpenStackUI);
    }

    private StackPuzzleUI ResolveStackUI()
    {
        if (stackOverlay != null)
        {
            var local = stackOverlay.GetComponent<StackPuzzleUI>();
            if (local != null) return local;
        }

        if (StackPuzzleUI.Instance != null) return StackPuzzleUI.Instance;

        return FindFirstObjectByType<StackPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenStackUI()
    {
        var puzzle = ResolveStackUI();
        if (puzzle == null)
        {
            Debug.LogError("[StackPuzzleProp] No StackPuzzleUI found. Is the Stack Panel present " +
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

        var puzzle = ResolveStackUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

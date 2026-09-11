using UnityEngine;

/// <summary>
/// IInteractable wrapper for the BST "Build the Catalog" puzzle.
///
/// The puzzle's UI panel (BstPuzzleUI) lives as a child of the ONE persisted
/// Canvas -- authored in Level 1's scene file, carried into Level 2 (and
/// back) via PersistentUIRoot/DontDestroyOnLoad -- while this prop is placed
/// in Level 2's scene (the library). Level 1's and Level 2's scene files are
/// never open together in the normal one-scene-at-a-time editing workflow, so
/// a serialized Inspector reference from this prop straight to that panel
/// can't be dragged in (there's nothing in the open scene to drag from) and
/// would show as Missing even if forced. Instead the panel is resolved at
/// runtime via BstPuzzleUI.Instance -- the same pattern LevelTransitionDoorProp
/// already uses for LockedDoorHUD.Instance -- so no cross-scene reference is
/// needed at all.
///
/// Otherwise mirrors HashTablePuzzleProp: pauses the player, hands off to the
/// puzzle UI's InitPuzzle(), and registers as an ICloseable so ESC works.
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class BstPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- the panel lives on the persisted Canvas in " +
             "a different scene and is resolved via BstPuzzleUI.Instance instead. Only " +
             "assign this if the Bst Puzzle Panel happens to be placed in THIS SAME scene " +
             "(e.g. a future level built as one single scene with no cross-scene split).")]
    public GameObject bstOverlay;

    public string InteractLabel => "Sort the Returns Cart";

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

        // PlayerInteractor.Pause() only hides the shared cycle-hint panel --
        // this prop's own "[E] ..." prompt is separate and has to be hidden here,
        // same as every other puzzle prop (see HashTablePuzzleProp.OpenPuzzle).
        // This prop's own InteractableBase.promptPanel field is left unassigned (it
        // resolves the prompt via PromptPanelUI.Instance instead -- see that class's
        // doc comment), so hiding must go through the same fallback-aware
        // InteractableBase.HidePrompt() rather than touching promptPanel directly --
        // touching the null field directly was a no-op, which is exactly what left
        // "[E]  Sort the Returns Cart" showing on top of the puzzle panel.
        GetComponent<InteractableBase>()?.HidePrompt();

        // "trees_bst" already has caption text in ConceptTutorials -- it will
        // show automatically once a diagram child is added to
        // ConceptTutorialUI's prefab for that tag; until then this call
        // safely no-ops straight through to OpenBstUI.
        ConceptTutorials.ShowIfUnseenThenContinue("trees_bst", OpenBstUI);
    }

    /// <summary>
    /// Same-scene override first, otherwise the cross-scene-safe Instance lookup.
    ///
    /// BstPuzzleUI.Instance is only set once its own Awake() has actually run -- and
    /// Unity never runs Awake() on a GameObject that starts INACTIVE in the hierarchy
    /// until something first calls SetActive(true) on it. The Bst Puzzle Panel starts
    /// inactive by design (same as every other puzzle overlay), so on a fresh play
    /// session Instance can still be null here even though the panel plainly exists in
    /// the Hierarchy -- a chicken-and-egg deadlock (we need Instance to know what to
    /// activate, but Instance only appears once something has been activated).
    /// FindFirstObjectByType(..., FindObjectsInactive.Include) sidesteps that: it can
    /// locate the component on an inactive GameObject without needing Awake() to have
    /// fired first, so it works as a one-time fallback the very first time this puzzle
    /// is opened. Once found, BstPuzzleUI.Awake() runs (because OpenBstUI immediately
    /// calls SetActive(true) on it), so every call after that hits BstPuzzleUI.Instance
    /// directly and this fallback is never needed again.
    /// </summary>
    private BstPuzzleUI ResolvePuzzleUI()
    {
        if (bstOverlay != null)
        {
            var local = bstOverlay.GetComponent<BstPuzzleUI>();
            if (local != null) return local;
        }

        if (BstPuzzleUI.Instance != null) return BstPuzzleUI.Instance;

        return FindFirstObjectByType<BstPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenBstUI()
    {
        var puzzle = ResolvePuzzleUI();
        if (puzzle == null)
        {
            Debug.LogError("[BstPuzzleProp] No BstPuzzleUI found. Is the Bst Puzzle Panel " +
                            "present under the persisted Canvas, and did you enter play mode " +
                            "via Level 1 (so the Canvas has actually loaded and persisted " +
                            "forward) rather than pressing Play directly inside Level 2?", this);
            ClosePuzzle();
            return;
        }

        puzzle.gameObject.SetActive(true);
        puzzle.InitPuzzle(ClosePuzzle);
        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    public void Close() => ClosePuzzle();

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        var puzzle = ResolvePuzzleUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}

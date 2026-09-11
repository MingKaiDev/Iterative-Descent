using UnityEngine;

/// <summary>
/// Attach alongside InteractableBase + InteractableRegistrar on the computer screen object.
/// Mirrors PuzzleProp pattern exactly.
///
/// Both overlays (PC Password Panel, Linked List Puzzle Panel) are resolved at runtime via
/// PasswordScreenUI.Instance / LinkedListPuzzleUI.Instance -- same cross-scene-safe pattern
/// as every other puzzle prop in this project (see BstPuzzleProp.ResolvePuzzleUI()). A plain
/// serialized reference goes stale across a Level 1 -> Level 2 -> Level 1 reload, because the
/// freshly-reloaded Canvas (and every panel under it) is immediately destroyed as a duplicate
/// by PersistentUIRoot -- this prop's own fresh copy would still point at that now-destroyed
/// panel.
/// </summary>
public class ComputerScreenProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via PasswordScreenUI.Instance. " +
             "Only assign this if the PC Password Panel happens to be placed in THIS SAME scene.")]
    public GameObject passwordOverlay;

    [Tooltip("Leave empty in the normal case -- resolved at runtime via LinkedListPuzzleUI.Instance. " +
             "Only assign this if the Linked List Puzzle Panel happens to be placed in THIS SAME scene.")]
    public GameObject linkedListOverlay;

    public string InteractLabel => "Use Computer";

    private bool _screenOpen;

    public void Interact(GameObject interactor)
    {
        if (_screenOpen) return;
        OpenScreen();
    }

    private PasswordScreenUI ResolvePasswordUI()
    {
        if (passwordOverlay != null)
        {
            var local = passwordOverlay.GetComponent<PasswordScreenUI>();
            if (local != null) return local;
        }

        if (PasswordScreenUI.Instance != null) return PasswordScreenUI.Instance;

        return FindFirstObjectByType<PasswordScreenUI>(FindObjectsInactive.Include);
    }

    private LinkedListPuzzleUI ResolveLinkedListUI()
    {
        if (linkedListOverlay != null)
        {
            var local = linkedListOverlay.GetComponent<LinkedListPuzzleUI>();
            if (local != null) return local;
        }

        if (LinkedListPuzzleUI.Instance != null) return LinkedListPuzzleUI.Instance;

        return FindFirstObjectByType<LinkedListPuzzleUI>(FindObjectsInactive.Include);
    }

    public void OpenScreen()
    {
        _screenOpen = true;
        PlayerInteractor.Pause();

        GetComponent<InteractableBase>()?.HidePrompt();

        var password = ResolvePasswordUI();
        if (password == null)
        {
            Debug.LogError("[ComputerScreenProp] No PasswordScreenUI found. Is the PC Password " +
                            "Panel present under the persisted Canvas, and did you enter play " +
                            "mode via Level 1 (so the Canvas has actually loaded and persisted " +
                            "forward)?", this);
            CloseScreen();
            return;
        }

        password.gameObject.SetActive(true);
        password.Setup(CloseScreen);

        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// Called by PasswordEventHandler after OnPC1LoggedIn fires.
    /// Swaps the password panel out for the linked list puzzle panel.
    /// </summary>
    public void ShowLinkedListPuzzle()
    {
        var password = ResolvePasswordUI();
        if (password != null) password.gameObject.SetActive(false);

        // First time the player sees this concept, a graphical tutorial panel
        // explains it before the puzzle opens. Every subsequent time (or if no
        // tutorial exists yet for this tag) the puzzle opens immediately, same
        // as before. OpenLinkedListUI is always eventually called either way.
        // Mirrors PuzzleProp.OpenPuzzle -- see ConceptTutorials.cs.
        ConceptTutorials.ShowIfUnseenThenContinue("linked_lists", OpenLinkedListUI);
    }

    private void OpenLinkedListUI()
    {
        var linkedList = ResolveLinkedListUI();
        if (linkedList == null)
        {
            Debug.LogError("[ComputerScreenProp] No LinkedListPuzzleUI found. Is the Linked List " +
                            "Puzzle Panel present under the persisted Canvas, and did you enter " +
                            "play mode via Level 1 (so the Canvas has actually loaded and " +
                            "persisted forward)?", this);
            CloseScreen();
            return;
        }

        linkedList.gameObject.SetActive(true);
        linkedList.InitPuzzle(CloseScreen);

        // Re-assert closeable/cursor/timescale -- ConceptTutorials may have
        // shown a tutorial panel in between, which registers itself as the
        // closeable and does not restore ours on close (see
        // ConceptTutorialUI's pause-ownership note).
        PlayerInteractor.RegisterCloseable(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;
    }

    // ICloseable
    public void Close() => CloseScreen();

    public void CloseScreen()
    {
        _screenOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        var password = ResolvePasswordUI();
        if (password != null) password.gameObject.SetActive(false);

        var linkedList = ResolveLinkedListUI();
        if (linkedList != null) linkedList.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}

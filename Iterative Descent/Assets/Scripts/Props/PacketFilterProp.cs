using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Packet Filter puzzle overlay.
/// Place on the ARBITEX Server Node terminal in the server room.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via PacketFilterPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the server node terminal prop GO.
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. 'packetFilterOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifyPacketFilterStarted() is called inside PacketFilterPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class PacketFilterProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via PacketFilterPuzzleUI.Instance.")]
    public GameObject packetFilterOverlay;

    // IInteractable
    public string InteractLabel => "Access ARBITEX Server Node";

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
        ConceptTutorials.ShowIfUnseenThenContinue("network_security", OpenPacketFilterUI);
    }

    private PacketFilterPuzzleUI ResolvePacketFilterUI()
    {
        if (packetFilterOverlay != null)
        {
            var local = packetFilterOverlay.GetComponent<PacketFilterPuzzleUI>();
            if (local != null) return local;
        }

        if (PacketFilterPuzzleUI.Instance != null) return PacketFilterPuzzleUI.Instance;

        return FindFirstObjectByType<PacketFilterPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenPacketFilterUI()
    {
        var puzzle = ResolvePacketFilterUI();
        if (puzzle == null)
        {
            Debug.LogError("[PacketFilterProp] No PacketFilterPuzzleUI found. Is the PacketFilterPanel " +
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

        var puzzle = ResolvePacketFilterUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

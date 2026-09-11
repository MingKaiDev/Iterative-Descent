using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Subnet Puzzle overlay.
/// Place on the exterior wall panel of the server room door.
///
/// Mirrors every other puzzle prop in this project exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///   - Resolves the overlay at runtime via SubnetPuzzleUI.Instance (cross-scene-safe --
///     see BstPuzzleProp.ResolvePuzzleUI()) instead of relying only on a serialized
///     reference, which goes stale across a Level 1 -> Level 2 -> Level 1 reload.
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the subnet terminal prop GO.
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. 'subnetOverlay' is optional -- only needed to force a specific same-scene instance.
///
/// NOTE: NotifySubnetStarted() is called inside SubnetPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class SubnetPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via SubnetPuzzleUI.Instance.")]
    public GameObject subnetOverlay;

    // IInteractable
    public string InteractLabel => "Access Server Room Panel";

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
        ConceptTutorials.ShowIfUnseenThenContinue("computer_networks", OpenSubnetUI);
    }

    private SubnetPuzzleUI ResolveSubnetUI()
    {
        if (subnetOverlay != null)
        {
            var local = subnetOverlay.GetComponent<SubnetPuzzleUI>();
            if (local != null) return local;
        }

        if (SubnetPuzzleUI.Instance != null) return SubnetPuzzleUI.Instance;

        return FindFirstObjectByType<SubnetPuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenSubnetUI()
    {
        var puzzle = ResolveSubnetUI();
        if (puzzle == null)
        {
            Debug.LogError("[SubnetPuzzleProp] No SubnetPuzzleUI found. Is the SubnetPanel present " +
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

        var puzzle = ResolveSubnetUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

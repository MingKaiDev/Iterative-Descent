using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Subnet Puzzle overlay.
/// Place on the exterior wall panel of the server room door.
///
/// Mirrors MatchingPuzzleProp exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the subnet terminal prop GO.
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. Set 'subnetOverlay' to SubnetPanel in the Canvas (starts inactive).
///   4. The Canvas needs a GraphicRaycaster; scene needs an EventSystem.
///
/// NOTE: NotifySubnetStarted() is called inside SubnetPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class SubnetPuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("SubnetPanel GameObject in the Canvas (inactive by default).")]
    public GameObject subnetOverlay;

    // IInteractable
    public string InteractLabel => "Access Server Room Panel";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (subnetOverlay != null)
            subnetOverlay.SetActive(false);
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
        if (interactBase?.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        if (subnetOverlay != null)
        {
            subnetOverlay.SetActive(true);
            subnetOverlay.GetComponent<SubnetPuzzleUI>().InitPuzzle(ClosePuzzle);
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

        if (subnetOverlay != null)
            subnetOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

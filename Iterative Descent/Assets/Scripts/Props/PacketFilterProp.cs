using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Packet Filter puzzle overlay.
/// Place on the ARBITEX Server Node terminal in the server room.
///
/// Mirrors SubnetPuzzleProp exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// -----------
///   1. Attach this script to the server node terminal prop GO.
///   2. Also attach: BoxCollider (trigger), InteractableBase, InteractableRegistrar.
///   3. Set 'packetFilterOverlay' to PacketFilterPanel in the Canvas (starts inactive).
///   4. The Canvas needs a GraphicRaycaster; scene needs an EventSystem.
///
/// NOTE: NotifyPacketFilterStarted() is called inside PacketFilterPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class PacketFilterProp : MonoBehaviour, IInteractable
{
    [Header("UI")]
    [Tooltip("PacketFilterPanel GameObject in the Canvas (inactive by default).")]
    public GameObject packetFilterOverlay;

    // IInteractable
    public string InteractLabel => "Access ARBITEX Server Node";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (packetFilterOverlay != null)
            packetFilterOverlay.SetActive(false);
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

        if (packetFilterOverlay != null)
        {
            packetFilterOverlay.SetActive(true);
            packetFilterOverlay.GetComponent<PacketFilterPuzzleUI>().InitPuzzle(ClosePuzzle);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.Resume();

        if (packetFilterOverlay != null)
            packetFilterOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

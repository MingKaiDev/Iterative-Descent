using UnityEngine;

/// <summary>
/// IInteractable prop that opens the Stack / Memory Corruption puzzle overlay.
///
/// Mirrors the SchedulingPuzzleProp pattern exactly:
///   - Pauses PlayerInteractor
///   - Unlocks cursor
///   - Sets Time.timeScale = 0
///   - Restores all of the above on close
///
/// SCENE SETUP
/// ───────────
///   1. Attach this script to the terminal prop GameObject (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. Set 'stackOverlay' to the Stack Panel in the Canvas (starts inactive).
///   4. The Canvas Stack Panel needs: StackPuzzleUI, EventSystem, GraphicRaycaster.
///
/// NOTE: NotifyStackStarted() is called inside StackPuzzleUI.InitPuzzle()
/// -- do NOT call it here too (would double-count in PlayerMetricsTracker).
/// </summary>
public class StackPuzzleProp : MonoBehaviour, IInteractable
{
    [Header("UI")]
    [Tooltip("Stack Panel GameObject in the Canvas (inactive by default).")]
    public GameObject stackOverlay;

    // IInteractable
    public string InteractLabel => "Access PDU Panel";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (stackOverlay != null)
            stackOverlay.SetActive(false);
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

        if (stackOverlay != null)
        {
            stackOverlay.SetActive(true);
            stackOverlay.GetComponent<StackPuzzleUI>().InitPuzzle(ClosePuzzle);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.Resume();

        if (stackOverlay != null)
            stackOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

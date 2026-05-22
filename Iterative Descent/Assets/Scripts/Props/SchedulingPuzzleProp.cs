using UnityEngine;

/// <summary>
/// IInteractable prop that opens the CPU Scheduling (SJF) puzzle overlay.
///
/// Mirrors the PuzzleProp / ComputerScreenProp pattern exactly:
///   • Pauses PlayerInteractor
///   • Unlocks cursor
///   • Sets Time.timeScale = 0
///   • Restores all of the above on close
///
/// SCENE SETUP
/// ───────────
///   1. Attach this script to Prop_Whiteboard (or a child trigger collider).
///   2. Also attach: BoxCollider, InteractableBase, InteractableRegistrar.
///   3. Set 'schedulingOverlay' to the Scheduling Panel in the Canvas (starts inactive).
///   4. The Canvas Scheduling Panel needs: SchedulingPuzzleUI, EventSystem, GraphicRaycaster.
///
/// NOTE: NotifySchedulingStarted() is called inside SchedulingPuzzleUI.InitPuzzle()
/// — do NOT call it here too (would double-count attempts in PlayerMetricsTracker).
/// </summary>
public class SchedulingPuzzleProp : MonoBehaviour, IInteractable
{
    [Header("UI")]
    [Tooltip("Scheduling Panel GameObject in the Canvas (inactive by default).")]
    public GameObject schedulingOverlay;

    // IInteractable
    public string InteractLabel => "Examine Whiteboard";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (schedulingOverlay != null)
            schedulingOverlay.SetActive(false);
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

        // Hide interaction prompt while puzzle is open
        var interactBase = GetComponent<InteractableBase>();
        if (interactBase?.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        if (schedulingOverlay != null)
        {
            schedulingOverlay.SetActive(true);
            schedulingOverlay.GetComponent<SchedulingPuzzleUI>().InitPuzzle(ClosePuzzle);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;
    }

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.Resume();

        if (schedulingOverlay != null)
            schedulingOverlay.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

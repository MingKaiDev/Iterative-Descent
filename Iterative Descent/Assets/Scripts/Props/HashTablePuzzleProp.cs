using UnityEngine;

/// <summary>
/// IInteractable wrapper for the hash-table conveyor puzzle. Mirrors
/// StackPuzzleProp exactly: pauses the player, hands off to the puzzle UI's
/// InitPuzzle(), and registers as an ICloseable so ESC works while open.
///
/// This is the script MeetingRoomKeyProp's puzzleInteractScript /
/// puzzleInteractableBase / puzzleInteractableRegistrar fields should point
/// at once this prop is placed in the Kitchen (see MeetingRoomKeyProp.cs).
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class HashTablePuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI")]
    [Tooltip("The HashTablePuzzlePanel instance in the scene Canvas (inactive by default).")]
    public GameObject hashTableOverlay;

    public string InteractLabel => "Sort the Order Line";

    private bool _puzzleOpen;

    private void Awake()
    {
        if (hashTableOverlay != null) hashTableOverlay.SetActive(false);
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

        // PlayerInteractor.Pause() only hides the shared cycle-hint panel --
        // this prop's own "[E] ..." label is a separate promptPanel on its
        // InteractableBase and has to be hidden here, same as every other
        // puzzle prop (see StackPuzzleProp.OpenPuzzle).
        var interactBase = GetComponent<InteractableBase>();
        if (interactBase?.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        ConceptTutorials.ShowIfUnseenThenContinue("hash_tables", OpenHashTableUI);
    }

    private void OpenHashTableUI()
    {
        if (hashTableOverlay == null)
        {
            Debug.LogError("[HashTablePuzzleProp] hashTableOverlay not assigned.", this);
            ClosePuzzle();
            return;
        }

        hashTableOverlay.SetActive(true);
        hashTableOverlay.GetComponent<HashTablePuzzleUI>().InitPuzzle(ClosePuzzle);
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
        if (hashTableOverlay != null) hashTableOverlay.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Time.timeScale = 1f;
    }
}

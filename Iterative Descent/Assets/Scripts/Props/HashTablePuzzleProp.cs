using UnityEngine;

/// <summary>
/// IInteractable wrapper for the hash-table conveyor puzzle. Mirrors every other
/// puzzle prop in this project exactly: pauses the player, hands off to the
/// puzzle UI's InitPuzzle(), and registers as an ICloseable so ESC works while
/// open. Resolves the overlay at runtime via HashTablePuzzleUI.Instance
/// (cross-scene-safe -- see BstPuzzleProp.ResolvePuzzleUI()) instead of relying
/// only on a serialized reference, which goes stale across a Level 1 -> Level 2
/// -> Level 1 reload.
///
/// This is the script MeetingRoomKeyProp's puzzleInteractScript /
/// puzzleInteractableBase / puzzleInteractableRegistrar fields should point
/// at once this prop is placed in the Kitchen (see MeetingRoomKeyProp.cs).
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class HashTablePuzzleProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("UI (optional override)")]
    [Tooltip("Leave empty in the normal case -- resolved at runtime via HashTablePuzzleUI.Instance.")]
    public GameObject hashTableOverlay;

    public string InteractLabel => "Sort the Order Line";

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
        // this prop's own "[E] ..." label has to be hidden here, same as
        // every other puzzle prop.
        GetComponent<InteractableBase>()?.HidePrompt();

        ConceptTutorials.ShowIfUnseenThenContinue("hash_tables", OpenHashTableUI);
    }

    private HashTablePuzzleUI ResolveHashTableUI()
    {
        if (hashTableOverlay != null)
        {
            var local = hashTableOverlay.GetComponent<HashTablePuzzleUI>();
            if (local != null) return local;
        }

        if (HashTablePuzzleUI.Instance != null) return HashTablePuzzleUI.Instance;

        return FindFirstObjectByType<HashTablePuzzleUI>(FindObjectsInactive.Include);
    }

    private void OpenHashTableUI()
    {
        var puzzle = ResolveHashTableUI();
        if (puzzle == null)
        {
            Debug.LogError("[HashTablePuzzleProp] No HashTablePuzzleUI found. Is the HashTablePuzzlePanel " +
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

    public void Close() => ClosePuzzle();

    public void ClosePuzzle()
    {
        _puzzleOpen = false;
        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        var puzzle = ResolveHashTableUI();
        if (puzzle != null) puzzle.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;
    }
}

using UnityEngine;

/// <summary>
/// Listens for LinkedListPuzzleUI.OnLinkedListSolved and unlocks the assigned door.
/// Attach to a persistent GameObject in the scene (same object as PasswordEventHandler).
/// Assign the door reference in the Inspector.
/// </summary>
public class LinkedListEventHandler : MonoBehaviour
{
    [SerializeField] private DoorController door;

    void OnEnable()
    {
        LinkedListPuzzleUI.OnLinkedListSolved += HandleLinkedListSolved;
    }

    void OnDisable()
    {
        LinkedListPuzzleUI.OnLinkedListSolved -= HandleLinkedListSolved;
    }

    void HandleLinkedListSolved(int attempt)
    {
        Debug.Log($"[LinkedListEventHandler] Fired on '{gameObject.name}' (scene: {gameObject.scene.name}) — unlocking door.", gameObject);
        door.Unlock();
    }
}
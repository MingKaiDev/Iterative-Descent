using UnityEngine;

/// <summary>
/// Listens for DrainPuzzleUI.OnDrainSolved and unlocks the Female Toilet door.
///
/// RULES (same as StackEventHandler / LinkedListEventHandler)
/// ----------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances -- the debug log below will expose
///     double-firing if it happens.
///   - Wire 'femaleToiletDoor' in the Inspector.
/// </summary>
public class DrainEventHandler : MonoBehaviour
{
    [Header("Door to unlock on drain puzzle solve")]
    [SerializeField] private DoorController femaleToiletDoor;

    private void OnEnable()  => DrainPuzzleUI.OnDrainSolved += HandleSolved;
    private void OnDisable() => DrainPuzzleUI.OnDrainSolved -= HandleSolved;

    private void HandleSolved(int wrongAttempts)
    {
        Debug.Log($"[DrainEventHandler] '{gameObject.name}' in scene '{gameObject.scene.name}'" +
                  $" -- Drain puzzle solved! Wrong attempts: {wrongAttempts}");

        if (femaleToiletDoor != null)
            femaleToiletDoor.Unlock();
        else
            Debug.LogWarning("[DrainEventHandler] No DoorController assigned in Inspector!");
    }
}

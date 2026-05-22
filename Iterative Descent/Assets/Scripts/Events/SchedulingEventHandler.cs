using UnityEngine;

/// <summary>
/// Listens for SchedulingPuzzleUI.OnSchedulingSolved and unlocks a door.
///
/// Mirror of LinkedListEventHandler — same pattern, same rules:
///   • Attach to a PERSISTENT GameObject in the scene (NOT the prop itself).
///   • Do NOT add duplicate instances — the gameObject.name debug log will
///     expose double-firing if it happens.
///   • Wire 'door' in the Inspector.
/// </summary>
public class SchedulingEventHandler : MonoBehaviour
{
    [Header("Door to unlock on scheduling puzzle solve")]
    [SerializeField] private DoorController door;

    private void OnEnable()  => SchedulingPuzzleUI.OnSchedulingSolved += HandleSolved;
    private void OnDisable() => SchedulingPuzzleUI.OnSchedulingSolved -= HandleSolved;

    private void HandleSolved(int wrongAttempts)
    {
        Debug.Log($"[SchedulingEventHandler] '{gameObject.name}' in scene '{gameObject.scene.name}'" +
                  $" — Scheduling puzzle solved! Wrong attempts: {wrongAttempts}");

        if (door != null)
            door.Unlock();
        else
            Debug.LogWarning("[SchedulingEventHandler] No DoorController assigned in Inspector!");
    }
}

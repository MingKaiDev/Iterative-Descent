using UnityEngine;

/// <summary>
/// Listens for SubnetPuzzleUI.OnSubnetSolved and unlocks the server room door.
///
/// RULES (same as DrainEventHandler / MatchingEventHandler)
/// ---------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. GameManager).
///   - Do NOT add duplicate instances.
///   - Wire 'serverRoomDoor' in the Inspector to the DoorController on the
///     server room door.
/// </summary>
public class SubnetEventHandler : MonoBehaviour
{
    [Header("Door to unlock")]
    [Tooltip("DoorController on the server room door.")]
    [SerializeField] private DoorController serverRoomDoor;

    private bool _solved;

    private void OnEnable()  => SubnetPuzzleUI.OnSubnetSolved += HandleSolved;
    private void OnDisable() => SubnetPuzzleUI.OnSubnetSolved -= HandleSolved;

    private void HandleSolved(int wrongSubmissions)
    {
        if (_solved) return;
        _solved = true;

        Debug.Log($"[SubnetEventHandler] '{gameObject.name}' -- " +
                  $"Subnet puzzle solved! Wrong submissions: {wrongSubmissions}. " +
                  "Unlocking server room door.");

        if (serverRoomDoor != null)
            serverRoomDoor.Unlock();
        else
            Debug.LogWarning("[SubnetEventHandler] No serverRoomDoor assigned -- door not unlocked.");
    }
}

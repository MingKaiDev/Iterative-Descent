using UnityEngine;

/// <summary>
/// Attach this to a trigger collider at each room's entrance.
/// Automatically notifies PlayerMetricsTracker when the player enters or exits.
///
/// Setup per room:
///   1. Create a child GameObject inside your room with a Box Collider (Is Trigger = true).
///   2. Attach this script and set roomID to a unique name (e.g. "Room_01").
///   3. Make sure the Player GameObject has the tag "Player".
/// </summary>
public class RoomTrigger : MonoBehaviour
{
    [Tooltip("Unique identifier for this room. Must match any keys used in your Director reward logic.")]
    [SerializeField] private string roomID = "Room_Unnamed";

    [Tooltip("Tag on the Player GameObject.")]
    [SerializeField] private string playerTag = "Player";
    //private void Start()
    //{
    //    // Check if player spawned inside this trigger
    //    Collider col = GetComponent<Collider>();
    //    Collider[] hits = Physics.OverlapBox(
    //        col.bounds.center,
    //        col.bounds.extents,
    //        transform.rotation
    //    );

    //    foreach (Collider hit in hits)
    //    {
    //        if (hit.CompareTag(playerTag))
    //        {
    //            PlayerMetricsTracker.Instance.EnterRoom(roomID);
    //            break;
    //        }
    //    }
    //}
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (PlayerMetricsTracker.Instance != null)
            PlayerMetricsTracker.Instance.EnterRoom(roomID);
        else
            Debug.LogWarning("[RoomTrigger] PlayerMetricsTracker not found in scene.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Only close if this room is still the active one
        // (prevents stale exits when transitioning directly roomÅ®room)
        if (PlayerMetricsTracker.Instance != null &&
            PlayerMetricsTracker.Instance.CurrentRoomID == roomID)
        {
            PlayerMetricsTracker.Instance.ExitRoom();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

        // Label in scene view
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f, $"Room: {roomID}");
#endif
    }
}
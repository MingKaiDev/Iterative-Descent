// PaintingEventHandler.cs
// Subscribes to PaintingPuzzleManager.OnPaintingSolved and unlocks the
// Hallway 2 exit door.
//
// SETUP:
//   Attach to any persistent GO in the scene.
//   Assign the Hallway 2 exit DoorController in the Inspector.

using UnityEngine;

public class PaintingEventHandler : MonoBehaviour
{
    [Tooltip("The DoorController that gates Hallway 2 exit.")]
    public DoorController exitDoor;

    private void OnEnable()
    {
        PaintingPuzzleManager.OnPaintingSolved += HandlePaintingSolved;
    }

    private void OnDisable()
    {
        PaintingPuzzleManager.OnPaintingSolved -= HandlePaintingSolved;
    }

    private void HandlePaintingSolved(int wrongAttempts)
    {
        Debug.Log($"[PaintingEventHandler] Painting puzzle solved (wrong attempts: {wrongAttempts}). Unlocking exit.");
        if (exitDoor != null)
            exitDoor.Unlock();
        else
            Debug.LogWarning("[PaintingEventHandler] exitDoor not assigned.");
    }
}

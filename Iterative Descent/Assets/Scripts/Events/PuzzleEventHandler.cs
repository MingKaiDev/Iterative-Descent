// PuzzleEventHandler.cs
using UnityEngine;

public class PuzzleEventHandler : MonoBehaviour
{
    [SerializeField] private DoorController door;

    void OnEnable()
    {
        // ✅ CHANGE 3: Subscribe to events
        PuzzleUI.OnPuzzleFinished += HandlePuzzleFinished;
    }

    void OnDisable()
    {
        // Always unsubscribe to prevent memory leaks
        PuzzleUI.OnPuzzleFinished -= HandlePuzzleFinished;
    }

    void HandlePuzzleFinished(int correct, int total)
    {
        Debug.Log($"Puzzle done: {correct}/{total}");
        door.Unlock();
    }
}
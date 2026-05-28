// StackEventHandler.cs
using UnityEngine;

/// <summary>
/// Listens for StackPuzzleUI.OnStackSolved and opens the assigned shutter.
///
/// RULES (same as LinkedListEventHandler / SchedulingEventHandler)
/// ---------------------------------------------------------------
///   - Attach to a PERSISTENT GameObject in the scene (e.g. the GameManager).
///   - Do NOT add duplicate instances -- the debug log below will expose
///     double-firing if it happens.
///   - Wire 'shutter' in the Inspector.
/// </summary>
public class StackEventHandler : MonoBehaviour
{
    [Header("Shutter to open on stack puzzle solve")]
    [SerializeField] private ShutterController shutter;

    private void OnEnable()  => StackPuzzleUI.OnStackSolved += HandleSolved;
    private void OnDisable() => StackPuzzleUI.OnStackSolved -= HandleSolved;

    private void HandleSolved(int wrongAttempts)
    {
        Debug.Log($"[StackEventHandler] '{gameObject.name}' in scene '{gameObject.scene.name}'" +
                  $" -- Stack puzzle solved! Wrong attempts: {wrongAttempts}");

        if (shutter != null)
            shutter.Unlock();
        else
            Debug.LogWarning("[StackEventHandler] No ShutterController assigned in Inspector!");
    }
}

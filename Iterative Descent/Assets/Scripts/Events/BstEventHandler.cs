using UnityEngine;

/// <summary>
/// Listens for BstPuzzleUI.OnBstSolved and unlocks every assigned door.
/// Attach to a persistent GameObject in the scene (same object as
/// PasswordEventHandler / LinkedListEventHandler) and assign each door in the
/// Inspector.
///
/// Array rather than a single DoorController: the Library Main Hall puzzle gates
/// 4 separate doors at once (per Ming Kai, 2026-09-10) rather than the single door
/// every other puzzle's EventHandler unlocks. Each entry is unlocked independently
/// with its own null check, so a still-unassigned slot logs a warning instead of
/// throwing and skipping every door after it.
/// </summary>
public class BstEventHandler : MonoBehaviour
{
    [SerializeField] private DoorController[] doors;

    void OnEnable()
    {
        BstPuzzleUI.OnBstSolved += HandleBstSolved;
    }

    void OnDisable()
    {
        BstPuzzleUI.OnBstSolved -= HandleBstSolved;
    }

    void HandleBstSolved(int wrongAttempts)
    {
        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning($"[BstEventHandler] '{gameObject.name}' has no doors assigned -- nothing to unlock.", gameObject);
            return;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
            {
                Debug.LogWarning($"[BstEventHandler] '{gameObject.name}' doors[{i}] is unassigned -- skipped.", gameObject);
                continue;
            }

            Debug.Log($"[BstEventHandler] Fired on '{gameObject.name}' (scene: {gameObject.scene.name}) — unlocking '{doors[i].name}'.", gameObject);
            doors[i].Unlock();
        }
    }
}

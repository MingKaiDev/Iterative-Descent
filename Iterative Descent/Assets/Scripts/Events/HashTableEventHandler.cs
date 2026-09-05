using UnityEngine;

/// <summary>
/// Listens for HashTablePuzzleUI.OnHashTableAttemptGraded and unlocks the
/// assigned door -- the SECOND kitchen door, gated behind this puzzle (the
/// first kitchen door is closed/relocked directly by MeetingRoomKeyProp on
/// pickup; see that script's header comment). Mirrors LinkedListEventHandler.
///
/// Gated on the pass/fail grade (>= 50% of attempted orders sorted correctly),
/// not just "the round ended" -- HashTablePuzzleUI.OnHashTableSolved fires for
/// every attempt (used for DDA/BKT telemetry regardless of outcome), but a
/// failed run should NOT unlock the door. The player can simply walk up and
/// interact with the prop again for a fresh (re-randomised) attempt.
///
/// Attach to a persistent GameObject in the scene (same object as
/// PasswordEventHandler / LinkedListEventHandler) and assign the door.
/// </summary>
public class HashTableEventHandler : MonoBehaviour
{
    [SerializeField] private DoorController door;

    void OnEnable()
    {
        HashTablePuzzleUI.OnHashTableAttemptGraded += HandleHashTableGraded;
    }

    void OnDisable()
    {
        HashTablePuzzleUI.OnHashTableAttemptGraded -= HandleHashTableGraded;
    }

    void HandleHashTableGraded(bool passed)
    {
        if (!passed)
        {
            Debug.Log($"[HashTableEventHandler] Fired on '{gameObject.name}' (scene: {gameObject.scene.name}) -- attempt failed (< 50% sorted), door stays locked.", gameObject);
            return;
        }

        Debug.Log($"[HashTableEventHandler] Fired on '{gameObject.name}' (scene: {gameObject.scene.name}) -- attempt passed, unlocking door.", gameObject);
        if (door != null) door.Unlock();
        else Debug.LogWarning("[HashTableEventHandler] No door assigned.", this);
    }
}

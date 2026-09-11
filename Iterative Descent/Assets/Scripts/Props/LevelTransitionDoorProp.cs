// LevelTransitionDoorProp.cs
// IInteractable for a door that loads a new level scene, rather than just opening.
// Mirrors LockedDoorProp's "locked -> flash HUD message" behaviour while still locked,
// but additionally does something once unlocked: interacting (pressing E) while
// door.isLocked is false calls LevelTransitionManager.Instance.TransitionToScene(
// targetScene, targetSpawnPointId) -- walking through the open doorway does nothing on
// its own, the player must actually interact with the door.
//
// Setup per transition door:
//   1. Add InteractableBase + InteractableRegistrar + this script to the door GameObject.
//   2. Assign 'door' to this door's DoorController (same door, so isLocked/Unlock() are
//      shared with whatever key/puzzle unlocks it -- see LevelExitKeyProp.cs).
//   3. Set 'targetScene' to the exact scene name as it appears in File > Build Settings >
//      Scenes In Build (e.g. "Level 2") -- the scene must be added there for
//      SceneManager.LoadScene(string) to find it.
//   4. Leave 'targetSpawnPointId' as "Default" unless the target scene uses a non-default
//      LevelSpawnPoint id (see that script).
//   5. Add LockedDoorHUD to the Canvas (if not already present -- same component every
//      other locked door in the project already uses) and assign its _panel + _label.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class LevelTransitionDoorProp : MonoBehaviour, IInteractable
{
    [Header("Door Reference")]
    [Tooltip("The DoorController on this door. While isLocked is true, interacting just " +
             "flashes the locked-door HUD message below, same as LockedDoorProp. Once " +
             "unlocked, interacting triggers the scene transition instead.")]
    public DoorController door;

    [Header("Transition Target")]
    [Tooltip("Scene name exactly as it appears in File > Build Settings > Scenes In Build.")]
    public string targetScene = "Level 2";
    [Tooltip("Matched against the LevelSpawnPoint.spawnId in the target scene. Leave as " +
             "\"Default\" unless that scene uses a non-default spawn point id.")]
    public string targetSpawnPointId = "Default";

    [Header("HUD Notification (while locked)")]
    [Tooltip("Text flashed on the LockedDoorHUD when the player tries this door while it's still locked.")]
    public string hudMessage = "LOCKED";
    [Tooltip("Seconds the message stays on screen.")]
    public float hudDuration = 2f;

    [Header("Dialogue (while locked, optional)")]
    [Tooltip("Dialogue specific to this door. Plays instead of the default if assigned.")]
    public DialogueSequence lockedDialogue;
    [Tooltip("Fallback dialogue for all doors that have no per-door override.")]
    public DialogueSequence defaultLockedDialogue;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => door != null && door.isLocked ? "Try Door" : "Leave Level";

    public void Interact(GameObject interactor)
    {
        if (door != null && door.isLocked)
        {
            if (LockedDoorHUD.Instance != null)
                LockedDoorHUD.Instance.Show(hudMessage, hudDuration);
            else
                Debug.LogWarning("[LevelTransitionDoorProp] LockedDoorHUD.Instance not found. " +
                                  "Attach LockedDoorHUD to a Canvas GameObject in the scene.", this);

            DialogueSequence seq = lockedDialogue != null ? lockedDialogue : defaultLockedDialogue;
            if (seq != null)
            {
                if (DialogueManager.Instance != null)
                    DialogueManager.Instance.Enqueue(seq);
                else
                    Debug.LogWarning("[LevelTransitionDoorProp] DialogueManager.Instance not found.", this);
            }
            return;
        }

        LevelTransitionManager.Instance.TransitionToScene(targetScene, targetSpawnPointId);
    }
}

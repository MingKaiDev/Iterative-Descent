// LevelExitKeyProp.cs
// IInteractable for the Level 2 exit key (test key, per Ming Kai's placeholder setup).
// On pickup, unlocks exitDoor (DoorController.Unlock() -- same call every other puzzle-
// unlocked door in this project uses, so the door also animates open immediately, same
// as normal). Does NOT trigger the scene transition itself -- that only happens when the
// player separately interacts with the door; see LevelTransitionDoorProp.cs on the door.
//
// Mirrors MeetingRoomKeyProp/KitchenKeyProp's "consumed on pickup" shape.
//
// Setup:
//   1. Add InteractableBase + InteractableRegistrar + this script to the key prop GameObject.
//   2. Assign 'exitDoor' to the DoorController on the Level 2 exit door.
//   3. Optionally assign 'unlockDialogue' to play a line the moment the door unlocks.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class LevelExitKeyPropA : MonoBehaviour, IInteractable
{
    [Header("Door To Unlock")]
    [Tooltip("The DoorController on the Level 2 exit door. Unlocked (and opened, via " +
             "DoorController.Unlock()) the instant this key is picked up.")]
    public DoorController exitDoor;

    [Header("Dialogue (optional)")]
    [Tooltip("Plays via DialogueManager.Interrupt() the instant exitDoor unlocks. Leave empty to skip.")]
    public DialogueSequence unlockDialogue;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "End This";

    private bool _collected;

    public void Interact(GameObject interactor)
    {
        if (_collected) return;
        _collected = true;

        if (exitDoor != null)
        {
            exitDoor.Unlock();

            if (unlockDialogue != null)
            {
                if (DialogueManager.Instance != null)
                    DialogueManager.Instance.Interrupt(unlockDialogue);
                else
                    Debug.LogWarning("[LevelExitKeyProp] DialogueManager.Instance is null -- unlockDialogue will not play.", this);
            }
        }
        else
        {
            Debug.LogWarning("[LevelExitKeyProp] No exitDoor assigned.", this);
        }

        Destroy(gameObject);
    }
}

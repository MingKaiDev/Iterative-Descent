// LockedDoorProp.cs
// IInteractable for locked doors.
//
// Setup per door:
//   1. Add InteractableBase   (handles outline, prompt, radius)
//   2. Add InteractableRegistrar  (auto-registers into PlayerInteractor)
//   3. Add this script
//   4. Optionally wire up the DoorController on this door.
//   5. Optionally assign a per-door lockedDialogue; otherwise defaultLockedDialogue fires.
//
// Scene-wide:
//   - Add LockedDoorHUD to the Canvas and assign its _panel + _label.
//   - Assign defaultLockedDialogue on one door and leave it empty on others,
//     OR create a shared DialogueSequence asset and assign it to every door's
//     defaultLockedDialogue field via the prefab.
using UnityEngine;

/// <summary>
/// When the player interacts with a locked door:
///   1. Flashes hudMessage on the LockedDoorHUD for hudDuration seconds.
///   2. Enqueues a dialogue line (per-door override takes priority over default fallback).
/// If a DoorController is referenced and its isLocked is false, interaction is silently ignored
/// (the door is already open or being opened by a puzzle event).
/// </summary>
[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class LockedDoorProp : MonoBehaviour, IInteractable
{
    [Header("Door Reference (optional)")]
    [Tooltip("The DoorController on this door. " +
             "If assigned, interaction does nothing once the door is unlocked. " +
             "Leave empty for doors that are always locked (no puzzle unlock).")]
    public DoorController door;

    [Header("HUD Notification")]
    [Tooltip("Text flashed on the LockedDoorHUD when the player tries this door.")]
    public string hudMessage = "LOCKED";

    [Tooltip("Seconds the message stays on screen.")]
    public float hudDuration = 2f;

    [Header("Dialogue")]
    [Tooltip("Dialogue specific to this door. Plays instead of the default if assigned.")]
    public DialogueSequence lockedDialogue;

    [Tooltip("Fallback dialogue for all doors that have no per-door override. " +
             "Assign a shared DialogueSequence asset here (e.g. Dialogue_DoorLocked_Default).")]
    public DialogueSequence defaultLockedDialogue;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (door != null)
            door.OnUnlocked += HandleDoorUnlocked;
    }

    void OnDisable()
    {
        if (door != null)
            door.OnUnlocked -= HandleDoorUnlocked;
    }

    void HandleDoorUnlocked()
    {
        this.enabled = false;
        var ib = GetComponent<InteractableBase>();
        if (ib != null) ib.enabled = false;
        var ir = GetComponent<InteractableRegistrar>();
        if (ir != null) ir.enabled = false;
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "Try Door";

    public void Interact(GameObject interactor)
    {
        // Door has been unlocked by a puzzle event — do nothing.
        if (door != null && !door.isLocked) return;

        // Flash HUD message.
        if (LockedDoorHUD.Instance != null)
        {
            LockedDoorHUD.Instance.Show(hudMessage, hudDuration);
        }
        else
        {
            Debug.LogWarning("[LockedDoorProp] LockedDoorHUD.Instance not found. " +
                             "Attach LockedDoorHUD to a Canvas GameObject in the scene.", this);
        }

        // Fire dialogue — per-door override wins, then fall back to default.
        DialogueSequence seq = lockedDialogue != null ? lockedDialogue : defaultLockedDialogue;
        if (seq != null)
        {
            if (DialogueManager.Instance != null)
                DialogueManager.Instance.Enqueue(seq);
            else
                Debug.LogWarning("[LockedDoorProp] DialogueManager.Instance not found.", this);
        }
    }
}

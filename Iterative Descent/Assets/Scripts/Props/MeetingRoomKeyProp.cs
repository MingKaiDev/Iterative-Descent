// MeetingRoomKeyProp.cs
// IInteractable for the meeting room key prop, located in the Kitchen.
//
// IMPORTANT: this involves TWO SEPARATE kitchen doors, not one door that
// closes then reopens:
//   - doorToClose  -- the door currently standing open. Closed and re-locked
//                     the instant this key is picked up.
//   - the door the puzzle later unlocks is a DIFFERENT door, and this script
//     has no reference to it at all. That Unlock() call belongs on whichever
//     puzzle prop ends up gated below, wired to its own separate
//     DoorController field on that puzzle's own script -- not here.
//
// On pickup:
//   1. Closes and re-locks doorToClose (via DoorController.CloseAndLock() --
//      same call BossEncounterTrigger-style arena gates use to seal once
//      triggered).
//   2. Enables whatever puzzle prop unlocks the OTHER (kitchen) door -- this
//      script doesn't care which puzzle it is (Drain/LinkedList/Matching/
//      PacketFilter/Scheduling/Stack/Subnet/Questionaire/ComputerScreenProp,
//      or a new one). It just flips the standard three components every
//      interactable prop in this project has (InteractableBase,
//      InteractableRegistrar, and the puzzle's own IInteractable script)
//      from disabled to enabled, mirroring how LockedDoorProp disables those
//      same three when a door unlocks, just in reverse.
//   3. Unlocks meetingRoomDoor -- the actual meeting room door (where Jason
//      is held), via DoorController.Unlock(). Fires immediately on pickup,
//      same moment as steps 1-2 above. Unwired by default -- assign in the
//      Inspector.
//   4. Destroys itself (consumed on pickup, same as RiflePickupProp/PickupProp).
//
// Setup:
//   1. Add InteractableBase + InteractableRegistrar + this script to the key prop GameObject.
//   2. Assign 'doorToClose' to the DoorController on the door that should close (the open one).
//   3. On the puzzle prop GameObject (whichever one ends up being built for this
//      room, guarding the OTHER, already-locked kitchen door), leave its
//      InteractableBase + InteractableRegistrar + puzzle script UNCHECKED
//      (disabled) in the Inspector by default, then assign all three here so
//      this script can turn them on. That puzzle prop's own "solved" callback
//      is what calls Unlock() on the other kitchen door -- separate from this script.
//   4. Assign 'meetingRoomDoor' to the DoorController on the actual meeting
//      room door.
//   5. Assign 'jasonAmbushEncounter' / 'jasonDeathDialogue' once the meeting
//      room's ambush trigger exists -- see the Jason Death Reveal header
//      below for details. Both left unwired by default. Their GameObjects
//      (not just the scripts) must start INACTIVE in the scene -- Unity
//      still delivers OnTriggerEnter to a disabled script's collider.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class MeetingRoomKeyProp : MonoBehaviour, IInteractable
{
    [Header("Kitchen Door To Close (the currently-open one)")]
    [Tooltip("The door currently standing open in the kitchen. Closed and re-locked the moment this key is picked up. This is NOT the door the puzzle unlocks -- that's a different door, referenced on the puzzle prop's own script instead.")]
    public DoorController doorToClose;

    [Header("Puzzle Gate (enabled once the key is picked up; guards the OTHER, already-locked kitchen door)")]
    [Tooltip("The puzzle prop's own IInteractable script -- whatever puzzle ends up being built to unlock the other kitchen door (DrainPuzzleProp, SubnetPuzzleProp, ComputerScreenProp, a new one, etc.). Leave disabled in the Inspector by default.")]
    public MonoBehaviour puzzleInteractScript;
    [Tooltip("The puzzle prop's InteractableBase. Leave disabled in the Inspector by default.")]
    public InteractableBase puzzleInteractableBase;
    [Tooltip("The puzzle prop's InteractableRegistrar. Leave disabled in the Inspector by default.")]
    public InteractableRegistrar puzzleInteractableRegistrar;

    [Header("Meeting Room Door (unlocked immediately on pickup)")]
    [Tooltip("The DoorController on the actual meeting room door (where Jason is held). Unlocked via DoorController.Unlock() the instant this key is picked up -- same moment doorToClose closes and the puzzle gate above is enabled. Unwired by default; assign in the Inspector.")]
    public DoorController meetingRoomDoor;

    [Header("Jason Death Reveal (activated once the key is picked up)")]
    [Tooltip("The EncounterTrigger guarding the ambush waiting in the meeting room. IMPORTANT: leave " +
             "its GameObject INACTIVE in the Inspector by default -- uncheck the box next to its name " +
             "at the top of the Inspector, NOT just the component's own enabled tickbox. Unity still " +
             "delivers OnTriggerEnter to a disabled script's collider (physics messages ignore " +
             "MonoBehaviour.enabled, unlike Update/OnEnable/OnDisable), so only an inactive GameObject " +
             "reliably keeps this inert until the key is picked up. Activated via SetActive(true) the " +
             "same instant meetingRoomDoor unlocks.")]
    public EncounterTrigger jasonAmbushEncounter;
    [Tooltip("The DialogueTrigger that plays Jason's death-scream reveal when the player walks into " +
             "the meeting room. Same inactive-GameObject-by-default convention as jasonAmbushEncounter " +
             "above (see its tooltip) -- can live on the same trigger GameObject as the EncounterTrigger, " +
             "or a separate one covering the same doorway, whichever is easier to place in the Editor. " +
             "Assign a DialogueSequence to it (see Assets/Dialogue/Jason/JasonDeath.asset) before this " +
             "does anything.")]
    public DialogueTrigger jasonDeathDialogue;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => "Pick Up Meeting Room Key";

    private bool _collected;

    public void Interact(GameObject interactor)
    {
        if (_collected) return;
        _collected = true;

        if (doorToClose != null)
            doorToClose.CloseAndLock();
        else
            Debug.LogWarning("[MeetingRoomKeyProp] No doorToClose assigned.", this);

        SetPuzzleGateEnabled(true);

        if (meetingRoomDoor != null)
            meetingRoomDoor.Unlock();
        else
            Debug.LogWarning("[MeetingRoomKeyProp] No meetingRoomDoor assigned.", this);

        SetJasonEncounterEnabled(true);

        Destroy(gameObject);
    }

    private void SetPuzzleGateEnabled(bool value)
    {
        if (puzzleInteractScript != null) puzzleInteractScript.enabled = value;
        if (puzzleInteractableBase != null) puzzleInteractableBase.enabled = value;
        if (puzzleInteractableRegistrar != null) puzzleInteractableRegistrar.enabled = value;

        if (value && puzzleInteractScript == null && puzzleInteractableBase == null && puzzleInteractableRegistrar == null)
            Debug.LogWarning("[MeetingRoomKeyProp] No puzzle gate references assigned -- nothing was enabled. " +
                              "Assign the future puzzle prop's components once it exists.", this);
    }

    private void SetJasonEncounterEnabled(bool value)
    {
        // GameObject.SetActive(), not .enabled -- Unity still delivers OnTriggerEnter to a disabled
        // script's collider (physics messages ignore MonoBehaviour.enabled), so toggling .enabled here
        // would not actually keep the ambush/dialogue inert. An inactive GameObject does, reliably.
        if (jasonAmbushEncounter != null) jasonAmbushEncounter.gameObject.SetActive(value);
        if (jasonDeathDialogue != null) jasonDeathDialogue.gameObject.SetActive(value);

        if (value && jasonAmbushEncounter == null && jasonDeathDialogue == null)
            Debug.LogWarning("[MeetingRoomKeyProp] No Jason death-reveal references assigned -- nothing was activated. " +
                              "Assign jasonAmbushEncounter/jasonDeathDialogue once the meeting room ambush trigger exists.", this);
    }
}

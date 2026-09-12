// LibraryInstructionsTerminalProp.cs
// IInteractable for the study hall instructions terminal that kicks off the
// 6-puzzle library sequence (see StudyHallPuzzleSequenceHandler.cs). Interacting
// with it just plays a DialogueSequence explaining the task -- it has no puzzle
// of its own and does not gate anything.
//
// Deliberately not built on DialoguePickupProp: this terminal is never picked
// up or destroyed, and its prompt should read like a terminal ("Read
// Terminal"), not "Pick Up X" -- otherwise the same
// Enqueue-a-DialogueSequence-on-interact shape as that script.
//
// Setup:
//   1. Add InteractableBase + InteractableRegistrar + this script to the terminal prop.
//   2. Assign 'instructions' to a DialogueSequence explaining the 6-terminal sequence --
//      see Assets/Dialogue/Library/StudyHallTerminalInstructions.asset.
//   3. Leave 'replayable' checked (default) so the player can walk back and re-read
//      the instructions at any point mid-sequence. Uncheck it if the terminal should
//      only speak once.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class LibraryInstructionsTerminalProp : MonoBehaviour, IInteractable
{
    [Header("Instructions")]
    [Tooltip("Played via DialogueManager.Enqueue() when this terminal is interacted with " +
             "(subject to 'Replayable' below). See " +
             "Assets/Dialogue/Library/StudyHallTerminalInstructions.asset.")]
    [SerializeField] private DialogueSequence instructions;

    [Tooltip("If true (default), the terminal can be read again any number of times. If false, " +
             "it only plays once -- every later interact is a silent no-op.")]
    [SerializeField] private bool replayable = true;

    public string InteractLabel => "Read Terminal";

    private bool _fired;

    public void Interact(GameObject interactor)
    {
        if (!replayable && _fired) return;
        _fired = true;

        if (instructions == null)
        {
            Debug.LogWarning($"[LibraryInstructionsTerminalProp] '{gameObject.name}' has no instructions DialogueSequence assigned.", this);
            return;
        }

        if (DialogueManager.Instance != null)
            DialogueManager.Instance.Enqueue(instructions);
        else
            Debug.LogWarning("[LibraryInstructionsTerminalProp] DialogueManager.Instance is null -- instructions will not play.", this);
    }
}

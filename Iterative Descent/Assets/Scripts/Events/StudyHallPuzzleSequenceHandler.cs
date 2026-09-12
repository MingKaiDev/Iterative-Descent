// StudyHallPuzzleSequenceHandler.cs
// Gates 6 library puzzle terminals (generic MCQ quizzes -- see PuzzleProp.cs)
// into a strict 1-6 order and unlocks the door(s) into Deimos Hall once the
// 6th is solved. Ming Kai's design (2026-09-11): a study hall terminal
// (LibraryInstructionsTerminalProp) explains the task, then the player walks
// the library solving 6 puzzle terminals IN ORDER -- each later terminal
// stays inert (no outline, no prompt, not interactable) until the one before
// it is solved.
//
// Mirrors two existing patterns in this project rather than inventing a new one:
//   - MeetingRoomKeyProp's puzzle-gate enable (InteractableBase +
//     InteractableRegistrar + the puzzle's own IInteractable script, all left
//     disabled in the Inspector by default and flipped on together).
//   - BstEventHandler's DoorController[] array (a single puzzle solve can gate
//     more than one door at once).
//
// Setup:
//   1. Attach to a persistent GameObject in the scene (same object as every
//      other *EventHandler in this project, e.g. the GameManager).
//   2. Place 6 PuzzleProp terminals around the library. Leave ALL SIX --
//      including Terminal 1 -- enabled in the Inspector (InteractableBase +
//      InteractableRegistrar + PuzzleProp all checked, same as any normal
//      prop). Do NOT hand-uncheck Terminals 2-6: this handler forcibly
//      disables every gated terminal itself in Awake() (see below), because
//      leaving them unchecked in the scene file directly was unreliable in
//      practice (Ming Kai, 2026-09-11 -- the disabled state didn't stick).
//      Owning the initial state in code instead of in scene-authored
//      m_Enabled flags means it can't drift out of sync again.
//   3. On EACH of the 6 PuzzleProp terminals, wire its "On Completed"
//      UnityEvent (Inspector, bottom of the PuzzleProp component) to call
//      StudyHallPuzzleSequenceHandler.NotifyPuzzleSolved(int), typing that
//      terminal's own fixed 1-based position (1 for the first terminal, 2 for
//      the second, ... 6 for the last) as the listener's static argument.
//      onCompleted only fires once the player answers every question in that
//      session correctly -- see PuzzleProp.ClosePuzzle()/OnCloseClicked().
//   4. Assign gates[0] (unlocks Terminal 2) through gates[4] (unlocks
//      Terminal 6) to the matching terminal's own InteractableBase /
//      InteractableRegistrar / PuzzleProp. Terminal 1 needs no gate entry --
//      this handler never touches it, so it simply stays enabled from scene
//      load.
//   5. Assign 'doors' to whatever DoorController(s) lead into Deimos Hall.
using UnityEngine;

public class StudyHallPuzzleSequenceHandler : MonoBehaviour
{
    [System.Serializable]
    public class PuzzleGate
    {
        [Tooltip("The gated terminal's own PuzzleProp component (or whichever IInteractable script it uses). Leave disabled in the Inspector by default.")]
        public MonoBehaviour puzzleInteractScript;
        [Tooltip("The gated terminal's InteractableBase. Leave disabled in the Inspector by default.")]
        public InteractableBase puzzleInteractableBase;
        [Tooltip("The gated terminal's InteractableRegistrar. Leave disabled in the Inspector by default.")]
        public InteractableRegistrar puzzleInteractableRegistrar;
    }

    [Header("Puzzle Gates (index 0 unlocks Terminal 2 ... index 4 unlocks Terminal 6)")]
    [Tooltip("Terminal 1 needs no entry here -- it starts enabled in the scene already.")]
    [SerializeField] private PuzzleGate[] gates = new PuzzleGate[5];

    [Header("Doors unlocked once Terminal 6 is solved (path into Deimos Hall)")]
    [SerializeField] private DoorController[] doors;

    // 1-based -- the next terminal number this handler is still waiting on.
    private int _nextExpected = 1;

    /// <summary>
    /// Force Terminals 2-6 disabled the instant this handler loads, regardless
    /// of whatever m_Enabled state got saved in the scene file for them.
    /// Ming Kai reported (2026-09-11) that hand-unchecking InteractableBase /
    /// InteractableRegistrar / PuzzleProp on Terminals 2-6 in the Inspector
    /// didn't reliably stay disabled -- so this handler no longer trusts that
    /// authored state at all and instead re-asserts "off" itself every time,
    /// the same way EnemyBase/EncounterTrigger own their own dormant-state
    /// resets elsewhere in this project rather than depending on the scene
    /// file being right. Safe to call on an already-disabled gate (SetGateEnabled
    /// no-ops cleanly either way). Terminal 1 is never in 'gates' and is left
    /// completely untouched -- it starts enabled like any normal prop.
    /// </summary>
    void Awake()
    {
        if (gates == null) return;

        foreach (var gate in gates)
            SetGateEnabled(gate, false);
    }

    /// <summary>
    /// Wire each of the 6 PuzzleProp terminals' "On Completed" UnityEvent to
    /// this, with 'puzzleNumber' set to that terminal's fixed 1-6 position in
    /// the Inspector (a static UnityEvent argument, not something read at
    /// runtime). A call for anything other than the terminal currently
    /// expected is ignored -- covers the player re-entering an
    /// already-solved terminal (PuzzleProp is freely re-enterable, same as
    /// every other puzzle in this project) firing onCompleted again, and
    /// guards against a wiring mistake skipping a step.
    /// </summary>
    public void NotifyPuzzleSolved(int puzzleNumber)
    {
        if (puzzleNumber != _nextExpected)
        {
            Debug.Log($"[StudyHallPuzzleSequenceHandler] Terminal {puzzleNumber} solved but " +
                      $"Terminal {_nextExpected} is next -- ignored (already solved, or out of order).", gameObject);
            return;
        }

        Debug.Log($"[StudyHallPuzzleSequenceHandler] Terminal {puzzleNumber} solved on '{gameObject.name}' " +
                  $"(scene: {gameObject.scene.name}).", gameObject);

        _nextExpected++;

        int gateIndex = puzzleNumber - 1; // gates[0] unlocks Terminal 2, ..., gates[4] unlocks Terminal 6
        if (gateIndex >= 0 && gateIndex < gates.Length)
            SetGateEnabled(gates[gateIndex], true);

        if (puzzleNumber >= 6)
            UnlockDoors();
    }

    private void SetGateEnabled(PuzzleGate gate, bool value)
    {
        if (gate == null) return;

        if (gate.puzzleInteractScript != null) gate.puzzleInteractScript.enabled = value;
        if (gate.puzzleInteractableBase != null) gate.puzzleInteractableBase.enabled = value;
        if (gate.puzzleInteractableRegistrar != null) gate.puzzleInteractableRegistrar.enabled = value;

        if (value && gate.puzzleInteractScript == null && gate.puzzleInteractableBase == null && gate.puzzleInteractableRegistrar == null)
            Debug.LogWarning("[StudyHallPuzzleSequenceHandler] A puzzle gate has no references assigned -- nothing was enabled.", gameObject);
    }

    private void UnlockDoors()
    {
        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning($"[StudyHallPuzzleSequenceHandler] '{gameObject.name}' has no doors assigned -- nothing to unlock.", gameObject);
            return;
        }

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] == null)
            {
                Debug.LogWarning($"[StudyHallPuzzleSequenceHandler] doors[{i}] is unassigned -- skipped.", gameObject);
                continue;
            }

            Debug.Log($"[StudyHallPuzzleSequenceHandler] All 6 terminals solved -- unlocking '{doors[i].name}'.", gameObject);
            doors[i].Unlock();
        }
    }
}

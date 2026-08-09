// PadlockCodeHintHandler.cs
// Bridges a readable note (the trophy-case padlock code hint) to PadlockProp.
// When the assigned note is closed for the first time, marks the padlock's
// code as revealed so NumberLockUI displays it on the next open.
//
// SETUP:
//   Attach to any persistent GO in the scene (or alongside the note itself).
//   Assign codeNote  -- the NoteProp that contains the padlock code.
//   Assign padlock   -- the PadlockProp this code applies to.
using UnityEngine;

public class PadlockCodeHintHandler : MonoBehaviour
{
    [Tooltip("The note that reveals the padlock code when read.")]
    [SerializeField] private NoteProp codeNote;

    [Tooltip("The padlock this note's code applies to.")]
    [SerializeField] private PadlockProp padlock;

    private void OnEnable()
    {
        if (codeNote != null) codeNote.OnClosed += HandleNoteClosed;
    }

    private void OnDisable()
    {
        if (codeNote != null) codeNote.OnClosed -= HandleNoteClosed;
    }

    private void HandleNoteClosed()
    {
        if (padlock != null) padlock.RevealCode();
    }
}

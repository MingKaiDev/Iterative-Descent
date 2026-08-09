using UnityEngine;

// DialogueChoice and DialogueEntry moved to their own files (DialogueChoice.cs,
// DialogueEntry.cs) 2026-08-09. Having 3 classes in one file with no filename
// match caused Unity to assign MonoScript fileID 11500000 to DialogueChoice
// (the first-declared class) instead of DialogueSequence, which broke every
// hand-authored .asset file referencing this guid as DialogueSequence -- see
// feedback-unity-gotchas.md #17. This file now contains only DialogueSequence,
// so fileID 11500000 for this guid is unambiguous again.

// ─── DialogueSequence ────────────────────────────────────────────────────────

/// <summary>
/// ScriptableObject asset holding an ordered list of dialogue entries.
/// Create via: Assets > Create > ARBITEX > Dialogue Sequence
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "ARBITEX/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("All lines in this conversation, played top to bottom.")]
    public DialogueEntry[] entries;
}

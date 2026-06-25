// NoteData.cs
// ScriptableObject that holds the content of a readable note.
//
// Create via: Assets > Create > ARBITEX > Note Data
//
// Note types:
//   Lore     -- ambient story / environmental storytelling
//   Tutorial -- recap of a CS topic, puzzle prep
//   Code     -- unlockable code / trophy easter egg
using UnityEngine;

[CreateAssetMenu(fileName = "Note_New", menuName = "ARBITEX/Note Data")]
public class NoteData : ScriptableObject
{
    [Header("Content")]
    [Tooltip("Displayed as the note's header/title.")]
    public string title = "Untitled";

    [Tooltip("Optional. Author line shown below the title. Leave blank to hide.")]
    public string author = "";

    [Tooltip("Main body text. Supports multi-line. TMP will word-wrap automatically.")]
    [TextArea(4, 20)]
    public string body = "";

    [Header("Classification")]
    [Tooltip("Used for filtering and potential future tracking.")]
    public NoteType noteType = NoteType.Lore;
}

public enum NoteType
{
    Lore,
    Tutorial,
    Code
}

using UnityEngine;

// ─── DialogueChoice ──────────────────────────────────────────────────────────

/// <summary>
/// A single player-selectable response option.
/// nextSequence: the DialogueSequence to play when this choice is picked.
/// Leave nextSequence null to end the dialogue after the choice.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Text shown on the choice button (keep short, one line).")]
    public string choiceText;

    [Tooltip("Dialogue that plays after this choice is selected. Null = end conversation.")]
    public DialogueSequence nextSequence;
}

// ─── DialogueEntry ───────────────────────────────────────────────────────────

/// <summary>
/// One line of dialogue. If choices is non-empty the player must pick a response
/// before the sequence continues; otherwise it auto-advances after displayDuration.
/// </summary>
[System.Serializable]
public class DialogueEntry
{
    [Tooltip("Name shown on the speaker label. Use ALL-CAPS for ARBITEX, Title Case for others.")]
    public string speakerName = "ARBITEX";

    [TextArea(2, 5)]
    [Tooltip("The dialogue line. Avoid dashes, em-dashes, or Unicode symbols.")]
    public string text;

    [Tooltip("How long the line stays on screen after the typewriter finishes. " +
             "Set to 0 to auto-calculate from text length (roughly 200 WPM reading speed, min 2s).")]
    public float displayDuration = 0f;

    [Tooltip("Player response options. Leave empty for auto-advancing narration.")]
    public DialogueChoice[] choices;

    [Tooltip("Optional voice acting clip for this line. Plays from DialogueManager's AudioSource when the line starts. " +
             "Leave null for silent lines.")]
    public AudioClip voiceClip;

    /// <summary>
    /// Returns displayDuration, or a value calculated from text length when displayDuration is 0.
    /// Minimum 2 seconds; roughly 200 WPM = 16.67 chars/second.
    /// </summary>
    public float GetDuration()
    {
        if (displayDuration > 0f) return displayDuration;
        float calculated = (text != null ? text.Length : 0) / 16.67f;
        return Mathf.Max(2f, calculated);
    }

    public bool HasChoices => choices != null && choices.Length > 0;
}

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

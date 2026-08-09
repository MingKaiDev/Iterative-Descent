using UnityEngine;

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

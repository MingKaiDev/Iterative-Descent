using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the subtitle panel on the Canvas.
/// Called exclusively by DialogueManager -- do not call these methods directly.
///
/// Canvas hierarchy required:
///   Canvas
///   └── DialogueUI                 (GameObject, attach THIS script here -- must stay active)
///       └── SubtitlePanel          (assign to _subtitlePanel; toggled active/inactive at runtime)
///           ├── SpeakerLabel       (TextMeshProUGUI)
///           ├── DialogueText       (TextMeshProUGUI)
///           └── ChoicePanel        (assign to _choicePanel; hidden by default)
///               ├── ChoiceButton_0 (Button + TextMeshProUGUI child)
///               ├── ChoiceButton_1 (Button + TextMeshProUGUI child)
///               └── ChoiceButton_2 (Button + TextMeshProUGUI child)
///
/// IMPORTANT: Attach SubtitleUI to DialogueUI, NOT to SubtitlePanel.
///            Awake calls SetActive(false) on _subtitlePanel (a child), which is safe.
///            Calling SetActive(false) on your own GameObject in Awake would break init.
/// </summary>
public class SubtitleUI : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────────────────

    [Header("Panel")]
    [Tooltip("Root panel GameObject. Toggled active/inactive to show or hide all subtitle UI.")]
    [SerializeField] private GameObject _subtitlePanel;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI _speakerLabel;
    [SerializeField] private TextMeshProUGUI _dialogueText;

    [Header("Choices")]
    [Tooltip("Parent GameObject that holds all choice buttons. Hidden when not in use.")]
    [SerializeField] private GameObject _choicePanel;

    [Tooltip("Exactly 3 choice buttons. Match order to ChoiceButton_0/1/2 in hierarchy.")]
    [SerializeField] private Button[] _choiceButtons;

    [Header("Speaker Colours")]
    [Tooltip("Fallback colour for any speaker not listed below.")]
    [SerializeField] private Color _defaultSpeakerColour = Color.white;

    [SerializeField] private SpeakerColourEntry[] _speakerColours = new SpeakerColourEntry[]
    {
        new SpeakerColourEntry { speakerName = "ARBITEX", colour = new Color(0.9f, 0.15f, 0.15f) },
        new SpeakerColourEntry { speakerName = "PLAYER",  colour = Color.white },
        new SpeakerColourEntry { speakerName = "NARRATOR", colour = new Color(0.7f, 0.7f, 0.7f) },
    };

    // ─── Private ─────────────────────────────────────────────────────────────────

    private Dictionary<string, Color> _colourMap;
    private string _fullText;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        BuildColourMap();

        // Panel starts hidden.
        if (_subtitlePanel != null) _subtitlePanel.SetActive(false);
        if (_choicePanel   != null) _choicePanel.SetActive(false);

        WireChoiceButtons();
    }

    // ─── Public API (called by DialogueManager) ───────────────────────────────

    /// <summary>
    /// Show the panel and set speaker label. DialogueText starts empty; call RevealChars or RevealFull next.
    /// </summary>
    public void BeginLine(string speaker, string text)
    {
        _fullText = text ?? string.Empty;

        if (_subtitlePanel != null) _subtitlePanel.SetActive(true);
        if (_choicePanel   != null) _choicePanel.SetActive(false);

        if (_speakerLabel != null)
        {
            _speakerLabel.text  = speaker ?? string.Empty;
            _speakerLabel.color = GetSpeakerColour(speaker);
        }

        if (_dialogueText != null)
            _dialogueText.text = string.Empty;
    }

    /// <summary>Reveal the first n characters of the current line (typewriter step).</summary>
    public void RevealChars(int count)
    {
        if (_dialogueText == null || _fullText == null) return;
        int clamped = Mathf.Clamp(count, 0, _fullText.Length);
        _dialogueText.text = _fullText.Substring(0, clamped);
    }

    /// <summary>Snap the dialogue text to its full content instantly (skip).</summary>
    public void RevealFull()
    {
        if (_dialogueText != null)
            _dialogueText.text = _fullText ?? string.Empty;
    }

    /// <summary>Hide the dialogue text line (but leave panel visible if choices follow).</summary>
    public void HideLine()
    {
        if (_subtitlePanel != null) _subtitlePanel.SetActive(false);
    }

    /// <summary>Show choice buttons for the given choices array (max 3).</summary>
    public void ShowChoices(DialogueChoice[] choices)
    {
        if (_choicePanel == null || _choiceButtons == null) return;

        // Hide all buttons first, then enable only the ones we need.
        for (int i = 0; i < _choiceButtons.Length; i++)
        {
            if (_choiceButtons[i] == null) continue;
            _choiceButtons[i].gameObject.SetActive(false);
        }

        int count = Mathf.Min(choices.Length, _choiceButtons.Length);
        for (int i = 0; i < count; i++)
        {
            if (_choiceButtons[i] == null) continue;

            _choiceButtons[i].gameObject.SetActive(true);

            // Label: "[1] choice text", "[2] ...", "[3] ..."
            var label = _choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"[{i + 1}]  {choices[i].choiceText}";
        }

        _choicePanel.SetActive(true);
    }

    /// <summary>Hide choice buttons.</summary>
    public void HideChoices()
    {
        if (_choicePanel != null) _choicePanel.SetActive(false);
    }

    // ─── Internals ───────────────────────────────────────────────────────────────

    void BuildColourMap()
    {
        _colourMap = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);
        if (_speakerColours == null) return;
        foreach (var entry in _speakerColours)
        {
            if (!string.IsNullOrEmpty(entry.speakerName))
                _colourMap[entry.speakerName] = entry.colour;
        }
    }

    Color GetSpeakerColour(string speaker)
    {
        if (speaker != null && _colourMap.TryGetValue(speaker, out Color c)) return c;
        return _defaultSpeakerColour;
    }

    void WireChoiceButtons()
    {
        if (_choiceButtons == null) return;
        for (int i = 0; i < _choiceButtons.Length; i++)
        {
            if (_choiceButtons[i] == null) continue;
            int captured = i; // capture loop variable for lambda
            _choiceButtons[i].onClick.AddListener(() =>
            {
                DialogueManager.Instance?.SelectChoice(captured);
            });
        }
    }

    // ─── Serializable helper ─────────────────────────────────────────────────────

    [System.Serializable]
    public class SpeakerColourEntry
    {
        public string speakerName;
        public Color  colour;
    }
}

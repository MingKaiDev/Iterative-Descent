// NoteUI.cs
// Singleton overlay for displaying readable notes.
//
// Setup:
//   1. Attach to the NotePanel GameObject (child of Canvas).
//   2. Assign _titleText, _authorText, _authorRow, _bodyText in the Inspector.
//   3. Wrap _bodyText in a ScrollRect for long notes (see setup guide).
//
// The panel starts inactive. NoteProp.OpenNote() calls Show(); E key or ESC closes it.
// Time is paused while the note is open (handled by NoteProp -- do NOT touch timeScale here).
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Run before any prop script so Instance is set before NoteProp.Awake() deactivates the panel.
[DefaultExecutionOrder(-100)]
public class NoteUI : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────────
    public static NoteUI Instance { get; private set; }

    // ── Inspector refs ─────────────────────────────────────────────────────────
    [Header("Text Fields")]
    [Tooltip("TMP text for the note title.")]
    [SerializeField] private TextMeshProUGUI _titleText;

    [Tooltip("TMP text for the author line.")]
    [SerializeField] private TextMeshProUGUI _authorText;

    [Tooltip("Parent GameObject of the author line. Hidden when author is empty.")]
    [SerializeField] private GameObject _authorRow;

    [Tooltip("TMP text for the note body. Should be inside a ScrollRect for long notes.")]
    [SerializeField] private TextMeshProUGUI _bodyText;

    [Header("Close Hint")]
    [Tooltip("Optional label telling the player how to close. E.g. 'Press E to close'")]
    [SerializeField] private TextMeshProUGUI _closeHintText;

    // ── Runtime ────────────────────────────────────────────────────────────────
    private System.Action _onClose;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NoteUI] Duplicate instance destroyed.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // E key closes the note (same key that opened it).
        // Time.timeScale = 0 so we must use unscaled input -- Input System handles this fine.
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Display the note overlay. onClose is called when the player dismisses it.
    /// </summary>
    public void Show(NoteData data, System.Action onClose)
    {
        _onClose = onClose;

        if (_titleText  != null) _titleText.text  = data.title;
        if (_bodyText   != null) _bodyText.text   = data.body;

        bool hasAuthor = !string.IsNullOrEmpty(data.author);
        if (_authorRow  != null) _authorRow.SetActive(hasAuthor);
        if (_authorText != null) _authorText.text = hasAuthor ? $"-- {data.author}" : "";

        if (_closeHintText != null) _closeHintText.text = "[ E ] Close";

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hide the overlay without triggering the close callback.
    /// Called by NoteProp after it has already handled cleanup.
    /// </summary>
    public void Hide()
    {
        _onClose = null;
        gameObject.SetActive(false);
    }

    // ── Private ────────────────────────────────────────────────────────────────

    void Close()
    {
        var callback = _onClose;
        _onClose = null;
        gameObject.SetActive(false);
        callback?.Invoke();   // NoteProp.CloseNote() handles Pause/cursor/timeScale cleanup
    }
}

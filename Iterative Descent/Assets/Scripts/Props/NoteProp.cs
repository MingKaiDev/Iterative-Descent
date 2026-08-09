// NoteProp.cs
// IInteractable for readable notes scattered through the level.
//
// Setup per note:
//   1. Add InteractableBase   (outline, prompt, radius)
//   2. Add InteractableRegistrar  (auto-registers into PlayerInteractor)
//   3. Add this script
//   4. Assign a NoteData ScriptableObject
//   5. Assign the NoteUI panel GameObject from the Canvas
//
// Notes are read-in-place (no inventory). Player can re-read by default.
// Set canReadAgain = false if the note should only be readable once.
using UnityEngine;

[RequireComponent(typeof(InteractableBase))]
[RequireComponent(typeof(InteractableRegistrar))]
public class NoteProp : MonoBehaviour, IInteractable, ICloseable
{
    [Header("Note Content")]
    [Tooltip("ScriptableObject holding the title, author, and body text.")]
    public NoteData noteData;

    [Header("UI")]
    [Tooltip("Assign the NotePanel GameObject (child of Canvas). NoteUI must be on it.")]
    public GameObject noteOverlay;

    [Header("Behaviour")]
    [Tooltip("If false, the prop disables itself after the first read.")]
    public bool canReadAgain = true;

    [Tooltip("Label shown on the interact prompt.")]
    public string interactLabel = "Read Note";

    /// <summary>Fired every time this note is closed (after being read). Generic hook
    /// for any downstream reaction (e.g. revealing a code elsewhere) -- not specific
    /// to any one use case.</summary>
    public event System.Action OnClosed;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public string InteractLabel => interactLabel;

    public void Interact(GameObject interactor)
    {
        if (_open) return;
        OpenNote();
    }

    // ── ICloseable (ESC via PlayerInteractor) ─────────────────────────────────

    public void Close() => CloseNote();

    // ── Private ───────────────────────────────────────────────────────────────

    private bool _open;

    void Awake()
    {
        if (noteOverlay != null) noteOverlay.SetActive(false);
    }

    void OpenNote()
    {
        if (noteData == null)
        {
            Debug.LogWarning($"[NoteProp] '{gameObject.name}' has no NoteData assigned.", this);
            return;
        }

        if (NoteUI.Instance == null)
        {
            Debug.LogWarning($"[NoteProp] NoteUI.Instance not found. " +
                             "Ensure NoteUI is attached to the noteOverlay GameObject in the Canvas.", this);
            return;
        }

        _open = true;
        PlayerInteractor.Pause();
        PlayerInteractor.RegisterCloseable(this);

        var interactBase = GetComponent<InteractableBase>();
        if (interactBase != null && interactBase.promptPanel != null)
            interactBase.promptPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        Time.timeScale   = 0f;

        NoteUI.Instance.Show(noteData, CloseNote);
    }

    void CloseNote()
    {
        if (!_open) return;
        _open = false;

        PlayerInteractor.DeregisterCloseable();
        PlayerInteractor.Resume();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        Time.timeScale   = 1f;

        if (NoteUI.Instance != null)
            NoteUI.Instance.Hide();

        if (!canReadAgain)
        {
            var ib = GetComponent<InteractableBase>();
            if (ib != null) ib.enabled = false;
            var ir = GetComponent<InteractableRegistrar>();
            if (ir != null) ir.enabled = false;
            this.enabled = false;
        }

        OnClosed?.Invoke();
    }
}

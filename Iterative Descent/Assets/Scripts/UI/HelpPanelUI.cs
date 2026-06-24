// HelpPanelUI.cs
// Controls / tutorial panel opened from the pause menu and the main menu.
//
// SCENE SETUP  (repeat in both game scene and main menu scene)
// ------------------------------------------------------------
//   1. Add a Canvas child panel called "Help Panel" (inactive by default).
//   2. Inside it, add a child Image displaying the controls.png sprite.
//   3. Add a close Button ("X" / "CLOSE") as a child of the panel.
//   4. Attach HelpPanelUI to the "Help Panel" GameObject.
//   5. Assign closeButton in the Inspector.
//
// WIRING
// ------
//   Game scene  : PauseMenuUI.howToPlayButton already wired in PauseMenuUI.Start().
//   Main menu   : Add a "How To Play" Button whose onClick calls HelpPanelUI.Instance.Show().
//
// ESC BEHAVIOUR
// -------------
//   In-game (opened from pause menu):
//     PlayerInteractor detects ESC while paused, calls Close() via ICloseable,
//     and marks EscConsumedThisFrame so PauseMenuUI does not also close.
//   Main menu (no PlayerInteractor instance in scene):
//     This script's own Update() handles ESC directly.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class HelpPanelUI : MonoBehaviour, ICloseable
{
    public static HelpPanelUI Instance { get; private set; }

    [SerializeField] private Button closeButton;

    private bool _isOpen;

    // ── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    void Update()
    {
        // Only handle ESC here when PlayerInteractor is absent (main menu scene).
        // In-game, PlayerInteractor.Update() routes ESC via ICloseable instead,
        // which also sets EscConsumedThisFrame so PauseMenuUI does not react.
        if (!_isOpen) return;
        if (PlayerInteractor.Instance != null) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Hide();
    }

    // ── Public API ────────────────────────────────────────────────

    public void Show()
    {
        if (_isOpen) return;
        _isOpen = true;
        gameObject.SetActive(true);

        // Register so PlayerInteractor routes ESC here while the panel is open.
        // Safe to call even when PlayerInteractor has no scene instance.
        PlayerInteractor.RegisterCloseable(this);
    }

    public void Hide()
    {
        if (!_isOpen) return;
        _isOpen = false;
        PlayerInteractor.DeregisterCloseable();
        gameObject.SetActive(false);
    }

    // ICloseable -- invoked by PlayerInteractor.Update() when ESC is pressed in-game.
    public void Close() => Hide();
}

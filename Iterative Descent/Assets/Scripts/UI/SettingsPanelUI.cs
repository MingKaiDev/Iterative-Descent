// SettingsPanelUI.cs
// Settings panel with a mouse sensitivity slider. Opened from the pause menu
// and the main menu. Mirrors HelpPanelUI's scene-local singleton + ICloseable
// pattern exactly (see HelpPanelUI.cs for the reasoning behind each part).
//
// SCENE SETUP  (repeat in both game scene and main menu scene)
// ------------------------------------------------------------
//   1. Add a Canvas child panel called "Settings Panel" (inactive by default).
//   2. Inside it, add:
//        - A Slider child (sensitivitySlider).
//        - A TMP_Text child showing the current numeric value (valueLabel). Optional.
//        - A close Button ("X" / "BACK") as a child of the panel.
//   3. Attach SettingsPanelUI to the "Settings Panel" GameObject.
//   4. Assign sensitivitySlider, valueLabel (optional), and closeButton in the Inspector.
//   5. Set the Slider's Min Value / Max Value in the Inspector to match
//      SettingsManager.MinMouseSensitivity / MaxMouseSensitivity (0.2 / 5),
//      Whole Numbers off.
//
// WIRING
// ------
//   Game scene  : PauseMenuUI.settingsButton -> SettingsPanelUI.Instance.Show() (wired in code, PauseMenuUI.Start()).
//   Main menu   : MainMenu.OpenOptions() -> SettingsPanelUI.Instance.Show() (wired in code).
//
// LIVE APPLY
// ----------
//   In the gameplay scene, dragging the slider updates the active PlayerMovement's
//   mouseSensitivity immediately (found via FindFirstObjectByType, no reference needed).
//   In the main menu scene there is no PlayerMovement to find, so this is skipped --
//   the value is still saved via SettingsManager and picked up by PlayerMovement.Start()
//   the next time the game scene loads.
//
// ESC BEHAVIOUR
// -------------
//   Same as HelpPanelUI: routed via ICloseable/PlayerInteractor in-game, handled
//   directly in Update() when PlayerInteractor.Instance is null (main menu).

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelUI : MonoBehaviour, ICloseable
{
    public static SettingsPanelUI Instance { get; private set; }

    [SerializeField] private Slider   sensitivitySlider;
    [SerializeField] private TMP_Text valueLabel;   // optional -- shows e.g. "1.5"
    [SerializeField] private Button   closeButton;

    private bool _isOpen;

    // ── Lifecycle ─────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    void Start()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = SettingsManager.MinMouseSensitivity;
            sensitivitySlider.maxValue = SettingsManager.MaxMouseSensitivity;
            sensitivitySlider.onValueChanged.AddListener(HandleSliderChanged);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);
    }

    void Update()
    {
        // Only handle ESC here when PlayerInteractor is absent (main menu scene).
        // In-game, PlayerInteractor.Update() routes ESC via ICloseable instead.
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

        // Reflect the saved value each time the panel opens.
        float current = SettingsManager.MouseSensitivity;
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(current);
        UpdateValueLabel(current);

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

    // ── Slider handling ──────────────────────────────────────────

    private void HandleSliderChanged(float value)
    {
        SettingsManager.MouseSensitivity = value;
        UpdateValueLabel(value);

        // Apply live if a player exists in this scene (gameplay only).
        var playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            playerMovement.mouseSensitivity = value;
    }

    private void UpdateValueLabel(float value)
    {
        if (valueLabel != null)
            valueLabel.text = value.ToString("0.0");
    }
}

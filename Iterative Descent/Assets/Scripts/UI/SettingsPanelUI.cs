// SettingsPanelUI.cs
// Settings panel with a mouse sensitivity slider plus Music/SFX volume sliders.
// Opened from the pause menu and the main menu. Mirrors HelpPanelUI's
// scene-local singleton + ICloseable pattern exactly (see HelpPanelUI.cs for
// the reasoning behind each part).
//
// SCENE SETUP  (repeat in both game scene and main menu scene)
// ------------------------------------------------------------
//   1. Add a Canvas child panel called "Settings Panel" (inactive by default).
//   2. Inside it, add:
//        - A Slider child (sensitivitySlider).
//        - A TMP_Text child showing the current numeric value (valueLabel). Optional.
//        - A Slider child for music (musicSlider) + optional TMP_Text (musicValueLabel).
//        - A Slider child for SFX (sfxSlider) + optional TMP_Text (sfxValueLabel).
//        - A close Button ("X" / "BACK") as a child of the panel.
//   3. Attach SettingsPanelUI to the "Settings Panel" GameObject.
//   4. Assign sensitivitySlider, valueLabel (optional), musicSlider, musicValueLabel
//      (optional), sfxSlider, sfxValueLabel (optional), audioMixer, and closeButton
//      in the Inspector.
//   5. Set the sensitivity Slider's Min Value / Max Value in the Inspector to
//      match SettingsManager.MinMouseSensitivity / MaxMouseSensitivity (0.2 / 5),
//      Whole Numbers off. Music/SFX sliders should be Min 0 / Max 1, Whole Numbers off.
//   6. Assign the project's AudioMixer asset to audioMixer. This does NOT exist
//      yet -- creating it, exposing MusicVolume/SFXVolume parameters, and routing
//      every existing AudioSource to the right group is a manual Editor step.
//      See FYP/setup-guides/VolumeSettings_UnitySetup.md.
//
// WIRING
// ------
//   Game scene  : PauseMenuUI.settingsButton -> SettingsPanelUI.Instance.Show() (wired in code, PauseMenuUI.Start()).
//   Main menu   : MainMenu.OpenOptions() -> SettingsPanelUI.Instance.Show() (wired in code).
//
// LIVE APPLY
// ----------
//   Sensitivity: in the gameplay scene, dragging the slider updates the active
//   PlayerMovement's mouseSensitivity immediately (found via FindFirstObjectByType,
//   no reference needed). In the main menu scene there is no PlayerMovement to find,
//   so this is skipped -- the value is still saved via SettingsManager and picked up
//   by PlayerMovement.Start() the next time the game scene loads.
//
//   Music/SFX: dragging either slider calls AudioMixer.SetFloat directly, so it is
//   always live in both scenes (no FindFirstObjectByType needed -- the mixer is a
//   shared asset, not a scene object). Values are also re-applied to the mixer on
//   Show() in case something else reset them, and once at scene start by
//   GameAudioManager/MainMenuAudioManager (see SettingsManager.ApplyVolumeToMixer)
//   so audio plays at the saved level even if the player never opens this panel.
//
// ESC BEHAVIOUR
// -------------
//   Same as HelpPanelUI: routed via ICloseable/PlayerInteractor in-game, handled
//   directly in Update() when PlayerInteractor.Instance is null (main menu).

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelUI : MonoBehaviour, ICloseable
{
    public static SettingsPanelUI Instance { get; private set; }

    [Header("Sensitivity")]
    [SerializeField] private Slider   sensitivitySlider;
    [SerializeField] private TMP_Text valueLabel;   // optional -- shows e.g. "1.5"

    [Header("Volume")]
    [Tooltip("Project AudioMixer asset. Must exist with exposed float parameters " +
             "named SettingsManager.MixerParamMusicVolume / MixerParamSFXVolume. " +
             "See FYP/setup-guides/VolumeSettings_UnitySetup.md.")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider   musicSlider;
    [SerializeField] private TMP_Text musicValueLabel;   // optional -- shows e.g. "50%"
    [SerializeField] private Slider   sfxSlider;
    [SerializeField] private TMP_Text sfxValueLabel;     // optional -- shows e.g. "100%"

    [Header("Close")]
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
            sensitivitySlider.onValueChanged.AddListener(HandleSensitivitySliderChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener(HandleMusicSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(HandleSFXSliderChanged);
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

        // Reflect the saved values each time the panel opens.
        float currentSensitivity = SettingsManager.MouseSensitivity;
        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(currentSensitivity);
        UpdateValueLabel(currentSensitivity);

        float currentMusic = SettingsManager.MusicVolume;
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(currentMusic);
        UpdateMusicLabel(currentMusic);

        float currentSFX = SettingsManager.SFXVolume;
        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(currentSFX);
        UpdateSFXLabel(currentSFX);

        // In case anything reset the mixer since the last write (e.g. a scene
        // load that ran before this panel's own Awake).
        SettingsManager.ApplyVolumeToMixer(audioMixer);

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

    private void HandleSensitivitySliderChanged(float value)
    {
        SettingsManager.MouseSensitivity = value;
        UpdateValueLabel(value);

        // Apply live if a player exists in this scene (gameplay only).
        var playerMovement = Object.FindFirstObjectByType<PlayerMovement>();
        if (playerMovement != null)
            playerMovement.mouseSensitivity = value;
    }

    private void HandleMusicSliderChanged(float value)
    {
        SettingsManager.MusicVolume = value;
        UpdateMusicLabel(value);

        if (audioMixer != null)
            audioMixer.SetFloat(SettingsManager.MixerParamMusicVolume, SettingsManager.LinearToDecibel(value));
    }

    private void HandleSFXSliderChanged(float value)
    {
        SettingsManager.SFXVolume = value;
        UpdateSFXLabel(value);

        if (audioMixer != null)
            audioMixer.SetFloat(SettingsManager.MixerParamSFXVolume, SettingsManager.LinearToDecibel(value));
    }

    private void UpdateValueLabel(float value)
    {
        if (valueLabel != null)
            valueLabel.text = value.ToString("0.0");
    }

    private void UpdateMusicLabel(float value)
    {
        if (musicValueLabel != null)
            musicValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void UpdateSFXLabel(float value)
    {
        if (sfxValueLabel != null)
            sfxValueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}

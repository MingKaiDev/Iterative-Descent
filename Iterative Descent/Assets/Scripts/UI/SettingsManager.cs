// SettingsManager.cs
// Static PlayerPrefs wrapper for persisted player settings.
//
// Not a MonoBehaviour / no scene object required -- PlayerPrefs is already
// process-wide persistent storage, so a static class is enough to share a
// value between the MainMenu scene and the gameplay scene. This deliberately
// does NOT follow the DontDestroyOnLoad-singleton convention used elsewhere
// (e.g. PlayerMetricsTracker) because there is no per-frame behaviour or
// scene-lifetime state to manage here, just a get/set wrapper.
//
// USAGE
// -----
//   Read  : float s = SettingsManager.MouseSensitivity;
//   Write : SettingsManager.MouseSensitivity = 2.1f;  // clamps + saves automatically
//
// Add future settings (e.g. volume) as additional properties following the
// same pattern.
//
// VOLUME (Music/SFX)
// -------------------
// MusicVolume / SFXVolume are stored as linear 0-1 (what a Slider gives you),
// same as every other setting here. AudioMixer exposed parameters are in
// decibels, not linear, so anything writing to the mixer must go through
// LinearToDecibel() first -- see ApplyVolumeToMixer(). This project has no
// AudioMixer asset yet; see FYP/setup-guides/VolumeSettings_UnitySetup.md for
// how to create one and route existing AudioSources to it. Until that setup
// is done, these properties are saved/read correctly but have no audible
// effect (nothing is listening to the mixer parameters yet).

using UnityEngine;
using UnityEngine.Audio;

public static class SettingsManager
{
    private const string MouseSensitivityKey = "Settings_MouseSensitivity";
    private const string MusicVolumeKey       = "Settings_MusicVolume";
    private const string SFXVolumeKey         = "Settings_SFXVolume";

    // NOTE: Default lowered from the old PlayerMovement Inspector default of 3f.
    // This is a starting guess, not a tuned value -- playtest and adjust.
    public const float DefaultMouseSensitivity = 1.5f;
    public const float MinMouseSensitivity     = 0.2f;
    public const float MaxMouseSensitivity     = 5f;

    // Starting guesses only, not tuned -- adjust after playtesting.
    public const float DefaultMusicVolume = 0.5f;
    public const float DefaultSFXVolume   = 1f;

    // Must match the exposed parameter names on the AudioMixer asset exactly
    // (Inspector > exposed slider > right-click > Rename). See the setup guide.
    public const string MixerParamMusicVolume = "MusicVolume";
    public const string MixerParamSFXVolume   = "SFXVolume";

    // Floor for LinearToDecibel -- linear 0 has no finite dB equivalent
    // (silence = -Infinity), and AudioMixer.SetFloat(-Infinity) is invalid.
    private const float MutedDecibels = -80f;

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity);
        set
        {
            float clamped = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
            PlayerPrefs.SetFloat(MouseSensitivityKey, clamped);
            PlayerPrefs.Save();
        }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        set
        {
            float clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, clamped);
            PlayerPrefs.Save();
        }
    }

    public static float SFXVolume
    {
        get => PlayerPrefs.GetFloat(SFXVolumeKey, DefaultSFXVolume);
        set
        {
            float clamped = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SFXVolumeKey, clamped);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Converts a linear 0-1 slider value to the decibel scale
    /// AudioMixer.SetFloat expects. 0 is floored to MutedDecibels instead of
    /// being passed through Log10 (which would be -Infinity).</summary>
    public static float LinearToDecibel(float linear)
    {
        return linear <= 0.0001f ? MutedDecibels : Mathf.Log10(linear) * 20f;
    }

    /// <summary>Pushes the saved Music/SFX volumes onto an AudioMixer's exposed
    /// parameters. AudioMixer.SetFloat does not persist across scene loads or
    /// game restarts by itself -- PlayerPrefs (via this class) is the source of
    /// truth, the mixer just reflects it at runtime. Call once at scene start
    /// (e.g. GameAudioManager.Awake(), MainMenuAudioManager.Awake()) so audio
    /// plays at the saved volume even if the player never opens Settings.
    /// No-ops if mixer is null so callers can skip the Inspector wiring during
    /// early development without errors.</summary>
    public static void ApplyVolumeToMixer(AudioMixer mixer)
    {
        if (mixer == null) return;
        mixer.SetFloat(MixerParamMusicVolume, LinearToDecibel(MusicVolume));
        mixer.SetFloat(MixerParamSFXVolume,   LinearToDecibel(SFXVolume));
    }
}

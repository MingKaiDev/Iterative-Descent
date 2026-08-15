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

using UnityEngine;

public static class SettingsManager
{
    private const string MouseSensitivityKey = "Settings_MouseSensitivity";

    // NOTE: Default lowered from the old PlayerMovement Inspector default of 3f.
    // This is a starting guess, not a tuned value -- playtest and adjust.
    public const float DefaultMouseSensitivity = 1.5f;
    public const float MinMouseSensitivity     = 0.2f;
    public const float MaxMouseSensitivity     = 5f;

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
}

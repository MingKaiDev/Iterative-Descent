// LockedDoorHUD.cs
// Singleton screen-space HUD that briefly flashes a message (e.g. "LOCKED") on screen.
// Attach this to a Canvas child GameObject.
// Wire up _panel and _label in the Inspector.
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Singleton that shows a temporary text message on screen (e.g. "LOCKED").
/// Called by LockedDoorProp whenever the player tries a locked door.
/// Uses WaitForSecondsRealtime so it works even if timeScale is 0.
/// </summary>
public class LockedDoorHUD : MonoBehaviour
{
    public static LockedDoorHUD Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("The root panel to show/hide (e.g. an Image + TMP_Text child).")]
    [SerializeField] private GameObject _panel;

    [Tooltip("The TMP_Text label inside the panel.")]
    [SerializeField] private TMP_Text _label;

    private Coroutine _hideRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LockedDoorHUD] Duplicate instance found on '" + gameObject.name +
                             "'. Removing extra component. LockedDoorHUD should be on a Canvas child, not on the door.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>
    /// Show a message on screen for the given duration, then hide automatically.
    /// Calling Show() again while already visible resets the timer.
    /// </summary>
    public void Show(string message, float duration)
    {
        if (_panel == null)
        {
            Debug.LogWarning("[LockedDoorHUD] _panel is not assigned.");
            return;
        }

        if (_label != null) _label.text = message;
        _panel.SetActive(true);

        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideAfter(duration));
    }

    IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (_panel != null) _panel.SetActive(false);
        _hideRoutine = null;
    }
}

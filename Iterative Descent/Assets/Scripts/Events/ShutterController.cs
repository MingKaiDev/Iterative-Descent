// ShutterController.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// Slides a metal shutter mesh upward by a configurable distance when Open() is called.
/// Mirrors the coroutine / easing-curve pattern used by DoorController.
///
/// INSPECTOR SETUP
/// ---------------
///   shutterMesh      The child Transform that is the visible shutter panel.
///   slideDistance    How far (metres) the shutter travels upward.  Should match the
///                    mesh height so it clears the opening completely.
///   animDuration     Seconds for the slide animation.
///   easeCurve        Animation easing (defaults to EaseInOut).
///   doorBreakClip    Optional audio clip played on Open().
///
/// USAGE
/// -----
///   Call Open() directly or wire via StackEventHandler.
///   The shutter is locked (will not move) until Unlock() is called.
/// </summary>
public class ShutterController : MonoBehaviour
{
    [Header("Shutter Mesh")]
    [Tooltip("The actual shutter mesh child object to animate.")]
    public Transform shutterMesh;

    [Header("Slide Settings")]
    [Tooltip("Distance in metres the shutter slides upward when opening.")]
    public float slideDistance = 3.5f;

    [Header("Animation")]
    public float animDuration = 1.2f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [Tooltip("Optional clip played when the shutter opens.")]
    public AudioClip openClip;

    [Header("State")]
    public bool isLocked = true;
    public bool isOpen   = false;

    private Vector3    _closedLocalPos;
    private Vector3    _openLocalPos;
    private Coroutine  _currentAnim;
    private AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        if (shutterMesh == null)
        {
            Debug.LogError("[ShutterController] shutterMesh is not assigned!", this);
            return;
        }

        _closedLocalPos = shutterMesh.localPosition;
        _openLocalPos   = _closedLocalPos + Vector3.up * slideDistance;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Unlocks and opens the shutter.  Safe to call multiple times -- subsequent
    /// calls are ignored once the shutter is open.
    /// </summary>
    public void Unlock()
    {
        isLocked = false;
        Open();
    }

    public void Open()
    {
        if (isLocked || isOpen) return;

        if (_audioSource != null && openClip != null)
            _audioSource.PlayOneShot(openClip);

        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimateShutter(_closedLocalPos, _openLocalPos));
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;

        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimateShutter(_openLocalPos, _closedLocalPos));
        isOpen = false;
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    private IEnumerator AnimateShutter(Vector3 from, Vector3 to)
    {
        if (shutterMesh == null) yield break;

        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / animDuration);
            float eased = easeCurve.Evaluate(t);

            shutterMesh.localPosition = Vector3.Lerp(from, to, eased);
            yield return null;
        }

        shutterMesh.localPosition = to;
    }
}

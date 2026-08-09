// DoorController.cs
using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    [Header("Door Mesh")]
    [Tooltip("The actual door mesh child object (Door_West)")]
    public Transform doorMesh;

    [Header("Open Rotation")]
    [Tooltip("Degrees to rotate open on local X. Was hardcoded to 1 before this field existed -- " +
             "defaults to 1 so every existing door in the project keeps its current behaviour untouched.")]
    public float openAngleX = 1f;
    [Tooltip("Degrees to rotate open on local Y. Was hardcoded to 1 before this field existed -- " +
             "defaults to 1 so every existing door in the project keeps its current behaviour untouched.")]
    public float openAngleY = 1f;
    [Tooltip("Degrees to rotate open on local Z. Negative = inward. This is the field every " +
             "existing door already uses -- unchanged, still serializes under the same name.")]
    public float openAngle = -90f;

    [Header("Open Position Offset")]
    [Tooltip("Local position offset added when door is fully open.")]
    public Vector3 openPositionOffset = new Vector3(3f, 6f, 0f);

    [Header("Animation")]
    public float animDuration = 1.0f;
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    [Tooltip("Clip to play when the door is busted open. Assign the AudioSource on this GameObject.")]
    public AudioClip doorBreakClip;

    [Header("State")]
    public bool isLocked = true;
    public bool isOpen = false;

    private Quaternion _closedLocalRot;
    private Quaternion _openLocalRot;
    private Vector3 _closedLocalPos;
    private Vector3 _openLocalPos;
    private Coroutine _currentAnim;
    private BoxCollider _boxCollider;
    private AudioSource _audioSource;

    void Awake()
    {
        _boxCollider = GetComponent<BoxCollider>();
        _audioSource = GetComponent<AudioSource>();

        if (doorMesh == null)
        {
            Debug.LogError("DoorController: doorMesh is not assigned!");
            return;
        }

        // Capture starting closed state
        _closedLocalRot = doorMesh.localRotation;
        _closedLocalPos = doorMesh.localPosition;

        // Derive open state
        _openLocalRot = _closedLocalRot * Quaternion.Euler(openAngleX, openAngleY, openAngle);
        _openLocalPos = _closedLocalPos + openPositionOffset;
    }

    /// <summary>
    /// Fired when the door is unlocked. LockedDoorProp subscribes to disable itself.
    /// </summary>
    public System.Action OnUnlocked;

    /// <summary>
    /// Called by PuzzleEventHandler — unlocks and opens the door.
    /// </summary>
    public void Unlock()
    {
        OnUnlocked?.Invoke();
        isLocked = false;
        Open();
    }

    public void Open()
    {
        if (isLocked || isOpen) return;

        if (_audioSource != null && doorBreakClip != null)
            _audioSource.PlayOneShot(doorBreakClip);

        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimateDoor(
            _closedLocalPos, _openLocalPos,
            _closedLocalRot, _openLocalRot
        ));
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;

        if (_currentAnim != null) StopCoroutine(_currentAnim);
        _currentAnim = StartCoroutine(AnimateDoor(
            _openLocalPos, _closedLocalPos,
            _openLocalRot, _closedLocalRot
        ));
        isOpen = false;
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private IEnumerator AnimateDoor(
        Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot)
    {
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            float eased = easeCurve.Evaluate(t);

            doorMesh.localPosition = Vector3.Lerp(fromPos, toPos, eased);
            doorMesh.localRotation = Quaternion.Lerp(fromRot, toRot, eased);

            yield return null;
        }

        // Snap to final values
        doorMesh.localPosition = toPos;
        doorMesh.localRotation = toRot;

        // Disable the collider once the door is fully open
        if (isOpen && _boxCollider != null)
            _boxCollider.enabled = false;
    }
}
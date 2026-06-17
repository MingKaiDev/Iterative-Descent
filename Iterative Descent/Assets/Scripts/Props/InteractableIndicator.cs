// InteractableIndicator.cs
// Attach to a child GameObject of any interactable prop.
// Shows an inverted-pyramid billboard icon only when the player is:
//   (a) within the interactable's highlight radius, AND
//   (b) roughly facing the object (dot-product gate).
// Fades in/out smoothly and bobs up and down.
// Changes colour when the parent object is the currently-selected interactable.

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class InteractableIndicator : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────
    [Header("Detection")]
    [Tooltip("Independent radius for showing the indicator. Can be larger than the interactable's highlight radius.")]
    [SerializeField] float indicatorRadius = 6f;

    [Header("Facing Gate")]
    [Tooltip("Dot-product threshold. 0.5 = ~60 deg cone, 0.7 = ~46 deg cone.")]
    [SerializeField] float facingThreshold = 0.55f;

    [Header("Animation")]
    [Tooltip("Local-space axis the bob moves along. Set to (0,1,0) for world-Y, (0,0,1) if your object's up is Z, etc.")]
    [SerializeField] Vector3 bobAxis      = Vector3.up;
    [SerializeField] float   bobAmplitude = 0.08f;
    [SerializeField] float   bobSpeed     = 1.8f;
    [SerializeField] float   fadeSpeed    = 6f;

    [Header("Colours")]
    [Tooltip("Tint when in range and facing but not yet selected.")]
    [SerializeField] Color colorIdle     = new Color(1f, 0.93f, 0.70f, 1f); // warm ivory
    [Tooltip("Tint when this object is the active selection (ready to interact).")]
    [SerializeField] Color colorSelected = new Color(1f, 0.85f, 0.10f, 1f); // yellow

    // ── Private ───────────────────────────────────────────────────────────────
    private SpriteRenderer   _sr;
    private InteractableBase _base;
    private Transform        _cam;
    private Vector3          _baseLocalPosition;
    private float            _currentAlpha;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _base = GetComponentInParent<InteractableBase>();

        _currentAlpha = 0f;
        _sr.color = new Color(colorIdle.r, colorIdle.g, colorIdle.b, 0f);
        _baseLocalPosition = transform.localPosition;

        if (_base == null)
            Debug.LogWarning("[InteractableIndicator] No InteractableBase found in parent chain.", this);
    }

    void Start()
    {
        if (Camera.main != null)
            _cam = Camera.main.transform;
        else
            Debug.LogWarning("[InteractableIndicator] No Camera.main found in scene.", this);
    }

    // LateUpdate: ensures InteractableBase.playerInRadius and isSelected have
    // already been set by PlayerInteractor.Update() this frame.
    void LateUpdate()
    {
        if (_cam == null) return;

        Billboard();
        Bob();
        FadeAndColour();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Make the sprite always face the camera.
    void Billboard()
    {
        transform.rotation = _cam.rotation;
    }

    // Sinusoidal float along the configured local axis.
    void Bob()
    {
        Vector3 offset = bobAxis.normalized * (Mathf.Sin(Time.time * bobSpeed) * bobAmplitude);
        transform.localPosition = _baseLocalPosition + offset;
    }

    void FadeAndColour()
    {
        bool shouldShow = ShouldShow();

        float targetAlpha = shouldShow ? 1f : 0f;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Pick tint based on whether this is the currently selected interactable.
        Color tint = (_base != null && _base.isSelected) ? colorSelected : colorIdle;
        tint.a = _currentAlpha;
        _sr.color = tint;
    }

    bool ShouldShow()
    {
        // Never show while a puzzle or menu overlay is open.
        if (PlayerInteractor.IsPaused) return false;

        // Must be within the indicator's own radius (independent of outline radius).
        if (_base == null) return false;
        if (Vector3.Distance(_cam.position, _base.Center) > indicatorRadius) return false;

        // Dot-product facing check: is the camera roughly aimed at this prop?
        if (_cam == null) return false;
        Vector3 toObj = (_base.Center - _cam.position).normalized;
        return Vector3.Dot(_cam.forward, toObj) >= facingThreshold;
    }

    // ── Scene gizmo ───────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (_base == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.DrawWireSphere(_base.Center, indicatorRadius);
    }
}

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic UI arrow that stretches between two RectTransforms.
///
/// How it works:
///   - Pivot is set to (0, 0.5) = left-middle, so the arrow STARTS at anchoredPosition.
///   - Anchor is set to (0.5, 0.5) = centre of parent, so anchoredPosition maps directly
///     to InverseTransformPoint coordinates (origin = parent pivot).
///   - Each LateUpdate, width = distance between source and target;
///     rotation = angle from source to target.
///
/// Prefab Setup:
///   Root: Image (solid white 1×1 sprite or simple arrow sprite) + LLArrow
///   Optional child: a small triangle Image named "ArrowHead" assigned to arrowHead field.
///
/// Inspector Notes:
///   - nodeContainer's pivot MUST be (0.5, 0.5) for coordinate maths to be correct.
///   - Remove any Layout Group from nodeContainer.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class LLArrow : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private float thickness = 4f;
    [SerializeField] private Color shaftColor  = new Color(0.95f, 0.80f, 0.20f, 0.90f);
    [SerializeField] private Color dragColor   = new Color(0.95f, 0.95f, 0.95f, 0.60f);

    /// <summary>Optional arrowhead child (anchored to right-middle of shaft).</summary>
    [SerializeField] private RectTransform arrowHead;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private RectTransform _rect;
    private Image         _image;
    private RectTransform _source;      // where arrow starts (handle RT)
    private RectTransform _target;      // where arrow ends  (null = floating)
    private RectTransform _panelRect;   // nodeContainer — used for coordinate conversion
    private Vector2       _floatingEnd; // panel-local cursor pos when _target == null

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _image = GetComponent<Image>();

        // Left-middle pivot, centred anchor — lets anchoredPosition == panel-local position
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot     = new Vector2(0f,   0.5f);
    }

    void LateUpdate() => Refresh();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once after Instantiate to wire the arrow up.
    /// </summary>
    public void Init(RectTransform source, RectTransform target, RectTransform panelRect)
    {
        _source    = source;
        _target    = target;
        _panelRect = panelRect;

        if (_image) _image.color = shaftColor;
        Refresh();
    }

    /// <summary>
    /// Switch to floating mode: the far end follows panelLocalPoint each frame
    /// (call from OnHandleDragging with a fresh cursor position every frame).
    /// </summary>
    public void SetFloatingEnd(Vector2 panelLocalPoint)
    {
        _target      = null;
        _floatingEnd = panelLocalPoint;
        if (_image)  _image.color = dragColor;
    }

    /// <summary>Snap far end back to a fixed RectTransform.</summary>
    public void SetTarget(RectTransform target)
    {
        _target = target;
        if (_image) _image.color = shaftColor;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (_source == null || _panelRect == null) return;

        // Convert world positions → panel-local space.
        // InverseTransformPoint works for both Overlay and Camera canvases because
        // RectTransform.position is always in world space (screen-space for Overlay).
        Vector2 src = _panelRect.InverseTransformPoint(_source.position);
        Vector2 tgt = _target != null
            ? (Vector2)_panelRect.InverseTransformPoint(_target.position)
            : _floatingEnd;

        Vector2 dir  = tgt - src;
        float   dist = dir.magnitude;

        // Hide if degenerate
        if (dist < 2f)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        _rect.anchoredPosition  = src;
        _rect.sizeDelta         = new Vector2(dist, thickness);
        _rect.localEulerAngles  = new Vector3(0f, 0f, angle);

        // Keep arrowhead at the far end of the shaft
        if (arrowHead != null)
        {
            // Child anchored to right-middle of shaft; just push it to the tip edge.
            arrowHead.anchoredPosition = new Vector2(dist - arrowHead.sizeDelta.x * 0.5f, 0f);
        }
    }
}

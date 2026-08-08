using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic UI arrow that stretches between two RectTransforms.
///
/// How it works:
///   - Pivot is set to (0, 0.5) = left-middle, so the arrow STARTS at anchoredPosition.
///   - Anchor is set to (0.5, 0.5) = centre of parent, so anchoredPosition maps directly
///     to InverseTransformPoint coordinates (origin = parent pivot).
///   - Each LateUpdate: both endpoints are edge-snapped so the shaft starts at the
///     nearest edge of the source rect and ends at the nearest edge of the target rect.
///     This keeps the arrowhead visually flush with the target box border.
///
/// Edge-snap algorithm:
///   Given a rect with half-extents (hw, hh) centred at C, and normalised direction D:
///     t_x = hw / |D.x|   (if D.x != 0)
///     t_y = hh / |D.y|   (if D.y != 0)
///     edge point = C + min(t_x, t_y) * D
///
/// Prefab Setup:
///   Root: Image (solid coloured rectangle, NO sprite) + LLArrow
///   Optional child: a small triangle Image named "ArrowHead" assigned to arrowHead field.
///
/// Inspector Notes:
///   - nodeContainer's pivot MUST be (0.5, 0.5) for coordinate maths to be correct.
///   - Remove any Layout Group from nodeContainer.
///   - Arrowhead child: ~20 wide x 15 tall. Root Image must have no sprite assigned.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class LLArrow : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private float thickness  = 3f;
    [SerializeField] private Color shaftColor = new Color(0.20f, 0.85f, 0.35f, 0.90f);  // terminal green
    [SerializeField] private Color dragColor  = new Color(0.95f, 0.75f, 0.10f, 0.75f);  // amber while dragging

    /// <summary>Optional arrowhead child (anchored to right-middle of shaft).</summary>
    [SerializeField] private RectTransform arrowHead;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private RectTransform _rect;
    private Image         _image;
    private RectTransform _source;      // where arrow starts (handle RT)
    private RectTransform _target;      // where arrow ends  (null = floating)
    private RectTransform _panelRect;   // nodeContainer -- used for coordinate conversion
    private Vector2       _floatingEnd; // panel-local cursor pos when _target == null

    // ── Fixed-point segment mode ─────────────────────────────────────────────
    // Used by the orthogonal elbow router in LinkedListPuzzleUI: a segment
    // between two explicit panel-local points, no per-frame rect edge-snapping.
    // Kept fully separate from the RectTransform-based mode above, which still
    // drives the floating/ghost arrow while a connection is pending.
    private bool _fixedMode;
    private Vector2 _pointA;
    private Vector2 _pointB;

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _image = GetComponent<Image>();

        // Shaft: left-middle pivot, centred anchor.
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot     = new Vector2(0f,   0.5f);

        // The shaft Image must be a plain coloured rectangle -- no sprite.
        // If a sprite was accidentally placed on the root Image it would stretch
        // across the full arrow length. Clear it here to protect against that.
        if (_image) _image.sprite = null;

        // Arrowhead child: anchor to the right-middle edge of the shaft so
        // its position tracks the tip automatically as the shaft length changes.
        if (arrowHead != null)
        {
            arrowHead.anchorMin = new Vector2(1f, 0.5f);
            arrowHead.anchorMax = new Vector2(1f, 0.5f);
            arrowHead.pivot     = new Vector2(0.5f, 0.5f);
        }
    }

    void LateUpdate()
    {
        // Fixed-point segments are static once placed (nodes don't move after
        // the grid is laid out) -- RefreshFixed() already ran once in InitPoints.
        // Only the RectTransform-based mode needs per-frame recomputation.
        if (_fixedMode) return;
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once after Instantiate to wire the arrow up.
    /// </summary>
    public void Init(RectTransform source, RectTransform target, RectTransform panelRect)
    {
        _fixedMode = false;
        _source    = source;
        _target    = target;
        _panelRect = panelRect;

        if (_image)     _image.color = shaftColor;
        if (arrowHead)  arrowHead.gameObject.SetActive(true);
        Refresh();
    }

    /// <summary>
    /// Call once after Instantiate to draw a single straight segment between two
    /// explicit panel-local points. Used by the orthogonal elbow router for
    /// committed connections -- each elbow is 1-2 of these chained together.
    /// </summary>
    /// <param name="showArrowHead">
    /// Only the last segment of a multi-segment path should show the arrowhead.
    /// </param>
    public void InitPoints(Vector2 pointA, Vector2 pointB, bool showArrowHead)
    {
        _fixedMode = true;
        _pointA    = pointA;
        _pointB    = pointB;

        if (_image)    _image.color = shaftColor;
        if (arrowHead) arrowHead.gameObject.SetActive(showArrowHead);
        RefreshFixed();
    }

    /// <summary>
    /// Switch to floating mode: the far end follows panelLocalPoint each frame.
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

    /// <summary>
    /// Draws the shaft as a straight line between the two fixed points set in
    /// InitPoints. No edge-snapping -- the router already computed exact
    /// exit/bend/entry points before calling in.
    /// </summary>
    private void RefreshFixed()
    {
        Vector2 shaftVec = _pointB - _pointA;
        float   shaftLen = shaftVec.magnitude;

        if (shaftLen < 2f)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        float angle = Mathf.Atan2(shaftVec.y, shaftVec.x) * Mathf.Rad2Deg;

        _rect.anchoredPosition = _pointA;
        _rect.sizeDelta        = new Vector2(shaftLen, thickness);
        _rect.localEulerAngles = new Vector3(0f, 0f, angle);

        if (arrowHead != null)
            arrowHead.anchoredPosition = Vector2.zero;
    }

    private void Refresh()
    {
        if (_source == null || _panelRect == null) return;

        // Step 1: get centres in panel-local space.
        Vector2 srcCenter = GetRectCenter(_source);
        Vector2 tgtCenter = _target != null
            ? GetRectCenter(_target)
            : _floatingEnd;

        Vector2 dir  = tgtCenter - srcCenter;
        float   dist = dir.magnitude;

        // Hide if degenerate (source and target are on top of each other).
        if (dist < 2f)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        Vector2 dirNorm = dir / dist;  // normalised direction src -> tgt

        // Step 2: edge-snap both endpoints.
        // Source: start from the edge of the source rect nearest to the target.
        Vector2 src = GetEdgePoint(_source, srcCenter, dirNorm);

        // Target: end at the edge of the target rect nearest to the source.
        // For floating mode there is no target rect -- use the raw cursor position.
        Vector2 tgt = _target != null
            ? GetEdgePoint(_target, tgtCenter, -dirNorm)
            : _floatingEnd;

        // Recompute shaft direction and length after edge-snapping.
        Vector2 shaftVec  = tgt - src;
        float   shaftLen  = shaftVec.magnitude;

        if (shaftLen < 2f)
        {
            gameObject.SetActive(false);
            return;
        }

        float angle = Mathf.Atan2(shaftVec.y, shaftVec.x) * Mathf.Rad2Deg;

        _rect.anchoredPosition = src;
        _rect.sizeDelta        = new Vector2(shaftLen, thickness);
        _rect.localEulerAngles = new Vector3(0f, 0f, angle);

        // Arrowhead sits at anchor (1, 0.5) = right-middle of shaft.
        // anchoredPosition (0,0) places its centre exactly at the shaft tip.
        if (arrowHead != null)
            arrowHead.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Returns the centre of <paramref name="rt"/> in panel-local space.
    /// </summary>
    private Vector2 GetRectCenter(RectTransform rt)
    {
        return _panelRect.InverseTransformPoint(rt.position);
    }

    /// <summary>
    /// Returns the point on the boundary of <paramref name="rt"/> (in panel-local space)
    /// where a ray from <paramref name="center"/> in direction <paramref name="dirNorm"/>
    /// first exits the rect.
    ///
    /// Uses the slab method: find the smallest t such that
    ///   center + t * dirNorm touches either the horizontal or vertical wall of the rect.
    /// </summary>
    private Vector2 GetEdgePoint(RectTransform rt, Vector2 center, Vector2 dirNorm)
    {
        // Get all four world corners and convert to panel-local.
        // Corner order: BL, TL, TR, BR  (Unity's GetWorldCorners guarantee).
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        Vector2 bl = _panelRect.InverseTransformPoint(corners[0]);
        Vector2 tr = _panelRect.InverseTransformPoint(corners[2]);

        float hw = (tr.x - bl.x) * 0.5f;  // half-width in panel-local units
        float hh = (tr.y - bl.y) * 0.5f;  // half-height in panel-local units

        // Guard against degenerate rects (zero size).
        hw = Mathf.Max(hw, 0.5f);
        hh = Mathf.Max(hh, 0.5f);

        float t = float.MaxValue;

        if (Mathf.Abs(dirNorm.x) > 0.0001f)
            t = Mathf.Min(t, hw / Mathf.Abs(dirNorm.x));
        if (Mathf.Abs(dirNorm.y) > 0.0001f)
            t = Mathf.Min(t, hh / Mathf.Abs(dirNorm.y));

        // If both components are near-zero (dirNorm is essentially zero), just
        // return the center -- the shaft will be degenerate and hidden anyway.
        if (t == float.MaxValue) return center;

        return center + dirNorm * t;
    }
}

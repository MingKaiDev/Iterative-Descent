using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A single straight connector ("branch") drawn between a parent card and the
/// child slot it was placed into -- the visual edge of the tree diagram.
///
/// Distinct from LLArrow on purpose: LLArrow carries linked-list-specific
/// rewiring logic (floating ghost end, arrowhead rotation, corruption flash)
/// this puzzle doesn't need. A BST branch is a plain segment between two card
/// centres; it is redrawn (not rebuilt) whenever BstPuzzleUI relays the tree
/// out after an insertion.
///
/// Prefab Setup:
///   Root: Image (catalog_branch_line.png, pivot 0, 0.5) + BstBranchLine
///   RectTransform height = the sprite's natural thickness in pixels; width
///   is overwritten at runtime by SetEndpoints().
///
/// LAYOUT OWNERSHIP -- read before editing the prefab
/// --------------------------------------------------
/// SetEndpoints() forces localScale back to one and clears preserveAspect on
/// every call, and it is deliberate. Two authoring-time prefab values used to
/// corrupt every branch drawn:
///   * localScale 0.8 on the prefab root -- sizeDelta.x is set to the true
///     parent-to-child distance, so a non-unit scale rendered every branch at
///     0.8x its real length and left a visible gap short of the child card.
///   * preserveAspect: 1 on the Image -- the branch sprite is 256x20, so a
///     line stretched to any other aspect got letterboxed down instead of
///     stretching, making long branches thin stubs.
/// Neither is recoverable from at draw time except by overriding both, so
/// this component owns them. Set thickness through the prefab's sizeDelta.y
/// (or the thickness argument), never through localScale.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BstBranchLine : MonoBehaviour
{
    private RectTransform _rect;
    private Image         _image;
    private float         _baseThickness = -1f;

    void Awake() => CacheRefs();

    private void CacheRefs()
    {
        if (_rect == null)  _rect  = GetComponent<RectTransform>();
        if (_image == null) _image = GetComponent<Image>();
        if (_baseThickness < 0f) _baseThickness = Mathf.Max(1f, _rect.sizeDelta.y);
    }

    /// <summary>
    /// Stretches and rotates this line from <paramref name="from"/> to
    /// <paramref name="to"/>, both in nodeContainer local space.
    /// </summary>
    /// <param name="thickness">
    /// Rendered thickness in nodeContainer units. Pass the tree's current fit
    /// scale times the prefab thickness so branches thin out in step with the
    /// cards when a deep tree is scaled down. Values &lt;= 0 keep the prefab's
    /// authored thickness.
    /// </param>
    public void SetEndpoints(Vector2 from, Vector2 to, float thickness = 0f)
    {
        CacheRefs();

        Vector2 delta    = to - from;
        float   distance = delta.magnitude;
        float   angle    = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        // Own the two prefab values that would otherwise silently mis-draw the
        // branch -- see the class comment.
        _rect.localScale = Vector3.one;
        if (_image != null) _image.preserveAspect = false;

        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot     = new Vector2(0f, 0.5f);
        _rect.anchoredPosition = from;
        _rect.sizeDelta = new Vector2(distance, thickness > 0f ? thickness : _baseThickness);
        _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}

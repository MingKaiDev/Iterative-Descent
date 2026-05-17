using UnityEngine;
using TMPro;

/// <summary>
/// The HEAD pointer box — a fixed UI element labeled "HEAD" with its own
/// drag handle.  The player drags the handle to redirect which node HEAD
/// points to, just like rewiring a node's next pointer.
///
/// Prefab Setup:
///   Root: Image (box background) + LLHeadPointer
///   Children:
///     "Label"      — TextMeshProUGUI showing "HEAD"
///     "HeadHandle" — Small circle/arrow child with LLNextHandle (isHead = true)
///
/// LinkedListPuzzleUI places this above the initial first node and keeps it
/// at a fixed screen position.  The arrow from this box to TargetNode is
/// drawn and managed by LinkedListPuzzleUI.RebuildArrows().
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLHeadPointer : MonoBehaviour
{
    // ── Public State ──────────────────────────────────────────────────────────
    /// <summary>The node HEAD currently points to.  Never null after Init.</summary>
    public LLNodeBlock TargetNode { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private LLNextHandle    headHandle; // child handle with LLNextHandle
    [SerializeField] private TextMeshProUGUI label;      // optional — shows "HEAD"

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>
    /// RectTransform used as the SOURCE of the HEAD→node arrow.
    /// Falls back to this object's own rect if no handle is assigned.
    /// </summary>
    public RectTransform HandleRect => headHandle != null
        ? headHandle.GetComponent<RectTransform>()
        : GetComponent<RectTransform>();

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Called by LinkedListPuzzleUI once after Instantiate.</summary>
    /// <param name="initialNode">First node in the forward list (HEAD starts here).</param>
    /// <param name="puzzle">Master controller forwarded to the handle.</param>
    /// <param name="anchoredPos">Fixed position in nodeContainer local space.</param>
    public void Init(LLNodeBlock initialNode, LinkedListPuzzleUI puzzle, Vector2 anchoredPos)
    {
        TargetNode = initialNode;

        if (label) label.text = "HEAD";

        var rect = GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;

        headHandle?.Init(ownerNode: null, puzzle: puzzle, isHead: true);
    }

    // ── Pointer Management ────────────────────────────────────────────────────

    /// <summary>Redirect HEAD to a different node (called by LinkedListPuzzleUI).</summary>
    public void SetTarget(LLNodeBlock node) => TargetNode = node;
}

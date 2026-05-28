using UnityEngine;
using TMPro;

/// <summary>
/// The HEAD pointer box -- a fixed UI element labelled "HEAD" with its own
/// clickable port handle.  The player clicks the handle to start a HEAD connection,
/// then clicks a destination node to redirect which node HEAD points to.
///
/// Prefab Setup:
///   Root: Image (box background) + LLHeadPointer
///   Children:
///     "Label"      -- TextMeshProUGUI showing "HEAD"
///     "HeadHandle" -- Small port widget, has LLNextHandle (isHead = true)
///
/// LinkedListPuzzleUI places this above the initial first node.
/// The arrow from this box to TargetNode is drawn by LinkedListPuzzleUI.RebuildArrows().
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLHeadPointer : MonoBehaviour
{
    // ── Public State ──────────────────────────────────────────────────────────
    /// <summary>The node HEAD currently points to. Never null after Init.</summary>
    public LLNodeBlock TargetNode { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private LLNextHandle    headHandle;
    [SerializeField] private TextMeshProUGUI label;

    // ── Accessors ─────────────────────────────────────────────────────────────
    public RectTransform HandleRect => headHandle != null
        ? headHandle.GetComponent<RectTransform>()
        : GetComponent<RectTransform>();

    // ── Initialisation ────────────────────────────────────────────────────────
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

    // ── Visual State ──────────────────────────────────────────────────────────
    /// <summary>
    /// Highlights the HEAD handle as the currently selected (pending) connection source.
    /// </summary>
    public void SetSourceSelected(bool selected)
    {
        headHandle?.SetSelected(selected);
    }
}

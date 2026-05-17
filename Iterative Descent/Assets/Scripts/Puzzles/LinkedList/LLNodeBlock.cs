using UnityEngine;
using TMPro;

/// <summary>
/// Represents one node in the visual linked-list puzzle.
/// Nodes are FIXED in position — the player rewires pointers, not node positions.
///
/// Prefab Setup:
///   Root: Image (node box background) + LLNodeBlock
///   Children:
///     "ValueText"  — TextMeshProUGUI displaying the node's integer value
///     "NextHandle" — Small circle/arrow child with LLNextHandle script attached
///
/// The NextHandle sits on the right edge of the box and is the drag origin for
/// the node's "next" pointer arrow.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLNodeBlock : MonoBehaviour
{
    // ── Public State ──────────────────────────────────────────────────────────
    public int          Value    { get; private set; }
    public LLNodeBlock  NextNode { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private LLNextHandle    nextHandle;

    // ── Accessors ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The RectTransform used as the SOURCE of this node's "next" arrow.
    /// Typically the NextHandle child (right-edge of the box).
    /// Falls back to the node's own rect if no handle is assigned.
    /// </summary>
    public RectTransform HandleRect => nextHandle != null
        ? nextHandle.GetComponent<RectTransform>()
        : GetComponent<RectTransform>();

    public RectTransform NodeRect => GetComponent<RectTransform>();

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LinkedListPuzzleUI immediately after Instantiate.
    /// </summary>
    /// <param name="value">Integer value displayed on this node.</param>
    /// <param name="anchoredPos">Position in the nodeContainer's local space.</param>
    /// <param name="puzzle">Master UI controller (wired into the handle).</param>
    public void Init(int value, Vector2 anchoredPos, LinkedListPuzzleUI puzzle)
    {
        Value = value;
        if (valueText) valueText.text = value.ToString();

        // Position the node — nodeContainer must have no LayoutGroup.
        var rect = GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;

        // Wire handle so it can call back into the puzzle on drag events.
        nextHandle?.Init(ownerNode: this, puzzle: puzzle, isHead: false);
    }

    // ── Pointer Management ────────────────────────────────────────────────────

    /// <summary>Set or change the node this node points to (null = points to NULL).</summary>
    public void SetNext(LLNodeBlock next) => NextNode = next;
}

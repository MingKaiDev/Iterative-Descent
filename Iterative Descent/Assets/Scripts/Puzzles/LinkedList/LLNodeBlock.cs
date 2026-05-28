using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents one memory block node in the linked-list puzzle.
/// Nodes are FIXED in position -- the player rewires pointers, not node positions.
///
/// Prefab Setup:
///   Root: Image (node background) + LLNodeBlock
///   Children:
///     "AddressText"  -- TextMeshProUGUI showing a fake hex address (optional)
///     "ValueText"    -- TextMeshProUGUI showing the node's integer value
///     "NextHandle"   -- Small port widget on the right edge, has LLNextHandle script
///
/// Changes from original:
///   - IPointerClickHandler: landing a pending connection on this node body
///   - SetSourceSelected(): amber highlight when this node is the pending source
///   - addressText field for terminal-style hex address label
///   - background Image field for programmatic color control
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLNodeBlock : MonoBehaviour, IPointerClickHandler
{
    // ── Public State ──────────────────────────────────────────────────────────
    public int         Value    { get; private set; }
    public LLNodeBlock NextNode { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI addressText;  // optional -- shows hex address
    [SerializeField] private LLNextHandle    nextHandle;
    [SerializeField] private Image           background;   // root Image for color feedback

    [Header("Terminal Colors")]
    [SerializeField] private Color defaultBgColor   = new Color(0.04f, 0.10f, 0.04f, 1f);
    [SerializeField] private Color selectedBgColor  = new Color(0.06f, 0.22f, 0.06f, 1f);
    [SerializeField] private Color corruptedBgColor = new Color(0.22f, 0.04f, 0.04f, 1f);

    // ── Accessors ─────────────────────────────────────────────────────────────
    public RectTransform HandleRect => nextHandle != null
        ? nextHandle.GetComponent<RectTransform>()
        : GetComponent<RectTransform>();

    public RectTransform NodeRect => GetComponent<RectTransform>();
    public LLNextHandle  Handle   => nextHandle;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private LinkedListPuzzleUI _puzzle;
    private bool               _isSourceSelected;

    // ── Initialisation ────────────────────────────────────────────────────────
    /// <param name="value">Integer value displayed on the node.</param>
    /// <param name="anchoredPos">Position in nodeContainer local space.</param>
    /// <param name="puzzle">Master UI controller.</param>
    /// <param name="hexAddr">Optional terminal hex address label passed from the puzzle generator.</param>
    public void Init(int value, Vector2 anchoredPos, LinkedListPuzzleUI puzzle, string hexAddr = "")
    {
        Value   = value;
        _puzzle = puzzle;
        _isSourceSelected = false;

        if (valueText)   valueText.text   = value.ToString();
        if (addressText) addressText.text = hexAddr;
        if (background)  background.color = defaultBgColor;

        var rect = GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;

        nextHandle?.Init(ownerNode: this, puzzle: puzzle, isHead: false);
    }

    // ── Pointer Management ────────────────────────────────────────────────────
    public void SetNext(LLNodeBlock next) => NextNode = next;

    // ── Visual State ──────────────────────────────────────────────────────────

    /// <summary>
    /// Highlights this node as the currently pending connection source.
    /// Also forwards the selected state to the port handle widget (turns it amber).
    /// </summary>
    public void SetSourceSelected(bool selected)
    {
        _isSourceSelected = selected;
        if (background) background.color = selected ? selectedBgColor : defaultBgColor;
        nextHandle?.SetSelected(selected);
    }

    /// <summary>
    /// Flashes this node red to indicate ARBITEX corrupted its pointer.
    /// LinkedListPuzzleUI drives the flash on/off via a coroutine.
    /// </summary>
    public void SetCorrupted(bool corrupted)
    {
        if (background)
            background.color = corrupted
                ? corruptedBgColor
                : (_isSourceSelected ? selectedBgColor : defaultBgColor);
    }

    // ── IPointerClickHandler ──────────────────────────────────────────────────

    /// <summary>
    /// Fires when the node body (not the port handle) is clicked.
    /// Forwards to the puzzle to land the pending connection here.
    /// Has no effect when no connection is pending.
    ///
    /// Unity routing note: LLNextHandle (child) intercepts clicks on the port area
    /// before this handler fires -- no double-handling occurs.
    /// </summary>
    public void OnPointerClick(PointerEventData e)
    {
        _puzzle?.OnNodeBodyClicked(this);
    }
}

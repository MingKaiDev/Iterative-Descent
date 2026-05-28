using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Click-based relay attached to each node's port widget and to the HEAD pointer's handle.
/// Replaced the old drag-handler model with click-to-select / click-to-connect.
///
/// Interaction rules:
///   - Click while idle           -> tell LinkedListPuzzleUI to START a connection from this port.
///   - Click while this port is   -> cancel the pending connection (deselect).
///     already the pending source
///   - Landing a connection on a  -> handled by LLNodeBlock.OnPointerClick (node body).
///     destination node
///
/// Unity event routing note:
///   LLNextHandle is a child of LLNodeBlock. When the player clicks the port,
///   Unity routes the event to this script only (deepest handler wins).
///   LLNodeBlock.OnPointerClick does NOT also fire -- no double-handling needed.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class LLNextHandle : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Terminal Port Colors")]
    [SerializeField] private Color normalColor   = new Color(0.20f, 0.75f, 0.30f, 1f); // green idle
    [SerializeField] private Color hoverColor    = new Color(0.45f, 1.00f, 0.55f, 1f); // bright hover
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.75f, 0.10f, 1f); // amber = pending

    // ── Wired at runtime ───────────────────────────────────────────────────────
    private LLNodeBlock        _ownerNode;
    private bool               _isHead;
    private LinkedListPuzzleUI _puzzle;
    private Image              _image;
    private bool               _isSelected;

    // ── Unity ──────────────────────────────────────────────────────────────────
    void Awake() => _image = GetComponent<Image>();

    // ── Initialisation ─────────────────────────────────────────────────────────
    /// <summary>Called by LLNodeBlock.Init or LLHeadPointer.Init immediately after spawn.</summary>
    public void Init(LLNodeBlock ownerNode, LinkedListPuzzleUI puzzle, bool isHead)
    {
        _ownerNode  = ownerNode;
        _puzzle     = puzzle;
        _isHead     = isHead;
        _isSelected = false;
        if (_image) _image.color = normalColor;
    }

    // ── Selection Visual ───────────────────────────────────────────────────────
    /// <summary>
    /// Called by LinkedListPuzzleUI to show this port as the currently selected
    /// (pending) connection source.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (_image) _image.color = selected ? selectedColor : normalColor;
    }

    // ── Pointer Events ─────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData e)
    {
        if (_isHead)
            _puzzle.OnHeadHandleClicked();
        else
            _puzzle.OnNodeHandleClicked(_ownerNode);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_image) _image.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData e)
    {
        // Restore correct state color on exit, not just normal.
        if (_image) _image.color = _isSelected ? selectedColor : normalColor;
    }
}

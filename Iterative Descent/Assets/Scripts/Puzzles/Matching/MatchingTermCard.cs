using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Draggable protocol name card for the Matching Puzzle.
///
/// Lives in the TermPool (left column). Drag to a PortSlot on the right to assign.
/// On EndDrag the card always snaps back to its original pool position --
/// the slot's assignment label is updated separately (no reparenting to the slot).
///
/// COMPONENT SETUP on each TermCard GameObject:
///   - Image (background, Raycast Target ON)
///   - CanvasGroup
///   - MatchingTermCard (this script)
///   - Child: TextMeshProUGUI for the protocol label
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public class MatchingTermCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private TextMeshProUGUI label;

    // ── Public ────────────────────────────────────────────────────────────────

    public string           TermName     { get; private set; }
    public MatchingPortSlot AssignedSlot { get; set; }

    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Color ColorFree     = new(0.12f, 0.18f, 0.22f, 1f); // dark blue-grey
    private static readonly Color ColorAssigned = new(0.08f, 0.28f, 0.12f, 1f); // dark green
    private static readonly Color ColorDragging = new(0.18f, 0.50f, 0.65f, 0.90f);

    // ── Private ───────────────────────────────────────────────────────────────

    private RectTransform _rect;
    private CanvasGroup   _group;
    private Image         _bg;
    private Canvas        _canvas;
    private Transform     _originalParent;
    private int           _originalSiblingIndex;

    private void Awake()
    {
        _rect  = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
        _bg    = GetComponent<Image>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call from MatchingPuzzleUI.InitPuzzle() to set term name and reset state.</summary>
    public void SetupCard(string term)
    {
        TermName              = term;
        AssignedSlot          = null;
        _originalParent       = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();
        _canvas               = GetComponentInParent<Canvas>();

        if (label != null) label.text = term;
        ApplyColor(ColorFree);
        _group.alpha          = 1f;
        _group.blocksRaycasts = true;
        _group.interactable   = true;
    }

    /// <summary>
    /// Called by MatchingPortSlot when another card displaces this one,
    /// or when the player drags this card off a slot back to pool area.
    /// </summary>
    public void ClearAssignment()
    {
        AssignedSlot = null;
        ApplyColor(ColorFree);
    }

    /// <summary>Called by MatchingPortSlot.OnDrop when this card lands on a slot.</summary>
    public void MarkAssigned()
    {
        ApplyColor(ColorAssigned);
    }

    // ── Drag Handlers ─────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Clear previous slot assignment visually
        if (AssignedSlot != null)
        {
            AssignedSlot.ClearOccupant();
            AssignedSlot = null;
        }

        // Float above everything by reparenting to canvas root
        _canvas ??= GetComponentInParent<Canvas>();
        transform.SetParent(_canvas.transform, true);

        _group.alpha          = 0.82f;
        _group.blocksRaycasts = false; // let pointer events reach the slot beneath
        ApplyColor(ColorDragging);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas == null) return;
        _rect.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Always return card to its original pool position
        transform.SetParent(_originalParent, false);
        transform.SetSiblingIndex(_originalSiblingIndex);

        _group.alpha          = 1f;
        _group.blocksRaycasts = true;

        // Colour reflects whether a slot accepted the drop
        ApplyColor(AssignedSlot != null ? ColorAssigned : ColorFree);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void ApplyColor(Color c)
    {
        if (_bg != null) _bg.color = c;
    }
}

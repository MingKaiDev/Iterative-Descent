using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop zone for the Matching Puzzle. Displays a port number and accepts one MatchingTermCard.
///
/// The card physically stays in the TermPool -- this slot just records WHICH card is
/// assigned and updates its own label to reflect that assignment.
///
/// COMPONENT SETUP on each PortSlot GameObject:
///   - Image (background, Raycast Target ON)
///   - MatchingPortSlot (this script)
///   - Child: PortLabel   TextMeshProUGUI (shows ":80" -- always visible)
///   - Child: TermLabel   TextMeshProUGUI (shows assigned term or "--" when empty)
/// </summary>
[RequireComponent(typeof(Image))]
public class MatchingPortSlot : MonoBehaviour, IDropHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private TextMeshProUGUI portLabel; // e.g. ":80"
    [SerializeField] private TextMeshProUGUI termLabel; // e.g. "HTTP" or "--"

    // ── Public ────────────────────────────────────────────────────────────────

    public int             PortNumber   { get; private set; }
    public MatchingTermCard OccupiedCard { get; set; }

    // ── Colors ────────────────────────────────────────────────────────────────

    private static readonly Color ColorEmpty   = new(0.06f, 0.10f, 0.14f, 1f);
    private static readonly Color ColorOccupied = new(0.08f, 0.18f, 0.10f, 1f);
    private static readonly Color ColorCorrect = new(0.15f, 0.70f, 0.20f, 1f);
    private static readonly Color ColorWrong   = new(0.70f, 0.15f, 0.15f, 1f);

    private Image _bg;

    private void Awake() => _bg = GetComponent<Image>();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call from MatchingPuzzleUI.InitPuzzle() each session.</summary>
    public void SetupSlot(int port)
    {
        PortNumber   = port;
        OccupiedCard = null;
        if (portLabel != null) portLabel.text = $":{port}";
        if (termLabel != null) termLabel.text  = "--";
        SetBgColor(ColorEmpty);
    }

    /// <summary>Called by MatchingPuzzleUI.OnSubmit to colour-code the result.</summary>
    public void SetHighlight(bool correct)
    {
        SetBgColor(correct ? ColorCorrect : ColorWrong);
    }

    /// <summary>Called by MatchingPuzzleUI between submissions to clear highlights.</summary>
    public void ResetHighlight()
    {
        SetBgColor(OccupiedCard != null ? ColorOccupied : ColorEmpty);
    }

    /// <summary>
    /// Clears this slot's occupant without touching the card itself.
    /// Called by MatchingTermCard.OnBeginDrag when the player re-drags a card
    /// that was already assigned here.
    /// </summary>
    public void ClearOccupant()
    {
        OccupiedCard = null;
        if (termLabel != null) termLabel.text = "--";
        SetBgColor(ColorEmpty);
    }

    // ── Drop Handler ──────────────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        var card = eventData.pointerDrag?.GetComponent<MatchingTermCard>();
        if (card == null) return;

        // If the slot already holds a different card, evict it first
        if (OccupiedCard != null && OccupiedCard != card)
        {
            OccupiedCard.ClearAssignment();
            OccupiedCard = null;
        }

        // If this card was already assigned to another slot, clear that slot
        if (card.AssignedSlot != null && card.AssignedSlot != this)
            card.AssignedSlot.ClearOccupant();

        // Record the assignment
        OccupiedCard      = card;
        card.AssignedSlot = this;
        card.MarkAssigned();

        if (termLabel != null) termLabel.text = card.TermName;
        SetBgColor(ColorOccupied);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void SetBgColor(Color c)
    {
        if (_bg != null) _bg.color = c;
    }
}

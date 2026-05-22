using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// A drop-target execution slot in the CPU Scheduling puzzle.
///
/// The player drags a SchedulingProcessCard onto this slot to assign it
/// a position in the SJF execution order (1st, 2nd, 3rd, …).
///
/// INSPECTOR SETUP
/// ───────────────
///   slotLabel     TMP label showing "1st", "2nd", etc.
///   emptyIcon     Image shown when the slot is empty (e.g. dashed border)
///   filledBg      Image shown (tinted to card colour) when a card occupies the slot
/// </summary>
public class SchedulingSlot : MonoBehaviour, IDropHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private TextMeshProUGUI slotLabel;
    [SerializeField] private Image           emptyIcon;   // shown when empty
    [SerializeField] private Image           filledBg;    // shown when occupied

    // ── Public State ──────────────────────────────────────────────────────────
    public int                   SlotIndex  { get; private set; }
    public SchedulingProcessCard HeldCard   { get; private set; }
    public bool                  IsOccupied => HeldCard != null;

    // ── Private ───────────────────────────────────────────────────────────────
    private SchedulingPuzzleUI _master;

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Called by SchedulingPuzzleUI.GeneratePuzzle().</summary>
    public void Init(int index, string label, SchedulingPuzzleUI master)
    {
        SlotIndex = index;
        _master   = master;

        // Auto-find the label TMP if it wasn't wired in the prefab Inspector.
        // GetComponentInChildren is safe here because Init() runs before any card
        // is placed in this slot, so the only TMP in the hierarchy is the label itself.
        if (slotLabel == null)
            slotLabel = GetComponentInChildren<TextMeshProUGUI>();

        if (slotLabel) slotLabel.text = label;
        SetVisuals(occupied: false, color: Color.white);
    }

    // ── IDropHandler ──────────────────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        var card = eventData.pointerDrag?.GetComponent<SchedulingProcessCard>();
        if (card == null) return;

        _master.OnCardDroppedOnSlot(card, this);
    }

    // ── Card Management (called by SchedulingPuzzleUI) ────────────────────────

    /// <summary>Snap a card into this slot.</summary>
    public void PlaceCard(SchedulingProcessCard card)
    {
        HeldCard          = card;
        card.CurrentSlot  = this;

        // Stretch the card to fill this slot exactly.
        // Using a fixed card size + center-anchor causes the card to overflow
        // the slot visually when the card is wider/taller than the slot rect.
        // Stretch-fill sidesteps that entirely: the card always fits perfectly
        // and looks centred regardless of card vs slot size.
        // OriginalSize is restored by ReturnToPool() and OnBeginDrag() when
        // the card leaves the slot.
        var rt = card.GetComponent<RectTransform>();
        rt.SetParent(transform, worldPositionStays: false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;   // left / bottom offset = 0
        rt.offsetMax = Vector2.zero;   // right / top offset   = 0

        SetVisuals(occupied: true, color: card.CardColor);
    }

    /// <summary>Remove the held card from this slot (card is NOT destroyed).</summary>
    public void RemoveCard()
    {
        if (HeldCard != null)
        {
            HeldCard.CurrentSlot = null;
            HeldCard             = null;
        }
        SetVisuals(occupied: false, color: Color.white);
    }

    // ── Visuals ───────────────────────────────────────────────────────────────

    private void SetVisuals(bool occupied, Color color)
    {
        if (emptyIcon) emptyIcon.enabled = !occupied;
        if (filledBg)
        {
            filledBg.enabled = occupied;
            if (occupied)
                filledBg.color = new Color(color.r, color.g, color.b, 0.30f); // subtle tint
        }
    }
}

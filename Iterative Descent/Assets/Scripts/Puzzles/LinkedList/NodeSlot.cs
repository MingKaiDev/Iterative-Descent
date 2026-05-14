using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop zone slot for the Linked List puzzle answer chain.
/// 
/// Prefab Setup:
///   Root GameObject
///     - Image (slot background — dim/empty look)
///     - NodeSlot (this script)
///     - SlotIndex set in Inspector or by LinkedListPuzzleUI at runtime
/// </summary>
public class NodeSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public int SlotIndex;
    public LinkedListNodeCard OccupiedBy;

    [SerializeField] private Image slotImage;
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hoverColor = new Color(0.4f, 0.8f, 0.4f, 0.8f);
    [SerializeField] private Color occupiedColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);

    void Awake()
    {
        if (slotImage == null) slotImage = GetComponent<Image>();
        if (slotImage != null) slotImage.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (OccupiedBy == null && slotImage != null)
            slotImage.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (slotImage != null)
            slotImage.color = OccupiedBy != null ? occupiedColor : normalColor;
    }

    public void OnDrop(PointerEventData e)
    {
        var card = e.pointerDrag?.GetComponent<LinkedListNodeCard>();
        if (card == null) return;

        // If slot already has a card, reject the drop
        if (OccupiedBy != null) return;

        OccupiedBy = card;
        card.PlaceInSlot(this);

        if (slotImage != null) slotImage.color = occupiedColor;

        // Auto-check only when all slots filled (optional — you can remove this
        // and rely solely on the Submit button instead)
        LinkedListPuzzleUI.Instance.CheckSolution();
    }
}
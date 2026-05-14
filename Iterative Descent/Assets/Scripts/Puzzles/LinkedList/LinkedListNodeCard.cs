using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Draggable node card for the Linked List puzzle.
/// 
/// Prefab Setup:
///   Root GameObject
///     - Image (background of card)
///     - CanvasGroup (required — controls raycast blocking during drag)
///     - LinkedListNodeCard (this script)
///     - Child: TextMeshProUGUI named "ValueText"
/// </summary>
public class LinkedListNodeCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int NodeValue { get; private set; }

    [SerializeField] private TextMeshProUGUI valueText;

    private RectTransform _rect;
    private Canvas _canvas;
    private CanvasGroup _group;
    private Transform _originalParent;
    private Vector2 _originalAnchoredPos;

    // Reference to the slot currently occupying us (null if in pool)
    private NodeSlot _currentSlot;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
    }

    /// <summary>Called by LinkedListPuzzleUI when spawning the card.</summary>
    public void Init(int value)
    {
        NodeValue = value;
        if (valueText != null) valueText.text = value.ToString();
    }

    public void OnBeginDrag(PointerEventData e)
    {
        // Cache canvas lazily (parent changes at runtime so do it here)
        _canvas = GetComponentInParent<Canvas>();

        _originalParent = transform.parent;
        _originalAnchoredPos = _rect.anchoredPosition;

        // Vacate slot so it's free for another card
        if (_currentSlot != null)
        {
            _currentSlot.OccupiedBy = null;
            _currentSlot = null;
        }

        // Move to root canvas so card renders on top of everything
        transform.SetParent(_canvas.transform, true);
        _group.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData e)
    {
        _rect.anchoredPosition += e.delta / _canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData e)
    {
        _group.blocksRaycasts = true;

        // If not successfully dropped on a slot (OnDrop not called),
        // return to where we came from
        if (_currentSlot == null)
        {
            transform.SetParent(_originalParent, true);
            _rect.anchoredPosition = _originalAnchoredPos;
        }
    }

    /// <summary>Called by NodeSlot.OnDrop when this card is accepted.</summary>
    public void PlaceInSlot(NodeSlot slot)
    {
        _currentSlot = slot;

        // Reparent to canvas root so HLG doesn't control position
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        transform.SetParent(canvas.transform, true);

        // Snap position to match the slot's world position
        _rect.position = slot.GetComponent<RectTransform>().position;
    }

    /// <summary>Called by LinkedListPuzzleUI when resetting after wrong answer.</summary>
    public void ReturnToPool(Transform pool)
    {
        _currentSlot = null;
        transform.SetParent(pool, true);
        _rect.anchoredPosition = Vector2.zero;
    }
}
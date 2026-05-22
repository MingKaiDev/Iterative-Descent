using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// A draggable process token in the CPU Scheduling puzzle.
///
/// Used in both SJF (one card per process) and Round Robin (one token per Gantt slot).
/// In RR mode, multiple tokens for the same process may exist simultaneously in the pool;
/// they are distinguished by ProcessIndex so CheckSolution can validate placement.
///
/// Displays:
///   • Process name  (P1, P2, …)
///   • Arrival Time  (always 0)
///   • Burst Time    (useful context for the player)
///
/// Drag behaviour:
///   BeginDrag → reparents to SchedulingPuzzleUI.DragLayer (renders on top).
///   Drag      → follows the cursor.
///   EndDrag   → notifies master; if not dropped on a slot, returns to pool.
///
/// INSPECTOR SETUP
/// ───────────────
///   processNameText   TMP label showing "P1", "P2", …
///   burstTimeText     TMP label showing "BT: 12"
///   arrivalTimeText   TMP label showing "AT: 0"
///   cardBackground    Image used for the coloured card body
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SchedulingProcessCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [SerializeField] private TextMeshProUGUI processNameText;
    [SerializeField] private TextMeshProUGUI burstTimeText;
    [SerializeField] private TextMeshProUGUI arrivalTimeText;
    [SerializeField] private Image           cardBackground;

    [Tooltip("Width and height of the card as authored in the prefab. " +
             "Used to restore size after every reparent (drag layer ↔ pool ↔ slot). " +
             "Must match the prefab RectTransform Width/Height exactly.")]
    [SerializeField] private Vector2 cardSize = new Vector2(180f, 70f);

    // ── Public State ──────────────────────────────────────────────────────────

    /// <summary>
    /// Index of the process this token represents (0 = P1, 1 = P2, 2 = P3, 3 = P4).
    /// Used by SchedulingPuzzleUI.CheckSolution() to validate RR slot placement.
    /// </summary>
    public int                ProcessIndex { get; private set; }

    public string             ProcessName { get; private set; }
    public int                BurstTime   { get; private set; }
    public int                ArrivalTime { get; private set; }
    public Color              CardColor   { get; private set; }

    /// <summary>The slot this card currently occupies, or null if in the pool.</summary>
    public SchedulingSlot CurrentSlot { get; set; }

    // ── Private ───────────────────────────────────────────────────────────────
    private SchedulingPuzzleUI _master;
    private CanvasGroup        _canvasGroup;
    private RectTransform      _rect;
    private Transform          _poolParent; // cardPool RectTransform
    private Vector2            _dragOffset; // cursor → card-pivot offset, held across OnDrag calls

    /// <summary>
    /// Canonical size of this card — equal to the Inspector-authored cardSize.
    /// Exposed so SchedulingSlot.PlaceCard can restore the card's dimensions
    /// after reparenting into a slot.
    /// </summary>
    public Vector2 OriginalSize { get; private set; }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by SchedulingPuzzleUI.GeneratePuzzle() immediately after Instantiate.
    /// </summary>
    /// <param name="processIndex">
    ///   0-based index of the process this token represents (0=P1, …, 3=P4).
    ///   Used by CheckSolution to validate Round Robin slot placement.
    /// </param>
    /// <param name="sizeOverride">
    ///   If non-zero, overrides the prefab's serialized cardSize.
    ///   Pass the computed slot width × height so token size matches the Gantt slots.
    ///   The Grid Layout Group on CardPool will ignore sizeDelta in the pool,
    ///   but OriginalSize is used to restore the card when dragging and on return-to-pool.
    /// </param>
    public void Init(
        int processIndex, string name, int burstTime, int arrivalTime,
        Color color, SchedulingPuzzleUI master, Transform poolParent,
        Vector2 sizeOverride = default)
    {
        ProcessIndex = processIndex;
        ProcessName  = name;
        BurstTime    = burstTime;
        ArrivalTime  = arrivalTime;
        CardColor    = color;
        _master      = master;
        _poolParent  = poolParent;

        _canvasGroup = GetComponent<CanvasGroup>();
        _rect        = GetComponent<RectTransform>();

        // Determine canonical size.
        // sizeOverride is supplied by SchedulingPuzzleUI when it computes a dynamic
        // slot width (RR mode).  If not supplied (default = zero), fall back to the
        // Inspector-authored cardSize.  This is also the size restored on ReturnToPool
        // and on BeginDrag so the drag preview matches the Gantt slot dimensions.
        OriginalSize = (sizeOverride != default) ? sizeOverride : cardSize;

        // Bake the correct anchor + size immediately so the card starts clean.
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot     = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = OriginalSize;

        // The pool's Vertical Layout Group uses LayoutElement values to size
        // children — it ignores sizeDelta entirely when Control Child Size is on.
        // Setting preferredWidth/Height + flexibleWidth=0 tells it to use exactly
        // our cardSize and never grow the card to fill available pool space.
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.minWidth        = cardSize.x;
        le.minHeight       = cardSize.y;
        le.preferredWidth  = cardSize.x;
        le.preferredHeight = cardSize.y;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        if (processNameText)  processNameText.text  = name;
        if (burstTimeText)    burstTimeText.text    = $"BT: {burstTime}";
        if (arrivalTimeText)  arrivalTimeText.text  = $"AT: {arrivalTime}";
        if (cardBackground)   cardBackground.color  = color;
    }

    // ── Drag Handlers ─────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Vacate the current slot so it becomes available again
        if (CurrentSlot != null)
        {
            CurrentSlot.RemoveCard();
            CurrentSlot = null;
        }

        // ── Reparent ──────────────────────────────────────────────────────────
        // worldPositionStays: FALSE — we will position the card explicitly using
        // screen-space cursor coords.  Using worldPositionStays:true + stretch
        // anchor (set by PlaceCard) creates an invalid rect in DragLayer space
        // and causes the card to shrink each cycle.
        transform.SetParent(_master.DragLayer, worldPositionStays: false);

        // Always restore a clean center-anchor + authored size in the drag layer.
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot     = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = OriginalSize;

        // ── Position card at cursor ────────────────────────────────────────────
        // Convert the current cursor screen position to the DragLayer's local
        // coordinate space.  This is the correct way to position a RectTransform
        // on the canvas regardless of CanvasScaler mode or canvas scale.
        // We snap the card's CENTRE to the cursor (offset = 0) so the card never
        // inherits stale anchoredPosition values from the pool or a slot.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _master.DragLayer, eventData.position,
                eventData.pressEventCamera, out var localPt))
        {
            _rect.anchoredPosition = localPt;
        }
        _dragOffset = Vector2.zero;

        _canvasGroup.blocksRaycasts = false; // let drops pass through to slots
        _canvasGroup.alpha          = 0.80f;

        _master.OnCardDragBegin(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Convert screen position → DragLayer local coords and apply the stored
        // offset.  This is more reliable than accumulating eventData.delta, which
        // drifts under variable frame rates and canvas scale changes.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _master.DragLayer, eventData.position,
                eventData.pressEventCamera, out var localPt))
        {
            _rect.anchoredPosition = localPt + _dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha          = 1f;

        // Master decides whether to snap to a slot or return here to pool
        _master.OnCardDragEnd(this, eventData);
    }

    // ── Pool Return ───────────────────────────────────────────────────────────

    /// <summary>
    /// Re-parent this card back to the card pool.
    /// The pool's Layout Group will reposition it automatically.
    /// </summary>
    public void ReturnToPool()
    {
        CurrentSlot = null;
        // worldPositionStays: false to avoid the card teleporting off-screen,
        // then immediately restore anchor + size so the Layout Group starts
        // from the correct preferred dimensions rather than whatever the drag
        // layer or slot had set.
        transform.SetParent(_poolParent, worldPositionStays: false);
        _rect.anchorMin         = new Vector2(0.5f, 0.5f);
        _rect.anchorMax         = new Vector2(0.5f, 0.5f);
        _rect.pivot             = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta         = OriginalSize;
        _rect.anchoredPosition  = Vector2.zero;
    }
}

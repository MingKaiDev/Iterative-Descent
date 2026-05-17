using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Thin drag-event relay attached to the small handle widget on each node
/// (and on the HEAD pointer).  It contains NO logic of its own — it simply
/// forwards all three drag callbacks to LinkedListPuzzleUI which owns all state.
///
/// Prefab Setup (child of LLNodeBlock or LLHeadPointer):
///   - Image (circle or arrow icon — something visually grab-able)
///   - LLNextHandle (this script)
///   - The Image's RaycastTarget must be ON so Unity routes drag events here.
///
/// The handle is placed on the RIGHT edge of its parent box so the arrow
/// visually "leaves" the node from the correct side.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class LLNextHandle : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // ── Wired at runtime by Init() ─────────────────────────────────────────
    private LLNodeBlock        _ownerNode; // null when isHead == true
    private bool               _isHead;
    private LinkedListPuzzleUI _puzzle;

    // ── Initialisation ─────────────────────────────────────────────────────

    /// <summary>
    /// Called by LLNodeBlock.Init or LLHeadPointer.Init immediately after spawn.
    /// </summary>
    /// <param name="ownerNode">The node this handle belongs to (null for HEAD).</param>
    /// <param name="puzzle">The master controller that will receive drag events.</param>
    /// <param name="isHead">True when this handle belongs to the HEAD pointer box.</param>
    public void Init(LLNodeBlock ownerNode, LinkedListPuzzleUI puzzle, bool isHead)
    {
        _ownerNode = ownerNode;
        _puzzle    = puzzle;
        _isHead    = isHead;
    }

    // ── IBeginDragHandler / IDragHandler / IEndDragHandler ─────────────────

    public void OnBeginDrag(PointerEventData e)
    {
        if (_isHead)
            _puzzle.OnHeadHandleDragBegin(e);
        else
            _puzzle.OnNodeHandleDragBegin(_ownerNode, e);
    }

    public void OnDrag(PointerEventData e)
    {
        // Same for both node and HEAD handles — just moves the floating end.
        _puzzle.OnHandleDragging(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_isHead)
            _puzzle.OnHeadHandleDragEnd(e);
        else
            _puzzle.OnNodeHandleDragEnd(_ownerNode, e);
    }
}

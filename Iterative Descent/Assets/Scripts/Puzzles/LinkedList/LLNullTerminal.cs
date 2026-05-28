using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The NULL terminal -- a fixed drop/click target on the far right of the puzzle panel
/// representing the end of the linked list (next = null).
///
/// In the click-based interaction model the player clicks this terminal
/// to set a node's next pointer to NULL.
///
/// Prefab Setup:
///   Root: Image (dark box) + LLNullTerminal
///   The Image's RaycastTarget must be ON.
///   Optional TextMeshProUGUI child -- set text to "NULL_PTR" or "0xFFFF" in terminal style.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLNullTerminal : MonoBehaviour, IPointerClickHandler
{
    private RectTransform      _rect;
    private LinkedListPuzzleUI _puzzle;

    void Awake() => _rect = GetComponent<RectTransform>();

    public RectTransform TerminalRect => _rect;

    /// <summary>Called by LinkedListPuzzleUI after Instantiate to wire the click callback.</summary>
    public void Init(LinkedListPuzzleUI puzzle)
    {
        _puzzle = puzzle;
    }

    /// <summary>Called by LinkedListPuzzleUI to place the terminal in the panel.</summary>
    public void SetPosition(Vector2 anchoredPos)
    {
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = anchoredPos;
    }

    // ── IPointerClickHandler ──────────────────────────────────────────────────
    /// <summary>
    /// Tells the puzzle to terminate the pending connection here (set next = null).
    /// Has no effect when no connection is pending.
    /// </summary>
    public void OnPointerClick(PointerEventData e)
    {
        _puzzle?.OnNullTerminalClicked();
    }
}

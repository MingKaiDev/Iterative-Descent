using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The NULL terminal — a fixed drop target on the far right of the puzzle panel
/// representing the end of the linked list (next = null).
///
/// It has no drag logic.  LinkedListPuzzleUI detects it via RaycastAll +
/// GetComponentInParent&lt;LLNullTerminal&gt;() and uses it as the target for the
/// last node's "next" arrow.
///
/// Prefab Setup:
///   Root: Image (dimmed box with "NULL" label) + LLNullTerminal
///   The Image's RaycastTarget must be ON so hit-testing picks it up.
///   Optionally add a TextMeshProUGUI child displaying "NULL".
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LLNullTerminal : MonoBehaviour
{
    private RectTransform _rect;

    void Awake() => _rect = GetComponent<RectTransform>();

    public RectTransform TerminalRect => _rect;

    /// <summary>Called by LinkedListPuzzleUI to position this terminal.</summary>
    public void SetPosition(Vector2 anchoredPos)
    {
        _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = anchoredPos;
    }
}

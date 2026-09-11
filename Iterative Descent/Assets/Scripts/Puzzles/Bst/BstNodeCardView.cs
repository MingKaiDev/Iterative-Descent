using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One shelved card in the BST "Build the Catalog" puzzle -- represents a
/// single call-number card that has already been inserted into the growing
/// tree diagram.
///
/// Unlike LLNodeBlock (linked-list puzzle), this node is never clicked
/// directly -- all placement input goes through BstPuzzleUI's Lower/Higher
/// branch buttons, which the puzzle repositions each step to sit at whichever
/// node is the current walk cursor. This view only needs to display its
/// value, hold its Left/Right children (mirrors LLNodeBlock's NextNode
/// field), and show a highlight while it's the cursor.
///
/// Prefab Setup:
///   Root: Image (catalog_card.png, Simple, preserve aspect) + BstNodeCardView
///   Children:
///     "ValueText" -- TextMeshProUGUI showing "No. 452" style call number
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class BstNodeCardView : MonoBehaviour
{
    // ── Public State ──────────────────────────────────────────────────────────
    public int Value { get; private set; }
    public int Depth { get; private set; }

    /// <summary>Left (smaller-value) child. Null = empty slot, matches LLNodeBlock's NextNode idiom.</summary>
    public BstNodeCardView Left  { get; set; }
    /// <summary>Right (larger-value) child. Null = empty slot.</summary>
    public BstNodeCardView Right { get; set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Image           background;   // root Image, catalog_card.png

    [Header("Highlight Tint")]
    [Tooltip("Multiplied over catalog_card.png -- white = untouched card colour.")]
    [SerializeField] private Color defaultTint = Color.white;
    [Tooltip("Warm brass tint while this card is the active walk cursor.")]
    [SerializeField] private Color cursorTint  = new Color(1f, 0.86f, 0.55f, 1f);

    // ── Accessors ─────────────────────────────────────────────────────────────
    public RectTransform Rect => (RectTransform)transform;

    // ── Initialisation ────────────────────────────────────────────────────────
    /// <param name="value">The call number shown on the card.</param>
    /// <param name="depth">Depth in the tree -- 0 = root. Used to size child branch spread.</param>
    /// <param name="anchoredPos">Position in nodeContainer local space.</param>
    public void Init(int value, int depth, Vector2 anchoredPos)
    {
        Value = value;
        Depth = depth;
        Left  = null;
        Right = null;

        if (valueText)  valueText.text  = "No. " + value;
        if (background) background.color = defaultTint;

        Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
        Rect.anchoredPosition = anchoredPos;
    }

    // ── Visual State ──────────────────────────────────────────────────────────
    /// <summary>Highlights this card as the current walk cursor (or clears the highlight).</summary>
    public void SetCursor(bool isCursor)
    {
        if (background) background.color = isCursor ? cursorTint : defaultTint;
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Generic pop-up notification card shared by all puzzles.
///
/// Shows a large ✗ / ✓ icon, a message body, and an OK button.
/// A full-panel transparent blocker sits behind the card so all puzzle
/// interaction is suspended while the pop-up is visible.
///
/// HIERARCHY (last child of the puzzle panel so it renders on top)
/// ──────────────────────────────────────────────────────────────────
///   [FeedbackPopup]         (this script — starts INACTIVE, full-stretch anchor)
///   ├── Blocker             Image, full-stretch, alpha=0, Raycast Target ON
///   └── PopupCard           Image (dark panel), anchored centre ~420 × 185
///       ├── IconText        TextMeshProUGUI — large ✗ or ✓, top-centre
///       ├── MessageText     TextMeshProUGUI — body copy, centre, word-wrap
///       └── OkButton        Button — "OK" label, bottom-centre
///
/// USAGE
/// ─────
///   popup.Show(false, "Wrong answer — try again!");
///   popup.Show(false, "Wrong answer.\nTarget: 5 → 12 → 7 → 3 → NULL");
///   popup.Show(true,  "Correct!\nList reversed.", onDismiss: ClosePanel);
///
/// Each puzzle has a thin named subclass (e.g. SchedulingFeedbackPopup,
/// LinkedListFeedbackPopup) whose only job is to preserve existing
/// Inspector component references in the Unity scene.
/// </summary>
public class PuzzleFeedbackPopup : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Card elements")]
    [SerializeField] private TextMeshProUGUI iconText;    // ✗ or ✓
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button          okButton;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Action _onDismiss;

    private static readonly Color ColourSuccess = new(0.20f, 0.85f, 0.20f);
    private static readonly Color ColourFail    = new(1.00f, 0.35f, 0.35f);

    // ── Unity ─────────────────────────────────────────────────────────────────
    protected virtual void Awake()    => gameObject.SetActive(false);
    private void OnEnable()           => okButton?.onClick.AddListener(Dismiss);
    private void OnDisable()          => okButton?.onClick.RemoveListener(Dismiss);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Display the pop-up.
    /// </summary>
    /// <param name="success">true = ✓ green (correct).  false = ✗ red (wrong).</param>
    /// <param name="message">Body text shown below the icon.</param>
    /// <param name="onDismiss">
    ///   Called when the player presses OK.
    ///   Pass ClosePanel here for the success case so the puzzle closes after
    ///   the player acknowledges the result.
    /// </param>
    public void Show(bool success, string message, Action onDismiss = null)
    {
        _onDismiss = onDismiss;

        if (iconText)
        {
            iconText.text  = success ? "✓" : "✗";
            iconText.color = success ? ColourSuccess : ColourFail;
        }

        if (messageText)
            messageText.text = message;

        gameObject.SetActive(true);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Dismiss()
    {
        gameObject.SetActive(false);
        _onDismiss?.Invoke();
    }
}

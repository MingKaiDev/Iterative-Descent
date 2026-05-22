using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Pop-up notification card for the CPU Scheduling puzzle.
///
/// Shows a large ✗ / ✓ icon, a message, and an OK button.
/// A full-panel transparent blocker sits behind the card so all interaction
/// with the puzzle is suspended while the pop-up is visible — the player
/// must press OK before continuing.
///
/// HIERARCHY (child of Scheduling Puzzle Panel — must be the LAST child so it renders on top)
/// ─────────────────────────────────────────────────────────────────────────────────────────
///   FeedbackPopup          (this script — starts INACTIVE, full-stretch anchor)
///   ├── Blocker            Image, full-stretch, alpha=0, Raycast Target ON
///   └── PopupCard          Image (dark panel), anchored centre, ~420 × 185
///       ├── IconText       TextMeshProUGUI — large ✗ or ✓, top-centre
///       ├── MessageText    TextMeshProUGUI — body copy, centre, word-wrap
///       └── OkButton       Button — "OK" label, bottom-centre
///
/// USAGE
/// ─────
///   // Wrong attempt (no hint yet):
///   feedbackPopup.Show(false, "Wrong answer — try again!");
///
///   // Wrong attempt (show answer after threshold):
///   feedbackPopup.Show(false, "Wrong answer — try again!\nCorrect RR:  P1 → P2 → …");
///
///   // Correct — close puzzle on dismiss:
///   feedbackPopup.Show(true, "Correct!\nGantt: P1[0–2] → P2[2–5] → …", onDismiss: ClosePanel);
///
/// See SchedulingPuzzle_UnitySetup.md §11 for full Inspector wiring.
/// </summary>
public class SchedulingFeedbackPopup : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Card elements")]
    [SerializeField] private TextMeshProUGUI iconText;    // shows ✗ or ✓
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button          okButton;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Action _onDismiss;

    // Cached colours
    private static readonly Color ColourSuccess = new(0.20f, 0.85f, 0.20f);
    private static readonly Color ColourFail    = new(1.00f, 0.35f, 0.35f);

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake()     => gameObject.SetActive(false);
    private void OnEnable()  => okButton?.onClick.AddListener(Dismiss);
    private void OnDisable() => okButton?.onClick.RemoveListener(Dismiss);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Display the pop-up.
    /// </summary>
    /// <param name="success">
    ///   true  → ✓ icon in green (correct answer).
    ///   false → ✗ icon in red  (wrong answer).
    /// </param>
    /// <param name="message">Body text shown below the icon.</param>
    /// <param name="onDismiss">
    ///   Optional callback invoked when the player presses OK.
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

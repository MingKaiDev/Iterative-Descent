using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drag-and-match puzzle: player pairs networking protocol names to their default port numbers.
///
/// PAIRS (fixed, no DDA scaling -- this is a reward terminal, not a gate):
///   HTTP=80, HTTPS=443, SSH=22, FTP=21, DNS=53, SMTP=25, Telnet=23, POP3=110
///
/// On solve: fires OnMatchingSolved(int wrongSubmissions), updates BKT via
///   PlayerMetricsTracker.
///
/// ── HIERARCHY ─────────────────────────────────────────────────────────────
///
///   MatchingPanel              (Canvas child; inactive by default)
///   ├── Background             Image, dark panel, stretch-fill
///   │   ├── TitleText          TMP  "NETWORK PORT DIAGNOSTIC"
///   │   ├── SubText            TMP  "Match each protocol to its default port"
///   │   ├── PuzzleArea         HorizontalLayoutGroup, spacing 40
///   │   │   ├── TermPool       VerticalLayoutGroup, spacing 8   [left column]
///   │   │   │   ├── TermCard_0 ... TermCard_7  (MatchingTermCard + Image + TMP child)
///   │   │   └── PortColumn     VerticalLayoutGroup, spacing 8   [right column]
///   │   │       ├── PortSlot_0 ... PortSlot_7  (MatchingPortSlot + Image + 2x TMP children)
///   │   ├── SubmitButton       Button + TMP "SUBMIT"
///   │   └── CloseButton        Button + TMP "CLOSE"
///   └── FeedbackPopup          MatchingFeedbackPopup (starts ACTIVE in Inspector)
///
/// ── INSPECTOR WIRING ──────────────────────────────────────────────────────
///   termCards[0..7]  -- drag TermCard_0..7 here (ORDER MUST MATCH SIBLING ORDER)
///   portSlots[0..7]  -- drag PortSlot_0..7 here
///   submitButton     -- SubmitButton
///   closeButton      -- CloseButton
///   feedbackPopup    -- FeedbackPopup
///
/// NOTE: The Canvas that owns this panel needs a GraphicRaycaster component
///   and an EventSystem in the scene for drag events to work.
/// </summary>
public class MatchingPuzzleUI : MonoBehaviour
{
    // ── Static Event ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player correctly matches all pairs.
    /// Argument: number of wrong submissions before the correct solve.
    /// </summary>
    public static event Action<int> OnMatchingSolved;

    // ── Port Pairs (fixed -- no DDA tier scaling) ─────────────────────────────

    private static readonly (string term, int port)[] AllPairs =
    {
        ("HTTP",   80),
        ("HTTPS",  443),
        ("SSH",    22),
        ("FTP",    21),
        ("DNS",    53),
        ("SMTP",   25),
        ("Telnet", 23),
        ("POP3",   110),
    };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Cards (8) -- drag TermCard_0..7 in order")]
    [SerializeField] private MatchingTermCard[] termCards;

    [Header("Slots (8) -- drag PortSlot_0..7 in order")]
    [SerializeField] private MatchingPortSlot[] portSlots;

    [Header("Buttons")]
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;

    [Header("Feedback")]
    [SerializeField] private MatchingFeedbackPopup feedbackPopup;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Action _onClose;
    private int    _wrongSubmissions;
    private bool   _solved;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);
        if (closeButton  != null) closeButton.onClick.AddListener(OnClosePressed);
    }

    private void OnDisable()
    {
        if (submitButton != null) submitButton.onClick.RemoveListener(OnSubmit);
        if (closeButton  != null) closeButton.onClick.RemoveListener(OnClosePressed);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call from MatchingPuzzleProp.OpenPuzzle().
    /// Shuffles terms and ports independently, resets state.
    /// </summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose          = onClose;
        _wrongSubmissions = 0;
        _solved           = false;

        // Hide feedback popup
        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        // Shuffle term names and port numbers independently so the visual
        // presentation is randomised but answers never give themselves away.
        var terms = new List<string>(AllPairs.Length);
        var ports = new List<int>(AllPairs.Length);
        foreach (var p in AllPairs) { terms.Add(p.term); ports.Add(p.port); }
        Shuffle(terms);
        Shuffle(ports);

        // Setup cards
        for (int i = 0; i < termCards.Length; i++)
            termCards[i].SetupCard(terms[i]);

        // Setup slots
        for (int i = 0; i < portSlots.Length; i++)
            portSlots[i].SetupSlot(ports[i]);

        // Notify metrics tracker for timing
        PlayerMetricsTracker.Instance?.NotifyMatchingStarted();
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private void OnSubmit()
    {
        if (_solved) return;

        // Require all slots to be filled before checking
        foreach (var slot in portSlots)
        {
            if (slot.OccupiedCard == null)
            {
                feedbackPopup?.Show(false, "Assign a protocol to every port before submitting.");
                return;
            }
        }

        // Evaluate each slot
        ResetHighlights();
        int wrongCount = 0;

        foreach (var slot in portSlots)
        {
            bool correct = IsCorrectPair(slot.OccupiedCard.TermName, slot.PortNumber);
            slot.SetHighlight(correct);
            if (!correct) wrongCount++;
        }

        if (wrongCount == 0)
        {
            _solved = true;
            feedbackPopup?.Show(
                true,
                "All protocols matched correctly!\n" +
                "Ammo and medical supplies dispensed.",
                onDismiss: OnSolvedDismissed);
        }
        else
        {
            _wrongSubmissions++;
            string msg = _wrongSubmissions >= 3
                ? $"{wrongCount} incorrect pair(s).\n" +
                  "Red slots need correcting.\n" +
                  "Hint: check the whiteboard."
                : $"{wrongCount} incorrect pair(s).\n" +
                  "Red-highlighted slots need correcting.";

            feedbackPopup?.Show(false, msg);
        }
    }

    // ── Close / Solved ────────────────────────────────────────────────────────

    private void OnSolvedDismissed()
    {
        OnMatchingSolved?.Invoke(_wrongSubmissions);
        DoClose();
    }

    private void OnClosePressed() => DoClose();

    private void DoClose() => _onClose?.Invoke();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsCorrectPair(string term, int port)
    {
        foreach (var p in AllPairs)
            if (p.term == term && p.port == port) return true;
        return false;
    }

    private void ResetHighlights()
    {
        foreach (var slot in portSlots)
            slot.ResetHighlight();
    }

    private static void Shuffle<T>(List<T> list)
    {
        // Fisher-Yates
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

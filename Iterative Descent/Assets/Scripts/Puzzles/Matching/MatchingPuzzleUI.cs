using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Drag-and-match puzzle: player pairs networking protocol names to their default port numbers.
///
/// PAIRS (fixed pool of 8; DDA TIER controls how many are in play):
///   HTTP=80, HTTPS=443, SSH=22, FTP=21, DNS=53, SMTP=25, Telnet=23, POP3=110
///   Tier 0 = 4 pairs ... Tier 4 = 8 pairs (see PairsPerTier). Low tier = fewer,
///   easier pairs on screen; top tier maxes out at the full pool of 8.
///
/// On solve: fires OnMatchingSolved(int wrongSubmissions), updates BKT via
///   PlayerMetricsTracker. Wrong submissions also feed PuzzleDDAController's
///   general puzzle pool the same way every other puzzle type does -- more
///   wrong submissions before solving pulls the DDA score (and tier) down.
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
///   sfxAudioSource / pickupSound / dropSound / sfxVolume -- audio (see Audio header)
///
/// NOTE: The Canvas that owns this panel needs a GraphicRaycaster component
///   and an EventSystem in the scene for drag events to work.
/// </summary>
public class MatchingPuzzleUI : MonoBehaviour
{
    // Singleton set in Awake() -- same cross-scene-safe pattern as every other
    // puzzle UI in this project (see BstPuzzleUI.Instance). Lets
    // MatchingPuzzleProp resolve this shared Canvas panel at runtime instead
    // of relying only on a serialized Inspector reference, which goes stale
    // across a Level 1 -> Level 2 -> Level 1 scene reload.
    public static MatchingPuzzleUI Instance { get; private set; }

    // ── Static Event ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fired when the player correctly matches all pairs.
    /// Argument: number of wrong submissions before the correct solve.
    /// </summary>
    public static event Action<int> OnMatchingSolved;

    // ── Port Pairs (fixed pool; DDA tier picks the subset in play) ────────────

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

    // ── DDA Tier Scaling ────────────────────────────────────────────────────
    // Number of pairs in play this session, indexed by PuzzleDDAController's
    // CurrentTier (0-4). Low tier = fewer pairs on screen, top tier = the
    // full pool of 8 ("maxes out" -- never exceeds AllPairs.Length).
    private static readonly int[] PairsPerTier = { 4, 5, 6, 7, 8 };

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

    [Header("Audio")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Tooltip("Played when a protocol card is picked up (drag begins).")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("Played when a protocol card is dropped (drag ends -- on a slot or back to the pool).")]
    [SerializeField] private AudioClip dropSound;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Action _onClose;
    private int    _wrongSubmissions;
    private bool   _solved;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake() => Instance = this;

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

        // DDA tier decides how many of the 8 fixed pairs are in play this
        // session -- low tier = fewer/easier, top tier maxes out at all 8.
        int tier = PuzzleDDAController.Instance != null
            ? Mathf.Clamp(PuzzleDDAController.Instance.CurrentTier, 0, PairsPerTier.Length - 1)
            : 2;
        int pairCount = Mathf.Clamp(PairsPerTier[tier], 1, AllPairs.Length);

        // Pick which subset of the fixed pool is in play, then shuffle term
        // and port presentation order independently so answers never give
        // themselves away.
        var pairIndices = new List<int>(AllPairs.Length);
        for (int i = 0; i < AllPairs.Length; i++) pairIndices.Add(i);
        Shuffle(pairIndices);
        pairIndices = pairIndices.GetRange(0, pairCount);

        var terms = new List<string>(pairCount);
        var ports = new List<int>(pairCount);
        foreach (int idx in pairIndices) { terms.Add(AllPairs[idx].term); ports.Add(AllPairs[idx].port); }
        Shuffle(terms);
        Shuffle(ports);

        // Setup cards -- only the first pairCount are used this session; the
        // rest are hidden (the VerticalLayoutGroup re-packs around them).
        for (int i = 0; i < termCards.Length; i++)
        {
            bool inUse = i < pairCount;
            termCards[i].gameObject.SetActive(inUse);
            if (inUse) termCards[i].SetupCard(this, terms[i]);
        }

        // Setup slots
        for (int i = 0; i < portSlots.Length; i++)
        {
            bool inUse = i < pairCount;
            portSlots[i].gameObject.SetActive(inUse);
            if (inUse) portSlots[i].SetupSlot(ports[i]);
        }

        // Notify metrics tracker for timing
        PlayerMetricsTracker.Instance?.NotifyMatchingStarted();
    }

    // ── Submit ────────────────────────────────────────────────────────────────

    private void OnSubmit()
    {
        if (_solved) return;

        // Require all ACTIVE slots to be filled before checking (inactive
        // slots are pairs not in play this session, per DDA tier scaling)
        foreach (var slot in portSlots)
        {
            if (!slot.gameObject.activeInHierarchy) continue;
            if (slot.OccupiedCard == null)
            {
                feedbackPopup?.Show(false, "Assign a protocol to every port before submitting.");
                return;
            }
        }

        // Evaluate each active slot
        ResetHighlights();
        int wrongCount = 0;

        foreach (var slot in portSlots)
        {
            if (!slot.gameObject.activeInHierarchy) continue;
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

    // ── Audio ─────────────────────────────────────────────────────────────────

    /// <summary>Called by MatchingTermCard.OnBeginDrag.</summary>
    public void OnCardDragBegin(MatchingTermCard card, PointerEventData e) => PlaySfx(pickupSound);

    /// <summary>Called by MatchingTermCard.OnEndDrag.</summary>
    public void OnCardDragEnd(MatchingTermCard card, PointerEventData e) => PlaySfx(dropSound);

    private void PlaySfx(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null) sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }

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

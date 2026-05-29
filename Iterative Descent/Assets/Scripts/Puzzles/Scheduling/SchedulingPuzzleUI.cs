using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Master controller for the CPU Scheduling (Round Robin) drag-and-drop puzzle.
///
/// WHAT THE PLAYER DOES
/// ────────────────────
/// 4 processes are shown (P1–P4) with their Burst Times and a displayed Time Quantum Q.
/// A Gantt timeline of N slots is shown (T=0, T=Q, T=2Q, …), one slot per time-quantum step.
/// The pool holds exactly one draggable token per Gantt slot (shuffled).
/// The player drags tokens into the correct slot order to reconstruct the Round Robin schedule.
/// Press Submit to validate.  A match on every slot fires OnSchedulingSolved.
///
/// HOW RR IS COMPUTED
/// ──────────────────
/// All 4 processes arrive at T=0.  RR cycles through P0→P1→P2→P3 repeatedly.
/// Each process runs for min(remaining, Q) time units per turn.
/// One Gantt slot = one such turn.  Partial last-quanta still count as one slot.
///
/// DDA SCALING
/// ───────────
///   Tier 0 / 1  → Q=3 or 4,  BT range 2–6    (fewer slots, wider quantum — easier)
///   Tier 2      → Q=3,        BT range 3–9
///   Tier 3 / 4  → Q=2,        BT range 4–10   (more slots, tighter quantum — harder)
///   Slot count capped at 10 (retry loop regenerates if exceeded).
///   Guaranteed fallback: BT=[2,4,3,5] Q=2 → 8 slots.
///
/// INSPECTOR SETUP  (see SchedulingPuzzle_UnitySetup.md for full details)
/// ───────────────────────────────────────────────────────────────────────
///   cardPrefab        Prefab with SchedulingProcessCard + CanvasGroup
///   slotPrefab        Prefab with SchedulingSlot
///   ganttBarPrefab    Not used in RR; field kept to avoid missing-reference warnings
///   cardPool          RectTransform — MUST use Grid Layout Group (4 cols) instead of VLG
///   slotRow           RectTransform — Horizontal Layout Group, spacing 8
///   feedbackPopup     SchedulingFeedbackPopup child panel (starts inactive)
///   instructionText   TMP for Q + process table header
///   submitButton      Wired in OnEnable; calls CheckSolution
///   closeButton       Wired in OnEnable; calls ClosePanel
///   dragLayer         Root Canvas RectTransform — cards reparented here while dragged
///
/// NOTES
/// ─────
///   • Time.timeScale = 0 while open (set by SchedulingPuzzleProp).
///     All timing uses Time.realtimeSinceStartup (handled by PlayerMetricsTracker).
///   • Wrong submissions do NOT reset the board.
///   • OnSchedulingSolved fires with the wrong-submission count (0 = first try).
/// </summary>
public class SchedulingPuzzleUI : MonoBehaviour
{
    // ── Singleton & Static Event ───────────────────────────────────────────────
    public static SchedulingPuzzleUI Instance { get; private set; }

    /// <summary>Fired when the player correctly fills the full RR Gantt.
    /// Argument = wrong-submission count (0 = solved on first try).</summary>
    public static event Action<int> OnSchedulingSolved;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private GameObject ganttBarPrefab; // unused in RR; kept to avoid null warnings

    [Header("References")]
    [SerializeField] private RectTransform          cardPool;       // Grid Layout Group (4 cols)
    [SerializeField] private RectTransform          slotRow;        // Horizontal Layout Group, spacing 8
    [SerializeField] private SchedulingFeedbackPopup feedbackPopup;
    [SerializeField] private TextMeshProUGUI         instructionText;
    [SerializeField] private Button          submitButton;
    [SerializeField] private Button          closeButton;

    [Header("HMI Token Sprites")]
    [SerializeField] private Sprite tokenSpriteLock;
    [SerializeField] private Sprite tokenSpriteCam;
    [SerializeField] private Sprite tokenSpriteVent;
    [SerializeField] private Sprite tokenSpriteAlarm;

    [Header("HMI Slot Sprites")]
    [SerializeField] private Sprite slotSpriteEmpty;
    [SerializeField] private Sprite slotSpriteLock;
    [SerializeField] private Sprite slotSpriteCam;
    [SerializeField] private Sprite slotSpriteVent;
    [SerializeField] private Sprite slotSpriteAlarm;

    [Header("Drag")]
    [Tooltip("Root RectTransform of the Canvas. Cards are reparented here while dragged.")]
    [SerializeField] private RectTransform dragLayer;

    // ── Public Accessors (used by SchedulingProcessCard during drag) ───────────
    public RectTransform DragLayer   => dragLayer;
    public float         CanvasScale => dragLayer.lossyScale.x;

    // ── Runtime State ─────────────────────────────────────────────────────────
    private readonly List<SchedulingProcessCard> _cards     = new();
    private readonly List<SchedulingSlot>        _slots     = new();
    private readonly List<Image>                 _ganttBars = new(); // empty in RR

    /// <summary>_solution[i] = process index (0–3) that belongs in Gantt slot i.</summary>
    private int[]  _solution;
    private int    _attempts;
    private Action _onClose;

    // Round Robin parameters for the current puzzle instance
    private int   _quantum;
    private int[] _burstTimes;

    // ── Process Names — HMI facility system labels ────────────────────────────
    private static readonly string[] ProcessNames =
    {
        "LOCK_SYS",   // index 0
        "CAM_ARRAY",  // index 1
        "VENT_CTRL",  // index 2
        "ALARM_NET",  // index 3
    };

    // ── Process Colours — stable, one per process index (0–3) ─────────────────
    private static readonly Color[] ProcessColors =
    {
        new(0.94f, 0.62f, 0.15f), // amber  — LOCK_SYS
        new(0.11f, 0.62f, 0.46f), // teal   — CAM_ARRAY
        new(0.50f, 0.46f, 0.87f), // purple — VENT_CTRL
        new(0.85f, 0.35f, 0.19f), // coral  — ALARM_NET
    };

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake() => Instance = this;

    private void OnEnable()
    {
        if (submitButton) submitButton.onClick.AddListener(CheckSolution);
        if (closeButton)  closeButton.onClick .AddListener(ClosePanel);
    }

    private void OnDisable()
    {
        if (submitButton) submitButton.onClick.RemoveListener(CheckSolution);
        if (closeButton)  closeButton.onClick .RemoveListener(ClosePanel);
    }

    // ── Public Entry Point ────────────────────────────────────────────────────

    /// <summary>
    /// Called by SchedulingPuzzleProp to open the puzzle.
    /// onClose is invoked when the panel should hide (success or manual close).
    /// </summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose  = onClose;
        _attempts = 0;

        // HMI: override submit button label each session to survive any Unity UI resets
        if (submitButton)
        {
            var lbl = submitButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl) lbl.text = "[ INJECT OVERRIDE ]";
        }

        // Ensure the popup is hidden at the start of every session.
        // Mirrors the same guard in LinkedListPuzzleUI — see PuzzleFeedbackPopup.Awake()
        // comment for why this is necessary instead of relying on Awake.
        if (feedbackPopup != null) feedbackPopup.gameObject.SetActive(false);

        // Notify metrics — timer starts here.
        // Time.timeScale is already 0 at this point; tracker uses realtimeSinceStartup.
        PlayerMetricsTracker.Instance?.NotifySchedulingStarted();

        GeneratePuzzle();
    }

    // ── Puzzle Generation ─────────────────────────────────────────────────────

    private void GeneratePuzzle()
    {
        ClearAll();

        // ── Generate RR parameters ──────────────────────────────────────────
        (_burstTimes, _quantum) = GenerateRRParameters();
        _solution = ComputeRRSchedule(_burstTimes, _quantum);
        int slotCount = _solution.Length;

        // ── Instruction text ────────────────────────────────────────────────
        if (instructionText)
        {
            var procList = string.Join("   ", Enumerable.Range(0, _burstTimes.Length)
                .Select(i =>
                    $"<color=#{ColorUtility.ToHtmlStringRGB(ProcessColors[i])}>" +
                    $"[{ProcessNames[i]}]</color> BT={_burstTimes[i]}"));

            instructionText.text =
                $"ARBITEX PROCESS TABLE -- Q={_quantum} -- ALL ARRIVE T=0\n" +
                $"{procList}\n" +
                "Drag process tokens into the correct time slots.";
        }

        // ── Compute dynamic slot width ──────────────────────────────────────
        // SlotRow is 760 px wide; HLG spacing between children is 8 px.
        const float rowWidth    = 760f;
        const float hlgSpacing  = 8f;
        float slotWidth = Mathf.Max(44f, (rowWidth - (slotCount - 1) * hlgSpacing) / slotCount);
        float slotHeight = 90f; // matches the prefab's authored height

        // Token size matches slot width so the drag preview aligns naturally with slots
        var tokenSize = new Vector2(slotWidth, slotHeight);

        // ── Spawn tokens in CardPool (one per Gantt slot, shuffled) ────────
        int[] shuffledSolution = _solution.ToArray();
        ShuffleArray(shuffledSolution);

        // Token sprite lookup — indexed by process (0=LOCK,1=CAM,2=VENT,3=ALARM)
        Sprite[] tokenSprites = { tokenSpriteLock, tokenSpriteCam, tokenSpriteVent, tokenSpriteAlarm };

        for (int i = 0; i < slotCount; i++)
        {
            int pIdx = shuffledSolution[i];
            var go   = Instantiate(cardPrefab, cardPool);
            var card = go.GetComponent<SchedulingProcessCard>();
            card.Init(
                processIndex: pIdx,
                name:         ProcessNames[pIdx],
                burstTime:    _burstTimes[pIdx],
                arrivalTime:  0,
                color:        ProcessColors[pIdx],
                master:       this,
                poolParent:   cardPool,
                sizeOverride: tokenSize);

            // HMI: swap root Image sprite to the process-specific token sprite
            if (tokenSprites[pIdx] != null)
            {
                var img = go.GetComponent<Image>();
                if (img) { img.sprite = tokenSprites[pIdx]; img.type = Image.Type.Sliced; img.color = Color.white; }
            }

            _cards.Add(card);
        }

        // ── Spawn Gantt slots with accurate time labels ─────────────────────
        int[] remaining = (int[])_burstTimes.Clone();
        int   time      = 0;

        for (int i = 0; i < slotCount; i++)
        {
            int pIdx     = _solution[i];
            int duration = Mathf.Min(remaining[pIdx], _quantum);

            var go   = Instantiate(slotPrefab, slotRow);
            var slot = go.GetComponent<SchedulingSlot>();
            slot.Init(i, $"T={time}", this, slotSpriteEmpty);

            // Override the prefab's fixed width to fit all slots in the row
            var rt  = go.GetComponent<RectTransform>();
            var sle = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            sle.minWidth       = slotWidth;
            sle.preferredWidth = slotWidth;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, slotWidth);

            _slots.Add(slot);
            remaining[pIdx] -= duration;
            time             += duration;
        }
    }

    // ── Drag Callbacks (called by SchedulingProcessCard) ──────────────────────

    public void OnCardDragBegin(SchedulingProcessCard card, PointerEventData e) { }

    /// <summary>
    /// Called from SchedulingProcessCard.OnEndDrag().
    /// IDropHandler.OnDrop fires BEFORE OnEndDrag, so card.CurrentSlot is already
    /// set if the card landed on a slot successfully.
    /// </summary>
    public void OnCardDragEnd(SchedulingProcessCard card, PointerEventData e)
    {
        if (card.CurrentSlot == null)
            card.ReturnToPool(); // missed all slots → back to pool
    }

    /// <summary>
    /// Called from SchedulingSlot.OnDrop().
    /// If the target slot is already occupied, the displaced card returns to pool.
    /// </summary>
    public void OnCardDroppedOnSlot(SchedulingProcessCard card, SchedulingSlot slot)
    {
        if (slot.IsOccupied)
        {
            SchedulingProcessCard displaced = slot.HeldCard;
            slot.RemoveCard(); // resets slot to empty HMI sprite via RemoveCard -> ApplyHmiSprite
            displaced.ReturnToPool();
        }
        slot.PlaceCard(card);
        // HMI: apply the filled process sprite for this slot
        slot.ApplyHmiSprite(GetSlotSprite(card.ProcessIndex));
    }

    /// <summary>Returns the HMI filled-slot sprite matching the given process index.</summary>
    private Sprite GetSlotSprite(int processIndex) => processIndex switch
    {
        0 => slotSpriteLock,
        1 => slotSpriteCam,
        2 => slotSpriteVent,
        3 => slotSpriteAlarm,
        _ => slotSpriteEmpty,
    };

    // ── Solution Validation ───────────────────────────────────────────────────

    public void CheckSolution()
    {
        // Guard: all slots must be filled before submitting
        if (_slots.Any(s => !s.IsOccupied))
        {
            feedbackPopup?.SetWarningSprite(SchedulingFeedbackPopup.WarningState.Amber);
            feedbackPopup?.Show(false, "SUBMISSION REJECTED -- FILL ALL TIME SLOTS");
            return;
        }

        // Check every slot: placed card's ProcessIndex must match the RR solution
        bool correct = true;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].HeldCard.ProcessIndex != _solution[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            OnSchedulingSolved?.Invoke(_attempts);
            feedbackPopup?.SetWarningSprite(SchedulingFeedbackPopup.WarningState.Ok);
            feedbackPopup?.Show(true, "OVERRIDE ACCEPTED -- LOCK_SYS DISABLED", onDismiss: ClosePanel);
        }
        else
        {
            _attempts++;

            if (_attempts > 2)
            {
                // Lockout: reveal correct sequence
                string exp = string.Join(" -> ", _solution.Select(i => ProcessNames[i]));
                feedbackPopup?.SetWarningSprite(SchedulingFeedbackPopup.WarningState.Red);
                feedbackPopup?.Show(false,
                    $"LOCKOUT INITIATED -- DISPLAYING AUTHORISED SEQUENCE\n{exp}");
            }
            else if (_attempts == 2)
            {
                feedbackPopup?.SetWarningSprite(SchedulingFeedbackPopup.WarningState.Amber);
                feedbackPopup?.Show(false, "OVERRIDE REJECTED -- FINAL ATTEMPT AUTHORISED");
            }
            else
            {
                feedbackPopup?.SetWarningSprite(SchedulingFeedbackPopup.WarningState.Amber);
                feedbackPopup?.Show(false, "OVERRIDE REJECTED -- RECHECK SEQUENCE");
            }
        }
    }

    public void ClosePanel() => _onClose?.Invoke();

    // ── Round Robin Core Logic ────────────────────────────────────────────────

    /// <summary>
    /// Simulates Round Robin and returns the ordered sequence of process indices,
    /// one entry per time-quantum turn.
    /// Processes cycle in ascending index order (P0 → P1 → P2 → P3) each round.
    /// A process is skipped once its remaining burst time reaches 0.
    /// </summary>
    private static int[] ComputeRRSchedule(int[] burstTimes, int quantum)
    {
        int   n         = burstTimes.Length;
        int[] remaining = (int[])burstTimes.Clone();
        var   schedule  = new List<int>();

        bool anyLeft = true;
        while (anyLeft)
        {
            anyLeft = false;
            for (int i = 0; i < n; i++)
            {
                if (remaining[i] <= 0) continue;
                anyLeft = true;
                schedule.Add(i);
                remaining[i] -= quantum;
                if (remaining[i] < 0) remaining[i] = 0;
            }
        }

        return schedule.ToArray();
    }

    // ── Tier / Generation Helpers ─────────────────────────────────────────────

    private (int[] burstTimes, int quantum) GenerateRRParameters()
    {
        int tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.CpuScheduling)
            : 2;
        const int maxSlots    = 10;
        const int maxAttempts = 30;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int   q         = QuantumForTier(tier);
            int[] bts       = GenerateBurstTimesRR(tier);
            int   slotCount = bts.Sum(bt => Mathf.CeilToInt((float)bt / q));

            if (slotCount <= maxSlots)
                return (bts, q);
        }

        // Safe fallback — guaranteed 8 slots: ceil(2/2)+ceil(4/2)+ceil(3/2)+ceil(5/2) = 1+2+2+3 = 8
        return (new[] { 2, 4, 3, 5 }, 2);
    }

    private static int QuantumForTier(int tier) => tier switch
    {
        0 or 1 => UnityEngine.Random.Range(3, 5), // Q=3 or Q=4 — fewer, more obvious slots
        2      => 3,                               // Q=3 — moderate
        3 or 4 => 2,                               // Q=2 — more slots, harder to trace
        _      => 3,
    };

    private static int[] GenerateBurstTimesRR(int tier)
    {
        // BT ranges are tuned to keep slot counts within maxSlots=10 for all tiers.
        // Tier3/4: BT=2-7 Q=2 has 65.8% single-draw success → 100% within 30 attempts.
        // BT=4+ with Q=2 is mathematically impossible to fit (minimum 4-combo gives 12 slots).
        (int min, int max) = tier switch
        {
            0 or 1 => (2, 6),  // small BTs + wide Q → ~4–6 slots
            2      => (2, 7),  // moderate BTs + Q=3 → ~6–8 slots
            3 or 4 => (2, 7),  // same range, Q=2 → ~8–10 slots (retry filters for valid combos)
            _      => (2, 7),
        };

        var used  = new HashSet<int>();
        int tries = 0;
        while (used.Count < 4 && tries++ < 60)
            used.Add(UnityEngine.Random.Range(min, max + 1));

        // Pad with sequential values if we couldn't get 4 distinct values
        int next = max + 1;
        while (used.Count < 4) used.Add(next++);

        return used.ToArray();
    }

    // ── Success Display ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a human-readable Gantt string:
    ///   "Gantt: P1[0–2] → P2[2–4] → P3[4–6] → …"
    /// Durations are computed from burst times and quantum (partial last quanta supported).
    /// </summary>
    private string BuildGanttString()
    {
        var   sb        = new StringBuilder("Gantt: ");
        int[] remaining = (int[])_burstTimes.Clone();
        int   time      = 0;

        for (int i = 0; i < _solution.Length; i++)
        {
            int p        = _solution[i];
            int duration = Mathf.Min(remaining[p], _quantum);
            if (i > 0) sb.Append(" -> ");
            sb.Append($"{ProcessNames[p]}[{time}-{time + duration}]");
            remaining[p] -= duration;
            time          += duration;
        }

        return sb.ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void ShuffleArray<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    private void ClearAll()
    {
        foreach (var c in _cards)     if (c) Destroy(c.gameObject);
        foreach (var s in _slots)     if (s) Destroy(s.gameObject);
        foreach (var b in _ganttBars) if (b) Destroy(b.gameObject);
        _cards.Clear();
        _slots.Clear();
        _ganttBars.Clear();
    }
}

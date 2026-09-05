using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// ARBITEX Power Distribution Unit -- Stack / Circuit Breaker Puzzle
///
/// PHASE STRUCTURE
/// ───────────────
///   Tier 0  (3 elements) : Phase 1 only  -- push/pop, no move limit
///   Tier 1  (4 elements) : Phase 1 only  -- push/pop, move limit
///   Tier 2  (5 elements) : Phase 1 + Phase 2  -- decimal values
///   Tier 3/4(6 elements) : Phase 1 + Phase 2  -- hex values, tight budget
///
/// PHASE 1 -- Circuit Bus Push/Pop
///   Target config is an ARBITRARY (non-sorted) sequence.
///   Junk pre-loaded on the stack must be popped before the correct
///   sequence can be built.  Token pool contains correct values + distractors.
///   Tier 0/1: AUTHORIZE completes the puzzle.
///   Tier 2+ : AUTHORIZE triggers the transition to Phase 2.
///
/// PHASE 2 -- Three-Bus LIFO Reconstruction  (tier 2+ only)
///   The Phase 1 target is scrambled across MAIN and AUX buses by running
///   ScrambleDepth random valid LIFO moves from the initial sorted state.
///   No ordering constraint -- any value may rest on any value.
///   Only rule: you may only move the TOS of any bus (pure LIFO).
///   Goal: reconstruct the Phase 1 target exactly on the RESTORE bus.
///   Move budget = ScrambleDepth * 2 (guaranteed solvable within budget).
///   On move exhaustion: wrong counter++ and Phase 2 regenerates (new scramble,
///   same target).
///
/// INSPECTOR SETUP
///   See StackPuzzle_UnitySetup.md for the full Canvas hierarchy,
///   prefab specs, and RectTransform/font settings.
/// </summary>
public class StackPuzzleUI : MonoBehaviour
{
    // ── Types ──────────────────────────────────────────────────────────────────

    private enum PuzzlePhase { Phase1, Transition, Phase2, Complete }

    /// <summary>Index maps to _buses array: Main=0, Aux=1, Restore=2.</summary>
    public enum BusId { Main = 0, Aux = 1, Restore = 2 }

    private enum CellState { Normal, TOS, Junk, Target, Lifted }

    // ── Singleton and Static Event ─────────────────────────────────────────────

    public static StackPuzzleUI Instance { get; private set; }

    /// <summary>
    /// Fired when both phases are complete (tier 2+) or Phase 1 is correct (tier 0/1).
    /// Argument = total wrong attempts accumulated across both phases.
    /// </summary>
    public static event Action<int> OnStackSolved;

    // ── Inspector: Header ──────────────────────────────────────────────────────

    [Header("Header UI")]
    [Tooltip("ARBITEX taunting line below the title bar.")]
    [SerializeField] private TextMeshProUGUI arbitexMessageText;

    [Tooltip("LOCKED / OVERRIDE ACCEPTED badge text.")]
    [SerializeField] private TextMeshProUGUI statusBadgeText;

    [Tooltip("Background Image of the status badge (color-coded).")]
    [SerializeField] private Image statusBadgeBg;

    [Tooltip("PHASE 1 / PHASE 2 / COMPLETE badge text.")]
    [SerializeField] private TextMeshProUGUI phaseBadgeText;

    [Tooltip("Background Image of the phase badge (color-coded).")]
    [SerializeField] private Image phaseBadgeBg;

    [Tooltip("Root GameObject of the phase badge -- hidden at tier 0 in Phase 1.")]
    [SerializeField] private GameObject phaseBadgeRoot;

    // ── Inspector: Phase 1 ────────────────────────────────────────────────────

    [Header("Phase 1")]
    [Tooltip("Root of the Phase 1 layout (token panel + bus + target). Toggled by SetPhase().")]
    [SerializeField] private GameObject phase1Section;

    [Tooltip("VerticalLayoutGroup parent for live stack cells. reverseArrangement=true so TOS is at top visually.")]
    [SerializeField] private RectTransform stackCellParent;

    [Tooltip("VerticalLayoutGroup parent for read-only target cells.")]
    [SerializeField] private RectTransform targetCellParent;

    [Tooltip("VerticalLayoutGroup or GridLayoutGroup parent for push-token buttons.")]
    [SerializeField] private RectTransform tokenButtonParent;

    [SerializeField] private Button popButton;
    [SerializeField] private Button submitButton;
    [SerializeField] private Button closeButton;

    [Tooltip("Shows remaining Phase 1 moves at tier 1+. Hide this object at tier 0.")]
    [SerializeField] private TextMeshProUGUI movesText;

    // ── Inspector: Transition ─────────────────────────────────────────────────

    [Header("Transition Screen")]
    [Tooltip("Root shown between phases. Contains transition body text and Proceed button.")]
    [SerializeField] private GameObject transitionSection;

    [Tooltip("Body text on the transition screen (ARBITEX message + target reminder).")]
    [SerializeField] private TextMeshProUGUI transitionBodyText;

    [SerializeField] private Button proceedButton;

    // ── Inspector: Phase 2 ────────────────────────────────────────────────────

    [Header("Phase 2")]
    [Tooltip("Root of the three-bus layout. Toggled by SetPhase().")]
    [SerializeField] private GameObject phase2Section;

    [Tooltip("VerticalLayoutGroup inside the MAIN bus panel.")]
    [SerializeField] private RectTransform mainBusContainer;

    [Tooltip("VerticalLayoutGroup inside the AUX bus panel.")]
    [SerializeField] private RectTransform auxBusContainer;

    [Tooltip("VerticalLayoutGroup inside the RESTORE bus panel.")]
    [SerializeField] private RectTransform restoreBusContainer;

    [Tooltip("Background Image of the MAIN bus panel (tinted when selected).")]
    [SerializeField] private Image mainBusPanelImage;

    [Tooltip("Background Image of the AUX bus panel.")]
    [SerializeField] private Image auxBusPanelImage;

    [Tooltip("Background Image of the RESTORE bus panel.")]
    [SerializeField] private Image restoreBusPanelImage;

    [Tooltip("Invisible full-panel Button overlaid on the MAIN bus for click detection.")]
    [SerializeField] private Button mainBusButton;

    [Tooltip("Invisible full-panel Button overlaid on the AUX bus.")]
    [SerializeField] private Button auxBusButton;

    [Tooltip("Invisible full-panel Button overlaid on the RESTORE bus.")]
    [SerializeField] private Button restoreBusButton;

    [Tooltip("Displays Phase 2 move counter.")]
    [SerializeField] private TextMeshProUGUI phase2MovesText;

    [Tooltip("Target reminder and interaction hint for Phase 2.")]
    [SerializeField] private TextMeshProUGUI phase2HintText;

    // ── Inspector: Shared ─────────────────────────────────────────────────────

    [Header("Shared")]
    [SerializeField] private StackFeedbackPopup feedbackPopup;

    [Header("Prefabs")]
    [Tooltip("Prefab: root Image + child TMP named 'ValueText' + child TMP named 'TagText'.")]
    [SerializeField] private GameObject breakerCellPrefab;

    [Tooltip("Prefab: root Button + child TMP named 'Label'.")]
    [SerializeField] private GameObject tokenButtonPrefab;

    // ── Inspector: Audio ──────────────────────────────────────────────────────

    [Header("Audio")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Tooltip("Played on a successful pop (breaker removed from the stack).")]
    [SerializeField] private AudioClip popSound;

    [Tooltip("Played when a breaker token is pushed onto the stack.")]
    [SerializeField] private AudioClip pushSound;

    [Tooltip("Played when a correct sequence is authorized (Phase 1 pass, Phase 2 reconstruction, or full solve).")]
    [SerializeField] private AudioClip authorizeSound;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    // ── Cell Background Colors ─────────────────────────────────────────────────
    // Industrial / circuit breaker palette -- dark theme

    private static readonly Color ColNormal   = new Color(0.18f, 0.20f, 0.22f); // dark slate
    private static readonly Color ColTOS      = new Color(0.50f, 0.32f, 0.05f); // amber
    private static readonly Color ColJunk     = new Color(0.42f, 0.08f, 0.08f); // dark red
    private static readonly Color ColTarget   = new Color(0.10f, 0.26f, 0.42f); // dark blue
    private static readonly Color ColLifted   = new Color(0.60f, 0.40f, 0.04f); // bright amber
    private static readonly Color ColEmpty    = new Color(0.10f, 0.11f, 0.12f, 0.50f);

    // Bus panel tints
    private static readonly Color BusNormal   = new Color(0.13f, 0.15f, 0.17f);
    private static readonly Color BusSelected = new Color(0.28f, 0.20f, 0.04f);
    private static readonly Color BusRestore  = new Color(0.07f, 0.20f, 0.09f);

    // Badge background colors
    private static readonly Color BadgeLocked = new Color(0.50f, 0.08f, 0.08f);
    private static readonly Color BadgePhase1 = new Color(0.48f, 0.28f, 0.04f);
    private static readonly Color BadgePhase2 = new Color(0.08f, 0.24f, 0.48f);
    private static readonly Color BadgeDone   = new Color(0.08f, 0.36f, 0.10f);

    // ── Runtime: Shared ───────────────────────────────────────────────────────

    private PuzzlePhase _phase;
    private int         _tier;
    private bool        _hexDisplay;
    private int         _totalWrongAttempts;
    private Action      _onClose;

    // ── Runtime: Phase 1 ──────────────────────────────────────────────────────

    private readonly List<int> _stack = new List<int>();
    private int[]   _target;
    private int[]   _availableTokens;
    private int     _capacity;
    private int     _p1MovesRemaining;
    private bool    _p1MoveLimitActive;

    // ── Runtime: Phase 2 ──────────────────────────────────────────────────────

    // Index matches BusId enum: [0]=Main [1]=Aux [2]=Restore
    private readonly List<int>[] _buses = { new List<int>(), new List<int>(), new List<int>() };
    private BusId?  _selectedBus;
    private int     _p2MovesRemaining;
    private int     _p2ScrambleDepth;

    // ── Cached delegates for AddListener / RemoveListener ─────────────────────

    private UnityEngine.Events.UnityAction _mainBusAction;
    private UnityEngine.Events.UnityAction _auxBusAction;
    private UnityEngine.Events.UnityAction _restoreBusAction;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        Instance = this;
        _mainBusAction    = () => OnBusClicked(BusId.Main);
        _auxBusAction     = () => OnBusClicked(BusId.Aux);
        _restoreBusAction = () => OnBusClicked(BusId.Restore);
    }

    private void OnEnable()
    {
        popButton?.onClick.AddListener(OnPopClicked);
        submitButton?.onClick.AddListener(OnSubmitClicked);
        closeButton?.onClick.AddListener(ClosePanel);
        proceedButton?.onClick.AddListener(OnProceedClicked);
        mainBusButton?.onClick.AddListener(_mainBusAction);
        auxBusButton?.onClick.AddListener(_auxBusAction);
        restoreBusButton?.onClick.AddListener(_restoreBusAction);
    }

    private void OnDisable()
    {
        popButton?.onClick.RemoveListener(OnPopClicked);
        submitButton?.onClick.RemoveListener(OnSubmitClicked);
        closeButton?.onClick.RemoveListener(ClosePanel);
        proceedButton?.onClick.RemoveListener(OnProceedClicked);
        mainBusButton?.onClick.RemoveListener(_mainBusAction);
        auxBusButton?.onClick.RemoveListener(_auxBusAction);
        restoreBusButton?.onClick.RemoveListener(_restoreBusAction);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by StackPuzzleProp to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose            = onClose;
        _totalWrongAttempts = 0;

        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        // Timer starts here. timeScale is already 0; tracker uses realtimeSinceStartup.
        PlayerMetricsTracker.Instance?.NotifyStackStarted();

        GeneratePhase1();
    }

    // =========================================================================
    // PHASE 1
    // =========================================================================

    private void GeneratePhase1()
    {
        ClearAll();
        SetPhase(PuzzlePhase.Phase1);

        _tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.StacksAndQueues)
            : 1;

        // ── Tier parameters ───────────────────────────────────────────────────
        int targetDepth;
        int junkCount;
        int moveBuffer;

        switch (_tier)
        {
            case 0:
                targetDepth = 3; junkCount = 1;
                _p1MoveLimitActive = false; _hexDisplay = false; moveBuffer = 0;
                break;
            case 1:
                targetDepth = 4; junkCount = 1;
                _p1MoveLimitActive = true;  _hexDisplay = false; moveBuffer = 2;
                break;
            case 2:
                targetDepth = 5; junkCount = 2;
                _p1MoveLimitActive = true;  _hexDisplay = false; moveBuffer = 2;
                break;
            default: // tier 3 and 4
                targetDepth = 6; junkCount = 2;
                _p1MoveLimitActive = true;  _hexDisplay = true;  moveBuffer = 1;
                break;
        }

        _capacity = targetDepth + 2;

        // ── Generate target (arbitrary -- NOT sorted) ─────────────────────────
        _target = GenerateUniqueValues(targetDepth, 10, 99);

        // ── Junk: values guaranteed outside target range ───────────────────────
        int[] junkValues = junkCount > 0
            ? GenerateUniqueValues(junkCount, 1, 9, _target)
            : Array.Empty<int>();

        // ── Seed the live stack with junk at the bottom ───────────────────────
        _stack.Clear();
        for (int i = 0; i < junkValues.Length; i++)
            _stack.Add(junkValues[i]);

        // ── Token pool: target values + distractors ───────────────────────────
        int distractorCount = _tier <= 1 ? 2 : 3;
        int[] distractors   = GenerateUniqueValues(distractorCount, 10, 99, _target);

        var tokens = new List<int>(_target);
        for (int i = 0; i < distractors.Length; i++)
            tokens.Add(distractors[i]);
        ShuffleList(tokens);
        _availableTokens = tokens.ToArray();

        // ── Move limit ────────────────────────────────────────────────────────
        int minMoves = targetDepth + junkCount;
        _p1MovesRemaining = _p1MoveLimitActive ? minMoves + moveBuffer : int.MaxValue;

        // ── Build UI ──────────────────────────────────────────────────────────
        bool hasPhase2 = _tier >= 2;

        SetStatusBadge("LOCKED", BadgeLocked);
        SetPhaseBadge("PHASE 1", BadgePhase1, hasPhase2);
        SetArbitexMsg(hasPhase2
            ? "[ARBITEX]: phase 1 of 2 -- build the correct load sequence on the circuit bus"
            : "[ARBITEX]: build the correct load sequence on the circuit bus to release the shutter");

        for (int i = 0; i < _availableTokens.Length; i++)
            SpawnTokenButton(_availableTokens[i]);

        for (int i = 0; i < _target.Length; i++)
            SpawnCell(targetCellParent, _target[i], CellState.Target, i == _target.Length - 1, i == 0);

        RefreshP1StackDisplay();
        RefreshP1MovesText();
    }

    // ── Phase 1: Interaction ──────────────────────────────────────────────────

    private void OnPushClicked(int value)
    {
        if (_phase != PuzzlePhase.Phase1) return;

        if (_stack.Count >= _capacity)
        {
            feedbackPopup?.Show(false, $"Stack overflow -- capacity is {_capacity}. Pop an element first.");
            return;
        }

        if (_p1MoveLimitActive && _p1MovesRemaining <= 0)
            return; // ConsumeP1Move already handled exhaustion

        _stack.Add(value);
        PlaySfx(pushSound);
        ConsumeP1Move();
        RefreshP1StackDisplay();
    }

    private void OnPopClicked()
    {
        if (_phase != PuzzlePhase.Phase1) return;

        if (_stack.Count == 0)
        {
            feedbackPopup?.Show(false, "Stack is empty -- nothing to pop.");
            return;
        }

        if (_p1MoveLimitActive && _p1MovesRemaining <= 0)
            return;

        _stack.RemoveAt(_stack.Count - 1);
        PlaySfx(popSound);
        ConsumeP1Move();
        RefreshP1StackDisplay();
    }

    private void OnSubmitClicked()
    {
        if (_phase != PuzzlePhase.Phase1) return;

        if (_stack.Count != _target.Length)
        {
            _totalWrongAttempts++;
            feedbackPopup?.Show(false,
                $"Wrong -- stack depth {_stack.Count}, target needs {_target.Length}. " +
                $"(Attempt {_totalWrongAttempts})");
            return;
        }

        bool correct = true;
        for (int i = 0; i < _target.Length; i++)
        {
            if (_stack[i] != _target[i]) { correct = false; break; }
        }

        if (!correct)
        {
            _totalWrongAttempts++;
            string hint = _totalWrongAttempts >= 3
                ? $"  Target (bottom to top): {BuildStackString(_target, false)}"
                : "";
            feedbackPopup?.Show(false,
                $"Wrong sequence. (Attempt {_totalWrongAttempts}){hint}");
            return;
        }

        // ── Phase 1 correct ───────────────────────────────────────────────────
        PlaySfx(authorizeSound);
        bool hasPhase2 = _tier >= 2;
        if (!hasPhase2)
        {
            OnStackSolved?.Invoke(_totalWrongAttempts);
            SetStatusBadge("OVERRIDE ACCEPTED", BadgeDone);
            SetPhaseBadge("COMPLETE", BadgeDone, true);
            SetArbitexMsg("[ARBITEX]: ...shutter disengaged. do not celebrate.");
            feedbackPopup?.Show(true, "Correct! Sequence accepted. Shutter released.", onDismiss: ClosePanel);
        }
        else
        {
            BeginTransition();
        }
    }

    private void ConsumeP1Move()
    {
        if (!_p1MoveLimitActive) return;

        _p1MovesRemaining--;
        RefreshP1MovesText();

        if (_p1MovesRemaining > 0) return;

        // Moves exhausted -- auto-check; if wrong, reset
        bool correct = (_stack.Count == _target.Length);
        if (correct)
        {
            for (int i = 0; i < _target.Length; i++)
                if (_stack[i] != _target[i]) { correct = false; break; }
        }

        if (correct)
        {
            OnSubmitClicked();
        }
        else
        {
            _totalWrongAttempts++;
            feedbackPopup?.Show(false,
                $"Moves exhausted -- sequence cannot be completed. Restarting phase 1. " +
                $"(Wrong attempts: {_totalWrongAttempts})",
                onDismiss: GeneratePhase1);
        }
    }

    // ── Phase 1: Display ──────────────────────────────────────────────────────

    private void RefreshP1StackDisplay()
    {
        ClearChildren(stackCellParent);

        if (_stack.Count == 0) return;

        var targetSet = new HashSet<int>(_target);

        for (int i = 0; i < _stack.Count; i++)
        {
            bool isTOS  = (i == _stack.Count - 1);
            bool isJunk = !targetSet.Contains(_stack[i]);

            CellState state = isJunk ? CellState.Junk
                            : isTOS  ? CellState.TOS
                            :          CellState.Normal;

            SpawnCell(stackCellParent, _stack[i], state, isTOS, i == 0);
        }
    }

    private void RefreshP1MovesText()
    {
        if (movesText == null) return;
        movesText.gameObject.SetActive(_p1MoveLimitActive);
        if (_p1MoveLimitActive)
            movesText.text = $"Moves remaining: {_p1MovesRemaining}";
    }

    // =========================================================================
    // TRANSITION
    // =========================================================================

    private void BeginTransition()
    {
        SetPhase(PuzzlePhase.Transition);
        SetStatusBadge("PHASE 1 DONE", BadgeDone);
        SetPhaseBadge("PHASE 1 DONE", BadgeDone, true);
        SetArbitexMsg("[ARBITEX]: sequence accepted. initiating bus reroute protocol.");

        if (transitionBodyText != null)
        {
            transitionBodyText.text =
                "PHASE 1 COMPLETE -- SEQUENCE ACCEPTED\n\n" +
                "[ARBITEX]: all breakers must be moved to the RESTORE bus.\n" +
                "You may only move the top breaker of any bus (LIFO).\n\n" +
                $"Target order (bottom to top): {BuildStackString(_target, false)}";
        }
    }

    private void OnProceedClicked()
    {
        if (_phase != PuzzlePhase.Transition) return;
        GeneratePhase2();
    }

    // =========================================================================
    // PHASE 2
    // =========================================================================

    private void GeneratePhase2()
    {
        SetPhase(PuzzlePhase.Phase2);
        SetPhaseBadge("PHASE 2", BadgePhase2, true);
        SetArbitexMsg("[ARBITEX]: reconstruct the sequence on RESTORE. LIFO only. no shortcuts.");

        // Scramble depth: elements * 4, minimum 12
        _p2ScrambleDepth = Mathf.Max(_target.Length * 4, 12);

        ScrambleTarget(_p2ScrambleDepth,
            out List<int> main, out List<int> aux, out List<int> restore);

        _buses[(int)BusId.Main]    = main;
        _buses[(int)BusId.Aux]     = aux;
        _buses[(int)BusId.Restore] = restore;

        _selectedBus      = null;
        _p2MovesRemaining = _p2ScrambleDepth * 2;

        if (phase2HintText != null)
        {
            phase2HintText.text =
                $"Target (bottom to top): {BuildStackString(_target, false)}\n" +
                "Click a bus to lift its TOS -- click destination bus to place it";
        }

        RefreshP2MovesText();
        RebuildAllBuses();
        RefreshBusTints();
    }

    /// <summary>
    /// Generates a scrambled starting state for Phase 2 by performing
    /// <paramref name="depth"/> random valid LIFO moves starting from
    /// (main=target, aux=empty, restore=empty).
    /// Guarantees the restore bus does not accidentally equal the target.
    /// </summary>
    private void ScrambleTarget(int depth,
        out List<int> main, out List<int> aux, out List<int> restore)
    {
        main    = new List<int>(_target); // bottom-to-top copy
        aux     = new List<int>();
        restore = new List<int>();

        var bufs = new List<int>[] { main, aux, restore };

        for (int step = 0; step < depth; step++)
        {
            // Collect all valid (fromIdx, toIdx) moves
            var moves = new List<int>();  // packed as from*3+to
            for (int f = 0; f < 3; f++)
            {
                if (bufs[f].Count == 0) continue;
                for (int t = 0; t < 3; t++)
                {
                    if (t != f) moves.Add(f * 3 + t);
                }
            }
            if (moves.Count == 0) break;

            int packed = moves[UnityEngine.Random.Range(0, moves.Count)];
            int from   = packed / 3;
            int to     = packed % 3;

            int top = bufs[from][bufs[from].Count - 1];
            bufs[from].RemoveAt(bufs[from].Count - 1);
            bufs[to].Add(top);
        }

        // Guard: if restore already matches target (astronomically unlikely), move TOS away
        if (StackMatchesTarget(restore))
        {
            if (restore.Count > 0)
            {
                int top = restore[restore.Count - 1];
                restore.RemoveAt(restore.Count - 1);
                aux.Add(top);
            }
        }
    }

    // ── Phase 2: Interaction ──────────────────────────────────────────────────

    private void OnBusClicked(BusId bus)
    {
        if (_phase != PuzzlePhase.Phase2) return;

        if (_selectedBus == null)
        {
            // Select -- only if bus has elements
            if (_buses[(int)bus].Count == 0) return;
            _selectedBus = bus;
            RefreshBusTints();
            RebuildBusDisplay(bus);
            return;
        }

        if (_selectedBus == bus)
        {
            // Deselect
            _selectedBus = null;
            RefreshBusTints();
            RebuildBusDisplay(bus);
            return;
        }

        // Move from selectedBus to bus
        TryP2Move(_selectedBus.Value, bus);
    }

    private void TryP2Move(BusId from, BusId to)
    {
        List<int> src = _buses[(int)from];

        if (src.Count == 0)
        {
            _selectedBus = null;
            RefreshBusTints();
            return;
        }

        if (_p2MovesRemaining <= 0)
        {
            _totalWrongAttempts++;
            _selectedBus = null;
            feedbackPopup?.Show(false,
                $"Moves exhausted -- reroute failed. Resetting phase 2. " +
                $"(Wrong attempts: {_totalWrongAttempts})",
                onDismiss: GeneratePhase2);
            RefreshBusTints();
            return;
        }

        // No ordering constraint -- any value may go on any bus (pure LIFO)
        int top = src[src.Count - 1];
        src.RemoveAt(src.Count - 1);
        _buses[(int)to].Add(top);

        _p2MovesRemaining--;
        _selectedBus = null;

        RefreshP2MovesText();
        RebuildAllBuses();
        RefreshBusTints();

        CheckPhase2Win();
    }

    private void CheckPhase2Win()
    {
        if (!StackMatchesTarget(_buses[(int)BusId.Restore])) return;

        SetPhase(PuzzlePhase.Complete);
        PlaySfx(authorizeSound);
        OnStackSolved?.Invoke(_totalWrongAttempts);

        SetStatusBadge("OVERRIDE ACCEPTED", BadgeDone);
        SetPhaseBadge("COMPLETE", BadgeDone, true);
        SetArbitexMsg("[ARBITEX]: ...shutter disengaged. this changes nothing.");

        if (phase2HintText != null)
            phase2HintText.text = "Reroute complete. Shutter released.";

        feedbackPopup?.Show(true,
            "Reroute complete!\nTarget sequence restored on RESTORE bus.",
            onDismiss: ClosePanel);
    }

    private bool StackMatchesTarget(List<int> stack)
    {
        if (stack.Count != _target.Length) return false;
        for (int i = 0; i < _target.Length; i++)
            if (stack[i] != _target[i]) return false;
        return true;
    }

    // ── Phase 2: Display ──────────────────────────────────────────────────────

    private void RebuildAllBuses()
    {
        RebuildBusDisplay(BusId.Main);
        RebuildBusDisplay(BusId.Aux);
        RebuildBusDisplay(BusId.Restore);
    }

    private void RebuildBusDisplay(BusId bus)
    {
        RectTransform container = BusContainer(bus);
        if (container == null) return;

        ClearChildren(container);

        List<int> stack  = _buses[(int)bus];
        bool isLifted = (_selectedBus == bus);

        if (stack.Count == 0)
        {
            SpawnEmptyPlaceholder(container);
            return;
        }

        for (int i = 0; i < stack.Count; i++)
        {
            bool isTOS = (i == stack.Count - 1);
            CellState state = (isTOS && isLifted) ? CellState.Lifted
                            : isTOS               ? CellState.TOS
                            :                       CellState.Normal;
            SpawnCell(container, stack[i], state, isTOS, i == 0);
        }
    }

    private void RefreshBusTints()
    {
        SetBusTint(mainBusPanelImage,    BusId.Main);
        SetBusTint(auxBusPanelImage,     BusId.Aux);
        SetBusTint(restoreBusPanelImage, BusId.Restore);
    }

    private void SetBusTint(Image panel, BusId bus)
    {
        if (panel == null) return;
        if (_selectedBus == bus)
        {
            panel.color = BusSelected;
        }
        else if (bus == BusId.Restore)
        {
            panel.color = _phase == PuzzlePhase.Complete ? BadgeDone : BusRestore;
        }
        else
        {
            panel.color = BusNormal;
        }
    }

    private void RefreshP2MovesText()
    {
        if (phase2MovesText != null)
            phase2MovesText.text = $"Moves remaining: {_p2MovesRemaining}";
    }

    // =========================================================================
    // CELL / BUTTON SPAWNING
    // =========================================================================

    private void SpawnCell(RectTransform parent, int value, CellState state,
                           bool isTOS, bool isBottom)
    {
        if (breakerCellPrefab == null) return;

        GameObject go  = Instantiate(breakerCellPrefab, parent);
        Image      img = go.GetComponent<Image>();
        if (img) img.color = StateToColor(state);

        Transform valT = go.transform.Find("ValueText");
        Transform tagT = go.transform.Find("TagText");

        TextMeshProUGUI valLabel = valT != null ? valT.GetComponent<TextMeshProUGUI>() : null;
        TextMeshProUGUI tagLabel = tagT != null ? tagT.GetComponent<TextMeshProUGUI>() : null;

        if (valLabel != null)
        {
            bool isJunk = (state == CellState.Junk);
            valLabel.text = isJunk
                ? $"{FormatValue(value)} <size=70%>[FAULT]</size>"
                : FormatValue(value);
        }

        if (tagLabel != null)
        {
            switch (state)
            {
                case CellState.Lifted: tagLabel.text = "LIFTED"; break;
                case CellState.TOS:    tagLabel.text = "TOS";    break;
                case CellState.Junk:   tagLabel.text = "FAULT";  break;
                default:
                    tagLabel.text = isBottom ? "BOT" : "";
                    break;
            }
        }
    }

    private void SpawnEmptyPlaceholder(RectTransform parent)
    {
        if (breakerCellPrefab == null) return;

        GameObject go  = Instantiate(breakerCellPrefab, parent);
        Image      img = go.GetComponent<Image>();
        if (img) img.color = ColEmpty;

        Transform valT = go.transform.Find("ValueText");
        Transform tagT = go.transform.Find("TagText");

        if (valT != null)
        {
            var tmp = valT.GetComponent<TextMeshProUGUI>();
            if (tmp) tmp.text = "-- empty --";
        }
        if (tagT != null)
        {
            var tmp = tagT.GetComponent<TextMeshProUGUI>();
            if (tmp) tmp.text = "";
        }
    }

    private void SpawnTokenButton(int value)
    {
        if (tokenButtonPrefab == null) return;

        GameObject go  = Instantiate(tokenButtonPrefab, tokenButtonParent);
        Button     btn = go.GetComponent<Button>();

        Transform lblT = go.transform.Find("Label");
        if (lblT != null)
        {
            var tmp = lblT.GetComponent<TextMeshProUGUI>();
            if (tmp) tmp.text = $"{FormatValue(value)} A";
        }

        int captured = value;
        btn?.onClick.AddListener(() => OnPushClicked(captured));
    }

    // =========================================================================
    // PHASE / BADGE HELPERS
    // =========================================================================

    private void SetPhase(PuzzlePhase p)
    {
        _phase = p;

        if (phase1Section)     phase1Section    .SetActive(p == PuzzlePhase.Phase1);
        if (transitionSection) transitionSection.SetActive(p == PuzzlePhase.Transition);
        if (phase2Section)     phase2Section    .SetActive(p == PuzzlePhase.Phase2 || p == PuzzlePhase.Complete);
        if (phaseBadgeRoot)    phaseBadgeRoot   .SetActive(p != PuzzlePhase.Phase1 || _tier >= 2);
    }

    private void SetStatusBadge(string text, Color col)
    {
        if (statusBadgeText) statusBadgeText.text  = text;
        if (statusBadgeBg)   statusBadgeBg.color   = col;
    }

    private void SetPhaseBadge(string text, Color col, bool visible)
    {
        if (phaseBadgeRoot) phaseBadgeRoot.SetActive(visible);
        if (phaseBadgeText) phaseBadgeText.text = text;
        if (phaseBadgeBg)   phaseBadgeBg.color  = col;
    }

    private void SetArbitexMsg(string msg)
    {
        if (arbitexMessageText) arbitexMessageText.text = msg;
    }

    // =========================================================================
    // FORMATTING
    // =========================================================================

    private string FormatValue(int v) =>
        _hexDisplay ? $"0x{v:X2}" : v.ToString();

    /// <summary>
    /// Returns a comma-separated stack string.
    /// tosFirst=false: bottom to top order (index 0 first).
    /// tosFirst=true : TOS first (index last first).
    /// </summary>
    private string BuildStackString(int[] values, bool tosFirst)
    {
        var sb = new StringBuilder();

        if (tosFirst)
        {
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (i < values.Length - 1) sb.Append(", ");
                sb.Append(FormatValue(values[i]));
                sb.Append('A');
            }
        }
        else
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(FormatValue(values[i]));
                sb.Append('A');
            }
        }

        return sb.ToString();
    }

    private static Color StateToColor(CellState s)
    {
        switch (s)
        {
            case CellState.TOS:    return ColTOS;
            case CellState.Junk:   return ColJunk;
            case CellState.Target: return ColTarget;
            case CellState.Lifted: return ColLifted;
            default:               return ColNormal;
        }
    }

    private RectTransform BusContainer(BusId bus)
    {
        switch (bus)
        {
            case BusId.Main:    return mainBusContainer;
            case BusId.Aux:     return auxBusContainer;
            case BusId.Restore: return restoreBusContainer;
            default:            return mainBusContainer;
        }
    }

    // =========================================================================
    // GENERATION HELPERS
    // =========================================================================

    /// <summary>
    /// Generates <paramref name="count"/> unique random ints in [min, max],
    /// excluding values in <paramref name="exclude"/>.
    /// Falls back to values above max if the range is exhausted.
    /// </summary>
    private static int[] GenerateUniqueValues(int count, int min, int max,
                                              int[] exclude = null)
    {
        var used    = new HashSet<int>(exclude != null ? exclude : Array.Empty<int>());
        var results = new List<int>();
        int tries   = 0;

        while (results.Count < count && tries++ < 400)
        {
            int v = UnityEngine.Random.Range(min, max + 1);
            if (used.Add(v)) results.Add(v);
        }

        int next = max + 1;
        while (results.Count < count)
        {
            if (used.Add(next)) results.Add(next);
            next++;
        }

        return results.ToArray();
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T tmp    = list[i];
            list[i]  = list[j];
            list[j]  = tmp;
        }
    }

    // =========================================================================
    // TEARDOWN
    // =========================================================================

    private void ClearAll()
    {
        _stack.Clear();
        _target          = null;
        _availableTokens = null;

        for (int b = 0; b < _buses.Length; b++) _buses[b].Clear();
        _selectedBus = null;

        ClearChildren(stackCellParent);
        ClearChildren(targetCellParent);
        ClearChildren(tokenButtonParent);
        ClearChildren(mainBusContainer);
        ClearChildren(auxBusContainer);
        ClearChildren(restoreBusContainer);
    }

    private static void ClearChildren(RectTransform rt)
    {
        if (rt == null) return;
        for (int i = rt.childCount - 1; i >= 0; i--)
            Destroy(rt.GetChild(i).gameObject);
    }

    // ── Audio ─────────────────────────────────────────────────────────────────

    private void PlaySfx(AudioClip clip)
    {
        if (sfxAudioSource == null || clip == null) return;
        sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }

    public void ClosePanel() => _onClose?.Invoke();
}

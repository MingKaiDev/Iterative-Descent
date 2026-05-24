using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Master controller for the Stack / Memory Corruption puzzle.
///
/// WHAT THE PLAYER DOES
/// ────────────────────
/// A corrupted memory terminal shows the current stack state and a TARGET
/// sequence the stack must match when submitted.  The player pushes tokens
/// from an available-token panel onto the stack, or pops the top element,
/// until the stack matches the target, then presses Submit.
///
/// LIFO teaching moment: to reach target [A(TOS), B, C(bottom)] the player
/// must push in reverse: C first, then B, then A.  Pre-loaded junk must be
/// popped before the correct sequence can be built.
///
/// DDA SCALING
/// ───────────
///   Tier 0/1  -- capacity 4, target depth 2, 0 pre-loaded junk, no move limit
///   Tier 2    -- capacity 5, target depth 3, 1 pre-loaded junk, 8 moves max
///   Tier 3/4  -- capacity 6, target depth 3-4, 2 pre-loaded junk, 7 moves max
///   Values displayed as decimal at tier 0-2, as hex strings at tier 3-4.
///   Overflow (push onto full stack) is blocked with a warning message.
///
/// INSPECTOR SETUP  (see StackPuzzle_UnitySetup.md)
/// ─────────────────────────────────────────────────
///   stackCellParent   VerticalLayoutGroup -- TOS at index 0 (last sibling)
///   targetCellParent  VerticalLayoutGroup -- shows target state, TOS at top
///   tokenButtonParent HorizontalLayoutGroup / Grid -- push token buttons
///   popButton         Button -- pops TOS
///   submitButton      Button -- validates the stack against target
///   closeButton       Button -- exits without solving
///   instructionText   TMP -- shows tier context and move counter
///   movesText         TMP -- shows "Moves: N remaining" (hide at tier 0/1)
///   feedbackPopup     StackFeedbackPopup child (starts inactive)
///   stackCellPrefab   Prefab: Image + TextMeshProUGUI child labelled "ValueText"
///   tokenButtonPrefab Prefab: Button + TextMeshProUGUI child labelled "Label"
///
/// NOTES
/// ─────
///   Time.timeScale = 0 while open (set by StackPuzzleProp).
///   All timing uses Time.realtimeSinceStartup (handled by PlayerMetricsTracker).
///   Wrong submissions do NOT reset the stack state.
///   OnStackSolved fires with the wrong-submission count (0 = first try).
/// </summary>
public class StackPuzzleUI : MonoBehaviour
{
    // ── Singleton & Static Event ───────────────────────────────────────────────
    public static StackPuzzleUI Instance { get; private set; }

    /// <summary>
    /// Fired when the player correctly matches the target stack state.
    /// Argument = wrong-submission count (0 = solved on first try).
    /// </summary>
    public static event Action<int> OnStackSolved;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Stack Display")]
    [Tooltip("Parent for stack cell prefabs. VerticalLayoutGroup; children are ordered " +
             "bottom-to-top in the hierarchy so TOS is the last sibling.")]
    [SerializeField] private RectTransform stackCellParent;

    [Tooltip("Parent for target state cells. Same layout as stackCellParent.")]
    [SerializeField] private RectTransform targetCellParent;

    [Header("Token Panel")]
    [Tooltip("Parent for push-token buttons.")]
    [SerializeField] private RectTransform tokenButtonParent;

    [Header("Controls")]
    [SerializeField] private Button               popButton;
    [SerializeField] private Button               submitButton;
    [SerializeField] private Button               closeButton;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [Tooltip("Displays remaining moves at tier 2+. Assign; hidden at tier 0/1.")]
    [SerializeField] private TextMeshProUGUI movesText;

    [Header("Feedback")]
    [SerializeField] private StackFeedbackPopup feedbackPopup;

    [Header("Prefabs")]
    [Tooltip("Prefab with Image background + TextMeshProUGUI child named 'ValueText'.")]
    [SerializeField] private GameObject stackCellPrefab;

    [Tooltip("Prefab with Button + TextMeshProUGUI child named 'Label'.")]
    [SerializeField] private GameObject tokenButtonPrefab;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly Color ColourNormal  = new(0.20f, 0.25f, 0.20f); // dark green
    private static readonly Color ColourJunk    = new(0.45f, 0.10f, 0.10f); // dark red  -- pre-loaded corrupt data
    private static readonly Color ColourTarget  = new(0.10f, 0.30f, 0.45f); // dark blue -- target cells
    private static readonly Color ColourTOS     = new(0.25f, 0.65f, 0.25f); // bright green -- top-of-stack highlight

    // ── Runtime State ─────────────────────────────────────────────────────────
    /// <summary>Live stack. Index 0 = bottom. Last element = TOS.</summary>
    private readonly List<int> _stack = new();

    /// <summary>Target state. Index 0 = bottom of target. Last = target TOS.</summary>
    private int[] _target;

    /// <summary>Full set of tokens the player can push (may contain duplicates).</summary>
    private int[] _availableTokens;

    private int   _capacity;
    private int   _movesRemaining;
    private bool  _moveLimitActive;
    private bool  _hexDisplay;
    private int   _attempts;
    private Action _onClose;

    // ── Unity ─────────────────────────────────────────────────────────────────
    private void Awake() => Instance = this;

    private void OnEnable()
    {
        if (popButton)    popButton.onClick   .AddListener(OnPopClicked);
        if (submitButton) submitButton.onClick.AddListener(CheckSolution);
        if (closeButton)  closeButton.onClick .AddListener(ClosePanel);
    }

    private void OnDisable()
    {
        if (popButton)    popButton.onClick   .RemoveListener(OnPopClicked);
        if (submitButton) submitButton.onClick.RemoveListener(CheckSolution);
        if (closeButton)  closeButton.onClick .RemoveListener(ClosePanel);
    }

    // ── Public Entry Point ────────────────────────────────────────────────────

    /// <summary>
    /// Called by StackPuzzleProp to open the puzzle.
    /// onClose is invoked when the panel should hide.
    /// </summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose  = onClose;
        _attempts = 0;

        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        // Notify metrics -- timer starts here.
        // Time.timeScale is already 0; tracker uses realtimeSinceStartup.
        PlayerMetricsTracker.Instance?.NotifyStackStarted();

        GeneratePuzzle();
    }

    // ── Puzzle Generation ─────────────────────────────────────────────────────

    private void GeneratePuzzle()
    {
        ClearAll();

        int tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.StacksAndQueues)
            : 2;

        // ── Tier parameters ──────────────────────────────────────────────────
        int targetDepth;
        int junkCount;
        (targetDepth, _capacity, junkCount, _moveLimitActive, _hexDisplay) = tier switch
        {
            0 or 1 => (2, 4, 0, false, false),
            2      => (3, 5, 1, true,  false),
            3 or 4 => (UnityEngine.Random.Range(3, 5), 6, 2, true, true),
            _      => (2, 4, 0, false, false),
        };

        int moveBase = targetDepth + junkCount; // minimum ops needed
        _movesRemaining = _moveLimitActive ? moveBase + 2 : int.MaxValue;

        // ── Generate target values (unique, avoid obvious patterns) ───────────
        _target = GenerateUniqueValues(targetDepth, min: 10, max: 99);

        // ── Generate junk pre-loaded onto the stack (not in target) ──────────
        int[] junkValues = junkCount > 0
            ? GenerateUniqueValues(junkCount, min: 1, max: 9, exclude: _target)
            : Array.Empty<int>();

        // ── Seed the stack: junk at bottom ────────────────────────────────────
        _stack.Clear();
        foreach (int v in junkValues)
            _stack.Add(v);

        // ── Available tokens: target values + distractors ─────────────────────
        int distractorCount = tier switch { 0 or 1 => 2, 2 => 2, _ => 3 };
        int[] distractors = GenerateUniqueValues(distractorCount, min: 10, max: 99,
                                                 exclude: _target);
        var tokenList = new List<int>(_target);
        tokenList.AddRange(distractors);
        ShuffleList(tokenList);
        _availableTokens = tokenList.ToArray();

        // ── Build instruction text ────────────────────────────────────────────
        if (instructionText)
        {
            string targetStr = BuildStackString(_target, toc: true);
            string moveStr   = _moveLimitActive ? $"  |  Moves: <b>{_movesRemaining}</b>" : "";
            instructionText.text =
                $"<b>MEMORY CORRUPTION TERMINAL</b>{moveStr}\n" +
                $"Target stack (top first):  <b>{targetStr}</b>\n" +
                "Push tokens to build the target state. Pop to remove the top element.";
        }

        RefreshMovesText();

        // ── Spawn token buttons ───────────────────────────────────────────────
        foreach (int val in _availableTokens)
            SpawnTokenButton(val);

        // ── Spawn target cells (static display) ───────────────────────────────
        // Rendered bottom-to-top so TOS is visually on top.
        for (int i = 0; i < _target.Length; i++)
            SpawnCell(targetCellParent, _target[i], ColourTarget, isJunk: false);

        // ── Render initial stack ──────────────────────────────────────────────
        RebuildStackDisplay();
    }

    // ── Button Callbacks ──────────────────────────────────────────────────────

    private void OnPushClicked(int value)
    {
        if (_stack.Count >= _capacity)
        {
            feedbackPopup?.Show(false,
                $"Stack overflow -- capacity is {_capacity}.\nPop an element first.");
            return;
        }

        if (_moveLimitActive && _movesRemaining <= 0)
        {
            feedbackPopup?.Show(false, "No moves remaining.");
            return;
        }

        _stack.Add(value);
        ConsumeMove();
        RebuildStackDisplay();
    }

    private void OnPopClicked()
    {
        if (_stack.Count == 0)
        {
            feedbackPopup?.Show(false, "Stack is empty -- nothing to pop.");
            return;
        }

        if (_moveLimitActive && _movesRemaining <= 0)
        {
            feedbackPopup?.Show(false, "No moves remaining.");
            return;
        }

        _stack.RemoveAt(_stack.Count - 1);
        ConsumeMove();
        RebuildStackDisplay();
    }

    private void ConsumeMove()
    {
        if (!_moveLimitActive) return;
        _movesRemaining--;
        RefreshMovesText();
    }

    // ── Solution Validation ───────────────────────────────────────────────────

    public void CheckSolution()
    {
        if (_stack.Count != _target.Length)
        {
            feedbackPopup?.Show(false,
                $"Stack has {_stack.Count} element(s) -- target requires {_target.Length}.");
            return;
        }

        bool correct = true;
        for (int i = 0; i < _target.Length; i++)
        {
            if (_stack[i] != _target[i])
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            OnStackSolved?.Invoke(_attempts);
            string tosStr = FormatValue(_stack[_stack.Count - 1]);
            feedbackPopup?.Show(true,
                $"Correct!\nStack matches target. TOS = {tosStr}",
                onDismiss: ClosePanel);
        }
        else
        {
            _attempts++;
            if (_attempts > 2)
            {
                string exp = BuildStackString(_target, toc: true);
                feedbackPopup?.Show(false,
                    $"Wrong -- keep trying! (Attempt {_attempts})\n" +
                    $"Target (top first): {exp}");
            }
            else
            {
                feedbackPopup?.Show(false, $"Wrong -- try again! (Attempt {_attempts})");
            }
        }
    }

    public void ClosePanel() => _onClose?.Invoke();

    // ── Display Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Destroys and rebuilds all stack cell GameObjects to reflect _stack.
    /// Children are ordered bottom-to-top so the last sibling (TOS) is visually
    /// at the top if the parent VerticalLayoutGroup has reverseArrangement = true,
    /// or at the bottom if false. Wire to taste in the prefab.
    /// </summary>
    private void RebuildStackDisplay()
    {
        // Destroy all existing cells
        for (int i = stackCellParent.childCount - 1; i >= 0; i--)
            Destroy(stackCellParent.GetChild(i).gameObject);

        if (_stack.Count == 0)
            return;

        // Spawn bottom-to-top; TOS = last element, highlight differently
        for (int i = 0; i < _stack.Count; i++)
        {
            bool isTOS  = (i == _stack.Count - 1);
            bool isJunk = (i < CountInitialJunk()); // junk is always at the bottom

            Color bg = isTOS  ? ColourTOS
                     : isJunk ? ColourJunk
                     :          ColourNormal;

            SpawnCell(stackCellParent, _stack[i], bg, isJunk);
        }
    }

    private void SpawnCell(RectTransform parent, int value, Color bg, bool isJunk)
    {
        if (stackCellPrefab == null) return;

        var go   = Instantiate(stackCellPrefab, parent);
        var img  = go.GetComponent<Image>();
        if (img) img.color = bg;

        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        if (label)
        {
            label.text = isJunk
                ? $"<color=#FF4444>{FormatValue(value)} [CORRUPT]</color>"
                : FormatValue(value);
        }
    }

    private void SpawnTokenButton(int value)
    {
        if (tokenButtonPrefab == null) return;

        var go  = Instantiate(tokenButtonPrefab, tokenButtonParent);
        var btn = go.GetComponent<Button>();
        var lbl = go.GetComponentInChildren<TextMeshProUGUI>();

        if (lbl) lbl.text = FormatValue(value);

        // Capture value for lambda
        int capturedValue = value;
        btn?.onClick.AddListener(() => OnPushClicked(capturedValue));
    }

    private void RefreshMovesText()
    {
        if (movesText == null) return;
        movesText.gameObject.SetActive(_moveLimitActive);
        if (_moveLimitActive)
            movesText.text = $"Moves remaining: <b>{_movesRemaining}</b>";
    }

    // ── Value Formatting ──────────────────────────────────────────────────────

    private string FormatValue(int v) =>
        _hexDisplay ? $"0x{v:X2}" : v.ToString();

    private string BuildStackString(int[] values, bool toc)
    {
        // toc = true: last element is TOS; display TOS first
        var sb = new StringBuilder();
        if (toc)
        {
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (i < values.Length - 1) sb.Append(", ");
                sb.Append(FormatValue(values[i]));
            }
        }
        else
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(FormatValue(values[i]));
            }
        }
        return sb.ToString();
    }

    // ── Generation Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Generates 'count' unique random integers in [min, max], excluding any in 'exclude'.
    /// </summary>
    private static int[] GenerateUniqueValues(int count, int min, int max,
                                               int[] exclude = null)
    {
        var used    = new HashSet<int>(exclude ?? Array.Empty<int>());
        var results = new List<int>();
        int tries   = 0;

        while (results.Count < count && tries++ < 200)
        {
            int v = UnityEngine.Random.Range(min, max + 1);
            if (!used.Contains(v))
            {
                used.Add(v);
                results.Add(v);
            }
        }

        // Fallback: pad sequentially above max if exhausted
        int next = max + 1;
        while (results.Count < count)
        {
            if (!used.Contains(next)) results.Add(next);
            next++;
        }

        return results.ToArray();
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Returns how many elements at the bottom of the current _stack were
    /// pre-loaded as junk (not part of the target).  Junk is always pushed
    /// first during GeneratePuzzle, so this is just the number of elements
    /// not belonging to _target at the base.
    ///
    /// Used for colouring junk cells red during RebuildStackDisplay.
    /// NOTE: this works correctly only while junk values are guaranteed to be
    /// outside the target range (enforced by GenerateUniqueValues with exclude).
    /// </summary>
    private int CountInitialJunk()
    {
        // Walk from the bottom until we hit a value that is in the target set.
        var targetSet = new HashSet<int>(_target);
        int junk = 0;
        foreach (int v in _stack)
        {
            if (targetSet.Contains(v)) break;
            junk++;
        }
        return junk;
    }

    // ── Teardown ──────────────────────────────────────────────────────────────

    private void ClearAll()
    {
        _stack.Clear();
        _target         = null;
        _availableTokens = null;

        for (int i = stackCellParent.childCount - 1; i >= 0; i--)
            Destroy(stackCellParent.GetChild(i).gameObject);

        for (int i = targetCellParent.childCount - 1; i >= 0; i--)
            Destroy(targetCellParent.GetChild(i).gameObject);

        for (int i = tokenButtonParent.childCount - 1; i >= 0; i--)
            Destroy(tokenButtonParent.GetChild(i).gameObject);
    }
}

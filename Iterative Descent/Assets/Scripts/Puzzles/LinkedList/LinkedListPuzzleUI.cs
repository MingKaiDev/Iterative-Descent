using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Master controller for the Linked-List pointer-rewiring puzzle.
///
/// WHAT THE PLAYER DOES
/// ────────────────────
/// A linked list is shown in its FORWARD order (e.g. 3→7→12→5→NULL with HEAD→3).
/// The player must REVERSE it by rewiring the pointer arrows:
///   • Drag any node's [ → ] handle to point at a different node or NULL.
///   • Drag HEAD's handle to point at a different node.
/// Goal: HEAD→5→12→7→3→NULL  (all arrows reversed).
/// Press Submit (or wait for auto-check on every drop) to validate.
///
/// DDA SCALING
/// ───────────
///   Tier 0 / 1  (Very Easy / Easy)   → 3 nodes
///   Tier 2      (Normal)              → 4 nodes
///   Tier 3      (Hard)                → 5 nodes
///   Tier 4      (Very Hard)           → 6 nodes
///
/// INSPECTOR SETUP
/// ───────────────
///   nodePrefab          Prefab with LLNodeBlock + LLNextHandle child
///   headPointerPrefab   Prefab with LLHeadPointer + LLNextHandle child
///   nullTerminalPrefab  Prefab with LLNullTerminal + Image (RaycastTarget ON)
///   arrowPrefab         Prefab with LLArrow + Image
///   nodeContainer       RectTransform — NO Layout Group; pivot must be (0.5, 0.5)
///   feedbackPopup       PuzzleFeedbackPopup child panel (starts inactive)
///   instructionText     TMP for task description
///   submitButton        Optional — calls CheckSolution
///   closeButton         Calls ClosePanel
///
/// NOTES
/// ─────
///   • Time.timeScale = 0 while open (set by the prop), so all timers use
///     Time.realtimeSinceStartup (handled by PlayerMetricsTracker).
///   • Arrows render as first siblings so they appear behind nodes.
///   • Wrong answers do NOT reset the board — the player keeps their wiring.
/// </summary>
public class LinkedListPuzzleUI : MonoBehaviour
{
    // ── Static Events ─────────────────────────────────────────────────────────
    public static LinkedListPuzzleUI Instance { get; private set; }

    /// <summary>Fired on correct solution. Argument = number of wrong attempts.</summary>
    public static event Action<int> OnLinkedListSolved;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Prefabs")]
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private GameObject headPointerPrefab;
    [SerializeField] private GameObject nullTerminalPrefab;
    [SerializeField] private GameObject arrowPrefab;

    [Header("References")]
    [SerializeField] private RectTransform       nodeContainer;  // pivot must be (0.5, 0.5)
    [SerializeField] private PuzzleFeedbackPopup feedbackPopup;
    [SerializeField] private TextMeshProUGUI     instructionText;
    [SerializeField] private Button             submitButton;
    [SerializeField] private Button             closeButton;

    [Header("Layout")]
    [Tooltip("Horizontal gap between node centres (pixels).")]
    [SerializeField] private float nodeSpacing = 160f;
    [Tooltip("How far above the node row the HEAD box sits (pixels).")]
    [SerializeField] private float headYOffset  = 90f;

    // ── Runtime State ─────────────────────────────────────────────────────────
    private List<LLNodeBlock> _nodes        = new();
    private LLHeadPointer     _headPointer;
    private LLNullTerminal    _nullTerminal;

    private List<LLArrow>     _staticArrows = new();   // live wiring arrows
    private LLArrow           _pendingArrow;            // dragging ghost arrow

    private int[]  _solution;    // values in correct reversed order
    private int    _attempts;
    private Action _onClose;

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake() => Instance = this;

    void OnEnable()
    {
        if (submitButton) submitButton.onClick.AddListener(CheckSolution);
        if (closeButton)  closeButton.onClick .AddListener(ClosePanel);
    }

    void OnDisable()
    {
        if (submitButton) submitButton.onClick.RemoveListener(CheckSolution);
        if (closeButton)  closeButton.onClick .RemoveListener(ClosePanel);
    }

    // ── Public Entry Point ────────────────────────────────────────────────────

    /// <summary>Called by the prop (e.g. PuzzleProp / ComputerScreenProp) to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose  = onClose;
        _attempts = 0;

        // Always hide the popup on entry — if the overlay was closed without the
        // player pressing OK (e.g. via the Close button), feedbackPopup.activeSelf
        // can be left true. The Blocker inside it would then silently eat the first
        // Submit click, making the puzzle appear unresponsive.
        if (feedbackPopup != null) feedbackPopup.gameObject.SetActive(false);

        PlayerMetricsTracker.Instance?.NotifyLinkedListStarted();

        if (instructionText)
            instructionText.text =
                "Reverse the linked list by rewiring the pointer arrows.\n" +
                "Drag a node's  [ → ]  handle to redirect its \"next\" pointer.\n" +
                "Also redirect  HEAD  to the new first node.";

        GeneratePuzzle();
    }

    // ── Puzzle Generation ─────────────────────────────────────────────────────

    private void GeneratePuzzle()
    {
        ClearAll();

        int   count  = NodeCountForCurrentTier();
        int[] values = GenerateUniqueValues(count);
        _solution    = values.Reverse().ToArray(); // goal = all values flipped

        float totalWidth = (count - 1) * nodeSpacing;
        float startX     = -totalWidth / 2f;

        // ── Spawn nodes ───────────────────────────────────────────────────────
        for (int i = 0; i < count; i++)
        {
            var go   = Instantiate(nodePrefab, nodeContainer);
            var node = go.GetComponent<LLNodeBlock>();
            node.Init(values[i], new Vector2(startX + i * nodeSpacing, 0f), this);
            _nodes.Add(node);
        }

        // ── Spawn NULL terminal (right of last node) ──────────────────────────
        var nullGo  = Instantiate(nullTerminalPrefab, nodeContainer);
        _nullTerminal = nullGo.GetComponent<LLNullTerminal>();
        _nullTerminal.SetPosition(new Vector2(startX + count * nodeSpacing, 0f));

        // ── Spawn HEAD pointer (above first node) ─────────────────────────────
        var headGo  = Instantiate(headPointerPrefab, nodeContainer);
        _headPointer = headGo.GetComponent<LLHeadPointer>();
        _headPointer.Init(_nodes[0], this, new Vector2(startX, headYOffset));

        // ── Wire initial FORWARD connections: 0→1→2→…→null ───────────────────
        for (int i = 0; i < _nodes.Count - 1; i++)
            _nodes[i].SetNext(_nodes[i + 1]);
        _nodes[^1].SetNext(null); // last node already → NULL

        RebuildArrows();
    }

    // ── Arrow Management ──────────────────────────────────────────────────────

    /// <summary>
    /// Destroys and recreates all static (non-drag) arrows to reflect the
    /// current pointer state.  Called after every successful drop.
    /// </summary>
    public void RebuildArrows()
    {
        foreach (var a in _staticArrows) if (a) Destroy(a.gameObject);
        _staticArrows.Clear();

        // HEAD → target node
        if (_headPointer != null && _headPointer.TargetNode != null)
        {
            _staticArrows.Add(SpawnArrow(
                _headPointer.HandleRect,
                _headPointer.TargetNode.NodeRect));
        }

        // Each node → next (or → NULL terminal)
        foreach (var node in _nodes)
        {
            RectTransform target = node.NextNode != null
                ? node.NextNode.NodeRect
                : _nullTerminal.TerminalRect;

            _staticArrows.Add(SpawnArrow(node.HandleRect, target));
        }
    }

    private LLArrow SpawnArrow(RectTransform from, RectTransform to)
    {
        var go    = Instantiate(arrowPrefab, nodeContainer);
        go.transform.SetAsFirstSibling(); // render BEHIND nodes
        var arrow = go.GetComponent<LLArrow>();
        arrow.Init(from, to, nodeContainer);
        return arrow;
    }

    // ── Drag Callbacks (called by LLNextHandle) ───────────────────────────────

    /// <summary>A node's handle started being dragged.</summary>
    public void OnNodeHandleDragBegin(LLNodeBlock fromNode, PointerEventData e)
    {
        BeginDragArrow(fromNode.HandleRect, e);
    }

    /// <summary>The HEAD handle started being dragged.</summary>
    public void OnHeadHandleDragBegin(PointerEventData e)
    {
        BeginDragArrow(_headPointer.HandleRect, e);
    }

    /// <summary>Shared drag-move handler: keeps the ghost arrow tip on the cursor.</summary>
    public void OnHandleDragging(PointerEventData e)
    {
        _pendingArrow?.SetFloatingEnd(ScreenToContainer(e.position, e.pressEventCamera));
    }

    /// <summary>A node's handle was released — set next pointer if valid.</summary>
    public void OnNodeHandleDragEnd(LLNodeBlock fromNode, PointerEventData e)
    {
        DestroyPendingArrow();

        var (hitNode, hitNull) = HitTest(e);

        // Self-pointer not allowed; miss (neither node nor NULL) → no change.
        if (hitNode == fromNode) return;
        if (hitNode != null || hitNull)
        {
            fromNode.SetNext(hitNode); // null → points to NULL terminal
            RebuildArrows();
        }
    }

    /// <summary>The HEAD handle was released — redirect HEAD if landed on a node.</summary>
    public void OnHeadHandleDragEnd(PointerEventData e)
    {
        DestroyPendingArrow();

        var (hitNode, _) = HitTest(e);
        if (hitNode != null)
        {
            _headPointer.SetTarget(hitNode);
            RebuildArrows();
        }
        // HEAD must always point to a node — ignore drops on NULL or misses.
    }

    // ── Solution Check ────────────────────────────────────────────────────────

    /// <summary>
    /// Walks the linked list from HEAD and compares the traversal to _solution.
    /// Fired by the Submit button, or you can call it from elsewhere.
    /// </summary>
    public void CheckSolution()
    {
        // Guard: HEAD must point somewhere
        if (_headPointer == null || _headPointer.TargetNode == null)
        {
            feedbackPopup?.Show(false, "HEAD must point to a node.");
            return;
        }

        // Traverse the rewired list
        var traversed = new List<int>();
        var visited   = new HashSet<LLNodeBlock>();
        var current   = _headPointer.TargetNode;

        while (current != null)
        {
            if (visited.Contains(current))
            {
                _attempts++;
                feedbackPopup?.Show(false,
                    "Cycle detected — a node's pointer loops back.\n" +
                    "Check each arrow carefully.");
                return;
            }
            visited.Add(current);
            traversed.Add(current.Value);
            current = current.NextNode;
        }

        // Compare with expected reversal
        bool correct = traversed.Count == _solution.Length
                    && traversed.SequenceEqual(_solution);

        if (correct)
        {
            OnLinkedListSolved?.Invoke(_attempts);

            // On correct: show success pop-up; puzzle closes when the player presses OK.
            feedbackPopup?.Show(true,
                "Correct!  List reversed successfully.",
                onDismiss: ClosePanel);
        }
        else
        {
            _attempts++;

            // Only reveal the correct order after the player has submitted wrong twice.
            if (_attempts > 2)
            {
                string expected = string.Join(" → ", _solution) + " → NULL";
                feedbackPopup?.Show(false,
                    $"Wrong answer — keep trying!  (Attempt {_attempts})\n" +
                    $"Target:  {expected}");
            }
            else
            {
                feedbackPopup?.Show(false,
                    $"Wrong answer — try again!  (Attempt {_attempts})");
            }
        }
    }

    public void ClosePanel() => _onClose?.Invoke();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void BeginDragArrow(RectTransform source, PointerEventData e)
    {
        DestroyPendingArrow();

        var go = Instantiate(arrowPrefab, nodeContainer);
        go.transform.SetAsFirstSibling();
        _pendingArrow = go.GetComponent<LLArrow>();
        _pendingArrow.Init(source, null, nodeContainer);
        _pendingArrow.SetFloatingEnd(ScreenToContainer(e.position, e.pressEventCamera));
    }

    private void DestroyPendingArrow()
    {
        if (_pendingArrow != null) Destroy(_pendingArrow.gameObject);
        _pendingArrow = null;
    }

    /// <summary>
    /// Single raycast that returns the first LLNodeBlock AND whether the NULL
    /// terminal was hit — avoids double RaycastAll calls.
    /// </summary>
    private (LLNodeBlock node, bool isNull) HitTest(PointerEventData e)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);

        foreach (var r in results)
        {
            var node = r.gameObject.GetComponentInParent<LLNodeBlock>();
            if (node != null) return (node, false);

            if (r.gameObject.GetComponentInParent<LLNullTerminal>() != null)
                return (null, true);
        }
        return (null, false);
    }

    /// <summary>
    /// Convert a screen-space point (from PointerEventData) into nodeContainer's
    /// local space.  Handles both Overlay (cam=null) and Camera canvases.
    /// </summary>
    private Vector2 ScreenToContainer(Vector2 screenPos, Camera cam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            nodeContainer, screenPos, cam, out Vector2 local);
        return local;
    }

    private int NodeCountForCurrentTier()
    {
        int tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.LinkedLists)
            : 2;
        return tier switch
        {
            0 => 3,   // Very Easy
            1 => 3,   // Easy
            2 => 4,   // Normal
            3 => 5,   // Hard
            4 => 6,   // Very Hard (future: add a second operation)
            _ => 4
        };
    }

    private static int[] GenerateUniqueValues(int count)
    {
        var pool   = Enumerable.Range(1, 20).ToList();
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int idx    = UnityEngine.Random.Range(0, pool.Count);
            result[i]  = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }

    private void ClearAll()
    {
        foreach (var n in _nodes)        if (n) Destroy(n.gameObject);
        foreach (var a in _staticArrows) if (a) Destroy(a.gameObject);
        _nodes.Clear();
        _staticArrows.Clear();

        DestroyPendingArrow();
        if (_headPointer)  Destroy(_headPointer.gameObject);
        if (_nullTerminal) Destroy(_nullTerminal.gameObject);
        _headPointer  = null;
        _nullTerminal = null;
    }
}

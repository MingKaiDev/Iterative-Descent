using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Master controller for the Linked-List pointer-rewiring puzzle.
/// Accessed via a computer prop -- styled as ARBITEX's memory management console.
///
/// WHAT THE PLAYER DOES
/// --------------------
/// A linked list is shown in forward order (e.g. 3->7->12->5->NULL with HEAD->3).
/// The player must REVERSE it by rewiring the pointer arrows.
///   1. Click a node's port  [ -> ]  to SELECT it as the connection source.
///      The port turns amber and a ghost arrow follows the cursor.
///   2. Click a destination NODE BODY to commit the connection.
///      Click the NULL terminal to terminate a chain there.
///      Click the same port again (or any empty space) to cancel.
/// Goal: HEAD->5->12->7->3->NULL  (all arrows reversed).
/// Press "EXECUTE PATCH" to validate.
///
/// CORRUPTION MECHANIC (Hard / Very Hard only)
/// -------------------------------------------
/// On Tier 3 (Hard), every 20 seconds ARBITEX overwrites a random node's
/// next pointer with a random incorrect target.
/// On Tier 4 (Very Hard), this happens every 12 seconds.
/// The corrupted node flashes red and a console warning fires.
/// All timers use Time.realtimeSinceStartup (immune to timeScale=0).
///
/// DDA SCALING
/// -----------
///   Tier 0 / 1  (Very Easy / Easy)   -> 3 nodes, no corruption
///   Tier 2      (Normal)              -> 4 nodes, no corruption
///   Tier 3      (Hard)                -> 5 nodes, corruption every 20s
///   Tier 4      (Very Hard)           -> 6 nodes, corruption every 12s
///
/// INSPECTOR SETUP
/// ---------------
///   nodePrefab          Prefab with LLNodeBlock + LLNextHandle child
///   headPointerPrefab   Prefab with LLHeadPointer + LLNextHandle child
///   nullTerminalPrefab  Prefab with LLNullTerminal + Image (RaycastTarget ON)
///   arrowPrefab         Prefab with LLArrow + Image
///   nodeContainer       RectTransform -- NO Layout Group; pivot (0.5, 0.5)
///   feedbackPopup       PuzzleFeedbackPopup child panel (starts inactive)
///   instructionText     TMP for task description
///   statusText          TMP for live console output (optional)
///   submitButton        Optional -- calls CheckSolution
///   closeButton         Calls ClosePanel
///
/// NOTES
/// -----
///   - Time.timeScale = 0 while open (set by the prop); ALL timers use
///     Time.realtimeSinceStartup.
///   - Arrows render as first siblings so they appear behind nodes.
///   - Wrong answers do NOT reset the board -- the player keeps their wiring.
///   - addressText on LLNodeBlock prefab is optional but adds terminal flavor.
/// </summary>
public class LinkedListPuzzleUI : MonoBehaviour
{
    // ── Static ────────────────────────────────────────────────────────────────
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
    [SerializeField] private RectTransform       nodeContainer;
    [SerializeField] private PuzzleFeedbackPopup feedbackPopup;
    [SerializeField] private TextMeshProUGUI     instructionText;
    [SerializeField] private TextMeshProUGUI     statusText;       // live console line (optional)
    [SerializeField] private TextMeshProUGUI     referenceText;    // static original-order display (optional)
    [SerializeField] private Button              submitButton;
    [SerializeField] private Button              closeButton;

    [Header("Layout - Grid")]
    // NOTE: field names kept as "scatter*" even though placement is now a fixed
    // grid, not a random scatter -- renaming would break the serialized values
    // already saved on the prefab (Unity keys them by field name).
    [Tooltip("Half-width of the area nodes are laid out in (pixels). Total width = 2x this.")]
    [SerializeField] private float scatterHalfW   = 320f;
    [Tooltip("Half-height of the area nodes are laid out in (pixels). Total height = 2x this.")]
    [SerializeField] private float scatterHalfH   = 140f;
    [Tooltip("Vertical offset of the entire grid zone from panel centre. Negative = push down.")]
    [SerializeField] private float scatterCentreY = -40f;
    [Tooltip("How far to the LEFT of the scatter area the HEAD box sits (pixels).")]
    [SerializeField] private float headSideOffset  = 80f;
    [Tooltip("How far to the RIGHT of the scatter area the NULL terminal sits (pixels).")]
    [SerializeField] private float nullSideOffset  = 80f;

    [Header("Corruption")]
    [Tooltip("Seconds between corruptions on Tier 3. 0 = disabled.")]
    [SerializeField] private float corruptionIntervalHard     = 20f;
    [Tooltip("Seconds between corruptions on Tier 4. 0 = disabled.")]
    [SerializeField] private float corruptionIntervalVeryHard = 12f;

    // ── Node / Arrow Collections ───────────────────────────────────────────────
    private List<LLNodeBlock> _nodes        = new();
    private LLHeadPointer     _headPointer;
    private LLNullTerminal    _nullTerminal;
    private List<LLArrow>     _staticArrows = new();
    private LLArrow           _pendingArrow;

    // ── Click State Machine ────────────────────────────────────────────────────
    private bool        _hasPending;
    private LLNodeBlock _pendingSourceNode;  // null when HEAD is the source
    private bool        _pendingSourceIsHead;

    // ── Puzzle State ───────────────────────────────────────────────────────────
    private int[]  _solution;
    private int[]  _originalValues;  // forward chain order -- shown as static reference
    private int    _attempts;
    private Action _onClose;

    // ── Corruption State ───────────────────────────────────────────────────────
    private float _corruptionInterval;  // 0 = disabled for this tier
    private float _nextCorruptionTime;

    // ── Canvas / Camera ────────────────────────────────────────────────────────
    private Camera _uiCamera;  // null for Overlay canvas

    // ── Unity ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        // Resolve UI camera once -- handles both Overlay and Camera render modes.
        var canvas = GetComponentInParent<Canvas>();
        _uiCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera
            : null;
    }

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

    void Update()
    {
        // Track mouse so the ghost arrow tip follows the cursor while a
        // connection is pending. (Replaces the old drag OnHandleDragging path.)
        if (_hasPending && _pendingArrow != null && Mouse.current != null)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                nodeContainer, Mouse.current.position.ReadValue(), _uiCamera, out local);
            _pendingArrow.SetFloatingEnd(local);
        }

        // Corruption timer -- must use realtimeSinceStartup; timeScale is 0.
        if (_corruptionInterval > 0f
            && Time.realtimeSinceStartup >= _nextCorruptionTime)
        {
            CorruptRandomPointer();
            _nextCorruptionTime = Time.realtimeSinceStartup + _corruptionInterval;
        }
    }

    // ── Public Entry Point ────────────────────────────────────────────────────
    /// <summary>Called by the prop to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose  = onClose;
        _attempts = 0;
        _hasPending = false;

        if (feedbackPopup != null) feedbackPopup.gameObject.SetActive(false);

        PlayerMetricsTracker.Instance?.NotifyLinkedListStarted();

        SetInstructionText();
        SetStatus("> AWAITING INPUT...");

        GeneratePuzzle();
    }

    // ── Puzzle Generation ─────────────────────────────────────────────────────
    private void GeneratePuzzle()
    {
        ClearAll();

        int   count    = NodeCountForCurrentTier();
        int[] values   = GenerateUniqueValues(count);
        int[] hexSeeds = GenerateHexSeeds(count);
        _originalValues = values;                       // forward order -- for reference display
        _solution       = values.Reverse().ToArray();   // reversed order -- the correct answer

        // Lay nodes out on a fixed grid, in spawn-order index -- position never
        // depends on the solution, so the layout can't leak the answer.
        Vector2[] positions = GenerateGridPositions(count);

        for (int i = 0; i < count; i++)
        {
            var go   = Instantiate(nodePrefab, nodeContainer);
            var node = go.GetComponent<LLNodeBlock>();
            string addr = "0x" + hexSeeds[i].ToString("X4");
            // Apply scatterCentreY offset so nodes sit below the instruction text.
            node.Init(values[i], positions[i] + new Vector2(0f, scatterCentreY), this, addr);
            _nodes.Add(node);
        }

        // NULL terminal -- far right, vertically centred on the scatter zone.
        var nullGo    = Instantiate(nullTerminalPrefab, nodeContainer);
        _nullTerminal = nullGo.GetComponent<LLNullTerminal>();
        _nullTerminal.Init(this);
        _nullTerminal.SetPosition(new Vector2(scatterHalfW + nullSideOffset, scatterCentreY));

        // HEAD pointer -- far left, vertically centred on the scatter zone.
        var headGo   = Instantiate(headPointerPrefab, nodeContainer);
        _headPointer = headGo.GetComponent<LLHeadPointer>();
        _headPointer.Init(_nodes[0], this, new Vector2(-(scatterHalfW + headSideOffset), scatterCentreY));

        // Wire initial forward chain: 0->1->2->...->null
        for (int i = 0; i < _nodes.Count - 1; i++)
            _nodes[i].SetNext(_nodes[i + 1]);
        _nodes[^1].SetNext(null);

        RebuildArrows();
        SetReferenceText();

        // Set corruption interval for this tier
        int tier = CurrentTier();
        _corruptionInterval = tier switch
        {
            3 => corruptionIntervalHard,
            4 => corruptionIntervalVeryHard,
            _ => 0f
        };
        _nextCorruptionTime = Time.realtimeSinceStartup + _corruptionInterval;
    }

    // ── Grid Placement ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates <paramref name="count"/> positions on a fixed cols x rows grid,
    /// filled in spawn-order (node i always sits in cell i, reading left-to-right,
    /// top-to-bottom). No randomness -- deterministic placement means:
    ///   1. The grid cell size only has to cover one card, not a card plus jitter,
    ///      so real margins are guaranteed between neighbours.
    ///   2. Node position never correlates with the solution, so a fixed layout
    ///      doesn't leak the answer.
    /// </summary>
    private Vector2[] GenerateGridPositions(int count)
    {
        int cols   = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count)));
        int rows   = Mathf.CeilToInt((float)count / cols);
        float cellW = (scatterHalfW * 2f) / cols;
        float cellH = (scatterHalfH * 2f) / rows;

        var positions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float cx = -scatterHalfW + col * cellW + cellW * 0.5f;
            float cy =  scatterHalfH - row * cellH - cellH * 0.5f;

            positions[i] = new Vector2(cx, cy);
        }

        return positions;
    }

    // ── Click Handlers (called by LLNextHandle, LLNodeBlock, LLNullTerminal) ───

    /// <summary>Port handle on a regular node was clicked.</summary>
    public void OnNodeHandleClicked(LLNodeBlock fromNode)
    {
        // Clicking the already-selected port cancels the pending connection.
        if (_hasPending && !_pendingSourceIsHead && _pendingSourceNode == fromNode)
        {
            CancelPending();
            SetStatus("> CONNECTION CANCELLED");
            return;
        }
        CancelPending();
        SetPendingSource(fromNode, isHead: false);
    }

    /// <summary>Port handle on the HEAD widget was clicked.</summary>
    public void OnHeadHandleClicked()
    {
        // Clicking HEAD handle while HEAD is already the pending source cancels.
        if (_hasPending && _pendingSourceIsHead)
        {
            CancelPending();
            SetStatus("> CONNECTION CANCELLED");
            return;
        }
        CancelPending();
        SetPendingSource(null, isHead: true);
    }

    /// <summary>
    /// A node BODY (not the port handle) was clicked.
    /// If a connection is pending, land it here.
    /// If no connection is pending, do nothing (port must be clicked first).
    /// </summary>
    public void OnNodeBodyClicked(LLNodeBlock targetNode)
    {
        if (!_hasPending) return;

        // Prevent a non-head source from pointing to itself.
        if (!_pendingSourceIsHead && targetNode == _pendingSourceNode)
        {
            CancelPending();
            SetStatus("> SELF-POINTER REJECTED -- SELECT A DIFFERENT TARGET");
            return;
        }

        if (_pendingSourceIsHead)
            _headPointer.SetTarget(targetNode);
        else
            _pendingSourceNode.SetNext(targetNode);

        string srcLabel = _pendingSourceIsHead ? "HEAD" : _pendingSourceNode.Value.ToString();
        SetStatus($"> PTR [{srcLabel}] -> [{targetNode.Value}]  UPDATED");

        CancelPending();
        RebuildArrows();
    }

    /// <summary>
    /// The NULL terminal was clicked.
    /// Terminates the pending connection (sets next = null).
    /// HEAD cannot point to NULL -- click is ignored in that case.
    /// </summary>
    public void OnNullTerminalClicked()
    {
        if (!_hasPending) return;

        if (_pendingSourceIsHead)
        {
            SetStatus("> ERROR: HEAD MUST POINT TO A VALID NODE");
            CancelPending();
            return;
        }

        _pendingSourceNode.SetNext(null);
        SetStatus($"> PTR [{_pendingSourceNode.Value}] -> [NULL_PTR]  UPDATED");

        CancelPending();
        RebuildArrows();
    }

    // ── Pending State ─────────────────────────────────────────────────────────
    private void SetPendingSource(LLNodeBlock sourceNode, bool isHead)
    {
        _hasPending           = true;
        _pendingSourceNode    = sourceNode;
        _pendingSourceIsHead  = isHead;

        if (isHead)
        {
            _headPointer?.SetSourceSelected(true);
            SetStatus("> HEAD PORT SELECTED -- CLICK DESTINATION NODE");
            BeginFloatingArrow(_headPointer.HandleRect);
        }
        else
        {
            sourceNode.SetSourceSelected(true);
            SetStatus($"> NODE [{sourceNode.Value}] PORT SELECTED -- CLICK DESTINATION");
            BeginFloatingArrow(sourceNode.HandleRect);
        }
    }

    private void CancelPending()
    {
        if (_pendingSourceIsHead)
            _headPointer?.SetSourceSelected(false);
        else
            _pendingSourceNode?.SetSourceSelected(false);

        _hasPending          = false;
        _pendingSourceNode   = null;
        _pendingSourceIsHead = false;

        DestroyPendingArrow();
    }

    // ── Arrow Management ──────────────────────────────────────────────────────
    //
    // Orthogonal elbow router: every committed connection renders as either one
    // straight segment (source and target share a row or column) or a 2-segment
    // elbow (horizontal leg out of the source, vertical leg into the target).
    // Angles are always exactly 0/90/180/270 -- there is no diagonal case.
    //
    // Multiple connections can share the same horizontal or vertical corridor
    // (e.g. two arrows both bending through row Y=40). Those are grouped into
    // "lanes" and nudged apart by laneStep pixels so they don't render on top
    // of each other. Applies uniformly to node-node, HEAD-node and node-NULL
    // connections since it only looks at the actual computed positions, not at
    // grid row/col indices.

    private const float RowColEpsilon = 6f;   // treat a Y/X gap this small as "aligned"
    private const float CorridorGrid  = 10f;  // corridor key rounding, groups near-identical rows/cols together
    private const float LaneStep      = 14f;  // pixel offset between parallel arrows sharing a corridor

    private enum ConnectionKind { Horizontal, Vertical, Elbow }

    public void RebuildArrows()
    {
        foreach (var a in _staticArrows) if (a) Destroy(a.gameObject);
        _staticArrows.Clear();

        // Collect every active connection as (source handle, target rect).
        var connections = new List<(RectTransform src, RectTransform tgt)>();

        if (_headPointer?.TargetNode != null)
            connections.Add((_headPointer.HandleRect, _headPointer.TargetNode.NodeRect));

        foreach (var node in _nodes)
        {
            RectTransform target = node.NextNode != null
                ? node.NextNode.NodeRect
                : _nullTerminal.TerminalRect;
            connections.Add((node.HandleRect, target));
        }

        int n = connections.Count;
        if (n == 0) return;

        // Pass 1: classify each connection and record which corridors it uses.
        var kinds      = new ConnectionKind[n];
        var srcPoints  = new Vector2[n];
        var tgtPoints  = new Vector2[n];
        var rowGroups  = new Dictionary<float, List<int>>(); // horizontal corridor key -> connection indices
        var colGroups  = new Dictionary<float, List<int>>(); // vertical corridor key   -> connection indices

        for (int i = 0; i < n; i++)
        {
            Vector2 s = nodeContainer.InverseTransformPoint(connections[i].src.position);
            Vector2 t = nodeContainer.InverseTransformPoint(connections[i].tgt.position);
            srcPoints[i] = s;
            tgtPoints[i] = t;

            bool sameRow = Mathf.Abs(s.y - t.y) <= RowColEpsilon;
            bool sameCol = Mathf.Abs(s.x - t.x) <= RowColEpsilon;

            if (sameRow && !sameCol)
            {
                kinds[i] = ConnectionKind.Horizontal;
                AddToCorridor(rowGroups, RoundToCorridor(s.y), i);
            }
            else if (sameCol)
            {
                kinds[i] = ConnectionKind.Vertical;
                AddToCorridor(colGroups, RoundToCorridor(s.x), i);
            }
            else
            {
                kinds[i] = ConnectionKind.Elbow;
                AddToCorridor(rowGroups, RoundToCorridor(s.y), i); // horizontal leg out of source
                AddToCorridor(colGroups, RoundToCorridor(t.x), i); // vertical leg into target
            }
        }

        // Pass 2: now that corridor membership is known, build the actual segments.
        for (int i = 0; i < n; i++)
        {
            Vector2 s = srcPoints[i];
            Vector2 t = tgtPoints[i];
            RectTransform tgtRect = connections[i].tgt;

            switch (kinds[i])
            {
                case ConnectionKind.Horizontal:
                {
                    float laneY = LaneOffset(rowGroups[RoundToCorridor(s.y)], i);
                    Vector2 a = new Vector2(s.x, s.y + laneY);
                    Vector2 b = new Vector2(t.x - Mathf.Sign(t.x - s.x) * HalfW(tgtRect), t.y + laneY);
                    _staticArrows.Add(SpawnSegment(a, b, showHead: true));
                    break;
                }
                case ConnectionKind.Vertical:
                {
                    float laneX = LaneOffset(colGroups[RoundToCorridor(s.x)], i);
                    Vector2 a = new Vector2(s.x + laneX, s.y);
                    Vector2 b = new Vector2(t.x + laneX, t.y - Mathf.Sign(t.y - s.y) * HalfH(tgtRect));
                    _staticArrows.Add(SpawnSegment(a, b, showHead: true));
                    break;
                }
                case ConnectionKind.Elbow:
                {
                    float laneY = LaneOffset(rowGroups[RoundToCorridor(s.y)], i);
                    float laneX = LaneOffset(colGroups[RoundToCorridor(t.x)], i);

                    Vector2 exitPt  = new Vector2(s.x, s.y + laneY);
                    Vector2 bendPt  = new Vector2(t.x + laneX, s.y + laneY);
                    Vector2 entryPt = new Vector2(t.x + laneX, t.y - Mathf.Sign(t.y - s.y) * HalfH(tgtRect));

                    _staticArrows.Add(SpawnSegment(exitPt, bendPt, showHead: false));
                    _staticArrows.Add(SpawnSegment(bendPt, entryPt, showHead: true));
                    break;
                }
            }
        }
    }

    private static float RoundToCorridor(float v) => Mathf.Round(v / CorridorGrid) * CorridorGrid;

    private static void AddToCorridor(Dictionary<float, List<int>> groups, float key, int index)
    {
        if (!groups.TryGetValue(key, out var list))
        {
            list = new List<int>();
            groups[key] = list;
        }
        list.Add(index);
    }

    /// <summary>Centred lane offset for connectionIndex within its corridor group.</summary>
    private static float LaneOffset(List<int> corridor, int connectionIndex)
    {
        int slot  = corridor.IndexOf(connectionIndex);
        int count = corridor.Count;
        return (slot - (count - 1) * 0.5f) * LaneStep;
    }

    private static float HalfW(RectTransform rt) => rt.rect.width  * 0.5f;
    private static float HalfH(RectTransform rt) => rt.rect.height * 0.5f;

    private LLArrow SpawnSegment(Vector2 pointA, Vector2 pointB, bool showHead)
    {
        var go = Instantiate(arrowPrefab, nodeContainer);
        go.transform.SetAsFirstSibling();
        var arrow = go.GetComponent<LLArrow>();
        arrow.InitPoints(pointA, pointB, showHead);
        return arrow;
    }

    private void BeginFloatingArrow(RectTransform source)
    {
        DestroyPendingArrow();
        var go = Instantiate(arrowPrefab, nodeContainer);
        go.transform.SetAsFirstSibling();
        _pendingArrow = go.GetComponent<LLArrow>();
        _pendingArrow.Init(source, null, nodeContainer);
    }

    private void DestroyPendingArrow()
    {
        if (_pendingArrow != null) Destroy(_pendingArrow.gameObject);
        _pendingArrow = null;
    }

    // ── Corruption ────────────────────────────────────────────────────────────
    private void CorruptRandomPointer()
    {
        if (_nodes.Count < 2) return;

        // Pick a random node that is not the current pending source.
        var candidates = _nodes
            .Where(n => n != _pendingSourceNode)
            .ToList();
        if (candidates.Count == 0) return;

        var victim = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        // Assign a random different target (different from current next).
        var wrongTargets = _nodes
            .Where(n => n != victim.NextNode)
            .Cast<LLNodeBlock>()
            .ToList();
        if (wrongTargets.Count == 0) return;

        var wrongTarget = wrongTargets[UnityEngine.Random.Range(0, wrongTargets.Count)];
        victim.SetNext(wrongTarget);

        RebuildArrows();
        StartCoroutine(FlashCorrupted(victim));

        SetStatus($"!! MEMORY CORRUPTION AT ADDR [{victim.Value}] -- POINTER OVERWRITTEN");
    }

    private IEnumerator FlashCorrupted(LLNodeBlock node)
    {
        node.SetCorrupted(true);
        // Wait in real time -- yield return new WaitForSecondsRealtime is immune to timeScale=0.
        yield return new WaitForSecondsRealtime(0.8f);
        node.SetCorrupted(false);
    }

    // ── Solution Check ────────────────────────────────────────────────────────
    public void CheckSolution()
    {
        // Cancel any open pending connection before checking.
        if (_hasPending) CancelPending();

        if (_headPointer == null || _headPointer.TargetNode == null)
        {
            feedbackPopup?.Show(false,
                "!! ERROR: HEAD POINTER IS NULL\n" +
                "!! ASSIGN HEAD BEFORE EXECUTING PATCH");
            return;
        }

        // Traverse the rewired list.
        var traversed = new List<int>();
        var visited   = new HashSet<LLNodeBlock>();
        var current   = _headPointer.TargetNode;

        while (current != null)
        {
            if (visited.Contains(current))
            {
                _attempts++;
                feedbackPopup?.Show(false,
                    "!! TRAVERSAL FAILED: CYCLE DETECTED\n" +
                    "!! A NODE'S POINTER LOOPS BACK ON ITSELF\n" +
                    $"!! RESUBMIT PATCH [ATTEMPT {_attempts}]");
                return;
            }
            visited.Add(current);
            traversed.Add(current.Value);
            current = current.NextNode;
        }

        bool correct = traversed.Count == _solution.Length
                    && traversed.SequenceEqual(_solution);

        if (correct)
        {
            // Disable corruption once solved.
            _corruptionInterval = 0f;
            OnLinkedListSolved?.Invoke(_attempts);

            feedbackPopup?.Show(true,
                "> PATCH ACCEPTED\n" +
                "> MEMORY BLOCK REVERSED SUCCESSFULLY\n" +
                "> ACCESS GRANTED",
                onDismiss: ClosePanel);
        }
        else
        {
            _attempts++;
            string actual   = string.Join("->", traversed) + "->NULL";
            string expected = string.Join("->", _solution) + "->NULL";

            string msg = _attempts > 2
                ? $"!! PATCH REJECTED [ATTEMPT {_attempts}]\n" +
                  $"!! CURRENT ORDER:  {actual}\n" +
                  $"!! REQUIRED ORDER: {expected}"
                : $"!! PATCH REJECTED [ATTEMPT {_attempts}]\n" +
                  $"!! CURRENT ORDER:  {actual}\n" +
                  $"!! REWIRE AND RESUBMIT";

            feedbackPopup?.Show(false, msg);
        }
    }

    public void ClosePanel()
    {
        CancelPending();
        _onClose?.Invoke();
    }

    // ── UI Text Helpers ───────────────────────────────────────────────────────
    private void SetInstructionText()
    {
        if (!instructionText) return;
        instructionText.text = "> TASK: REVERSE THE LINKED LIST  |  [->] SELECT   BODY CONNECT   NULL_PTR TERMINATE";
    }

    /// <summary>
    /// Builds the static reference line showing the ORIGINAL forward chain.
    /// Displayed once after puzzle generation and never updated -- it is the
    /// player's reference so they know the starting order even after rewiring.
    /// Example output: "> ORIGINAL: [16] -> [14] -> [15] -> NULL_PTR"
    /// </summary>
    private void SetReferenceText()
    {
        if (!referenceText || _originalValues == null) return;

        var parts = new System.Text.StringBuilder("> ORIGINAL: ");
        for (int i = 0; i < _originalValues.Length; i++)
        {
            parts.Append('[');
            parts.Append(_originalValues[i]);
            parts.Append(']');
            parts.Append(" -> ");
        }
        parts.Append("NULL_PTR");

        referenceText.text = parts.ToString();
    }

    private void SetStatus(string line)
    {
        if (statusText) statusText.text = line;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private int CurrentTier()
    {
        return PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.LinkedLists)
            : 2;
    }

    private int NodeCountForCurrentTier()
    {
        return CurrentTier() switch
        {
            0 => 3,
            1 => 3,
            2 => 4,
            3 => 5,
            4 => 6,
            _ => 4
        };
    }

    private static int[] GenerateUniqueValues(int count)
    {
        var pool   = Enumerable.Range(1, 20).ToList();
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            int idx   = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        return result;
    }

    /// <summary>
    /// Generates unique-ish hex seed values for fake memory addresses on each node.
    /// These are purely cosmetic -- they do not affect puzzle logic.
    /// </summary>
    private static int[] GenerateHexSeeds(int count)
    {
        var seeds = new int[count];
        int base_ = UnityEngine.Random.Range(0x1000, 0xD000);
        for (int i = 0; i < count; i++)
            seeds[i] = base_ + i * 0x40 + UnityEngine.Random.Range(0, 0x20);
        return seeds;
    }

    private void ClearAll()
    {
        CancelPending();
        foreach (var n in _nodes)        if (n) Destroy(n.gameObject);
        foreach (var a in _staticArrows) if (a) Destroy(a.gameObject);
        _nodes.Clear();
        _staticArrows.Clear();

        if (_headPointer)  Destroy(_headPointer.gameObject);
        if (_nullTerminal) Destroy(_nullTerminal.gameObject);
        _headPointer  = null;
        _nullTerminal = null;
    }
}

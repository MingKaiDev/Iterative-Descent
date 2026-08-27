using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ARBITEX Reagent Routing -- Dijkstra's Shortest Path Puzzle (Chemistry Lab)
///
/// WHAT THE PLAYER DOES
/// --------------------
/// A weighted pipe network is shown on a schematic panel. Every pipe segment
/// (edge) has a flow-resistance cost, visible up front -- unlike DrainPuzzle,
/// there is no fog-of-war/hidden-node discovery here. The player builds a
/// route node-by-node from INTAKE to CHAMBER by clicking adjacent junctions,
/// then commits. The route is only accepted if its total cost equals the
/// true minimum-cost (Dijkstra) route, not merely a route that reaches the
/// target.
///
/// RELATIONSHIP TO DrainPuzzle
/// ---------------------------
/// DrainPuzzle teaches BFS/DFS (unweighted graph, traversal ORDER via a
/// queue/stack). This teaches Dijkstra's algorithm (weighted graph, path
/// COST). Deliberately separate BKT concept key (dijkstra, prerequisite
/// bfs_dfs per bkt-concept-map.md) and deliberately separate mechanic --
/// see the FYP setup guide for the full design rationale.
///
/// ON A FAILED (NON-OPTIMAL) COMMIT
/// ---------------------------------
/// A popup always shows the cost delta (your cost vs the true minimum).
/// The true optimal path itself is only revealed overlaid in a distinct
/// colour once the player has racked up `attemptsBeforeReveal` (default 3)
/// failed commits on the SAME graph -- below that threshold they're told
/// they were wrong and by how much, but the answer stays hidden, and the
/// popup tells them how many more misses until it reveals.
///
/// The shared PuzzleFeedbackPopup only has a single OK/dismiss button (see
/// PuzzleFeedbackPopup.cs), so unlike the prototype's two-button "Retry /
/// New Graph" choice, OK here simply hides the popup. The player can then
/// either press UNDO to back off just their last step and try a different
/// branch (re-committing is allowed the moment the path reaches CHAMBER
/// again -- no full walk from INTAKE required), or press RESET ROUTE to
/// clear back to INTAKE entirely, or close the terminal to get a fresh
/// graph next visit. Any optimal-path reveal stays on screen as a running
/// reference while the player keeps adjusting; it only clears on RESET
/// ROUTE or on solving.
///
/// PASS CONDITION
/// --------------
/// Exact match to the optimal cost is required, consistent with Stack and
/// PacketFilter's precise win conditions in this project. Confirmed with
/// the user -- no tolerance.
///
/// TIER SUMMARY (ported and verified against the HTML prototype: 2500
/// randomised trials against a brute-force path search, zero mismatches)
/// ------------------------------------------------------------------
///   Tier 0 -- 5 nodes,  0 skip-edges, cost range  2-9
///   Tier 1 -- 6 nodes,  1 skip-edge,  cost range  2-12
///   Tier 2 -- 7 nodes,  1 skip-edge,  cost range  3-15
///   Tier 3 -- 8 nodes,  2 skip-edges, cost range  3-18
///   Tier 4 -- 9 nodes,  3 skip-edges, cost range  4-22
///
/// INSPECTOR SETUP
/// ----------------
/// See FYP/setup-guides/ReagentRoutingPuzzle_UnitySetup.md for the full
/// Canvas hierarchy and RectTransform settings.
/// </summary>
public class ReagentRoutingUI : MonoBehaviour
{
    // =========================================================================
    // Types
    // =========================================================================

    private enum NodeKind { Source, Target, Junction }
    private enum NodeVisualState { Source, Target, Selected, Available, Optimal, Junction }

    private class RNode
    {
        public int      Id;
        public string   Label;
        public NodeKind Kind;
        public int      Layer;
        public int      IndexInLayer;
        public int      CountInLayer;
        public Vector2  NormPos; // 0..1, same convention as DrainPuzzleUI
    }

    private class REdge
    {
        public int From;
        public int To;
        public int Cost;
    }

    // =========================================================================
    // Singleton and Static Event
    // =========================================================================

    public static ReagentRoutingUI Instance { get; private set; }

    /// <summary>Fired on solve. Argument = total failed commit attempts this session.</summary>
    public static event Action<int> OnReagentRoutingSolved;

    // =========================================================================
    // Tier Configuration (ported 1:1 from the verified HTML/JS prototype)
    // =========================================================================

    private static readonly int[][] LayerConfig =
    {
        new[] { 1, 2, 1, 1 },       // Tier 0 -- 5 nodes
        new[] { 1, 3, 1, 1 },       // Tier 1 -- 6 nodes
        new[] { 1, 2, 2, 1, 1 },    // Tier 2 -- 7 nodes
        new[] { 1, 3, 2, 1, 1 },    // Tier 3 -- 8 nodes
        new[] { 1, 3, 3, 1, 1 },    // Tier 4 -- 9 nodes
    };

    private static readonly (int min, int max)[] CostRange =
    {
        (2, 9), (2, 12), (3, 15), (3, 18), (4, 22)
    };

    private static readonly int[] SkipEdgeCount = { 0, 1, 1, 2, 3 };

    // =========================================================================
    // Inspector: Graph Area
    // =========================================================================

    [Header("Graph")]
    [Tooltip("RectTransform containing edge Images. Must be behind nodeContainer in hierarchy.")]
    [SerializeField] private RectTransform edgeContainer;

    [Tooltip("RectTransform containing edge cost chip labels. Must be above edgeContainer, behind nodeContainer.")]
    [SerializeField] private RectTransform chipContainer;

    [Tooltip("RectTransform containing node GameObjects. No Layout Group -- positions set manually.")]
    [SerializeField] private RectTransform nodeContainer;

    [Tooltip("Prefab: root Image (uses ReagentRouting_NodeValve sprite, tint set at runtime) + Button + child TMP named 'LabelText'.")]
    [SerializeField] private GameObject nodePrefab;

    [Tooltip("Prefab: root Image (uses ReagentRouting_PipeSegment sprite). Pivot (0,0.5) -- script sizes and rotates it.")]
    [SerializeField] private GameObject edgePrefab;

    [Tooltip("Prefab: root Image (uses ReagentRouting_CostChip sprite) + child TMP named 'CostText'.")]
    [SerializeField] private GameObject costChipPrefab;

    // =========================================================================
    // Inspector: Status / Info Panel
    // =========================================================================

    [Header("Info Panel")]
    [SerializeField] private TextMeshProUGUI tierBadgeText;
    [SerializeField] private TextMeshProUGUI routeCostText;
    [SerializeField] private TextMeshProUGUI attemptsText;
    [SerializeField] private TextMeshProUGUI statusText;

    // =========================================================================
    // Inspector: Win Panel
    // =========================================================================

    [Header("Win Panel")]
    [SerializeField] private GameObject      winPanel;
    [SerializeField] private TextMeshProUGUI winText;

    // =========================================================================
    // Inspector: Buttons
    // =========================================================================

    [Header("Buttons")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button commitButton;
    [SerializeField] private Button closeButton;

    // =========================================================================
    // Inspector: Feedback Popup
    // =========================================================================

    [Header("Feedback")]
    [SerializeField] private ReagentFeedbackPopup feedbackPopup;

    [Header("Reveal Tuning")]
    [Tooltip("Number of failed (non-optimal) commits on this graph before the true " +
             "optimal path is revealed overlaid. Below this, the player is told they " +
             "were wrong and by how much, but the answer itself stays hidden.")]
    [SerializeField] private int attemptsBeforeReveal = 3;

    // =========================================================================
    // Colours (amber/copper industrial-chemical palette --
    // deliberately distinct from Drain's green CRT / Stack's charcoal PDU /
    // PacketFilter's green terminal)
    // =========================================================================

    private static readonly Color ColSource    = new Color(0.27f, 0.67f, 0.51f, 1f); // teal-green -- INTAKE
    private static readonly Color ColTarget    = new Color(0.78f, 0.27f, 0.22f, 1f); // hazard red -- CHAMBER
    private static readonly Color ColSelected  = new Color(0.90f, 0.67f, 0.24f, 1f); // amber -- your route
    private static readonly Color ColAvailable = new Color(1.00f, 0.78f, 0.43f, 1f); // bright copper glow -- clickable next
    private static readonly Color ColOptimal   = new Color(0.35f, 0.82f, 0.90f, 1f); // cyan -- revealed optimum
    private static readonly Color ColJunction  = new Color(0.51f, 0.41f, 0.27f, 1f); // dim copper -- idle junction

    private static readonly Color ColEdgeDim      = new Color(0.35f, 0.30f, 0.24f, 0.45f);
    private static readonly Color ColEdgeSelected = ColSelected;
    private static readonly Color ColEdgeOptimal  = ColOptimal;

    // =========================================================================
    // Runtime State
    // =========================================================================

    private int            _tier;
    private List<RNode>    _nodes;
    private List<REdge>    _edges;
    private int            _sourceId;
    private int            _targetId;

    private List<int>      _path;          // always starts with _sourceId
    private bool           _committed;     // true after a commit attempt (pass or fail) until Reset/solve
    private bool           _done;          // true once solved
    private int            _attempts;      // failed commit count this puzzle session

    private List<int>      _optimalPath;   // populated on commit, shown if the commit failed
    private int            _optimalCost;
    private bool           _showOptimal;

    private Action         _onClose;

    private GameObject[]           _nodeObjects;   // indexed by node id
    private Dictionary<(int, int), GameObject> _edgeObjects;
    private Dictionary<(int, int), GameObject> _chipObjects;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        undoButton?.onClick.AddListener(OnUndoClicked);
        resetButton?.onClick.AddListener(OnResetClicked);
        commitButton?.onClick.AddListener(OnCommitClicked);
        closeButton?.onClick.AddListener(ClosePanel);
    }

    private void OnDisable()
    {
        undoButton?.onClick.RemoveListener(OnUndoClicked);
        resetButton?.onClick.RemoveListener(OnResetClicked);
        commitButton?.onClick.RemoveListener(OnCommitClicked);
        closeButton?.onClick.RemoveListener(ClosePanel);
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>Called by ReagentRoutingProp to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose = onClose;

        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);

        // NotifyReagentRoutingStarted() lives in PlayerMetricsTracker.InitPuzzle-equivalent --
        // see the setup guide's PlayerMetricsTracker patch. Do NOT also call it from the prop
        // (same rule as every other puzzle in this project -- would double-count attempts).
        PlayerMetricsTracker.Instance?.NotifyReagentRoutingStarted();

        GeneratePuzzle();
    }

    // =========================================================================
    // Generation
    // =========================================================================

    private void GeneratePuzzle()
    {
        _tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.Dijkstra)
            : 2;
        _tier = Mathf.Clamp(_tier, 0, LayerConfig.Length - 1);

        _done       = false;
        _committed  = false;
        _showOptimal = false;
        _attempts   = 0;
        _optimalPath = null;

        BuildGraph(_tier);

        _path = new List<int> { _sourceId };

        ClearGraph();
        SpawnNodes();
        StartCoroutine(SpawnEdgesAfterLayout());

        if (winPanel != null) winPanel.SetActive(false);
        if (tierBadgeText != null)
            tierBadgeText.text = $"TIER {_tier} -- {PuzzleDDAController.TierNames[_tier].ToUpperInvariant()}";

        RefreshInfoPanel();
        SetStatus("Click INTAKE, then click connected junctions to route to CHAMBER.");
    }

    /// <summary>
    /// Builds a layered directed graph: source -> N mid-layers -> target, plus
    /// a small number of "skip" edges (layer i -> layer i+2) for branching
    /// decoy shortcuts. Guarantees every node has both an incoming (except
    /// source) and outgoing (except target) edge, so the target is always
    /// reachable. Logic ported 1:1 from the verified HTML/JS prototype
    /// (2500 randomised trials against a brute-force search, zero mismatches).
    /// </summary>
    private void BuildGraph(int tier)
    {
        int[] sizes = LayerConfig[tier];
        (int min, int max) cost = CostRange[tier];

        _nodes = new List<RNode>();
        var layers = new List<List<int>>();
        int counter = 0;
        int junctionCounter = 1;

        for (int li = 0; li < sizes.Length; li++)
        {
            var layerNodes = new List<int>();
            int size = sizes[li];
            for (int i = 0; i < size; i++)
            {
                int id = counter++;
                NodeKind kind = li == 0 ? NodeKind.Source
                              : li == sizes.Length - 1 ? NodeKind.Target
                              : NodeKind.Junction;
                string label = kind == NodeKind.Source ? "INTAKE"
                             : kind == NodeKind.Target ? "CHAMBER"
                             : $"J{junctionCounter++}";

                layerNodes.Add(id);
                _nodes.Add(new RNode
                {
                    Id = id, Label = label, Kind = kind,
                    Layer = li, IndexInLayer = i, CountInLayer = size
                });
            }
            layers.Add(layerNodes);
        }

        _sourceId = layers[0][0];
        _targetId = layers[layers.Count - 1][0];

        _edges = new List<REdge>();
        var edgeSet = new HashSet<(int, int)>();

        Action<int, int> addEdge = (from, to) =>
        {
            var key = (from, to);
            if (edgeSet.Contains(key)) return;
            edgeSet.Add(key);
            _edges.Add(new REdge { From = from, To = to, Cost = UnityEngine.Random.Range(cost.min, cost.max + 1) });
        };

        for (int li = 0; li < layers.Count - 1; li++)
        {
            List<int> cur = layers[li], next = layers[li + 1];

            // Every node in `next` gets >=1 incoming edge from `cur`.
            foreach (int n in next)
                addEdge(cur[UnityEngine.Random.Range(0, cur.Count)], n);

            // Every node in `cur` gets >=1 outgoing edge to `next`.
            foreach (int c in cur)
            {
                if (!_edges.Any(e => e.From == c))
                    addEdge(c, next[UnityEngine.Random.Range(0, next.Count)]);
            }

            // Extra branching edges for decoy paths.
            int extra = Mathf.Max(0, Mathf.FloorToInt(cur.Count * next.Count * 0.3f));
            for (int k = 0; k < extra; k++)
                addEdge(cur[UnityEngine.Random.Range(0, cur.Count)], next[UnityEngine.Random.Range(0, next.Count)]);
        }

        int skipCount = SkipEdgeCount[tier];
        int guard = skipCount * 8;
        while (skipCount > 0 && guard > 0 && layers.Count >= 3)
        {
            guard--;
            int li = UnityEngine.Random.Range(0, layers.Count - 2);
            int from = layers[li][UnityEngine.Random.Range(0, layers[li].Count)];
            int to   = layers[li + 2][UnityEngine.Random.Range(0, layers[li + 2].Count)];
            if (!edgeSet.Contains((from, to)))
            {
                addEdge(from, to);
                skipCount--;
            }
        }

        int numLayers = sizes.Length;
        foreach (RNode n in _nodes)
        {
            float x = 0.08f + (n.Layer / (float)(numLayers - 1)) * 0.84f;
            float y = n.CountInLayer == 1 ? 0.5f : 0.15f + (n.IndexInLayer / (float)(n.CountInLayer - 1)) * 0.70f;
            n.NormPos = new Vector2(x, y);
        }
    }

    // =========================================================================
    // Dijkstra's Algorithm
    // =========================================================================

    private (Dictionary<int, int> dist, Dictionary<int, int> prev) Dijkstra(int sourceId)
    {
        var dist = new Dictionary<int, int>();
        var prev = new Dictionary<int, int>();
        var visited = new HashSet<int>();

        foreach (RNode n in _nodes) dist[n.Id] = int.MaxValue;
        dist[sourceId] = 0;

        var adjacency = new Dictionary<int, List<REdge>>();
        foreach (RNode n in _nodes) adjacency[n.Id] = new List<REdge>();
        foreach (REdge e in _edges) adjacency[e.From].Add(e);

        while (visited.Count < _nodes.Count)
        {
            int u = -1, best = int.MaxValue;
            foreach (RNode n in _nodes)
            {
                if (!visited.Contains(n.Id) && dist[n.Id] < best) { best = dist[n.Id]; u = n.Id; }
            }
            if (u == -1) break;
            visited.Add(u);

            foreach (REdge e in adjacency[u])
            {
                int alt = dist[u] + e.Cost;
                if (alt < dist[e.To]) { dist[e.To] = alt; prev[e.To] = u; }
            }
        }

        return (dist, prev);
    }

    private List<int> ShortestPath(Dictionary<int, int> prev, int sourceId, int targetId)
    {
        if (sourceId == targetId) return new List<int> { sourceId };
        if (!prev.ContainsKey(targetId)) return null;

        var path = new List<int> { targetId };
        int cur = targetId;
        while (cur != sourceId)
        {
            if (!prev.TryGetValue(cur, out cur)) return null;
            path.Insert(0, cur);
        }
        return path;
    }

    private int PathCost(List<int> path)
    {
        int total = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            REdge e = _edges.FirstOrDefault(x => x.From == path[i] && x.To == path[i + 1]);
            if (e != null) total += e.Cost;
        }
        return total;
    }

    private bool IsAdjacent(int a, int b) => _edges.Any(e => e.From == a && e.To == b);

    // =========================================================================
    // Node Spawning
    // =========================================================================

    private void SpawnNodes()
    {
        _nodeObjects = new GameObject[_nodes.Count];

        foreach (RNode n in _nodes)
        {
            if (nodePrefab == null) break;

            GameObject go = Instantiate(nodePrefab, nodeContainer);
            _nodeObjects[n.Id] = go;

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(n.NormPos.x, 1f - n.NormPos.y);
                rt.anchoredPosition = Vector2.zero;
            }

            Transform lblT = go.transform.Find("LabelText");
            if (lblT != null)
            {
                TextMeshProUGUI lbl = lblT.GetComponent<TextMeshProUGUI>();
                if (lbl != null) lbl.text = n.Label;
            }

            int capturedId = n.Id;
            Button btn = go.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnNodeClicked(capturedId));

            ApplyNodeVisual(n.Id);
        }
    }

    private IEnumerator SpawnEdgesAfterLayout()
    {
        yield return null; // wait one frame so nodeContainer.rect is valid

        _edgeObjects = new Dictionary<(int, int), GameObject>();
        _chipObjects = new Dictionary<(int, int), GameObject>();

        if (edgePrefab == null || edgeContainer == null) yield break;

        float w = nodeContainer.rect.width;
        float h = nodeContainer.rect.height;

        foreach (REdge e in _edges)
        {
            RNode from = _nodes[e.From];
            RNode to   = _nodes[e.To];

            Vector2 fromPos = new Vector2(from.NormPos.x * w, -(from.NormPos.y * h));
            Vector2 toPos   = new Vector2(to.NormPos.x * w, -(to.NormPos.y * h));

            GameObject edgeGo = Instantiate(edgePrefab, edgeContainer);
            edgeGo.name = $"Edge_{e.From}_{e.To}";
            _edgeObjects[(e.From, e.To)] = edgeGo;

            RectTransform rt = edgeGo.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 dir = toPos - fromPos;
                float length = dir.magnitude;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = fromPos;
                rt.sizeDelta = new Vector2(length, rt.sizeDelta.y);
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            Image img = edgeGo.GetComponent<Image>();
            if (img != null) img.color = ColEdgeDim;

            if (costChipPrefab != null && chipContainer != null)
            {
                Vector2 mid = (fromPos + toPos) / 2f;
                GameObject chipGo = Instantiate(costChipPrefab, chipContainer);
                chipGo.name = $"Chip_{e.From}_{e.To}";
                _chipObjects[(e.From, e.To)] = chipGo;

                RectTransform chipRt = chipGo.GetComponent<RectTransform>();
                if (chipRt != null)
                {
                    chipRt.anchorMin = chipRt.anchorMax = new Vector2(0f, 1f);
                    chipRt.anchoredPosition = mid;
                }

                Transform costT = chipGo.transform.Find("CostText");
                if (costT != null)
                {
                    TextMeshProUGUI costLbl = costT.GetComponent<TextMeshProUGUI>();
                    if (costLbl != null) costLbl.text = e.Cost.ToString();
                }
            }
        }

        RefreshAllEdgeVisuals();
    }

    // =========================================================================
    // Node Click Handling
    // =========================================================================

    private void OnNodeClicked(int id)
    {
        if (_done || _committed) return;
        if (feedbackPopup != null && feedbackPopup.gameObject.activeSelf) return;

        int last = _path[_path.Count - 1];
        if (id == last) return;
        if (_path.Contains(id)) return;
        if (!IsAdjacent(last, id))
        {
            SetStatus("[!] No pipe connects there directly.");
            return;
        }

        _path.Add(id);
        RefreshAllNodeVisuals();
        RefreshAllEdgeVisuals();

        SetStatus(id == _targetId
            ? "Route reaches CHAMBER. Commit when ready, or keep adjusting."
            : $"Routed to {_nodes[id].Label}.");

        RefreshInfoPanel();
    }

    private void OnUndoClicked()
    {
        // Deliberately allowed even while _committed is true (i.e. right after a
        // failed, non-optimal commit): un-committing here lets the player back off
        // just their last step and try a different branch instead of being forced
        // to press RESET ROUTE and re-walk the entire path from INTAKE. Any optimal-
        // path reveal stays on screen as a reference while they keep adjusting --
        // it only clears on RESET ROUTE or on solving.
        if (_done) return;
        if (_path.Count > 1)
        {
            _path.RemoveAt(_path.Count - 1);
            _committed = false;
            RefreshAllNodeVisuals();
            RefreshAllEdgeVisuals();
            RefreshInfoPanel();
            SetStatus("Step removed. Keep adjusting or route to CHAMBER again.");
        }
    }

    private void OnResetClicked()
    {
        if (_done) return;
        _path = new List<int> { _sourceId };
        _committed = false;
        _showOptimal = false;
        RefreshAllNodeVisuals();
        RefreshAllEdgeVisuals();
        RefreshInfoPanel();
        SetStatus("Route cleared. Click INTAKE-adjacent junctions to begin.");
    }

    private void OnCommitClicked()
    {
        if (_done || _committed) return;
        if (_path[_path.Count - 1] != _targetId) return;

        _committed = true;

        (Dictionary<int, int> dist, Dictionary<int, int> prev) = Dijkstra(_sourceId);
        _optimalPath = ShortestPath(prev, _sourceId, _targetId);
        _optimalCost = _optimalPath != null ? PathCost(_optimalPath) : dist[_targetId];
        int yourCost = PathCost(_path);

        if (yourCost == _optimalCost)
        {
            EndPuzzle(yourCost);
            return;
        }

        _attempts++;
        _showOptimal = _attempts >= attemptsBeforeReveal;
        RefreshAllNodeVisuals();
        RefreshAllEdgeVisuals();
        RefreshInfoPanel();

        int diff = yourCost - _optimalCost;
        string msg;
        if (_showOptimal)
        {
            msg = $"Route accepted but not optimal.\nYour cost: {yourCost}   Minimum possible: {_optimalCost}   Excess: {diff}\n" +
                  "The cheapest route is now highlighted. Press UNDO to adjust your last step, or RESET ROUTE to start over.";
        }
        else
        {
            int remaining = attemptsBeforeReveal - _attempts;
            msg = $"Route accepted but not optimal.\nYour cost: {yourCost}   Minimum possible: {_optimalCost}   Excess: {diff}\n" +
                  $"Press UNDO to adjust your last step, or RESET ROUTE to start over. " +
                  $"The optimal route will be revealed after {remaining} more incorrect attempt{(remaining == 1 ? "" : "s")}.";
        }
        SetStatus(_showOptimal
            ? $"[!] Route not optimal (attempt {_attempts}). Excess resistance: {diff}. Optimal route revealed."
            : $"[!] Route not optimal (attempt {_attempts}). Excess resistance: {diff}.");

        feedbackPopup?.Show(false, msg, onDismiss: null);
    }

    // =========================================================================
    // Win / End
    // =========================================================================

    private void EndPuzzle(int finalCost)
    {
        _done = true;

        OnReagentRoutingSolved?.Invoke(_attempts);

        string msg = $"Synthesis stable. Route cost {finalCost} matches the minimum possible.\nFailed attempts this session: {_attempts}.";
        if (winText != null) winText.text = msg;
        if (winPanel != null) winPanel.SetActive(true);

        SetStatus("[SOLVED] Optimal route confirmed. Chamber online.");
        RefreshAllNodeVisuals();
        RefreshAllEdgeVisuals();
        RefreshInfoPanel();

        feedbackPopup?.Show(true, msg, onDismiss: null);
    }

    // =========================================================================
    // Visual Refresh
    // =========================================================================

    private void RefreshAllNodeVisuals()
    {
        foreach (RNode n in _nodes) ApplyNodeVisual(n.Id);
    }

    private void ApplyNodeVisual(int id)
    {
        if (_nodeObjects == null || id >= _nodeObjects.Length) return;
        GameObject go = _nodeObjects[id];
        if (go == null) return;

        NodeVisualState state = GetNodeState(id);

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = StateToColor(state);

        Button btn = go.GetComponent<Button>();
        if (btn != null)
        {
            // NOTE: deliberately NOT gated on IsAdjacent(last, id) here. Non-adjacent
            // junctions stay clickable (still visually dim/"Junction" via GetNodeState,
            // since that check still applies there) so OnNodeClicked's own adjacency
            // check can fire and show "No pipe connects there directly." If this button
            // were disabled for non-adjacent nodes, that click would never register and
            // the warning could never appear -- this was reported after playtesting.
            int last = _path.Count > 0 ? _path[_path.Count - 1] : _sourceId;
            bool clickable = !_done && !_committed && id != last && !_path.Contains(id);
            btn.interactable = clickable;
        }
    }

    private NodeVisualState GetNodeState(int id)
    {
        if (id == _sourceId) return NodeVisualState.Source;
        if (id == _targetId) return NodeVisualState.Target;
        if (_showOptimal && _optimalPath != null && _optimalPath.Contains(id) && !_path.Contains(id))
            return NodeVisualState.Optimal;
        if (_path.Contains(id)) return NodeVisualState.Selected;

        int last = _path.Count > 0 ? _path[_path.Count - 1] : _sourceId;
        if (!_committed && IsAdjacent(last, id)) return NodeVisualState.Available;

        return NodeVisualState.Junction;
    }

    private Color StateToColor(NodeVisualState state)
    {
        switch (state)
        {
            case NodeVisualState.Source:    return ColSource;
            case NodeVisualState.Target:    return ColTarget;
            case NodeVisualState.Selected:  return ColSelected;
            case NodeVisualState.Available: return ColAvailable;
            case NodeVisualState.Optimal:   return ColOptimal;
            default:                        return ColJunction;
        }
    }

    private void RefreshAllEdgeVisuals()
    {
        if (_edgeObjects == null) return;

        foreach (REdge e in _edges)
        {
            if (!_edgeObjects.TryGetValue((e.From, e.To), out GameObject go) || go == null) continue;

            bool isSelected = IsConsecutiveInPath(e.From, e.To, _path);
            bool isOptimal  = _showOptimal && _optimalPath != null && IsConsecutiveInPath(e.From, e.To, _optimalPath);

            Image img = go.GetComponent<Image>();
            if (img == null) continue;

            if (isSelected)      img.color = ColEdgeSelected;
            else if (isOptimal)  img.color = ColEdgeOptimal;
            else                 img.color = ColEdgeDim;
        }
    }

    private static bool IsConsecutiveInPath(int from, int to, List<int> path)
    {
        if (path == null) return false;
        for (int i = 0; i < path.Count - 1; i++)
            if (path[i] == from && path[i + 1] == to) return true;
        return false;
    }

    private void RefreshInfoPanel()
    {
        if (routeCostText != null)
            routeCostText.text = $"ROUTE COST: {PathCost(_path)}";

        if (attemptsText != null)
            attemptsText.text = $"ATTEMPTS: {_attempts}";

        if (commitButton != null)
            commitButton.interactable = !_done && !_committed && _path[_path.Count - 1] == _targetId;

        if (undoButton != null)
            undoButton.interactable = !_done && _path.Count > 1;

        if (resetButton != null)
            resetButton.interactable = !_done;
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private void ClearGraph()
    {
        ClearChildren(nodeContainer);
        ClearChildren(edgeContainer);
        ClearChildren(chipContainer);
        _edgeObjects?.Clear();
        _chipObjects?.Clear();
    }

    private static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private static void ClearChildren(RectTransform rt) => ClearChildren(rt as Transform);

    public void ClosePanel() => _onClose?.Invoke();
}

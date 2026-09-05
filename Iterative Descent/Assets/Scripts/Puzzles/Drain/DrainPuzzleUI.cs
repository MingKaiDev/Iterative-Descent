using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ARBITEX Drain Diagnostic -- BFS / DFS Pipe Network Puzzle
///
/// WHAT THE PLAYER DOES
/// --------------------
/// A pipe network (binary tree) is shown on a CRT terminal.
/// ARBITEX designates a protocol (BFS or DFS).
/// The player must click nodes in the correct traversal order to scan and
/// clear each pipe segment. Wrong clicks raise the flood level; 4 errors
/// resets the puzzle with a new tree.
///
/// FOG OF WAR
/// ----------
/// At Tier 1+, only the INLET node is visible at the start.
/// Scanning a node reveals its children.
/// At Tier 0, all nodes are visible from the start.
///
/// PROTOCOL SWITCH
/// ---------------
/// At Tier 2+, ARBITEX fires a mid-traversal protocol override after a
/// randomised number of correct scans (2 or 3). The data structure
/// contents are preserved but the active end flips (queue front <-> stack
/// top). The player must adapt immediately.
///
/// DATA STRUCTURE DISPLAY
/// ----------------------
/// Queue/stack contents shown as chips. The expected next node is marked.
/// At Tier 3/4, the display is hidden and the player must track mentally.
/// A hint button reveals the display temporarily.
///
/// TIER SUMMARY
/// ------------
///   Tier 0 -- 7-node balanced tree, fog OFF,  no switch, DS shown
///   Tier 1 -- 7-node balanced tree, fog ON,   no switch, DS shown
///   Tier 2 -- 5-9 node random tree, fog ON,   switch ON, DS shown
///   Tier 3 -- asymmetric tree,      fog ON,   switch ON, DS HIDDEN
///   Tier 4 -- asymmetric tree,      fog ON,   switch ON, DS HIDDEN
///
/// INSPECTOR SETUP
/// ---------------
/// See DrainPuzzle_UnitySetup.md for the full Canvas hierarchy and
/// RectTransform settings.
///
/// KNOWN ISSUE (prototype)
/// -----------------------
/// BFS chip display can show stale order immediately after a protocol
/// switch before the DS is refreshed -- mitigated by always calling
/// RefreshDSDisplay() after any state change.
/// </summary>
public class DrainPuzzleUI : MonoBehaviour
{
    // =========================================================================
    // Types
    // =========================================================================

    private class DrainNode
    {
        public int         Id;
        public string      Label;
        public int         Parent;       // -1 for root
        public List<int>   Children = new List<int>();
        public Vector2     NormPos;     // 0..1 in container space
    }

    private enum NodeVisualState { Hidden, Visible, Correct, Wrong, Inlet, Hint }

    // =========================================================================
    // Singleton and Static Event
    // =========================================================================

    public static DrainPuzzleUI Instance { get; private set; }

    /// <summary>Fired on solve. Argument = total wrong attempts.</summary>
    public static event Action<int> OnDrainSolved;

    // =========================================================================
    // Inspector: Graph Area
    // =========================================================================

    [Header("Graph")]
    [Tooltip("RectTransform containing edge Images. Must be behind nodeContainer in hierarchy.")]
    [SerializeField] private RectTransform edgeContainer;

    [Tooltip("RectTransform containing node GameObjects. No Layout Group -- positions are set manually.")]
    [SerializeField] private RectTransform nodeContainer;

    [Tooltip("Prefab: root Image (node circle) + child TMP named 'LabelText'.")]
    [SerializeField] private GameObject nodePrefab;

    [Tooltip("Prefab: root Image (1px white strip). Width=1, height=3 -- script sizes and rotates it.")]
    [SerializeField] private GameObject edgePrefab;

    // =========================================================================
    // Inspector: Data Structure Panel
    // =========================================================================

    [Header("Data Structure Panel")]
    [Tooltip("Horizontal layout parent for queue/stack chip elements.")]
    [SerializeField] private RectTransform dsChipsParent;

    [Tooltip("Prefab: root Image + child TMP named 'ChipText'.")]
    [SerializeField] private GameObject chipPrefab;

    [Tooltip("Label above the chip row -- shows QUEUE (FIFO) or STACK (LIFO).")]
    [SerializeField] private TextMeshProUGUI dsTypeLabel;

    [Tooltip("Root of the DS panel section -- hidden at tier 3/4 unless hint is active.")]
    [SerializeField] private GameObject dsPanelRoot;

    // =========================================================================
    // Inspector: Next Node Panel
    // =========================================================================

    [Header("Next Node")]
    [SerializeField] private TextMeshProUGUI nextNodeText;

    // =========================================================================
    // Inspector: Protocol Badge
    // =========================================================================

    [Header("Protocol")]
    [SerializeField] private TextMeshProUGUI protocolBadgeText;
    [SerializeField] private Image           protocolBadgeBg;

    // =========================================================================
    // Inspector: Flood Bar
    // =========================================================================

    [Header("Flood Bar")]
    [SerializeField] private Image           floodFillImage;   // fillAmount 0..1
    [SerializeField] private TextMeshProUGUI floodLevelText;   // "LEVEL: 0 / 4"

    // =========================================================================
    // Inspector: Status and Alerts
    // =========================================================================

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Tooltip("Panel shown briefly when ARBITEX fires the protocol switch.")]
    [SerializeField] private GameObject      switchAlertPanel;

    [Tooltip("Text inside the switch alert panel.")]
    [SerializeField] private TextMeshProUGUI switchAlertText;

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
    [SerializeField] private Button hintButton;
    [SerializeField] private Button closeButton;

    [Header("Water Fill")]
    [Tooltip("DrainWaterFill component on the WaterFill child GameObject (first in hierarchy).")]
    [SerializeField] private DrainWaterFill waterFill;

    [Header("Audio")]
    [Tooltip("Looping AudioSource for the constant water-flowing ambience -- kept separate from " +
             "sfxAudioSource so the flush one-shot never cuts the loop off. Starts on GeneratePuzzle(), " +
             "stops on OnDisable() (covers the panel being closed either via ClosePanel or SetActive(false)).")]
    [SerializeField] private AudioSource waterAudioSource;
    [Tooltip("Continuous water-flowing sound. Looped for as long as the puzzle panel is open.")]
    [SerializeField] private AudioClip waterLoopClip;
    [Tooltip("Water loop volume. Applied every time the loop (re)starts, and live while the puzzle " +
             "is open in Play Mode so it can be tuned without stopping.")]
    [Range(0f, 1f)]
    [SerializeField] private float waterVolume = 0.25f;

    [Tooltip("AudioSource used for the one-shot flush sting.")]
    [SerializeField] private AudioSource sfxAudioSource;
    [Tooltip("Played each time a correct scan drains the water down one step.")]
    [SerializeField] private AudioClip flushSound;
    [Tooltip("Volume for the flush one-shot.")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.4f;

    // =========================================================================
    // Inspector: Feedback Popup
    // =========================================================================

    [Header("Feedback")]
    [SerializeField] private DrainFeedbackPopup feedbackPopup;

    // =========================================================================
    // Colors
    // =========================================================================

    // Option C -- Emergency Alert palette
    private static readonly Color ColHidden  = new Color(0.10f, 0.05f, 0.05f, 0.80f);  // barely-visible dark node
    private static readonly Color ColVisible = new Color(0.15f, 0.53f, 0.12f, 1.00f);  // mid-green pipe (#268820)
    private static readonly Color ColCorrect = new Color(0.22f, 0.72f, 0.14f, 1.00f);  // bright green = cleared
    private static readonly Color ColWrong   = new Color(0.85f, 0.08f, 0.03f, 1.00f);  // intense red flash
    private static readonly Color ColInlet   = new Color(0.80f, 0.20f, 0.04f, 1.00f);  // orange-red (#cc3300)
    private static readonly Color ColHint    = new Color(0.80f, 0.50f, 0.04f, 1.00f);  // bright amber hint
    private static readonly Color ColEdge    = new Color(0.35f, 0.75f, 0.18f, 0.70f);  // green pipe connection
    private static readonly Color ColEdgeDim = new Color(0.20f, 0.10f, 0.10f, 0.30f);  // dim hidden connection

    private static readonly Color ColBFS = new Color(0.10f, 0.02f, 0.00f, 1.00f);  // dark red badge bg
    private static readonly Color ColDFS = new Color(0.12f, 0.04f, 0.00f, 1.00f);  // dark amber-red badge bg

    private static readonly Color ColChipNext   = new Color(1.00f, 0.38f, 0.19f, 1.00f);  // #ff6030 orange (next node)
    private static readonly Color ColChipNormal = new Color(0.16f, 0.06f, 0.01f, 1.00f);  // dark orange (queued)

    private static readonly Color ColFlood = new Color(0.80f, 0.13f, 0.00f, 1.00f);  // #cc2200 urgent red

    // =========================================================================
    // Runtime State
    // =========================================================================

    private int                 _tier;
    private bool                _fogOn;
    private bool                _dsHidden;
    private bool                _switchEnabled;
    private int                 _switchTriggerCount;   // correct scans before switch fires
    private bool                _switchFired;
    private bool                _hintShown;
    private bool                _done;

    private string              _protocol;             // "BFS" or "DFS"
    private List<DrainNode>     _nodes;
    private List<int>           _dsContents;           // pending nodes in DS
    private HashSet<int>        _visited;
    private HashSet<int>        _revealed;

    private int                 _floodLevel;
    private int                 _wrongAttempts;
    private int                 _correctScans;
    private float               _waterLevel;
    private const float         WaterInitial = 0.65f; // starting fill (drain is blocked)

    private Action              _onClose;

    // Node GameObjects: indexed by node id
    private GameObject[]        _nodeObjects;
    private Coroutine           _switchAlertRoutine;
    private Coroutine           _wrongFlashRoutine;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        hintButton?.onClick.AddListener(ToggleHint);
        closeButton?.onClick.AddListener(ClosePanel);
    }

    private void OnDisable()
    {
        hintButton?.onClick.RemoveListener(ToggleHint);
        closeButton?.onClick.RemoveListener(ClosePanel);
        StopWaterAudio(); // covers the panel being closed via ClosePanel or SetActive(false)
    }

    /// <summary>Lets waterVolume be scrubbed live in the Inspector while the puzzle is open in Play
    /// Mode, instead of only taking effect on the next GeneratePuzzle(). sfxVolume doesn't need this --
    /// it's read fresh on every PlayOneShot() call anyway.</summary>
    private void OnValidate()
    {
        if (waterAudioSource != null) waterAudioSource.volume = waterVolume;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>Called by DrainPuzzleProp to open the puzzle.</summary>
    public void InitPuzzle(Action onClose)
    {
        _onClose = onClose;

        if (feedbackPopup != null)
            feedbackPopup.gameObject.SetActive(false);

        if (winPanel != null)
            winPanel.SetActive(false);

        if (switchAlertPanel != null)
            switchAlertPanel.SetActive(false);

        PlayerMetricsTracker.Instance?.NotifyDrainStarted();

        GeneratePuzzle();
    }

    // =========================================================================
    // Generation
    // =========================================================================

    private void GeneratePuzzle()
    {
        _tier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.BfsDfs)
            : 1;

        _fogOn          = _tier >= 1;
        _dsHidden       = _tier >= 3;
        _switchEnabled  = _tier >= 2;
        _switchFired    = false;
        _hintShown      = false;
        _done           = false;
        _floodLevel     = 0;
        _wrongAttempts  = 0;
        _correctScans   = 0;
        _waterLevel     = WaterInitial;
        waterFill?.SetFillLevel(_waterLevel, instant: true);

        // Protocol: always start BFS; DFS appears after switch
        _protocol = "BFS";

        // Switch fires after 2 or 3 correct scans
        _switchTriggerCount = UnityEngine.Random.Range(2, 4);

        // Build tree
        _nodes = BuildTree();

        // Initialise traversal state
        _visited    = new HashSet<int>();
        _revealed   = new HashSet<int> { 0 };   // root always revealed
        _dsContents = new List<int> { 0 };       // root in DS

        if (!_fogOn)
        {
            foreach (DrainNode n in _nodes)
                _revealed.Add(n.Id);
        }

        // Build visuals
        ClearGraph();
        SpawnNodes();
        StartCoroutine(SpawnEdgesAfterLayout());

        if (dsPanelRoot != null)
            dsPanelRoot.SetActive(!_dsHidden);

        if (winPanel != null)
            winPanel.SetActive(false);

        RefreshFlood();
        RefreshProtocolBadge();
        RefreshDSDisplay();
        RefreshAllNodeVisuals();
        SetStatus("Click the INLET node to begin scanning.");

        StartWaterAudio();
    }

    // ── Audio ────────────────────────────────────────────────────────────────
    private void StartWaterAudio()
    {
        if (waterAudioSource == null || waterLoopClip == null) return;
        waterAudioSource.clip         = waterLoopClip;
        waterAudioSource.loop         = true;
        waterAudioSource.playOnAwake  = false;
        waterAudioSource.spatialBlend = 0f; // 2D -- same volume regardless of camera position
        waterAudioSource.volume       = waterVolume;
        if (!waterAudioSource.isPlaying) waterAudioSource.Play();
    }

    private void StopWaterAudio()
    {
        if (waterAudioSource != null && waterAudioSource.isPlaying) waterAudioSource.Stop();
    }

    /// <summary>Defensive one-shot helper -- no-ops if either the AudioSource or clip is unassigned.</summary>
    private void PlaySfx(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null) sfxAudioSource.PlayOneShot(clip, sfxVolume);
    }

    // =========================================================================
    // Tree Building
    // =========================================================================

    /// <summary>
    /// Returns the node list for the current tier.
    /// Node 0 is always the root (INLET).
    /// NormPos is (x,y) in 0..1 space where y=0 is top of container.
    /// </summary>
    private List<DrainNode> BuildTree()
    {
        switch (_tier)
        {
            case 0:
            case 1:
                return BuildFixedTree();
            case 2:
                return BuildRandomTree(minNodes: 5, maxNodes: 9, maxDepth: 3, asymmetric: false);
            default:
                return BuildRandomTree(minNodes: 7, maxNodes: 9, maxDepth: 4, asymmetric: true);
        }
    }

    /// <summary>Fixed balanced 7-node tree used at Tier 0 / 1.</summary>
    private List<DrainNode> BuildFixedTree()
    {
        //         0 (INLET)
        //        / \
        //       1   2
        //      / \ / \
        //     3  4 5  6

        var nodes = new List<DrainNode>();

        void Add(int id, int parent, float nx, float ny, params int[] children)
        {
            var n = new DrainNode { Id = id, Parent = parent, NormPos = new Vector2(nx, ny) };
            foreach (int c in children) n.Children.Add(c);
            n.Label = id == 0 ? "INLET" : $"P-0{id + 1}";
            nodes.Add(n);
        }

        Add(0, -1, 0.50f, 0.08f, 1, 2);
        Add(1, 0,  0.25f, 0.38f, 3, 4);
        Add(2, 0,  0.75f, 0.38f, 5, 6);
        Add(3, 1,  0.12f, 0.70f);
        Add(4, 1,  0.38f, 0.70f);
        Add(5, 2,  0.62f, 0.70f);
        Add(6, 2,  0.88f, 0.70f);

        return nodes;
    }

    /// <summary>Randomly generates a binary tree.</summary>
    private List<DrainNode> BuildRandomTree(int minNodes, int maxNodes, int maxDepth, bool asymmetric)
    {
        var nodes  = new List<DrainNode>();
        int target = UnityEngine.Random.Range(minNodes, maxNodes + 1);

        // Root
        nodes.Add(new DrainNode { Id = 0, Parent = -1, Label = "INLET", NormPos = new Vector2(0.5f, 0.08f) });

        // BFS expansion
        var queue  = new Queue<int>();
        queue.Enqueue(0);

        while (nodes.Count < target && queue.Count > 0)
        {
            int parentId = queue.Dequeue();
            DrainNode parent = nodes[parentId];

            int depth = GetDepth(nodes, parentId);
            if (depth >= maxDepth) continue;

            // How many children for this node?
            int maxChildren = asymmetric
                ? (UnityEngine.Random.value < 0.4f ? 1 : 2)
                : (nodes.Count + 2 <= target ? 2 : 1);
            maxChildren = Mathf.Min(maxChildren, target - nodes.Count);

            for (int c = 0; c < maxChildren; c++)
            {
                int childId = nodes.Count;
                parent.Children.Add(childId);
                nodes.Add(new DrainNode
                {
                    Id     = childId,
                    Parent = parentId,
                    Label  = $"P-{childId + 1:00}"
                });
                queue.Enqueue(childId);
            }
        }

        // Assign positions using simple level-based layout
        AssignPositions(nodes);

        return nodes;
    }

    private int GetDepth(List<DrainNode> nodes, int id)
    {
        int depth = 0;
        int cur   = id;
        while (nodes[cur].Parent != -1)
        {
            cur = nodes[cur].Parent;
            depth++;
        }
        return depth;
    }

    /// <summary>Assigns NormPos by computing per-level X spread.</summary>
    private void AssignPositions(List<DrainNode> nodes)
    {
        // Group nodes by depth
        var levels = new Dictionary<int, List<int>>();

        void Visit(int id, int depth)
        {
            if (!levels.ContainsKey(depth)) levels[depth] = new List<int>();
            levels[depth].Add(id);
            foreach (int c in nodes[id].Children) Visit(c, depth + 1);
        }
        Visit(0, 0);

        int maxDepth = 0;
        foreach (int d in levels.Keys) if (d > maxDepth) maxDepth = d;

        float yPad = 0.08f;
        float yStep = maxDepth > 0 ? (0.88f / maxDepth) : 0f;

        foreach (var kv in levels)
        {
            int   depth = kv.Key;
            var   ids   = kv.Value;
            float y     = yPad + depth * yStep;

            for (int i = 0; i < ids.Count; i++)
            {
                float x = ids.Count == 1 ? 0.5f : 0.08f + (0.84f / (ids.Count - 1)) * i;
                nodes[ids[i]].NormPos = new Vector2(x, y);
            }
        }
    }

    // =========================================================================
    // Node Spawning
    // =========================================================================

    private void SpawnNodes()
    {
        _nodeObjects = new GameObject[_nodes.Count];

        foreach (DrainNode n in _nodes)
        {
            if (nodePrefab == null) break;

            GameObject go = Instantiate(nodePrefab, nodeContainer);
            _nodeObjects[n.Id] = go;

            // Position
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(n.NormPos.x, 1f - n.NormPos.y);
                rt.anchoredPosition = Vector2.zero;
            }

            // Label
            Transform lblT = go.transform.Find("LabelText");
            if (lblT != null)
            {
                TextMeshProUGUI lbl = lblT.GetComponent<TextMeshProUGUI>();
                if (lbl != null) lbl.text = n.Label;
            }

            // Click handler
            int capturedId = n.Id;
            Button btn = go.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnNodeClicked(capturedId));

            ApplyNodeVisual(n.Id);
        }
    }

    private IEnumerator SpawnEdgesAfterLayout()
    {
        // Wait one frame so the Canvas layout pass has set nodeContainer.rect correctly
        yield return null;

        if (edgePrefab == null || edgeContainer == null) yield break;

        float w = nodeContainer.rect.width;
        float h = nodeContainer.rect.height;

        foreach (DrainNode n in _nodes)
        {
            if (n.Parent < 0) continue;
            DrainNode parent = _nodes[n.Parent];

            // Top-left origin: x goes right, y goes down (negative in Unity UI)
            Vector2 from = new Vector2(parent.NormPos.x * w, -(parent.NormPos.y * h));
            Vector2 to   = new Vector2(n.NormPos.x * w,     -(n.NormPos.y * h));

            SpawnEdgeLine(from, to, n.Id);
        }
    }

    private void SpawnEdgeLine(Vector2 from, Vector2 to, int childId)
    {
        GameObject go = Instantiate(edgePrefab, edgeContainer);
        go.name = $"Edge_{childId}";

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) return;

        Vector2 dir    = to - from;
        float   length = dir.magnitude;
        float   angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.anchorMin        = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot            = new Vector2(0f, 0.5f);
        rt.anchoredPosition = from;
        rt.sizeDelta        = new Vector2(length, 3f);
        rt.localRotation    = Quaternion.Euler(0f, 0f, angle);

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = ColEdgeDim;
    }

    // =========================================================================
    // Node Click Handling
    // =========================================================================

    private void OnNodeClicked(int id)
    {
        if (_done) return;

        if (feedbackPopup != null && feedbackPopup.gameObject.activeSelf) return;

        DrainNode node = _nodes[id];

        // Fog check
        if (_fogOn && !_revealed.Contains(id))
        {
            SetStatus("[!] Node not yet discovered. Scan a revealed node first.");
            return;
        }

        // Already visited
        if (_visited.Contains(id))
        {
            SetStatus("[!] Node already scanned.");
            return;
        }

        int expected = GetExpectedNext();

        if (id == expected)
        {
            ProcessCorrectScan(id);
        }
        else
        {
            ProcessWrongScan(id);
        }
    }

    private void ProcessCorrectScan(int id)
    {
        DrainNode node = _nodes[id];

        // Remove from DS
        if (_protocol == "BFS")
            _dsContents.RemoveAt(0);
        else
            _dsContents.RemoveAt(_dsContents.Count - 1);

        // Mark visited
        _visited.Add(id);
        _correctScans++;

        // Water drops evenly across all nodes as drain clears
        _waterLevel = Mathf.Max(0f, WaterInitial - (float)_correctScans / _nodes.Count * WaterInitial);
        waterFill?.SetFillLevel(_waterLevel);
        PlaySfx(flushSound);

        // Add children to DS and reveal them.
        // BFS: enqueue left to right (FIFO preserves order naturally).
        // DFS: push in reverse so the leftmost child sits on top of the stack
        //      and is therefore popped first (left-to-right traversal convention).
        if (_protocol == "BFS")
        {
            foreach (int childId in node.Children)
            {
                if (!_visited.Contains(childId))
                {
                    _dsContents.Add(childId);
                    _revealed.Add(childId);
                }
            }
        }
        else
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                int childId = node.Children[i];
                if (!_visited.Contains(childId))
                {
                    _dsContents.Add(childId);
                    _revealed.Add(childId);
                }
            }
        }

        SetStatus($"[OK] Scanned: {node.Label}. Pipe segment cleared.");

        RefreshAllNodeVisuals();
        RefreshDSDisplay();

        // Win check
        if (_visited.Count == _nodes.Count)
        {
            EndPuzzle();
            return;
        }

        // Protocol switch check
        if (_switchEnabled && !_switchFired && _correctScans >= _switchTriggerCount)
            TriggerProtocolSwitch();
    }

    private void ProcessWrongScan(int id)
    {
        _wrongAttempts++;
        _floodLevel++;

        // Wrong scan spikes water level up -- flood is rising
        _waterLevel = Mathf.Min(0.92f, _waterLevel + 0.12f);
        waterFill?.SetFillLevel(_waterLevel);

        SetStatus($"[ERR] Wrong node. Expected {_nodes[GetExpectedNext()].Label} under {_protocol}. ({_wrongAttempts} wrong)");

        // Flash red
        if (_wrongFlashRoutine != null) StopCoroutine(_wrongFlashRoutine);
        _wrongFlashRoutine = StartCoroutine(FlashWrong(id));

        RefreshFlood();

        if (_floodLevel >= 4)
        {
            feedbackPopup?.Show(false,
                $"Flood level critical. Drain system resetting.\nWrong attempts so far: {_wrongAttempts}",
                onDismiss: GeneratePuzzle);
        }
    }

    // =========================================================================
    // Protocol Switch
    // =========================================================================

    private void TriggerProtocolSwitch()
    {
        _switchFired = true;
        string oldProto = _protocol;
        _protocol = _protocol == "BFS" ? "DFS" : "BFS";

        string alertMsg = $"[ARBITEX] PROTOCOL OVERRIDE -- {oldProto} terminated. " +
                          $"{_protocol} engaged. Active end is now the {(_protocol == "BFS" ? "FRONT" : "TOP")}.";

        if (switchAlertText != null) switchAlertText.text = alertMsg;

        if (switchAlertPanel != null)
        {
            if (_switchAlertRoutine != null) StopCoroutine(_switchAlertRoutine);
            _switchAlertRoutine = StartCoroutine(ShowSwitchAlert());
        }

        SetStatus($"[!] Protocol switched to {_protocol}!");
        RefreshProtocolBadge();
        RefreshDSDisplay();
    }

    private IEnumerator ShowSwitchAlert()
    {
        switchAlertPanel.SetActive(true);
        yield return new WaitForSecondsRealtime(4f);
        if (switchAlertPanel != null)
            switchAlertPanel.SetActive(false);
    }

    // =========================================================================
    // Win / End
    // =========================================================================

    private void EndPuzzle()
    {
        _done = true;

        // Drain fully cleared -- animate water to empty
        _waterLevel = 0f;
        waterFill?.SetFillLevel(0f);

        OnDrainSolved?.Invoke(_wrongAttempts);

        // Compute alt-algorithm comparison
        string altProto   = _protocol == "BFS" ? "DFS" : "BFS";
        string usedOrder  = BuildOrderString(_visited.Count);
        string altOrder   = BuildAltOrderString(altProto);
        string comparison = BuildComparison(altProto, altOrder);

        string msg = $"Drain cleared in {_visited.Count} scans using {_protocol}.\n{comparison}";

        if (winText != null) winText.text = msg;
        if (winPanel != null) winPanel.SetActive(true);

        SetStatus("[SOLVED] Blockage located. Drain cleared. Door unlocked.");
        RefreshAllNodeVisuals();
    }

    private string BuildComparison(string altProto, string altOrder)
    {
        return $"{altProto} traversal order would be: {altOrder}.";
    }

    /// <summary>
    /// Simulates the alt-protocol traversal on the same tree from scratch.
    /// </summary>
    private string BuildAltOrderString(string proto)
    {
        var visited = new List<int>();
        var ds      = new List<int> { 0 };

        while (ds.Count > 0)
        {
            int next = proto == "BFS" ? ds[0] : ds[ds.Count - 1];
            if (proto == "BFS") ds.RemoveAt(0); else ds.RemoveAt(ds.Count - 1);

            visited.Add(next);
            foreach (int c in _nodes[next].Children)
                ds.Add(c);
        }

        var labels = new System.Text.StringBuilder();
        for (int i = 0; i < visited.Count; i++)
        {
            if (i > 0) labels.Append(" > ");
            labels.Append(_nodes[visited[i]].Label);
        }
        return labels.ToString();
    }

    private string BuildOrderString(int count)
    {
        return count.ToString();
    }

    // =========================================================================
    // Hint
    // =========================================================================

    private void ToggleHint()
    {
        _hintShown = !_hintShown;

        if (hintButton != null)
        {
            Transform lblT = hintButton.transform.Find("Label");
            if (lblT != null)
            {
                TextMeshProUGUI lbl = lblT.GetComponent<TextMeshProUGUI>();
                if (lbl != null) lbl.text = _hintShown ? "Hide hint" : "Show hint";
            }
        }

        if (_dsHidden && dsPanelRoot != null)
            dsPanelRoot.SetActive(_hintShown);

        RefreshAllNodeVisuals();
        RefreshDSDisplay();
    }

    // =========================================================================
    // Visual Refresh
    // =========================================================================

    private void RefreshAllNodeVisuals()
    {
        foreach (DrainNode n in _nodes)
            ApplyNodeVisual(n.Id);
    }

    private void ApplyNodeVisual(int id)
    {
        if (_nodeObjects == null || id >= _nodeObjects.Length) return;
        GameObject go = _nodeObjects[id];
        if (go == null) return;

        NodeVisualState state = GetNodeState(id);

        Image img = go.GetComponent<Image>();
        if (img != null) img.color = StateToColor(state);

        // Show/hide label based on fog
        Transform lblT = go.transform.Find("LabelText");
        if (lblT != null)
        {
            TextMeshProUGUI lbl = lblT.GetComponent<TextMeshProUGUI>();
            if (lbl != null)
            {
                bool showLabel = state != NodeVisualState.Hidden;
                lbl.gameObject.SetActive(showLabel);
                if (showLabel && _visited.Contains(id))
                    lbl.text = "OK";
                else if (showLabel)
                    lbl.text = _nodes[id].Label;
            }
        }

        // Interactable only if revealed, not visited, not done
        Button btn = go.GetComponent<Button>();
        if (btn != null)
            btn.interactable = !_done && state != NodeVisualState.Hidden && !_visited.Contains(id);
    }

    private NodeVisualState GetNodeState(int id)
    {
        if (_visited.Contains(id))        return NodeVisualState.Correct;
        if (_fogOn && !_revealed.Contains(id)) return NodeVisualState.Hidden;
        if (id == 0 && !_visited.Contains(0))  return NodeVisualState.Inlet;

        bool showHint = _hintShown || !_dsHidden;
        if (showHint && GetExpectedNext() == id) return NodeVisualState.Hint;

        return NodeVisualState.Visible;
    }

    private Color StateToColor(NodeVisualState state)
    {
        switch (state)
        {
            case NodeVisualState.Hidden:  return ColHidden;
            case NodeVisualState.Correct: return ColCorrect;
            case NodeVisualState.Inlet:   return ColInlet;
            case NodeVisualState.Hint:    return ColHint;
            case NodeVisualState.Wrong:   return ColWrong;
            default:                      return ColVisible;
        }
    }

    private void RefreshDSDisplay()
    {
        // DS type label
        if (dsTypeLabel != null)
            dsTypeLabel.text = _protocol == "BFS" ? "QUEUE (FIFO)" : "STACK (LIFO)";

        if (dsChipsParent == null) return;

        ClearChildren(dsChipsParent);

        if (_dsContents.Count == 0)
        {
            SpawnChip("empty", false);
        }
        else
        {
            // BFS: display front to back, next = index 0
            // DFS: display bottom to top, next = last index
            // Both cases: we display in _dsContents order, mark the "next" one
            int nextId = GetExpectedNext();

            for (int i = 0; i < _dsContents.Count; i++)
            {
                int    nodeId  = _dsContents[i];
                string label   = _nodes[nodeId].Label;
                bool   isNext  = (nodeId == nextId);
                if (isNext) label += " *";
                SpawnChip(label, isNext);
            }
        }

        // Next node text
        if (nextNodeText != null)
        {
            if (_dsContents.Count > 0)
            {
                int nextId = GetExpectedNext();
                nextNodeText.text = $"{_nodes[nextId].Label} *";
            }
            else
            {
                nextNodeText.text = "--";
            }
        }
    }

    private void SpawnChip(string label, bool isNext)
    {
        if (chipPrefab == null) return;

        GameObject go  = Instantiate(chipPrefab, dsChipsParent);
        Image      img = go.GetComponent<Image>();
        if (img != null) img.color = isNext ? ColChipNext : ColChipNormal;

        Transform lblT = go.transform.Find("ChipText");
        if (lblT != null)
        {
            TextMeshProUGUI lbl = lblT.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.text = label;
        }
    }

    private void RefreshProtocolBadge()
    {
        if (protocolBadgeText != null)
        {
            protocolBadgeText.text  = _protocol == "BFS" ? "BFS -- QUEUE" : "DFS -- STACK";
            protocolBadgeText.color = new Color(1.00f, 0.38f, 0.19f, 1f); // #ff6030 orange text
        }

        if (protocolBadgeBg != null)
            protocolBadgeBg.color = _protocol == "BFS" ? ColBFS : ColDFS;
    }

    private void RefreshFlood()
    {
        float pct = Mathf.Clamp01(_floodLevel / 4f);

        if (floodFillImage != null)
            floodFillImage.fillAmount = pct;

        if (floodLevelText != null)
            floodLevelText.text = $"LEVEL: {_floodLevel} / 4";
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    private int GetExpectedNext()
    {
        if (_dsContents.Count == 0) return -1;
        return _protocol == "BFS" ? _dsContents[0] : _dsContents[_dsContents.Count - 1];
    }

    private IEnumerator FlashWrong(int id)
    {
        if (_nodeObjects == null || id >= _nodeObjects.Length) yield break;
        GameObject go = _nodeObjects[id];
        if (go == null) yield break;

        Image img = go.GetComponent<Image>();
        if (img == null) yield break;

        img.color = ColWrong;
        yield return new WaitForSecondsRealtime(0.5f);
        ApplyNodeVisual(id);
    }

    private void ClearGraph()
    {
        ClearChildren(nodeContainer);
        ClearChildren(edgeContainer);
        ClearChildren(dsChipsParent);
    }

    private static void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private static void ClearChildren(RectTransform rt)
    {
        ClearChildren(rt as Transform);
    }

    public void ClosePanel() => _onClose?.Invoke();
}

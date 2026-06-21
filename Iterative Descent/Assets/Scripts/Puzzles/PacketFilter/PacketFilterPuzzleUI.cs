using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// ARBITEX Packet Filter -- Server Room Lords Objective (Story 17)
///
/// WHAT THE PLAYER DOES
/// --------------------
/// Three sequential phases:
///
///   Phase 1 -- Log Analysis (Reconnaissance)
///     A frozen packet capture log is shown with full payloads visible.
///     Player studies it to identify ARBITEX C2 traffic patterns.
///     A "Initiate Scan" button advances to Phase 2.
///
///   Phase 2 -- Live Stream Classification
///     Packets arrive encrypted ("[TLS ENCRYPTED]" payload).
///     Player clicks ALLOW or DENY on each packet using patterns
///     memorised from Phase 1 -- no payload visible here.
///     False positives (denying legitimate traffic) are tracked.
///
///   Phase 3 -- Firewall Rule Commit
///     Player writes rules in a simplified terminal syntax:
///       [ALLOW|DENY] [proto:TCP|UDP|ICMP] [src:IP] [dst:IP] [dst_port:N] [payload:KEYWORD]
///     Rules evaluate top-down, first match wins.
///     Validation shows: which C2 channels are still leaking, which
///     legitimate services would be collaterally blocked.
///     Win = all C2 channels blocked AND no collateral damage.
///
/// CORE DIFFICULTY (always active, not DDA-gated)
/// -----------------------------------------------
///   - All C2 src IPs spoof legitimate client IPs: DENY src:... rules fail.
///     Players must use payload keyword + port combinations.
///   - Legitimate services share ports with C2 channels. Broad port-only rules
///     trigger collateral damage and fail validation.
///   - Phase 2 payloads are encrypted. Classification requires pattern memory
///     from Phase 1.
///   - Rules evaluate first-match-wins. Order matters.
///
/// DDA FLOOR
/// ---------
///   DDA tier clamped to minimum 2. This puzzle never runs below Tier 2 even
///   if PuzzleDDAController reports a lower tier.
///
/// BOSS CONSEQUENCE
/// ----------------
///   On solve, PacketFilterEventHandler sets ServerNodeDisabled = true.
///   The ARES boss fight reads this flag: if false, ARES gets +25% HP
///   in Phase 2 and +10% damage output (hardcoded, not DDA).
///
/// INSPECTOR HIERARCHY
/// -------------------
///   See PacketFilterPuzzle_UnitySetup.md for the full Canvas hierarchy,
///   prefab specs, and wiring checklist.
/// </summary>
public class PacketFilterPuzzleUI : MonoBehaviour
{
    // =========================================================================
    // Types
    // =========================================================================

    private enum Phase { LogAnalysis, LiveClassification, RuleCommit, Solved }

    // =========================================================================
    // Static Event
    // =========================================================================

    /// <summary>Fired on solve. Argument = wrong commit attempts.</summary>
    public static event Action<int> OnPacketFilterSolved;

    // =========================================================================
    // Inspector: Phase 1
    // =========================================================================

    [Header("Phase 1 -- Log Analysis")]
    [Tooltip("Root GO shown only during Phase 1.")]
    [SerializeField] private GameObject      logPanel;

    [Tooltip("VerticalLayoutGroup parent. Log rows are instantiated here.")]
    [SerializeField] private RectTransform   logTableParent;

    [Tooltip("Prefab with Image (root) and children named: Timestamp, Src, Dst, Proto, Port, Payload (all TMP).")]
    [SerializeField] private GameObject      logRowPrefab;

    [Tooltip("Displays packet count summary below the table.")]
    [SerializeField] private TextMeshProUGUI logStatsText;

    [Tooltip("Advances to Phase 2.")]
    [SerializeField] private Button          initiateButton;

    // =========================================================================
    // Inspector: Phase 2
    // =========================================================================

    [Header("Phase 2 -- Live Classification")]
    [Tooltip("Root GO shown only during Phase 2.")]
    [SerializeField] private GameObject      livePanel;

    [Tooltip("VerticalLayoutGroup inside a ScrollRect. Packet cards are instantiated here.")]
    [SerializeField] private RectTransform   packetCardParent;

    [Tooltip("Prefab: Image (root) + children Src, Dst, Proto, Port, Payload (TMP) + AllowButton, DenyButton + optional ExpiryBar (Image).")]
    [SerializeField] private GameObject      packetCardPrefab;

    [Tooltip("Shows false positive count e.g. '1 / 3'.")]
    [SerializeField] private TextMeshProUGUI falsePositiveCountText;

    [Tooltip("Horizontal fill image for the false positive bar (fillAmount 0-1).")]
    [SerializeField] private Image           falsePositiveFill;

    [Tooltip("Shows how many packets are still queued.")]
    [SerializeField] private TextMeshProUGUI packetsRemainingText;

    [Tooltip("VerticalLayoutGroup for session log lines (right panel).")]
    [SerializeField] private RectTransform   sessionLogParent;

    [Tooltip("Prefab: single child TMP named 'LogText'.")]
    [SerializeField] private GameObject      sessionLogLinePrefab;

    // =========================================================================
    // Inspector: Phase 3
    // =========================================================================

    [Header("Phase 3 -- Rule Commit")]
    [Tooltip("Root GO shown during Phase 3 and after solve.")]
    [SerializeField] private GameObject      rulePanel;

    [Tooltip("Player types rule syntax here.")]
    [SerializeField] private TMP_InputField  ruleInputField;

    [Tooltip("Parses and adds the current input as a rule.")]
    [SerializeField] private Button          addRuleButton;

    [Tooltip("Inline parse error text (starts inactive in Inspector).")]
    [SerializeField] private TextMeshProUGUI ruleParseErrorText;

    [Tooltip("VerticalLayoutGroup for written rule rows.")]
    [SerializeField] private RectTransform   ruleListParent;

    [Tooltip("Prefab: HLG with children IconText (TMP), RuleText (TMP), DeleteButton (Button).")]
    [SerializeField] private GameObject      ruleRowPrefab;

    [Tooltip("VerticalLayoutGroup for C2 channel status rows.")]
    [SerializeField] private RectTransform   channelListParent;

    [Tooltip("Prefab: HLG with children Dot (Image), ChannelLabel (TMP), StatusText (TMP).")]
    [SerializeField] private GameObject      channelRowPrefab;

    [Tooltip("Warning banner shown when rules would collaterally block a legitimate service (starts inactive).")]
    [SerializeField] private TextMeshProUGUI collateralWarningText;

    [Tooltip("Runs final validation. Succeeds only if all C2 blocked AND no collateral.")]
    [SerializeField] private Button          commitButton;

    [Tooltip("Always visible. Closes the puzzle overlay.")]
    [SerializeField] private Button          closeButton;

    // =========================================================================
    // Inspector: Feedback
    // =========================================================================

    [Header("Feedback")]
    [SerializeField] private PacketFilterFeedbackPopup feedbackPopup;

    // =========================================================================
    // Inspector: Timing
    // =========================================================================

    [Header("Timing")]
    [Tooltip("Seconds between each packet spawning in Phase 2. Per tier: T2=2s T3=1.5s T4=1s")]
    [SerializeField] private float packetIntervalTier2 = 2f;
    [SerializeField] private float packetIntervalTier3 = 1.5f;
    [SerializeField] private float packetIntervalTier4 = 1f;

    [Tooltip("Seconds before an unclassified packet auto-allows in Phase 2. Tier 4 only. 0 = no expiry.")]
    [SerializeField] private float packetExpiryTier4 = 4f;

    // =========================================================================
    // Runtime
    // =========================================================================

    private Phase                    _phase = Phase.LogAnalysis;
    private PacketFilterScenario     _scenario;
    private int                      _tier;
    private Action                   _onClose;

    // Phase 2
    private int              _falsePositives;
    private List<PacketData> _pendingLive = new List<PacketData>();
    private const int        MaxFP = 3;

    // Phase 2 -> Phase 3 pre-blocking (Option B)
    private readonly Dictionary<string, int> _channelDeniedCount = new Dictionary<string, int>();
    private readonly Dictionary<string, int> _channelTotalCount  = new Dictionary<string, int>();
    private readonly HashSet<string>         _phase2PreBlocked   = new HashSet<string>();
    // Tracks proto:port combos where the player false-positived -- voids pre-blocks on that port
    private readonly HashSet<string>         _fpPortCombos       = new HashSet<string>();

    // Phase 2 classification gating (Option A -- all cards must be classified before advancing)
    private int  _totalCards;
    private int  _classifiedCards;
    private bool _allPacketsSpawned;

    // Phase 3
    private readonly List<FirewallRule> _rules    = new List<FirewallRule>();
    private int                         _wrongSubmits;

    // Colours
    private static readonly Color ColC2Bg   = new Color(1f,    0.22f, 0.22f, 0.18f);
    private static readonly Color ColCollBg = new Color(1f,    0.75f, 0.20f, 0.18f);
    private static readonly Color ColGreen  = new Color(0.20f, 0.82f, 0.40f, 1f);
    private static readonly Color ColRed    = new Color(1f,    0.22f, 0.22f, 1f);
    private static readonly Color ColAmber  = new Color(1f,    0.75f, 0.20f, 1f);

    // =========================================================================
    // Unity
    // =========================================================================

    private void Awake()
    {
        initiateButton?.onClick.AddListener(OnInitiateScan);
        addRuleButton ?.onClick.AddListener(OnAddRule);
        commitButton  ?.onClick.AddListener(OnCommitRules);
        closeButton   ?.onClick.AddListener(OnClose);
    }

    private void OnDestroy()
    {
        initiateButton?.onClick.RemoveListener(OnInitiateScan);
        addRuleButton ?.onClick.RemoveListener(OnAddRule);
        commitButton  ?.onClick.RemoveListener(OnCommitRules);
        closeButton   ?.onClick.RemoveListener(OnClose);
    }

    // =========================================================================
    // Public API
    // =========================================================================

    public void InitPuzzle(Action onClose)
    {
        _onClose        = onClose;
        _falsePositives = 0;
        _wrongSubmits   = 0;
        _rules.Clear();
        _channelDeniedCount.Clear();
        _channelTotalCount.Clear();
        _phase2PreBlocked.Clear();
        _fpPortCombos.Clear();
        _totalCards       = 0;
        _classifiedCards  = 0;
        _allPacketsSpawned = false;
        _phase = Phase.LogAnalysis;

        // DDA tier -- hard floor at 2 (lord puzzle never runs below Tier 2)
        int ddaTier = PuzzleDDAController.Instance != null
            ? PuzzleDDAController.Instance.GetTierForConcept(BayesianKnowledgeTracker.NetworkSecurity)
            : 2;
        _tier = Mathf.Max(2, ddaTier);

        _scenario = BuildScenario(_tier);

        // Count C2 packets per channel in the live stream for Option B tracking
        foreach (var pkt in _scenario.LivePackets)
            if (pkt.IsMalicious && pkt.C2Channel != null)
            {
                if (!_channelTotalCount.ContainsKey(pkt.C2Channel))
                    _channelTotalCount[pkt.C2Channel] = 0;
                _channelTotalCount[pkt.C2Channel]++;
            }

        PlayerMetricsTracker.Instance?.NotifyPacketFilterStarted();

        if (feedbackPopup != null)        feedbackPopup.gameObject.SetActive(false);
        if (ruleParseErrorText != null)   ruleParseErrorText.gameObject.SetActive(false);
        if (collateralWarningText != null) collateralWarningText.gameObject.SetActive(false);

        SetPhase(Phase.LogAnalysis);
        PopulateLogTable();
    }

    // =========================================================================
    // Phase switching
    // =========================================================================

    private void SetPhase(Phase p)
    {
        _phase = p;
        logPanel ?.SetActive(p == Phase.LogAnalysis);
        livePanel?.SetActive(p == Phase.LiveClassification);
        rulePanel?.SetActive(p == Phase.RuleCommit || p == Phase.Solved);
    }

    // =========================================================================
    // Phase 1 -- Log Analysis
    // =========================================================================

    private void PopulateLogTable()
    {
        if (logTableParent == null || logRowPrefab == null) return;

        foreach (Transform t in logTableParent) Destroy(t.gameObject);

        int c2Count = 0, riskCount = 0;
        foreach (var pkt in _scenario.LogPackets)
        {
            var row = Instantiate(logRowPrefab, logTableParent);
            SetTMP(row, "Timestamp", pkt.Timestamp);
            SetTMP(row, "Src",       pkt.SrcIp);
            SetTMP(row, "Dst",       pkt.DstIp);
            SetTMP(row, "Proto",     pkt.Proto);
            SetTMP(row, "Port",      pkt.DstPort == 0 ? "--" : pkt.DstPort.ToString());
            SetTMP(row, "Payload",   pkt.Payload);

            var img = row.GetComponent<Image>();
            if (img != null)
            {
                if      (pkt.IsMalicious)      img.color = ColC2Bg;
                else if (pkt.IsCollateralRisk) img.color = ColCollBg;
                else                           img.color = Color.clear;
            }

            if (pkt.IsMalicious)      c2Count++;
            if (pkt.IsCollateralRisk) riskCount++;
        }

        if (logStatsText != null)
            logStatsText.text =
                $"{_scenario.LogPackets.Count} captured  |  {c2Count} malicious  |  {riskCount} at-risk services";
    }

    private void OnInitiateScan()
    {
        _pendingLive      = new List<PacketData>(_scenario.LivePackets);
        _totalCards       = _scenario.LivePackets.Count;
        _classifiedCards  = 0;
        _allPacketsSpawned = false;
        SetPhase(Phase.LiveClassification);
        UpdateRemainingText();
        StartCoroutine(SpawnLivePackets());
    }

    // =========================================================================
    // Phase 2 -- Live Classification
    // =========================================================================

    private IEnumerator SpawnLivePackets()
    {
        foreach (var pkt in _scenario.LivePackets)
        {
            if (_phase != Phase.LiveClassification) yield break;

            SpawnCard(pkt);
            _pendingLive.Remove(pkt);
            UpdateRemainingText();

            yield return new WaitForSecondsRealtime(_scenario.PacketInterval);
        }

        // All packets spawned -- advance only once every card is classified
        _allPacketsSpawned = true;
        CheckAllClassified();
    }

    private void SpawnCard(PacketData pkt)
    {
        if (packetCardParent == null || packetCardPrefab == null) return;

        var card = Instantiate(packetCardPrefab, packetCardParent, false);

        SetTMP(card, "Src",     pkt.SrcIp);
        SetTMP(card, "Dst",     pkt.DstIp);
        SetTMP(card, "Proto",   pkt.Proto);
        SetTMP(card, "Port",    pkt.DstPort == 0 ? "--" : pkt.DstPort.ToString());
        SetTMP(card, "Payload", "[TLS ENCRYPTED]");

        var allowBtn = FindDeep(card.transform, "AllowButton")?.GetComponent<Button>();
        var denyBtn  = FindDeep(card.transform, "DenyButton") ?.GetComponent<Button>();

        if (allowBtn != null)
            allowBtn.onClick.AddListener(() => Classify(pkt, card, allow: true,  allowBtn, denyBtn));
        if (denyBtn != null)
            denyBtn .onClick.AddListener(() => Classify(pkt, card, allow: false, allowBtn, denyBtn));

        // Tier 4: animated expiry bar
        if (_tier >= 4 && _scenario.PacketExpiry > 0)
        {
            var expiryBar = FindDeep(card.transform, "ExpiryBar")?.GetComponent<Image>();
            if (expiryBar != null)
                StartCoroutine(RunExpiry(expiryBar, _scenario.PacketExpiry, pkt, card, allowBtn, denyBtn));
        }

        Canvas.ForceUpdateCanvases();
        var scroll = packetCardParent.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 0f;
    }

    private IEnumerator RunExpiry(Image bar, float duration, PacketData pkt,
                                  GameObject card, Button allow, Button deny)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (bar == null) yield break;
            bar.fillAmount = 1f - elapsed / duration;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        // Auto-allow on expiry (packet slips through unclassified)
        if (allow != null && allow.interactable)
            Classify(pkt, card, allow: true, allow, deny);
    }

    private void CheckAllClassified()
    {
        if (_allPacketsSpawned && _classifiedCards >= _totalCards)
            AdvanceToRuleCommit();
    }

    private void Classify(PacketData pkt, GameObject card, bool allow, Button allowBtn, Button denyBtn)
    {
        if (allowBtn != null) allowBtn.interactable = false;
        if (denyBtn  != null) denyBtn .interactable = false;

        _classifiedCards++;
        CheckAllClassified();

        string portStr = pkt.DstPort == 0 ? "ICMP" : $":{pkt.DstPort}";

        if (allow)
        {
            if (pkt.IsMalicious)
            {
                AddSessionLog($"[ALLOWED] {pkt.SrcIp} {portStr} -- C2 packet passed through", ColRed);
                TintCard(card, new Color(1f, 0.2f, 0.2f, 0.12f));
            }
            else
            {
                AddSessionLog($"[ALLOWED] {pkt.SrcIp} {portStr}", Color.white);
                TintCard(card, Color.clear);
            }
        }
        else
        {
            if (!pkt.IsMalicious)
            {
                _falsePositives = Mathf.Min(_falsePositives + 1, MaxFP);
                AddSessionLog($"[DENIED] {pkt.SrcIp} {portStr} -- WARNING: false positive x{_falsePositives}", ColAmber);
                TintCard(card, new Color(1f, 0.75f, 0.2f, 0.15f));
                // Void any pre-block for channels that share this port -- blanket deny doesn't earn credit
                _fpPortCombos.Add($"{pkt.Proto}:{pkt.DstPort}");

                if (_falsePositives >= MaxFP)
                {
                    AddSessionLog("CRITICAL: TOO MANY FALSE POSITIVES -- RESETTING SCAN", ColRed);
                    StartCoroutine(ResetToPhase1());
                }
            }
            else
            {
                AddSessionLog($"[DENIED] {pkt.SrcIp} {portStr} -- C2 blocked", ColGreen);
                TintCard(card, new Color(0.2f, 0.82f, 0.4f, 0.15f));

                // Option B: track correct C2 denials for Phase 3 pre-blocking
                if (pkt.C2Channel != null)
                {
                    if (!_channelDeniedCount.ContainsKey(pkt.C2Channel))
                        _channelDeniedCount[pkt.C2Channel] = 0;
                    _channelDeniedCount[pkt.C2Channel]++;
                }
            }
            UpdateFPBar();
        }
    }

    private void TintCard(GameObject card, Color c)
    {
        var img = card.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    private void UpdateFPBar()
    {
        if (falsePositiveCountText != null)
            falsePositiveCountText.text = $"{_falsePositives} / {MaxFP}";
        if (falsePositiveFill != null)
            falsePositiveFill.fillAmount = (float)_falsePositives / MaxFP;
    }

    private void UpdateRemainingText()
    {
        if (packetsRemainingText != null)
            packetsRemainingText.text = $"{_pendingLive.Count} remaining";
    }

    private void AddSessionLog(string text, Color col)
    {
        if (sessionLogParent == null || sessionLogLinePrefab == null) return;

        var go  = Instantiate(sessionLogLinePrefab, sessionLogParent, false);
        var tmp = go.transform.Find("LogText")?.GetComponent<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; tmp.color = col; }

        // Cap log at 8 visible lines (if not while -- Destroy is deferred, childCount doesn't update immediately)
        if (sessionLogParent.childCount > 8)
            Destroy(sessionLogParent.GetChild(0).gameObject);
    }

    // =========================================================================
    // Phase 3 -- Rule Commit
    // =========================================================================

    private void AdvanceToRuleCommit()
    {
        // Option B: channel pre-blocks only if ALL its C2 packets were correctly denied
        // AND no false positive occurred on the same proto:port (blanket deny doesn't count)
        _phase2PreBlocked.Clear();
        foreach (var channel in _scenario.C2Channels)
        {
            int denied = _channelDeniedCount.TryGetValue(channel, out int d) ? d : 0;
            int total  = _channelTotalCount .TryGetValue(channel, out int t) ? t : 0;
            if (total <= 0 || denied < total) continue;

            string portCombo = _scenario.ChannelHints.TryGetValue(channel, out string h) ? h : "";
            if (!_fpPortCombos.Contains(portCombo))
                _phase2PreBlocked.Add(channel);
        }

        _rules.Clear();
        SetPhase(Phase.RuleCommit);

        if (ruleParseErrorText != null)    ruleParseErrorText.gameObject.SetActive(false);
        if (collateralWarningText != null) collateralWarningText.gameObject.SetActive(false);

        // Tier 4: inject a pre-filled trap rule player must notice and delete
        if (_tier >= 4 && _scenario.HasDecoyRules)
        {
            var trap = FirewallRuleParser.Parse("DENY proto:TCP dst_port:22");
            if (trap.Success) _rules.Add(trap.Rule);
        }

        RefreshRuleList();
        RefreshChannelStatus();
    }

    private IEnumerator ResetToPhase1()
    {
        // Brief pause so the player sees the final false positive tint and log message
        yield return new WaitForSecondsRealtime(2f);

        // Reset all Phase 2 state
        _falsePositives   = 0;
        _classifiedCards  = 0;
        _totalCards       = 0;
        _allPacketsSpawned = false;
        _pendingLive.Clear();
        _channelDeniedCount.Clear();
        _fpPortCombos.Clear();

        // Destroy spawned cards and session log entries
        if (packetCardParent != null)
            foreach (Transform t in packetCardParent) Destroy(t.gameObject);
        if (sessionLogParent != null)
            foreach (Transform t in sessionLogParent) Destroy(t.gameObject);

        UpdateFPBar();
        SetPhase(Phase.LogAnalysis);
        PopulateLogTable();
    }

    private void OnAddRule()
    {
        if (ruleInputField == null) return;
        string input = ruleInputField.text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var result = FirewallRuleParser.Parse(input);

        if (!result.Success)
        {
            if (ruleParseErrorText != null)
            {
                ruleParseErrorText.text = $"Syntax error: {result.Error}";
                ruleParseErrorText.gameObject.SetActive(true);
            }
            return;
        }

        if (ruleParseErrorText != null) ruleParseErrorText.gameObject.SetActive(false);
        _rules.Add(result.Rule);
        ruleInputField.text = string.Empty;

        RefreshRuleList();
        RefreshChannelStatus();
    }

    private void OnCommitRules()
    {
        if (_rules.Count == 0)
        {
            feedbackPopup?.Show(false, "No rules committed.\nWrite at least one DENY rule.");
            return;
        }

        var allPackets = AllPackets();
        var eval       = FirewallRuleParser.EvaluateAll(_rules, allPackets);

        bool allBlocked   = _scenario.C2Channels.TrueForAll(c => _phase2PreBlocked.Contains(c) || eval.BlockedC2Channels.Contains(c));
        bool noCollateral = eval.CollateralServices.Count == 0;

        if (allBlocked && noCollateral)
        {
            _phase = Phase.Solved;
            OnPacketFilterSolved?.Invoke(_wrongSubmits);
            PlayerMetricsTracker.Instance?.NotifyPacketFilterSolved(_wrongSubmits);

            feedbackPopup?.Show(true,
                $"All C2 channels severed.\n" +
                $"{_scenario.C2Channels.Count} ARBITEX connections terminated.\n" +
                "Server node offline.",
                onDismiss: OnClose);
        }
        else
        {
            _wrongSubmits++;
            feedbackPopup?.Show(false, BuildFailMessage(eval, allBlocked, noCollateral));
        }

        RefreshChannelStatus();
    }

    private string BuildFailMessage(EvaluationResult eval, bool allBlocked, bool noCollateral)
    {
        var sb = new StringBuilder();

        if (!allBlocked)
        {
            var leaking = new List<string>();
            foreach (var ch in _scenario.C2Channels)
                if (!eval.BlockedC2Channels.Contains(ch)) leaking.Add(ch);
            sb.AppendLine($"Still open: {string.Join(", ", leaking)}");
            sb.AppendLine("Rules do not cover all C2 traffic.");
        }

        if (!noCollateral)
        {
            sb.AppendLine($"Collateral: {string.Join(", ", eval.CollateralServices)} will go offline.");
            sb.AppendLine("Revise rules to spare legitimate services.");
        }

        return sb.ToString().TrimEnd();
    }

    private void RefreshRuleList()
    {
        if (ruleListParent == null || ruleRowPrefab == null) return;

        foreach (Transform t in ruleListParent) Destroy(t.gameObject);

        var allPackets = AllPackets();

        foreach (var rule in new List<FirewallRule>(_rules))
        {
            var row = Instantiate(ruleRowPrefab, ruleListParent);
            SetTMP(row, "RuleText", rule.OriginalText);

            // Per-rule status
            bool hitsC2  = false;
            bool hitsColl = false;
            if (rule.Action == RuleAction.Deny)
            {
                foreach (var pkt in allPackets)
                {
                    if (!FirewallRuleParser.Matches(rule, pkt)) continue;
                    if (pkt.IsMalicious)      hitsC2   = true;
                    if (pkt.IsCollateralRisk) hitsColl = true;
                }
            }

            var icon = row.transform.Find("IconText")?.GetComponent<TextMeshProUGUI>();
            if (icon != null)
            {
                if (hitsColl)    { icon.text = "!";  icon.color = ColAmber; }
                else if (hitsC2) { icon.text = "OK"; icon.color = ColGreen; }
                else             { icon.text = "-";  icon.color = Color.gray; }
            }

            // Capture rule by reference to avoid C# loop-variable closure bug
            FirewallRule captured = rule;
            var del = row.transform.Find("DeleteButton")?.GetComponent<Button>();
            if (del != null)
                del.onClick.AddListener(() =>
                {
                    _rules.Remove(captured);
                    RefreshRuleList();
                    RefreshChannelStatus();
                });
        }
    }

    private void RefreshChannelStatus()
    {
        if (channelListParent == null || channelRowPrefab == null) return;

        foreach (Transform t in channelListParent) Destroy(t.gameObject);

        var eval = FirewallRuleParser.EvaluateAll(_rules, AllPackets());

        foreach (var channel in _scenario.C2Channels)
        {
            bool preBlocked = _phase2PreBlocked.Contains(channel);
            bool ruleBlocked = eval.BlockedC2Channels.Contains(channel);
            bool blocked = preBlocked || ruleBlocked;

            var row = Instantiate(channelRowPrefab, channelListParent);

            // Show channel ID + port hint so player knows what rule to write
            string hint = _scenario.ChannelHints.TryGetValue(channel, out string h) ? $"  {h}" : "";
            SetTMP(row, "ChannelLabel", channel + hint);

            string statusLabel = preBlocked ? "BLOCKED*" : blocked ? "BLOCKED" : "OPEN";
            SetTMP(row, "StatusText", statusLabel);

            Color c    = blocked ? ColGreen : ColRed;
            var   dot  = FindDeep(row.transform, "Dot")        ?.GetComponent<Image>();
            var   stat = FindDeep(row.transform, "StatusText") ?.GetComponent<TextMeshProUGUI>();
            if (dot  != null) dot.color  = c;
            if (stat != null) stat.color = c;
        }

        bool anyColl = eval.CollateralServices.Count > 0;
        if (collateralWarningText != null)
        {
            collateralWarningText.gameObject.SetActive(anyColl);
            if (anyColl)
                collateralWarningText.text =
                    $"COLLATERAL: {string.Join(", ", eval.CollateralServices)} will go offline.";
        }
    }

    // =========================================================================
    // Close
    // =========================================================================

    private void OnClose()
    {
        StopAllCoroutines();
        _onClose?.Invoke();
    }

    // =========================================================================
    // Scenario Generation
    // =========================================================================

    private PacketFilterScenario BuildScenario(int tier)
    {
        // DESIGN NOTE: All C2 packets spoof src IPs from the 10.0.1.x client range.
        // The Network Monitor lives at 10.0.2.1 -- a different subnet -- so in Phase 2
        // the player CAN distinguish Monitor (10.0.2.1:4444) from C2 (10.0.1.x:4444)
        // by IP subnet, even without payload. However they must have noticed this
        // in Phase 1. A broad "DENY dst_port:4444" would still kill the Monitor.

        var s = new PacketFilterScenario();

        // ── Legitimate base traffic ────────────────────────────────────────────
        var legit = new List<PacketData>
        {
            P("08:14:02", "10.0.1.4",  "10.0.0.1", "TCP",  80,   "GET /index.html HTTP/1.1",      false, null,   false, null),
            P("08:14:09", "10.0.1.12", "10.0.0.5", "UDP",  53,   "DNS QUERY: sys.internal",       false, null,   false, null),
            P("08:14:14", "10.0.2.1",  "10.0.0.1", "TCP",  4444, "MONITOR::ping_status",          false, null,   true,  "Network Monitor"),
            P("08:14:22", "10.0.1.8",  "10.0.0.3", "TCP",  22,   "SSH-2.0 session open",         false, null,   false, null),
            P("08:14:29", "10.0.1.2",  "10.0.0.2", "ICMP", 0,    "echo-request seq=4",           false, null,   false, null),
            P("08:14:37", "10.0.2.1",  "10.0.0.1", "TCP",  4444, "MONITOR::system_check",        false, null,   true,  "Network Monitor"),
            P("08:14:45", "10.0.1.4",  "10.0.0.1", "TCP",  443,  "TLS: client hello",            false, null,   false, null),
        };

        // Tier 3+: DNS Resolver becomes a second collateral risk (shares UDP :53 with C2-04)
        if (tier >= 3)
        {
            legit.Add(P("08:14:05", "10.0.1.6", "10.0.0.5", "UDP", 53, "DNS QUERY: auth.internal", false, null, true, "DNS Resolver"));
            legit.Add(P("08:14:47", "10.0.1.6", "10.0.0.5", "UDP", 53, "DNS QUERY: login.local",   false, null, true, "DNS Resolver"));
        }

        // Tier 4+: HTTPS Gateway becomes a third collateral risk (shares TCP :443 with C2-05)
        if (tier >= 4)
            legit.Add(P("08:14:48", "10.0.1.5", "10.0.0.1", "TCP", 443, "HTTPS: api_call secure", false, null, true, "HTTPS Gateway"));

        // ── C2 channels ────────────────────────────────────────────────────────
        var c2_01 = new List<PacketData>
        {
            P("08:14:07", "10.0.1.4", "10.0.0.1", "TCP", 4444, "ARBITEX_CMD::EXEC shell_init",   true, "C2-01", false, null),
            P("08:14:25", "10.0.1.4", "10.0.0.1", "TCP", 4444, "ARBITEX_CMD::EXEC watchdog",     true, "C2-01", false, null),
        };
        var c2_02 = new List<PacketData>
        {
            P("08:14:18", "10.0.1.8", "10.0.0.1", "TCP", 4444, "ARBITEX_CMD::SYNC heartbeat",    true, "C2-02", false, null),
            P("08:14:41", "10.0.1.8", "10.0.0.1", "TCP", 4444, "ARBITEX_CMD::SYNC node_delta",   true, "C2-02", false, null),
        };
        var c2_03 = new List<PacketData>
        {
            P("08:14:33", "10.0.1.12", "10.0.0.1", "TCP", 8080, "ARBITEX_CMD::PUSH payload_delta", true, "C2-03", false, null),
            P("08:14:53", "10.0.1.12", "10.0.0.1", "TCP", 8080, "ARBITEX_CMD::PUSH config_sync",   true, "C2-03", false, null),
        };
        var c2_04 = new List<PacketData>
        {
            P("08:14:11", "10.0.1.2", "10.0.0.5", "UDP", 53, "ARBITEX_CMD::DNS_BEACON a1b2c3",   true, "C2-04", false, null),
            P("08:14:43", "10.0.1.2", "10.0.0.5", "UDP", 53, "ARBITEX_CMD::DNS_BEACON d4e5f6",   true, "C2-04", false, null),
        };
        var c2_05 = new List<PacketData>
        {
            P("08:14:16", "10.0.1.3", "10.0.0.1", "TCP", 443, "ARBITEX_CMD::TLS_TUNNEL exfil",   true, "C2-05", false, null),
            P("08:14:50", "10.0.1.3", "10.0.0.1", "TCP", 443, "ARBITEX_CMD::TLS_TUNNEL beacon",  true, "C2-05", false, null),
        };

        // ── Assemble log (Phase 1) ─────────────────────────────────────────────
        var log = new List<PacketData>(legit);
        log.AddRange(c2_01); log.AddRange(c2_02); log.AddRange(c2_03);
        if (tier >= 3) log.AddRange(c2_04);
        if (tier >= 4) log.AddRange(c2_05);
        log.Sort((a, b) => string.Compare(a.Timestamp, b.Timestamp, StringComparison.Ordinal));
        s.LogPackets = log;

        // ── C2 channel IDs ────────────────────────────────────────────────────
        s.C2Channels.AddRange(new[] { "C2-01", "C2-02", "C2-03" });
        if (tier >= 3) s.C2Channels.Add("C2-04");
        if (tier >= 4) s.C2Channels.Add("C2-05");

        // ── Collateral services ────────────────────────────────────────────────
        s.Services.Add(new CollateralService { Name = "Network Monitor", OverlapNote = "shares port :4444 with C2-01/02" });
        if (tier >= 3) s.Services.Add(new CollateralService { Name = "DNS Resolver",  OverlapNote = "shares UDP :53 with C2-04" });
        if (tier >= 4) s.Services.Add(new CollateralService { Name = "HTTPS Gateway", OverlapNote = "shares TCP :443 with C2-05" });

        // ── Assemble live stream (Phase 2) ────────────────────────────────────
        var live = new List<PacketData>(legit);
        live.AddRange(c2_01); live.AddRange(c2_02); live.AddRange(c2_03);
        if (tier >= 3) live.AddRange(c2_04);
        if (tier >= 4) live.AddRange(c2_05);
        Shuffle(live);
        s.LivePackets = live;

        s.PacketInterval = tier >= 4 ? packetIntervalTier4 : tier >= 3 ? packetIntervalTier3 : packetIntervalTier2;
        s.PacketExpiry   = tier >= 4 ? packetExpiryTier4 : 0f;
        s.HasDecoyRules  = tier >= 4;

        // Hints shown in Phase 3 channel status panel so player knows what rule to write
        s.ChannelHints["C2-01"] = "TCP:4444";
        s.ChannelHints["C2-02"] = "TCP:4444";
        s.ChannelHints["C2-03"] = "TCP:8080";
        if (tier >= 3) s.ChannelHints["C2-04"] = "UDP:53";
        if (tier >= 4) s.ChannelHints["C2-05"] = "TCP:443";

        return s;
    }

    private static PacketData P(string ts, string src, string dst, string proto, int port,
                                string payload, bool mal, string c2, bool coll, string svc) =>
        new PacketData
        {
            Timestamp        = ts,
            SrcIp            = src,
            DstIp            = dst,
            Proto            = proto,
            DstPort          = port,
            Payload          = payload,
            IsMalicious      = mal,
            C2Channel        = c2,
            IsCollateralRisk = coll,
            ServiceName      = svc,
        };

    // =========================================================================
    // Utilities
    // =========================================================================

    /// <summary>
    /// Merged packet set for Phase 3 validation.
    /// Deduplicates live packets that are already in the log (same channel/port/proto).
    /// </summary>
    private List<PacketData> AllPackets()
    {
        var all = new List<PacketData>(_scenario.LogPackets);
        foreach (var p in _scenario.LivePackets)
        {
            bool duplicate = all.Exists(x =>
                x.Payload   == p.Payload &&
                x.DstPort   == p.DstPort &&
                x.Proto     == p.Proto   &&
                x.IsMalicious == p.IsMalicious);
            if (!duplicate) all.Add(p);
        }
        return all;
    }

    private static void SetTMP(GameObject go, string childName, string text)
    {
        var child = FindDeep(go.transform, childName);
        if (child == null) return;
        var tmp = child.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    /// <summary>Recursive Transform.Find -- Unity's built-in Find only checks direct children.</summary>
    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

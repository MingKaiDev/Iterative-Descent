# Packet Filter Puzzle -- Unity Scene Setup (Story 17)

Server Room Lords Objective. Three-phase network security puzzle.
Theme: dark terminal, green-on-black CRT aesthetic, red/amber threat indicators.

---

## Scripts Written

| File | Path |
|------|------|
| PacketData.cs | Assets/Scripts/Puzzles/PacketFilter/ |
| FirewallRuleParser.cs | Assets/Scripts/Puzzles/PacketFilter/ |
| PacketFilterFeedbackPopup.cs | Assets/Scripts/Puzzles/PacketFilter/ |
| PacketFilterPuzzleUI.cs | Assets/Scripts/Puzzles/PacketFilter/ |
| PacketFilterProp.cs | Assets/Scripts/Props/ |
| PacketFilterEventHandler.cs | Assets/Scripts/Events/ |

Modified:
- BayesianKnowledgeTracker.cs -- added `NetworkSecurity = "network_security"`
- PlayerMetricsTracker.cs -- added PacketFilter API + event subscription
- quiz_bank.json -- 5 MCQs tagged `network_security`

---

## Prefabs to Create (Before Building Canvas)

### LogRow prefab
- Root: Image (height 24, no sprite, Raycast Target OFF)
- Add Horizontal Layout Group: Spacing 8, Child Force Expand Width OFF, Padding Left/Right 6
- Children (all TextMeshProUGUI, Raycast Target OFF):
  - Timestamp -- Width 70, Size 10, color #888888, Middle Left
  - Src -- Width 90, Size 10, color #AAFFAA, Middle Left
  - Dst -- Width 90, Size 10, color #AAAAFF, Middle Left
  - Proto -- Width 40, Size 10, color #FFCC00, Middle Left
  - Port -- Width 40, Size 10, color #FFCC00, Middle Left
  - Payload -- flexible (Layout Element: Flexible Width 1), Size 10, color #CCCCCC, Middle Left, overflow Ellipsis

### PacketCard prefab
- Root: Image (height 68, color #111A11, Raycast Target OFF)
- Children:
  - InfoRow (HLG, Spacing 8, Padding 6, height 28):
    - Src -- Width 90, Size 10, color #AAFFAA, Middle Left
    - Dst -- Width 90, Size 10, color #AAAAFF, Middle Left
    - Proto -- Width 40, Size 10, color #FFCC00, Middle Left
    - Port -- Width 40, Size 10, color #FFCC00, Middle Left
    - Payload -- Flexible Width 1, Size 10, color #888888, Middle Left (shows "[TLS ENCRYPTED]")
  - ActionRow (HLG, Spacing 8, Padding Left 6, height 28):
    - AllowButton (Button, Width 80, Height 24, Image color #0D260D):
      - Child TMP: "ALLOW", Size 11, color #00FF88, Middle Center, Bold
    - DenyButton (Button, Width 80, Height 24, Image color #260D0D):
      - Child TMP: "DENY", Size 11, color #FF4444, Middle Center, Bold
  - ExpiryBar (Image, height 4, anchor bottom/stretch, Left 0 Right 0 Bottom 0):
    - Image Type: Filled, Fill Method: Horizontal, Fill Origin: Left, Fill Amount: 1
    - Color: #FF4444

### LogLine prefab
- Root: empty RectTransform (height 18)
- Child: LogText (TextMeshProUGUI, anchor stretch/stretch, Size 10, color #AAAAAA, Middle Left, word wrap OFF)

### RuleRow prefab
- Root: Image (height 28, color #0A1A0A, Raycast Target OFF)
- Add Horizontal Layout Group: Spacing 6, Padding Left/Right 6, Child Force Expand Width OFF
- Children:
  - IconText (TMP, Width 28, Size 11, color #AAAAAA, Middle Center, Bold) -- shows "OK"/"!"/"--"
  - RuleText (TMP, Flexible Width 1, Size 10, color #CCCCCC, Middle Left, overflow Ellipsis)
  - DeleteButton (Button, Width 24, Height 24, Image color #260D0D):
    - Child TMP: "X", Size 11, color #FF4444, Middle Center

### ChannelRow prefab
- Root: empty RectTransform (height 24)
- Add Horizontal Layout Group: Spacing 6, Padding Left 6
- Children:
  - Dot (Image, Width 12, Height 12, sprite a small circle, color set by script)
  - ChannelLabel (TMP, Width 60, Size 11, color #CCCCCC, Middle Left)
  - StatusText (TMP, Width 70, Size 11, color set by script, Middle Left, Bold)

---

## Canvas Hierarchy

```
[PacketFilterPanel]         -- full-screen overlay, starts INACTIVE
  [Background]
  [Header]
    [TitleText]
    [PhaseText]
  [LogPanel]                -- Phase 1, starts ACTIVE (first phase)
    [LogScrollRect]
      [Viewport]
        [LogTableParent]
    [LogStatsText]
    [InitiateButton]
  [LivePanel]               -- Phase 2, starts INACTIVE
    [TopBar]
      [FPLabel]
      [FPRow]
        [FalsePositiveCountText]
        [FalsePositiveFill]
      [PacketsRemainingText]
    [MainArea]
      [PacketScrollRect]
        [Viewport]
          [PacketCardParent]
      [SessionLogScrollRect]
        [Viewport]
          [SessionLogParent]
  [RulePanel]               -- Phase 3 + Solved, starts INACTIVE
    [LeftPanel]
      [RuleTerminal]
        [TerminalHeader]
        [SyntaxHint]
        [InputRow]
          [RuleInputField]
          [AddRuleButton]
        [RuleParseErrorText]  -- starts INACTIVE
        [RuleScrollRect]
          [Viewport]
            [RuleListParent]
      [CollateralWarningText] -- starts INACTIVE
      [CommitButton]
    [RightPanel]
      [ChannelHeader]
      [ChannelScrollRect]
        [Viewport]
          [ChannelListParent]
      [CloseButton]
  [FeedbackPopup]           -- PacketFilterFeedbackPopup, starts INACTIVE
    [Blocker]
    [PopupCard]
      [IconText]
      [MessageText]
      [OkButton]
```

---

## Component Settings (per GO)

### PacketFilterPanel
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Left/Right/Top/Bottom = 0 |
| PacketFilterPuzzleUI | (this script -- wire all Inspector fields, see wiring section) |
| Active in scene | OFF |

### Background
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: #000000 alpha 210, Raycast Target ON |

### Header
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Height 48, Left 0, Right 0, Top 0 |
| Image | Color: #050F05, Raycast Target OFF |

### TitleText
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: left/stretch, Width 400, Left 16 |
| TextMeshProUGUI | Text: "ARBITEX -- PACKET FILTER", Size 14, color #00FF88, Middle Left, Bold |

### PhaseText
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: right/stretch, Width 240, Right 16 |
| TextMeshProUGUI | Text: "PHASE 1 -- LOG ANALYSIS", Size 11, color #558855, Middle Right |

> PhaseText is decorative only -- the script does not update it. Set it to Phase 1 label and leave it.

---

## LogPanel (Phase 1)

### LogPanel
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Top 48, Bottom 0, Left 0, Right 0 |
| Active in scene | ON (first phase shown by default) |

### LogScrollRect
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Top 0, Bottom 52, Left 16, Right 16 |
| ScrollRect | Vertical: ON, Horizontal: OFF, Movement Type: Clamped, Scrollbar: none |

### LogScrollRect > Viewport
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | any color, Raycast Target OFF |
| Mask | Show Mask Graphic: OFF |

### LogTableParent (wire to logTableParent)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Left 0, Right 0, Top 0 |
| Vertical Layout Group | Spacing 2, Child Force Expand Width ON, Child Force Expand Height OFF, Padding Top 4 |
| Content Size Fitter | Vertical Fit: Preferred Size |

### LogStatsText (wire to logStatsText)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5,0), Height 20, Left 16, Right 16, Bottom 30 |
| TextMeshProUGUI | Text: "", Size 10, color #558855, Middle Left |

### InitiateButton (wire to initiateButton)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: bottom/center, Pivot (0.5,0), Width 240, Height 36, Bottom 8 |
| Image | Color: #0D260D |
| Button | Highlighted Color: #1A4A1A, Pressed: #071007 |
| Child TMP | Text: "INITIATE LIVE SCAN", Size 13, color #00FF88, Middle Center, Bold |

---

## LivePanel (Phase 2)

### LivePanel
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Top 48, Bottom 0, Left 0, Right 0 |
| Active in scene | OFF |

### TopBar
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Height 44, Left 16, Right 16, Top 0 |
| Horizontal Layout Group | Spacing 16, Child Alignment: Middle Left, Child Force Expand: OFF |

### FPLabel
| Component | Setting |
|-----------|---------|
| RectTransform | Width 120, Height 24 (Layout Element) |
| TextMeshProUGUI | Text: "FALSE POSITIVES", Size 10, color #558855, Middle Left |

### FPRow
| Component | Setting |
|-----------|---------|
| RectTransform | Width 160, Height 24 (Layout Element) |
| Horizontal Layout Group | Spacing 8, Child Force Expand: OFF |

### FalsePositiveCountText (wire to falsePositiveCountText)
| Component | Setting |
|-----------|---------|
| RectTransform | Width 40, Height 24 (Layout Element) |
| TextMeshProUGUI | Text: "0 / 3", Size 11, color #FF4444, Middle Left, Bold |

### FalsePositiveFill (wire to falsePositiveFill)
| Component | Setting |
|-----------|---------|
| RectTransform | Width 100, Height 12 (Layout Element) -- vertically centered by HLG |
| Image | Color: #FF4444, Image Type: Filled, Fill Method: Horizontal, Fill Origin: Left, Fill Amount: 0 |

### PacketsRemainingText (wire to packetsRemainingText)
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 1, Height 24 (Layout Element) |
| TextMeshProUGUI | Text: "-- remaining", Size 10, color #558855, Middle Right |

### MainArea
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Top 44, Bottom 0, Left 0, Right 0 |
| Horizontal Layout Group | Spacing 8, Padding 16, Child Force Expand Width ON |

### PacketScrollRect
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 3 (Layout Element) -- takes ~75% of width |
| ScrollRect | Vertical: ON, Horizontal: OFF, Movement Type: Clamped |

### PacketScrollRect > Viewport
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Mask | Show Mask Graphic: OFF |
| Image | any color |

### PacketCardParent (wire to packetCardParent)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Top 0 |
| Vertical Layout Group | Spacing 4, Child Force Expand Width ON, Child Force Expand Height OFF, Padding 4 |
| Content Size Fitter | Vertical Fit: Preferred Size |

### SessionLogScrollRect
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 1 (Layout Element) -- takes ~25% of width |
| ScrollRect | Vertical: ON, Horizontal: OFF, Movement Type: Clamped |
| Image | Color: #050F05 |

### SessionLogScrollRect > Viewport
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Mask | Show Mask Graphic: OFF |
| Image | any color |

### SessionLogParent (wire to sessionLogParent)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Top 0 |
| Vertical Layout Group | Spacing 2, Child Force Expand Width ON, Child Force Expand Height OFF, Padding 4 |
| Content Size Fitter | Vertical Fit: Preferred Size |

---

## RulePanel (Phase 3)

### RulePanel
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Top 48, Bottom 0, Left 0, Right 0 |
| Horizontal Layout Group | Spacing 8, Padding 16, Child Force Expand Width ON |
| Active in scene | OFF |

### LeftPanel
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 3 (Layout Element) -- ~65% of width |
| Vertical Layout Group | Spacing 8, Child Force Expand Width ON, Child Force Expand Height OFF |

### RuleTerminal
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Height 1 (Layout Element) |
| Image | Color: #050F05 |
| Vertical Layout Group | Spacing 4, Padding 10, Child Force Expand Width ON, Child Force Expand Height OFF |

### TerminalHeader
| Component | Setting |
|-----------|---------|
| RectTransform | Height 22 (Layout Element) |
| TextMeshProUGUI | Text: "FIREWALL RULE EDITOR", Size 12, color #00FF88, Middle Left, Bold |

### SyntaxHint
| Component | Setting |
|-----------|---------|
| RectTransform | Height 18 (Layout Element) |
| TextMeshProUGUI | Text: "[ALLOW/DENY] [proto:TCP/UDP/ICMP] [src:IP] [dst:IP] [dst_port:N] [payload:KEYWORD]", Size 9, color #336633, Middle Left |

### InputRow
| Component | Setting |
|-----------|---------|
| RectTransform | Height 32 (Layout Element) |
| Horizontal Layout Group | Spacing 6, Child Force Expand Width OFF |

### RuleInputField (wire to ruleInputField)
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 1 (Layout Element), Height 32 |
| TMP_InputField | Font size 11, color #CCCCCC |
| Background Image | Color: #0A1A0A |
| Placeholder TMP | Text: "DENY proto:TCP dst_port:4444 payload:ARBITEX_CMD", Size 10, color #334433 |
| Text TMP | Size 11, color #00FF88 |

### AddRuleButton (wire to addRuleButton)
| Component | Setting |
|-----------|---------|
| RectTransform | Width 60, Height 32 (Layout Element) |
| Image | Color: #0D260D |
| Button | Highlighted: #1A4A1A |
| Child TMP | Text: "ADD", Size 11, color #00FF88, Middle Center, Bold |

### RuleParseErrorText (wire to ruleParseErrorText)
| Component | Setting |
|-----------|---------|
| RectTransform | Height 18 (Layout Element) |
| TextMeshProUGUI | Text: "", Size 10, color #FF4444, Middle Left |
| Active in scene | OFF |

### RuleScrollRect
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Height 1 (Layout Element) |
| ScrollRect | Vertical: ON, Horizontal: OFF, Movement Type: Clamped |

### RuleScrollRect > Viewport
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Mask | Show Mask Graphic: OFF |
| Image | any color |

### RuleListParent (wire to ruleListParent)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Top 0 |
| Vertical Layout Group | Spacing 3, Child Force Expand Width ON, Child Force Expand Height OFF |
| Content Size Fitter | Vertical Fit: Preferred Size |

### CollateralWarningText (wire to collateralWarningText)
| Component | Setting |
|-----------|---------|
| RectTransform | Height 32 (Layout Element) |
| TextMeshProUGUI | Text: "", Size 10, color #FFCC00, Middle Left, word wrap ON |
| Active in scene | OFF |

### CommitButton (wire to commitButton)
| Component | Setting |
|-----------|---------|
| RectTransform | Height 40 (Layout Element) |
| Image | Color: #1A0D00 |
| Button | Highlighted: #3A2000, Pressed: #0D0800 |
| Child TMP | Text: "COMMIT FIREWALL RULES", Size 13, color #FFAA00, Middle Center, Bold |

### RightPanel
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Width 1 (Layout Element) -- ~35% of width |
| Image | Color: #050F05 |
| Vertical Layout Group | Spacing 8, Padding 10, Child Force Expand Width ON, Child Force Expand Height OFF |

### ChannelHeader
| Component | Setting |
|-----------|---------|
| RectTransform | Height 22 (Layout Element) |
| TextMeshProUGUI | Text: "C2 CHANNEL STATUS", Size 12, color #00FF88, Middle Left, Bold |

### ChannelScrollRect
| Component | Setting |
|-----------|---------|
| RectTransform | Flexible Height 1 (Layout Element) |
| ScrollRect | Vertical: ON, Horizontal: OFF, Movement Type: Clamped |

### ChannelScrollRect > Viewport
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Mask | Show Mask Graphic: OFF |
| Image | any color |

### ChannelListParent (wire to channelListParent)
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/stretch, Pivot (0.5,1), Top 0 |
| Vertical Layout Group | Spacing 4, Child Force Expand Width ON, Child Force Expand Height OFF |
| Content Size Fitter | Vertical Fit: Preferred Size |

### CloseButton (wire to closeButton)
| Component | Setting |
|-----------|---------|
| RectTransform | Height 36 (Layout Element) |
| Image | Color: #1A0D0D |
| Button | Highlighted: #2A1414 |
| Child TMP | Text: "CLOSE", Size 12, color #FF4444, Middle Center |

---

## FeedbackPopup

### FeedbackPopup
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| PacketFilterFeedbackPopup | Wire IconText, MessageText, OkButton (see below) |
| Active in scene | OFF |

### FeedbackPopup > Blocker
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: #000000 alpha 0, Raycast Target ON |

### FeedbackPopup > PopupCard
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: center/center, Width 440, Height 200 |
| Image | Color: #0A1A0A |

### FeedbackPopup > PopupCard > IconText
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: top/center, Pivot (0.5,1), Width 60, Height 60, Top -10 |
| TextMeshProUGUI | Text: "X", Size 36, Middle Center, color set by script (green on win, red on fail) |

### FeedbackPopup > PopupCard > MessageText
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: stretch/stretch, Left 20, Right 20, Top 60, Bottom 52 |
| TextMeshProUGUI | Size 12, color #CCCCCC, Middle Center, word wrap ON |

### FeedbackPopup > PopupCard > OkButton
| Component | Setting |
|-----------|---------|
| RectTransform | Anchor: bottom/center, Pivot (0.5,0), Width 100, Height 36, Bottom 8 |
| Image | Color: #0D260D |
| Button | Highlighted: #1A4A1A |
| Child TMP | Text: "OK", Size 13, color #00FF88, Middle Center |

---

## Inspector Wiring -- PacketFilterPuzzleUI

Select PacketFilterPanel. Drag into each slot on the PacketFilterPuzzleUI component:

**Phase 1 -- Log Analysis**
| Field | Drag in |
|-------|---------|
| Log Panel | LogPanel GO |
| Log Table Parent | LogTableParent RectTransform |
| Log Row Prefab | LogRow prefab (Project window) |
| Log Stats Text | LogStatsText TMP |
| Initiate Button | InitiateButton Button |

**Phase 2 -- Live Classification**
| Field | Drag in |
|-------|---------|
| Live Panel | LivePanel GO |
| Packet Card Parent | PacketCardParent RectTransform |
| Packet Card Prefab | PacketCard prefab (Project window) |
| False Positive Count Text | FalsePositiveCountText TMP |
| False Positive Fill | FalsePositiveFill Image |
| Packets Remaining Text | PacketsRemainingText TMP |
| Session Log Parent | SessionLogParent RectTransform |
| Session Log Line Prefab | LogLine prefab (Project window) |

**Phase 3 -- Rule Commit**
| Field | Drag in |
|-------|---------|
| Rule Panel | RulePanel GO |
| Rule Input Field | RuleInputField TMP_InputField |
| Add Rule Button | AddRuleButton Button |
| Rule Parse Error Text | RuleParseErrorText TMP |
| Rule List Parent | RuleListParent RectTransform |
| Rule Row Prefab | RuleRow prefab (Project window) |
| Channel List Parent | ChannelListParent RectTransform |
| Channel Row Prefab | ChannelRow prefab (Project window) |
| Collateral Warning Text | CollateralWarningText TMP |
| Commit Button | CommitButton Button |
| Close Button | CloseButton Button |

**Feedback**
| Field | Drag in |
|-------|---------|
| Feedback Popup | FeedbackPopup GO (PacketFilterFeedbackPopup component) |

Also wire the PacketFilterFeedbackPopup component on FeedbackPopup:
| Field | Drag in |
|-------|---------|
| Icon Text | IconText TMP |
| Message Text | MessageText TMP |
| Ok Button | OkButton Button |

---

## Inspector Wiring -- PacketFilterProp

Select the server node terminal mesh GO in the server room scene.

| Component | Setting |
|-----------|---------|
| BoxCollider | Is Trigger: ON, size to cover terminal front face |
| InteractableBase | Prompt Panel: assign if you have a world-space prompt GO, otherwise leave empty |
| InteractableRegistrar | no fields |
| PacketFilterProp | Packet Filter Overlay: drag PacketFilterPanel GO |
| InteractableIndicator (child GO) | Indicator Radius: 2.5, Bob Axis: (0,1,0), Idle Colour: #00FF88 alpha 180, Selected Colour: #FFFFFF alpha 255 |

---

## Inspector Wiring -- PacketFilterEventHandler

Select the persistent GameManager GO.

| Component | Setting |
|-----------|---------|
| PacketFilterEventHandler | Arbitex Commentator: (optional) drag a DialogueTrigger GO. Leave empty to skip the commentary reaction. |

If you wire a DialogueTrigger: assign the DialogueSequence asset you want to play directly on that DialogueTrigger component (not on this handler). The handler calls `PlayDialogue()` on it.

---

## ARES Boss Wiring

In the ARES boss controller, check the flag at Phase 2 entry:

```csharp
if (!PacketFilterEventHandler.ServerNodeDisabled)
{
    _maxHp = Mathf.RoundToInt(_maxHp * 1.25f);
    _damageMultiplier *= 1.10f;
    Debug.Log("[ARES] Server node active -- Phase 2 stat bonus applied.");
}
```

---

## DDA / BKT

- Concept key: `BayesianKnowledgeTracker.NetworkSecurity` ("network_security")
- DDA tier floor: clamped to minimum 2 in `PacketFilterPuzzleUI.InitPuzzle()`
- 5 MCQs tagged `network_security` added to quiz_bank.json

---

## Tier Differences

| Feature | Tier 2 | Tier 3 | Tier 4 |
|---------|--------|--------|--------|
| C2 channels | 3 | 4 | 5 |
| Collateral services | 1 (Monitor :4444) | 2 (+DNS Resolver :53) | 3 (+HTTPS Gateway :443) |
| Packet spawn rate | 2s | 1.5s | 1s |
| Expiry timer (Phase 2) | none | none | 4s per card |
| Decoy trap rule (Phase 3) | no | no | yes (DENY proto:TCP dst_port:22) |

---

## Correct Rule Set (Reference)

Tier 2 (3 channels):
```
ALLOW proto:TCP dst_port:4444 src:10.0.2.x
DENY proto:TCP dst_port:4444 payload:ARBITEX_CMD
DENY proto:TCP dst_port:8080 payload:ARBITEX_CMD
```

Tier 3 (adds C2-04 DNS beacon):
```
ALLOW proto:TCP dst_port:4444 src:10.0.2.x
DENY proto:TCP dst_port:4444 payload:ARBITEX_CMD
DENY proto:TCP dst_port:8080 payload:ARBITEX_CMD
DENY proto:UDP dst_port:53 payload:ARBITEX_CMD
```

Tier 4 (adds C2-05 TLS tunnel):
```
ALLOW proto:TCP dst_port:4444 src:10.0.2.x
DENY proto:TCP dst_port:4444 payload:ARBITEX_CMD
DENY proto:TCP dst_port:8080 payload:ARBITEX_CMD
DENY proto:UDP dst_port:53 payload:ARBITEX_CMD
DENY proto:TCP dst_port:443 payload:ARBITEX_CMD
```

Note: Tier 4 pre-fills a decoy `DENY proto:TCP dst_port:22`. It causes no collateral damage (SSH is not flagged collateral) so the player can leave it or delete it -- it does not affect the win condition. It exists to waste the player's attention.

---

## Testing Checklist

- [ ] Phase 1: log rows appear, C2 rows tinted red, Monitor row tinted amber
- [ ] Phase 1: Initiate button advances to Phase 2
- [ ] Phase 2: packet cards spawn at correct interval per tier
- [ ] Phase 2: ALLOW/DENY buttons disable after click
- [ ] Phase 2: false positive bar increments on denying a legit packet
- [ ] Phase 2: after all packets spawn + 1.5s, advances to Phase 3 automatically
- [ ] Phase 3: rule input parses correctly; syntax errors show RuleParseErrorText
- [ ] Phase 3: adding DENY proto:TCP dst_port:4444 payload:ARBITEX_CMD shows "OK" icon on that rule row
- [ ] Phase 3: adding DENY proto:TCP dst_port:4444 (no payload) shows "!" icon and CollateralWarningText
- [ ] Phase 3: C2 channel status updates live as rules are added/deleted
- [ ] Phase 3: Commit with all channels blocked and no collateral shows win popup
- [ ] Phase 3: Commit with leaking channel shows fail popup with channel names
- [ ] OnPacketFilterSolved fires (check Debug.Log in PlayerMetricsTracker)
- [ ] PacketFilterEventHandler.ServerNodeDisabled = true after solve
- [ ] Close button works and restores timeScale + cursor

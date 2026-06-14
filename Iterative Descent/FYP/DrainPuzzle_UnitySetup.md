# DrainPuzzle -- Unity Scene Setup

## Files created

| File | Path |
|---|---|
| DrainPuzzleUI.cs | Assets/Scripts/Puzzles/Drain/ |
| DrainFeedbackPopup.cs | Assets/Scripts/Puzzles/Drain/ |
| DrainTerminal.fbx | Assets/Models/School House/props/ |
| DrainTerminal_Screen.png | Assets/Sprites/Puzzles/ |
| DrainNode_Hidden.png | Assets/Sprites/Puzzles/ |
| DrainNode_Visible.png | Assets/Sprites/Puzzles/ |
| DrainNode_Active.png | Assets/Sprites/Puzzles/ |
| DrainNode_Correct.png | Assets/Sprites/Puzzles/ |
| DrainNode_Wrong.png | Assets/Sprites/Puzzles/ |
| DrainNode_Inlet.png | Assets/Sprites/Puzzles/ |
| DrainNode_Hint.png | Assets/Sprites/Puzzles/ |

## Prefabs to create

### NodePrefab
- Root: Image (128x128, use DrainNode_Visible.png as default sprite), Raycast Target ON, Button component
- Child: LabelText -- TextMeshProUGUI, centered, font size 14, color #00FF88

### EdgePrefab
- Root: Image (white, 1x3px), Raycast Target OFF, no Button
- Pivot (0, 0.5) -- script sizes and rotates it at runtime

### ChipPrefab
- Root: Image (background color set by script), ~90x28, Raycast Target OFF
- Child: ChipText -- TextMeshProUGUI, centered, font size 12

## Canvas Hierarchy

```
[DrainPuzzlePanel]                   -- starts INACTIVE, full-screen overlay
  [Background]                       -- dark semi-transparent Image
  [Header]
    [TitleText]                      -- TMP "ARBITEX DRAIN DIAGNOSTIC"
    [ProtocolBadge]
      [ProtocolBadgeBg]              -- Image (color set by script)
      [ProtocolBadgeText]            -- TMP "BFS -- QUEUE"
  [FloodBar]
    [FloodBarBg]                     -- Image (dark)
    [FloodFillImage]                 -- Image, Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left
    [FloodLevelText]                 -- TMP "LEVEL: 0 / 4"
  [GraphSection]
    [EdgeContainer]                  -- RectTransform, NO Image, no Layout Group
    [NodeContainer]                  -- RectTransform, NO Image, no Layout Group
  [BottomPanel]
    [DSPanel]                        -- root toggled by script at tier 3/4
      [DSTypeLabel]                  -- TMP "QUEUE (FIFO)"
      [DSChipsParent]                -- RectTransform with Horizontal Layout Group, spacing 6
    [NextNodePanel]
      [NextNodeLabel]                -- TMP static "NEXT:"
      [NextNodeText]                 -- TMP updated by script
    [StatusText]                     -- TMP status line
  [SwitchAlertPanel]                 -- starts INACTIVE
    [SwitchAlertText]                -- TMP alert message
  [Buttons]
    [HintButton]                     -- Button, child Label TMP "Show hint"
    [CloseButton]                    -- Button, child TMP "Close"
  [WinPanel]                         -- starts INACTIVE
    [WinText]                        -- TMP win message
  [FeedbackPopup]                    -- DrainFeedbackPopup, starts INACTIVE
    [Blocker]                        -- Image alpha=0, Raycast Target ON, full stretch
    [PopupCard]
      [IconText]                     -- TMP (OK / X icon)
      [MessageText]                  -- TMP body
      [OkButton]                     -- Button
```

## Canvas component settings

### DrainPuzzlePanel (root)
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left/Right/Top/Bottom = 0 |
| Canvas Group (optional) | Blocks Raycasts ON |
| DrainPuzzleUI | (this script) |
| Active in scene | OFF (starts inactive -- prop activates it) |

### Background
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: #000000 at alpha 200, Raycast Target ON |

### Header
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/stretch, Pivot (0.5, 1), Height 60, Left/Right 0, Top 0 |

### TitleText
| Component | Setting |
|---|---|
| RectTransform | Anchor: left/stretch, Width 400, Left 16 |
| TextMeshProUGUI | Text: "ARBITEX DRAIN DIAGNOSTIC", Size 14, color #00FF88, alignment middle-left |

### ProtocolBadge
| Component | Setting |
|---|---|
| RectTransform | Anchor: right/middle, Pivot (1, 0.5), Width 160, Height 32, Right 16 |

### ProtocolBadgeBg
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: set by script, Raycast Target OFF |

### ProtocolBadgeText
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 4 |
| TextMeshProUGUI | Text: "BFS -- QUEUE", Size 12, color #FFFFFF, alignment middle-center, font style Bold |

### FloodBar
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/stretch, Pivot (0.5, 1), Height 28, Left 16, Right 16, Top 64 |

### FloodBarBg
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left 80, Right 80, Top 4, Bottom 4 |
| Image | Color: #1A0D0D, Raycast Target OFF |

### FloodFillImage
| Component | Setting |
|---|---|
| RectTransform | Same anchor/size as FloodBarBg (sits on top) |
| Image | Color: #CC1A1A, Image Type: Filled, Fill Method: Horizontal, Fill Origin: Left, Fill Amount: 0 |

### FloodLevelText
| Component | Setting |
|---|---|
| RectTransform | Anchor: left/stretch, Width 76, Left 0 |
| TextMeshProUGUI | Text: "LEVEL: 0 / 4", Size 11, color #FF4444, alignment middle-left |

### GraphSection
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Top 96, Bottom 160, Left 16, Right 16 |

### EdgeContainer
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| No Image, no Layout Group | Edges render as children using absolute positioning |

### NodeContainer
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| No Image, no Layout Group | Nodes position themselves via NormPos at runtime |

### BottomPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5, 0), Height 155, Left 16, Right 16, Bottom 0 |

### DSPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/left, Pivot (0, 1), Width 340, Height 72, Left 0, Top 0 |
| Active in scene | ON (script hides it at tier 3/4) |

### DSTypeLabel
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/stretch, Height 18, Left 4, Right 4, Top 0 |
| TextMeshProUGUI | Text: "QUEUE (FIFO)", Size 10, color #00FF8866, alignment middle-left |

### DSChipsParent
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Height 36, Left 4, Right 4, Bottom 4 |
| Horizontal Layout Group | Spacing: 6, Child Alignment: Middle Left, Child Force Expand Width: OFF, Child Force Expand Height: OFF |
| Content Size Fitter | Horizontal Fit: Preferred Size |

### NextNodePanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/right, Pivot (1, 1), Width 180, Height 72, Right 0, Top 0 |

### NextNodeLabel
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/stretch, Height 18, Left 4, Right 4, Top 0 |
| TextMeshProUGUI | Text: "NEXT:", Size 10, color #00FF8866, alignment middle-left |

### NextNodeText
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Height 36, Left 4, Right 4, Bottom 4 |
| TextMeshProUGUI | Text: "--", Size 13, color #FFCC00, alignment middle-left, font style Bold |

### StatusText
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Height 28, Left 0, Right 0, Bottom 0 |
| TextMeshProUGUI | Text: "", Size 11, color #00FF8899, alignment middle-left, overflow: Ellipsis |

### SwitchAlertPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5, 0), Height 44, Left 16, Right 16, Bottom 160 |
| Image | Color: #1A0D26, Raycast Target OFF |
| Active in scene | OFF (script activates it briefly on protocol switch) |

### SwitchAlertText
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left 10, Right 10, Top 4, Bottom 4 |
| TextMeshProUGUI | Text: "", Size 11, color #AA44FF, alignment middle-left, word wrap ON |

### Buttons
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5, 0), Height 36, Left 16, Right 16, Bottom 124 |
| Horizontal Layout Group | Spacing 12, Child Alignment: Middle Center |

### HintButton / CloseButton
| Component | Setting |
|---|---|
| RectTransform | Width 120, Height 32 (Layout Element) |
| Image | Color: #0D1A0D, Raycast Target ON |
| Button | Transition: Color Tint, Normal: #0D1A0D, Highlighted: #1A3A1A |
| Child TMP | Size 12, color #00FF88, alignment middle-center |

### WinPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: center/center, Width 500, Height 180 |
| Image | Color: #0D2A0D |
| Outline / Border Image (optional) | Color: #00FF88 |
| Active in scene | OFF |

### WinText
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left 20, Right 20, Top 16, Bottom 16 |
| TextMeshProUGUI | Text: "", Size 13, color #00FF88, alignment middle-center, word wrap ON |

### FeedbackPopup
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| DrainFeedbackPopup | Wire: IconText, MessageText, OkButton |
| Active in scene | OFF |

### FeedbackPopup > Blocker
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: #000000 alpha 0, Raycast Target ON |

### FeedbackPopup > PopupCard
| Component | Setting |
|---|---|
| RectTransform | Anchor: center/center, Width 420, Height 185 |
| Image | Color: #0D1A0D |

### FeedbackPopup > PopupCard > IconText
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/center, Pivot (0.5, 1), Width 60, Height 60, Top -10 |
| TextMeshProUGUI | Text: "X", Size 36, alignment middle-center, color set by script |

### FeedbackPopup > PopupCard > MessageText
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left 20, Right 20, Top 60, Bottom 48 |
| TextMeshProUGUI | Size 13, alignment middle-center, word wrap ON, color #CCCCCC |

### FeedbackPopup > PopupCard > OkButton
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/center, Pivot (0.5, 0), Width 100, Height 36, Bottom 8 |
| Button | Normal color #0D1A0D, Highlighted #1A3A1A |
| Child TMP | Text: "OK", Size 13, color #00FF88 |

## Inspector wiring (DrainPuzzleUI)

| Field | Assign |
|---|---|
| edgeContainer | EdgeContainer RectTransform |
| nodeContainer | NodeContainer RectTransform |
| nodePrefab | NodePrefab |
| edgePrefab | EdgePrefab |
| dsChipsParent | DSChipsParent RectTransform |
| chipPrefab | ChipPrefab |
| dsTypeLabel | DSTypeLabel TMP |
| dsPanelRoot | DSPanel GameObject |
| nextNodeText | NextNodeText TMP |
| protocolBadgeText | ProtocolBadgeText TMP |
| protocolBadgeBg | ProtocolBadgeBg Image |
| floodFillImage | FloodFillImage Image |
| floodLevelText | FloodLevelText TMP |
| statusText | StatusText TMP |
| switchAlertPanel | SwitchAlertPanel GameObject |
| switchAlertText | SwitchAlertText TMP |
| winPanel | WinPanel GameObject |
| winText | WinText TMP |
| hintButton | HintButton Button |
| closeButton | CloseButton Button |
| feedbackPopup | FeedbackPopup DrainFeedbackPopup |

## DrainPuzzleProp (TODO next)
- Attach to DrainTerminal prop in Washroom exterior wall
- On Interact(): set Time.timeScale=0, cursor visible, open DrainPuzzlePanel, call InitPuzzle(onClose)
- onClose: set Time.timeScale=1, cursor hidden, close panel

## DrainEventHandler (TODO next)
- Subscribes to DrainPuzzleUI.OnDrainSolved
- Calls FemaleToiletDoor.Unlock()

## Testing checklist
- [ ] Tier 0: all nodes visible, no switch, DS shown, correct BFS order works
- [ ] Tier 1: only INLET visible, clicking hidden node shows status message
- [ ] Tier 1: scanning INLET reveals its children
- [ ] Tier 2: protocol switch fires after 2-3 correct scans, badge updates
- [ ] Tier 3: DS panel hidden, hint button shows it temporarily
- [ ] 4 wrong clicks resets puzzle (flood level 4)
- [ ] Win panel shows comparison message on solve
- [ ] OnDrainSolved fires (check Debug.Log in PlayerMetricsTracker)
- [ ] Close button works and restores timeScale

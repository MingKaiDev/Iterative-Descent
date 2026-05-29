# Stack Puzzle -- Unity Setup Guide
## Circuit Breaker / PDU Panel -- Two-Phase Design

Replaces the previous Memory Corruption Terminal setup.
All surrounding files (StackPuzzleProp, StackEventHandler, ShutterController,
PlayerMetricsTracker) are unchanged. Only the Canvas panel needs rebuilding.

---

## 1. Physical Prop

### Import PDU_Panel.fbx
- Location: `Assets/Art/Props/PDU_Panel.fbx`
- In the Import Settings Inspector:
  - Scale Factor: 1
  - Convert Units: OFF
  - Import Normals: Import
  - Generate Lightmap UVs: ON
- The FBX contains 27 named meshes. Unity imports them all under a
  single prefab root. Place the root in your corridor scene.

### Sub-meshes and materials
The FBX ships with embedded material slots. Assign your URP lit materials:

| Mesh group | Slot name | Suggested look |
|---|---|---|
| Panel_Body, Panel_Interior | M_PDU_Body / M_PDU_Int | Dark charcoal metal |
| Panel_Door | M_PDU_Door | Slightly lighter charcoal |
| BusBar_Main/Aux/Restore + Slots | M_PDU_Bus | Brushed aluminium |
| Conduit_L, Conduit_R | M_PDU_Conduit | Matte black rubber |
| Label_Plate | M_PDU_Label | Amber / yellow |
| Label_WarnStripe | M_PDU_WarnStripe | Dark amber |
| Screw_00-03 | M_PDU_Screw | Polished steel |

### Scene wiring (physical prop)
On the PDU_Panel root GameObject add:
- BoxCollider (size ~0.60 x 0.80 x 0.14, centre at 0, 0, 0.4)
- InteractableBase
- InteractableRegistrar
- StackPuzzleProp -- set stackOverlay to the Stack Panel in Canvas

Update InteractLabel in StackPuzzleProp.cs from "Access Terminal" to
"Access PDU Panel".

---

## 2. Canvas Hierarchy

Create the following under your existing scene Canvas.
All sections start inactive except Stack Panel which StackPuzzleProp toggles.

```
[Canvas]
  Stack Panel                     <-- StackPuzzleUI, StackFeedbackPopup
    Header Panel                  (Image, dark bg, HLG)
      ARBITEX Label               (TMP, monospace, 11pt)
      Status Badge Root           (Image, colour-coded bg)
        Status Badge Text         (TMP, 11pt bold)
      Phase Badge Root            (Image, starts inactive)
        Phase Badge Text          (TMP, 11pt bold)
    ARBITEX Message Text          (TMP, monospace, 11pt italic)

    Phase 1 Section               (starts active)
      Content Row                 (HLG spacing=10)
        Token Panel               (VLG, fixed width 150)
          Token Label             (TMP "AVAILABLE BREAKERS", 10pt)
          Token Container         (VLG)    <-- tokenButtonParent
        Circuit Bus Panel         (HLG outer, flexibleWidth 1)
          Left Accent Strip       (Image, amber, LayoutElement fixed 3px)
          Circuit Bus Content     (VLG inner, flexibleWidth 1)
            Bus Label             (TMP "CIRCUIT BUS", 10pt)
            Stack Container       (VLG)    <-- stackCellParent
            Controls Row          (HLG)
              Pop Button          (labelled "-- POP TOS")
              Submit Button       (labelled "AUTHORIZE")
        Target Config Panel       (HLG outer, fixed width ~200)
          Left Accent Strip       (Image, green, LayoutElement fixed 3px)
          Target Config Content   (VLG inner, flexibleWidth 1)
            Bus Label             (TMP "TARGET CONFIG", 10pt)
            Target Container      (VLG)    <-- targetCellParent
      Moves Text                  (TMP)    <-- movesText
      Close Button

    Transition Section            (starts inactive)
      Transition Body Text        (TMP, centred)   <-- transitionBodyText
      Proceed Button              (labelled "-- PROCEED TO BUS REROUTE")

    Phase 2 Section               (starts inactive)
      Phase 2 Header Row          (HLG)
        Phase 2 Moves Text        (TMP)    <-- phase2MovesText
        Phase 2 Hint Text         (TMP)    <-- phase2HintText
      Bus Row                     (HLG spacing=10)
        Main Bus Panel            (Image)  <-- mainBusPanelImage
          Main Bus Btn            (Button, transparent) <-- mainBusButton
          Bus Label               (TMP "MAIN BUS (source)")
          Main Container          (VLG)    <-- mainBusContainer
        Aux Bus Panel             (Image)  <-- auxBusPanelImage
          Aux Bus Btn             (Button, transparent) <-- auxBusButton
          Bus Label               (TMP "AUX BUS (buffer)")
          Aux Container           (VLG)    <-- auxBusContainer
        Restore Bus Panel         (Image)  <-- restoreBusPanelImage
          Restore Bus Btn         (Button, transparent) <-- restoreBusButton
          Bus Label               (TMP "RESTORE BUS (target)")
          Restore Container       (VLG)    <-- restoreBusContainer

    Feedback Popup                (StackFeedbackPopup, starts inactive)
```

---

## 3. StackPuzzleUI Inspector Fields

### Header UI
| Field | Assign to |
|---|---|
| Arbitex Message Text | ARBITEX Message Text TMP |
| Status Badge Text | Status Badge Text TMP |
| Status Badge Bg | Status Badge Root Image component |
| Phase Badge Text | Phase Badge Text TMP |
| Phase Badge Bg | Phase Badge Root Image component |
| Phase Badge Root | Phase Badge Root GameObject |

### Phase 1
| Field | Assign to |
|---|---|
| Phase 1 Section | Phase 1 Section GameObject |
| Stack Cell Parent | Stack Container RectTransform |
| Target Cell Parent | Target Container RectTransform |
| Token Button Parent | Token Container RectTransform |
| Pop Button | Pop Button |
| Submit Button | Submit Button |
| Close Button | Close Button |
| Moves Text | Moves Text TMP |

### Transition Screen
| Field | Assign to |
|---|---|
| Transition Section | Transition Section GameObject |
| Transition Body Text | Transition Body Text TMP |
| Proceed Button | Proceed Button |

### Phase 2
| Field | Assign to |
|---|---|
| Phase 2 Section | Phase 2 Section GameObject |
| Main Bus Container | Main Container RectTransform |
| Aux Bus Container | Aux Container RectTransform |
| Restore Bus Container | Restore Container RectTransform |
| Main Bus Panel Image | Main Bus Panel Image component |
| Aux Bus Panel Image | Aux Bus Panel Image component |
| Restore Bus Panel Image | Restore Bus Panel Image component |
| Main Bus Button | Main Bus Btn Button component |
| Aux Bus Button | Aux Bus Btn Button component |
| Restore Bus Button | Restore Bus Btn Button component |
| Phase 2 Moves Text | Phase 2 Moves Text TMP |
| Phase 2 Hint Text | Phase 2 Hint Text TMP |

### Shared
| Field | Assign to |
|---|---|
| Feedback Popup | Feedback Popup StackFeedbackPopup component |
| Breaker Cell Prefab | BreakerCellPrefab |
| Token Button Prefab | TokenButtonPrefab |

---

## 4. Prefab Specs

### BreakerCellPrefab
Path: `Assets/Prefabs/Stack/BreakerCellPrefab.prefab`

```
BreakerCellPrefab                 (Image -- background colour set at runtime)
  RectTransform: width 220, height 42
  Image.color: any default (overridden by code)

  ValueText                       (TextMeshProUGUI)
    Anchor: stretch-fill, left offset 10, right offset 52, top 0, bottom 0
    Font: monospace TMP font (e.g. Liberation Mono SDF or Share Tech Mono SDF)
    Font size: 16, Bold
    Alignment: Middle Left
    Color: white
    Overflow: Overflow

  TagText                         (TextMeshProUGUI)
    Anchor: right-centre, width 48, height 42, pivot right
    Font: same monospace font
    Font size: 11, Normal
    Alignment: Middle Right
    Color: (0.65, 0.65, 0.65, 1)
    Overflow: Overflow
```

Child names "ValueText" and "TagText" are case-sensitive.
StackPuzzleUI uses transform.Find() -- wrong names = blank cells.

### TokenButtonPrefab
Path: `Assets/Prefabs/Stack/TokenButtonPrefab.prefab`

```
TokenButtonPrefab                 (Button)
  RectTransform: width 130, height 38
  Image.color: (0.18, 0.20, 0.22, 1) -- dark slate
  Navigation: None

  Label                           (TextMeshProUGUI)
    Anchor: stretch-fill, padding 4
    Font: monospace TMP font
    Font size: 14, Bold
    Alignment: Middle Centre
    Color: white
```

Child name "Label" is case-sensitive. Same reason as above.

---

## 5. Layout Settings

### Stack Panel (root)
- Anchor: stretch-stretch (fills canvas)
- Background Image color: (0.06, 0.07, 0.08, 0.92) -- dark overlay

### Header Panel
- Height: 68, anchor top-stretch
- HorizontalLayoutGroup: spacing 8, padding left/right 12, top/bottom 8
- ControlChildSize Height: ON

### Phase 1 Content Row (HLG)
- Child Alignment: Upper Left
- Spacing: 10, Padding: 10 all sides
- ControlChildSize Width: OFF (children set their own widths)
- ControlChildSize Height: OFF

### Token Panel
- LayoutElement: preferredWidth 150, flexibleWidth 0
- VLG: spacing 6, padding 8

### Circuit Bus Panel (HLG outer)
- LayoutElement: preferredWidth 220, flexibleWidth 1, minHeight 260
- HorizontalLayoutGroup: spacing 0, padding 0
- ControlChildSize Width: ON, Height: ON
- ChildForceExpand Width: OFF, Height: ON

### Left Accent Strip (first child of Circuit Bus Panel or Target Config Panel)
- LayoutElement: preferredWidth 3, flexibleWidth 0, flexibleHeight 1
- Image.color for Circuit Bus: (0.85, 0.55, 0.05, 1) -- amber
- Image.color for Target Config: (0.15, 0.65, 0.20, 1) -- green
- raycastTarget: OFF
- Do NOT set a manual anchor or RectTransform width -- let the HLG and LayoutElement control it.

### Circuit Bus Content (inner VLG, second child of Circuit Bus Panel)
- LayoutElement: flexibleWidth 1
- VLG: spacing 5, padding left 6, right 4, top 8, bottom 8
- ControlChildSize Width: ON, Height: OFF
- ChildForceExpand Width: ON, Height: OFF

### Target Config Panel (HLG outer)
- LayoutElement: preferredWidth 200, flexibleWidth 0, minHeight 260
- HorizontalLayoutGroup: spacing 0, padding 0
- ControlChildSize Width: ON, Height: ON
- ChildForceExpand Width: OFF, Height: ON

### Target Config Content (inner VLG, second child of Target Config Panel)
- LayoutElement: flexibleWidth 1
- VLG: spacing 5, padding left 6, right 4, top 8, bottom 8
- ControlChildSize Width: ON, Height: OFF
- ChildForceExpand Width: ON, Height: OFF

### Stack Container / Target Container VLG
- Child Alignment: Lower Center (stack grows upward visually)
- ReverseArrangement: OFF
- ControlChildSize Width: ON, Height: OFF
- Spacing: 4

### Bus Row (Phase 2, HLG)
- Spacing: 10, Padding: 10
- ControlChildSize Width: ON

### Bus Panel (each of the three)
- LayoutElement: flexibleWidth 1 (equal thirds)
- Min height: 220

### Bus Container VLG (inside each bus panel)
- Child Alignment: Lower Center
- ReverseArrangement: OFF
- ControlChildSize Width: ON, Height: OFF
- Spacing: 4, Padding: 8

### Transition Section
- Anchor: centre, sizeDelta ~560 x 280
- Background Image: (0.10, 0.11, 0.13, 0.95)
- VLG: centred, spacing 16, padding 24

---

## 6. Bus Button Click Detection

Each bus panel needs an invisible Button so the player can click anywhere
on the bus column.

For each bus panel (Main / Aux / Restore):
1. Create child GameObject named "Main Bus Btn" (or Aux / Restore)
2. RectTransform: anchor stretch-stretch, all offsets 0
3. Image component: color (0, 0, 0, 0) -- fully transparent
   CRITICAL: raycastTarget must be ON. In some Unity versions it
   defaults to OFF when alpha is 0. Tick it manually in the Inspector.
4. Button component: Transition = None, Navigation = None
5. Drag this Button into the matching bus button field on StackPuzzleUI

---

## 7. Font Setup

Recommended monospace fonts (both free):
- Liberation Mono -- import OTF, generate TMP font asset at 90pt, atlas 4096x4096
- Share Tech Mono -- same process

Generate via Window > TextMeshPro > Font Asset Creator.
Set Sampling Point Size: 90, Padding: 9, Atlas Resolution: 4096 x 4096.

If you do not have a monospace font available, LiberationSans SDF (Unity built-in)
is acceptable. The industrial feel is reduced but everything will function.

---

## 8. StackEventHandler Wiring (unchanged)

No changes needed. StackEventHandler subscribes to OnStackSolved and calls
shutter.Unlock(). Verify it is still on the persistent GameManager GameObject
with ShutterController assigned in the Inspector.

Important: OnStackSolved fires ONLY after Phase 2 completes for tier 2+.
It fires after Phase 1 for tier 0/1. It does NOT fire at Phase 1 completion
during a two-phase run.

---

## 9. Testing Checklist

### Tier 0 (3 elements, push/pop only)
- [ ] Junk pre-loaded at bottom of stack on open
- [ ] 3 target values + 2 distractor buttons in token panel
- [ ] Phase badge hidden
- [ ] Moves Text hidden
- [ ] Wrong AUTHORIZE increments attempt counter shown in popup
- [ ] Correct AUTHORIZE fires OnStackSolved, shows success popup
- [ ] Dismiss popup closes puzzle and resumes game

### Tier 1 (4 elements, push/pop + move limit)
- [ ] Moves Text visible and counts down on each push/pop
- [ ] Move exhaustion auto-resets Phase 1, increments wrong count
- [ ] Same solve flow as tier 0

### Tier 2 (5 elements, two-phase, decimal)
- [ ] Phase badge shows "PHASE 1" at open
- [ ] Correct Phase 1 shows Transition Section (not solve event yet)
- [ ] Transition body text shows target order reminder
- [ ] Proceed button loads Phase 2 (3 bus columns appear)
- [ ] Phase badge changes to "PHASE 2"
- [ ] Clicking non-empty bus highlights its panel (colour tint)
- [ ] Clicking same bus again deselects
- [ ] Clicking destination bus moves TOS across
- [ ] Move counter decrements per move
- [ ] Move exhaustion resets Phase 2 only, increments wrong count
- [ ] RESTORE matching target order fires OnStackSolved
- [ ] Shutter unlocks after dismissing success popup

### Tier 3/4 (6 elements, two-phase, hex)
- [ ] All values shown as 0x## format in both phases
- [ ] Phase 2 starts with breakers distributed across all 3 buses
  (RESTORE is not empty at start)
- [ ] Move budget feels tighter (ScrambleDepth = 24, budget = 48)

### General
- [ ] Time.timeScale = 0 while puzzle open (enemies frozen)
- [ ] Cursor visible and unlocked during puzzle
- [ ] Game resumes on close/solve
- [ ] No duplicate OnStackSolved fires (check StackEventHandler log)
- [ ] DDADisplayHUD (H key) shows TotalStackSolved increments by 1

---

## 10. Known Gotchas

- BreakerCellPrefab and TokenButtonPrefab child names are case-sensitive.
  "ValueText", "TagText", "Label" must match exactly.

- Bus Button raycastTarget: transparent Images default to raycastTarget OFF
  in some Unity versions. Tick it ON manually or clicks will not register.

- Bus VLG Child Alignment Lower Center: breakers stack from bottom.
  If they appear at the top instead, check ReverseArrangement is OFF.

- Left Accent Strip cannot be a direct VLG child when ControlChildSize Width is ON.
  The VLG will override the strip's width and stretch it to fill the panel.
  Fix: Circuit Bus Panel and Target Config Panel are HLG outer containers.
  The accent strip is the first HLG child with a LayoutElement (preferredWidth 3,
  flexibleWidth 0). The inner VLG (flexibleWidth 1) is the second child and holds
  all the actual stack/target content. Never set a manual RectTransform width on
  the accent strip -- LayoutElement controls it through the HLG.

- StackFeedbackPopup Awake must NOT call SetActive(false). InitPuzzle()
  does this. The gotcha from the base class pattern still applies here.

- Phase 2 scramble depth is elements * 4 (20 for tier 2, 24 for tier 3/4).
  Move budget is depth * 2. If Phase 2 feels too punishing, lower the
  multiplier in GeneratePhase2() -- the budget scales with it automatically.

- OnStackSolved fires once total per puzzle open. If you see it firing
  twice, check for a duplicate StackEventHandler component on GameManager.

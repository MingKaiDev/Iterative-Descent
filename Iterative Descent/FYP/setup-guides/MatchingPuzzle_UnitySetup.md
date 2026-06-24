# Matching Puzzle -- Unity Setup Guide (Story 15)

Computer Lab network port matching terminal. Reward-only puzzle (no door unlock).

---

## Files Created

| File | Purpose |
|---|---|
| Assets/Scripts/Puzzles/Matching/MatchingPuzzleUI.cs | Main controller |
| Assets/Scripts/Puzzles/Matching/MatchingTermCard.cs | Draggable protocol card |
| Assets/Scripts/Puzzles/Matching/MatchingPortSlot.cs | Drop zone (port number) |
| Assets/Scripts/Puzzles/Matching/MatchingFeedbackPopup.cs | Thin feedback subclass |
| Assets/Scripts/Props/MatchingPuzzleProp.cs | IInteractable terminal prop |
| Assets/Scripts/Events/MatchingEventHandler.cs | Fires reward drop on solve |
| Assets/Sprites/UI/Whiteboard_PortReference.png | Whiteboard texture (512x512) |

---

## Canvas Hierarchy

The panel sits as a child of the existing scene Canvas (same Canvas used by other puzzles).

```
Canvas
└── MatchingPanel              [inactive on start] [MatchingPuzzleUI]
    ├── Background             Image (dark, e.g. #0A1017), Anchor = stretch-fill
    │   ├── TitleText          TMP  "NETWORK PORT DIAGNOSTIC"  size 28 Bold
    │   ├── SubText            TMP  "Match each protocol to its default port"  size 16
    │   ├── PuzzleArea         HorizontalLayoutGroup  spacing=60  childForceExpand=false
    │   │   ├── TermPool       VerticalLayoutGroup  spacing=8  [left column, width=200]
    │   │   │   ├── TermCard_0 ... TermCard_7   (see Card setup below)
    │   │   └── PortColumn     VerticalLayoutGroup  spacing=8  [right column, width=220]
    │   │       ├── PortSlot_0 ... PortSlot_7   (see Slot setup below)
    │   ├── ButtonRow          HorizontalLayoutGroup
    │   │   ├── SubmitButton   Button + TMP "SUBMIT"
    │   │   └── CloseButton    Button + TMP "CLOSE"
    └── FeedbackPopup          MatchingFeedbackPopup  (starts ACTIVE in Inspector -- see gotcha)
        ├── Blocker            Image alpha=0, Raycast Target ON, stretch-fill
        └── PopupCard          Image dark panel, anchored centre, width=420 height=185
            ├── IconText       TMP size 42 Bold
            ├── MessageText    TMP size 13 word-wrap
            └── OkButton       Button "OK" width=100 height=34
```

### MatchingPuzzleUI Inspector wiring (on MatchingPanel)

- termCards[0..7]: drag TermCard_0 through TermCard_7 in ORDER
- portSlots[0..7]: drag PortSlot_0 through PortSlot_7 in ORDER
- submitButton: SubmitButton
- closeButton: CloseButton
- feedbackPopup: FeedbackPopup

---

## TermCard Setup (repeat for TermCard_0 through TermCard_7)

Each card is a GameObject under TermPool.

Components:
- RectTransform: width=190, height=46
- Image: any dark color (MatchingTermCard overrides at runtime), Raycast Target ON
- CanvasGroup: (added by [RequireComponent])
- MatchingTermCard: drag "Label" child into the `label` field

Children:
- Label: TextMeshProUGUI, size=18, center-middle anchor, white text

The term name is set at runtime by InitPuzzle() -- leave the TMP text blank.

---

## PortSlot Setup (repeat for PortSlot_0 through PortSlot_7)

Each slot is a GameObject under PortColumn.

Components:
- RectTransform: width=210, height=46
- Image: dark color, Raycast Target ON
- MatchingPortSlot: wire the two TMP children

Children:
- PortLabel: TMP size=16, anchored left (e.g. left 10px offset), shows ":80" set at runtime
- TermLabel: TMP size=16, anchored right (e.g. right 10px offset), shows assigned term or "--"

Leave both TMP texts blank -- set at runtime by InitPuzzle() and OnDrop().

---

## MatchingPuzzleProp Setup (terminal prop in Computer Lab)

Attach to the terminal prop GameObject (or a child BoxCollider trigger GO):

- BoxCollider: set as Trigger
- InteractableBase
- InteractableRegistrar
- MatchingPuzzleProp: drag MatchingPanel into `matchingOverlay`

Interact label shown to player: "Access Network Terminal"

---

## MatchingEventHandler Setup

Attach to the persistent GameManager GameObject (same one with PlayerMetricsTracker, ItemSpawner).

- MatchingEventHandler: assign `rewardSpawnPoint` to an empty Transform placed near the terminal
  (or on the floor in front of it -- wherever pickups should drop)

ItemSpawner must be on GameManager. On solve it calls ItemSpawner.GuaranteedSpawn() which
drops one AmmoBox and one HealthKit at the spawn point.

---

## Whiteboard Prop Setup

The PNG `Assets/Sprites/UI/Whiteboard_PortReference.png` is an in-world hint showing the
correct port answers. Apply it as a texture on the whiteboard mesh in Classroom 2 / Computer Lab.

Steps in Unity:
1. Import Whiteboard_PortReference.png -- set Texture Type to "Default" (not Sprite),
   sRGB ON, Compression = High Quality.
2. Create a Material: Unlit/Texture (or URP Lit with Albedo only).
3. Assign the texture to the material.
4. Apply the material to the whiteboard quad/mesh in the scene.

The PNG is 512x512. If the whiteboard geometry is wide, tile it 1x1 (no tiling) and
stretch the UV to cover the board face.

Note: the whiteboard is intentionally visible in-game as a diegetic hint. Players who
read it get a free pass on the puzzle. This is the design intent -- it is a reward
terminal, not a gate.

---

## FeedbackPopup Gotcha (applies to all puzzles)

The FeedbackPopup GameObject MUST have activeSelf = TRUE in the Inspector.
Do NOT uncheck it manually. Its initial hidden state is set by InitPuzzle().
If it starts inactive, Awake() is deferred and the first popup call silently does nothing.

---

## BKT / ConceptProfile

A new concept key `networking_ports` is defined in BayesianKnowledgeTracker.cs.

Create a ConceptProfile ScriptableObject:
- Right-click Assets/Concept Profile > Create > ARBITEX > Concept Profile
- Set conceptKey = "networking_ports"
- BKT parameters: leave at defaults (pL0=0.3, pT=0.09, pG=0.2, pS=0.1)
- Assign it to the conceptProfiles array on PlayerMetricsTracker in the Inspector

5 MCQs with conceptTag "networking_ports" were added to quiz_bank.json. These will appear
in any quiz that reads from that bank and are BKT-tracked automatically.

---

## Testing Checklist

- [ ] Open MatchingPanel via Editor button or PlayMode interact -- all 8 cards and 8 slots appear
- [ ] Cards are shuffled (terms and ports in random order each InitPuzzle call)
- [ ] Drag HTTP card to :80 slot -- slot TermLabel shows "HTTP", card turns green in pool
- [ ] Drag HTTP card to :443 slot -- TermLabel on :80 clears, :443 shows "HTTP"
- [ ] Submit with empty slot -- feedback popup "Assign all protocols..."
- [ ] Submit with 1 wrong pair -- wrong slot turns red, correct slots turn green, popup shows count
- [ ] After 3 wrong submissions -- popup hint says "check the whiteboard"
- [ ] Submit with all correct -- success popup; on OK: reward drops, puzzle closes
- [ ] ItemSpawner drops AmmoBox + HealthKit at rewardSpawnPoint
- [ ] PlayerMetricsTracker debug log shows "[Metrics] Matching puzzle started/solved"
- [ ] quiz_bank.json validated (JSON parse OK -- run `python3 -c "import json,open; json.load(open('quiz_bank.json'))"` in StreamingAssets)
- [ ] Whiteboard texture visible on whiteboard mesh in Computer Lab scene

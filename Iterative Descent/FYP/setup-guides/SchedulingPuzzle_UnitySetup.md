# Scheduling Puzzle — Unity Setup Instructions
### Last updated: Round Robin rewrite (replaces SJF)

Mirrors the Linked List puzzle panel exactly. All RectTransform values match the LL panel's conventions (full-stretch anchor, scale 0.8, same Canvas).

> **If you already built the SJF version in Unity**, the only Inspector changes required are in §3 (CardPool → Grid Layout Group) and §4 (InstructionText height). Everything else — prefabs, props, event handlers — is unchanged.

---

## 1. Scheduling Puzzle Panel (Canvas child)

### Where to create it
In the **Hierarchy**, find the existing `Canvas` GameObject (the same one that contains `Linked List Puzzle Panel`). Right-click it → **UI → Panel**. Rename to `Scheduling Puzzle Panel`.

### RectTransform
| Field | Value |
|---|---|
| Anchor Min | (0, 0) |
| Anchor Max | (1, 1) |
| Anchored Position | (0, 0) |
| Size Delta | (0, 0) |
| Scale | (0.8, 0.8, 0.8) |
| Pivot | (0.5, 0.5) |

The panel covers the full screen at 80 % scale, identical to `Linked List Puzzle Panel`.

### Set inactive
With the panel selected, **uncheck the checkbox** at the top of the Inspector (next to the name). It must start inactive — `SchedulingPuzzleProp.Awake()` also sets it inactive as a safeguard.

### Background image
The default Panel comes with an Image component. Leave it. Set the color to something dark and semi-transparent (e.g. `R:0.1 G:0.1 B:0.15 A:0.92`).

---

## 2. Child hierarchy inside Scheduling Puzzle Panel

Create all children as UI GameObjects (Right-click panel → UI → ...). Use the exact names — the scripts reference them by component, not name, but these names make wiring obvious.

```
Scheduling Puzzle Panel
├── TitleText               (TextMeshPro - Text (UI))
├── InstructionText         (TextMeshPro - Text (UI))
├── CardPool                (Empty — Vertical Layout Group)
├── SlotRow                 (Empty — Horizontal Layout Group)
├── GanttContainer          (Empty — raw RectTransform)
├── FeedbackText            (TextMeshPro - Text (UI))
├── SubmitButton            (Button — TextMeshPro)
└── CloseButton             (Button — TextMeshPro)
```

### TitleText
- **Component**: TextMeshProUGUI
- **Text**: `CPU Scheduling Puzzle`
- **RectTransform**: Width=760, Height=50, Anchored Pos Y=+170
- **Anchor**: top-centre (0.5, 1 → 0.5, 1), Pivot (0.5, 1)
- **Font Size**: 24, Bold, Centre-aligned

### InstructionText  ← this is the `instructionText` field
- **Component**: TextMeshProUGUI
- **Text**: leave blank (filled by `InitPuzzle()`)
- **RectTransform**: Width=760, **Height=100** (increased from 80 — RR instruction is 3 lines: title, process table, prompt)
- **Anchor**: top-centre (0.5, 1 → 0.5, 1), Pivot (0.5, 1)
- **Font Size**: 13, Centre-aligned, word-wrap on

### CardPool  ← this is the `cardPool` field
> **Round Robin change:** Use a **Grid Layout Group** (not Vertical Layout Group).
> The pool can hold up to 10 tokens; a 4-column grid keeps them compact and on-screen.

- **RectTransform**: Width=320, Height=200, Anchored Pos X=−230, Y=+20
- **Anchor**: middle-left relative to panel centre; or use anchor (0, 0.5→0, 0.5)
- **Remove** any existing `Vertical Layout Group` / `Content Size Fitter`
- **Add Component**: `Grid Layout Group`
  - Cell Size: **(70, 55)** — tokens auto-resize to slot width when dragged; pool just needs to fit them compactly
  - Spacing: **(6, 6)**
  - Start Corner: Upper Left
  - Start Axis: Horizontal
  - Child Alignment: Upper Center
  - Constraint: **Fixed Column Count = 4**
- **Add Component**: `Content Size Fitter`
  - Vertical Fit: Preferred Size

### SlotRow  ← this is the `slotRow` field
- **RectTransform**: Width=760, Height=120, Anchored Pos Y=−60
- **Anchor**: middle-centre (0.5, 0.5 → 0.5, 0.5)
- **Add Component**: `Horizontal Layout Group`
  - Child Alignment: Middle Centre
  - Control Child Size: Width ✗, Height ✗
  - Child Force Expand: Width ✗, Height ✗
  - Spacing: 12

### GanttContainer
> **Round Robin:** Delete this GameObject from the hierarchy — it is not used in RR mode.
> If you want to keep it for reference, leave it; the code null-checks it and hides it at runtime.

### FeedbackText
> **Round Robin:** Delete this GameObject. Feedback is now handled by the `FeedbackPopup` panel (see §11 below).

### SubmitButton  ← this is the `submitButton` field
- **RectTransform**: Width=120, Height=36, Anchored Pos (−75, −240)
- **Button label text**: `Submit`
- Leave the `onClick` list empty — `SchedulingPuzzleUI.OnEnable()` wires it in code.

### CloseButton  ← this is the `closeButton` field
- **RectTransform**: Width=120, Height=36, Anchored Pos (+75, −240)
- **Button label text**: `Close`
- Leave the `onClick` list empty — wired in code.

---

## 3. Add SchedulingPuzzleUI to the Panel

1. Select `Scheduling Puzzle Panel`.
2. **Add Component → Scripts → SchedulingPuzzleUI**.
3. Wire every Inspector field (drag from Hierarchy):

| Field | GameObject to drag |
|---|---|
| `cardPrefab` | The ProcessCard prefab (see §4) |
| `slotPrefab` | The ExecutionSlot prefab (see §5) |
| `ganttBarPrefab` | The GanttBar prefab (see §6) — kept to avoid missing-reference warnings |
| `cardPool` | `Scheduling Puzzle Panel / CardPool` |
| `slotRow` | `Scheduling Puzzle Panel / SlotRow` |
| `ganttContainer` | Leave empty / null — field is optional, code null-checks it |
| `feedbackPopup` | `Scheduling Puzzle Panel / FeedbackPopup` (see §11) |
| `instructionText` | `Scheduling Puzzle Panel / InstructionText` |
| `submitButton` | `Scheduling Puzzle Panel / SubmitButton` |
| `closeButton` | `Scheduling Puzzle Panel / CloseButton` |
| `dragLayer` | The **Canvas root** RectTransform (drag the `Canvas` GameObject itself) |

> **`dragLayer` must be the Canvas root**, not the panel. Cards are reparented here while dragged so they render above all other UI.

---

## 4. ProcessCard Prefab

Create in `Assets/Prefabs/Scheduling/` (or wherever your other puzzle prefabs live).

### Steps
1. In Hierarchy, Right-click → **UI → Image**. Rename to `ProcessCard`.
2. **RectTransform**: Width=180, Height=70, Pivot (0.5, 0.5).
3. **Image component**: Color = white (tinted at runtime by `card.Init()`). Check **Raycast Target ✓**.
4. **Add Component → Scripts → SchedulingProcessCard**.
5. **Add Component → Canvas Group** (auto-required by `[RequireComponent]`).

### Children
Right-click `ProcessCard` → UI → Text (TextMeshPro) for each:

**Background** (optional visual polish)  
- This is the root Image itself — it doubles as the background.

**NameText** (top-left)
- TextMeshProUGUI, text `P1`, font size 16, bold
- RectTransform: Width=80, Height=28, Anchored Pos (−40, +18)
- Anchor: middle-left relative to card centre

**BurstTimeText** (centre)
- TextMeshProUGUI, text `BT: 10`, font size 14
- RectTransform: Width=130, Height=24, Anchored Pos (0, −4)
- Anchor: middle-centre

**ArrivalTimeText** (bottom-left, small)
- TextMeshProUGUI, text `AT: 0`, font size 11, alpha ~60 %
- RectTransform: Width=130, Height=20, Anchored Pos (0, −22)
- Anchor: middle-centre

### Wire SchedulingProcessCard fields
| Field | Child |
|---|---|
| `processNameText` | `NameText` |
| `burstTimeText` | `BurstTimeText` |
| `arrivalTimeText` | `ArrivalTimeText` |
| `cardBackground` | Root `ProcessCard` Image component |

6. **Drag `ProcessCard` from Hierarchy into the Project window** to create the prefab. Delete the scene instance.

---

## 5. ExecutionSlot Prefab

Create in the same prefab folder.

### Steps
1. Right-click Hierarchy → **UI → Image**. Rename to `ExecutionSlot`.
2. **RectTransform**: Width=130, Height=90, Pivot (0.5, 0.5).
3. **Image component**: Color = dark grey (0.2, 0.2, 0.2, 0.6), **Raycast Target ✓** — the slot itself must receive drops.
4. **Add Component → Scripts → SchedulingSlot**.

### Children

**SlotLabel**
- TextMeshProUGUI, text `1st`, font size 12, top-centre
- RectTransform: Width=120, Height=24, Anchored Pos (0, +32)
- Anchor: top-centre

**EmptyIcon**
- UI Image, Source Image = any dashed-border or "?" sprite (or leave default), colour grey
- RectTransform: Width=60, Height=60, Anchored Pos (0, −6)
- **Raycast Target ✗** — the parent slot handles the drop
- This image is hidden when a card occupies the slot.

**FilledBg** (optional tint overlay)
- UI Image, color white (tinted at runtime), alpha 0.30, starts **disabled**
- RectTransform: stretch to fill parent (anchor 0,0→1,1, size delta 0,0)
- **Raycast Target ✗**
- `SchedulingSlot.SetVisuals()` enables/disables this.

### Wire SchedulingSlot fields
| Field | Child |
|---|---|
| `slotLabel` | `SlotLabel` |
| `emptyIcon` | `EmptyIcon` |
| `filledBg` | `FilledBg` |

5. Drag to Project to create prefab. Delete scene instance.

---

## 6. GanttBar Prefab

The simplest prefab.

### Steps
1. Right-click Hierarchy → **UI → Image**. Rename to `GanttBar`.
2. **RectTransform**:
   - Anchor Min/Max: (0, 0.5) → (0, 0.5)  ← **left-centre anchor**
   - Pivot: (0, 0.5)  ← **left edge** so bars grow rightward
   - Width = 1, Height = 36 (width set at runtime; height is fixed)
3. **Image component**: Color = white (tinted per process at runtime), **Raycast Target ✗**.
4. Starts **disabled** (Image.enabled = false) — `GeneratePuzzle()` handles this.
5. Drag to Project as prefab. Delete scene instance.

---

## 7. Wire SchedulingPuzzleProp to Prop_Whiteboard in the scene

1. In the **Hierarchy**, find `Prop_Whiteboard` (it's a child of the ClassRoom1 FBX root, likely under something like `Map / Classroom / Prop_Whiteboard`).
2. Select it. Confirm it already has (or add):
   - **BoxCollider** — size the trigger to cover the whiteboard face. Set **Is Trigger ✓**.
   - **InteractableBase** — this handles the interaction prompt. Wire its `promptPanel` to whatever prompt UI you use.
   - **InteractableRegistrar** — no fields needed, just the component present.
3. **Add Component → Scripts → SchedulingPuzzleProp**.
4. Wire `schedulingOverlay` → drag `Scheduling Puzzle Panel` from the Hierarchy.

> `SchedulingPuzzleProp` calls `PlayerInteractor.Pause()` and sets `Time.timeScale=0` — exactly the same as `PuzzleProp` / `ComputerScreenProp`. Do **not** call `NotifySchedulingStarted()` from the prop; it is called inside `InitPuzzle()`.

---

## 8. Add SchedulingEventHandler to GameManager

The event handler must live on a **persistent** GameObject (one that is never destroyed between scenes, or at minimum survives the duration of Level 1).

1. In the Hierarchy, find your **GameManager** GameObject (the same one that has `PlayerMetricsTracker` and `DDAController` attached).
2. **Add Component → Scripts → SchedulingEventHandler**.
3. Wire `door` → the **DoorController** you want unlocked when the scheduling puzzle is solved. This is the same door you wire for the Linked List puzzle's `LinkedListEventHandler`, or a different door if the scheduling puzzle guards a separate exit.

---

## 9. EventSystem check

The Canvas already has an **EventSystem** (confirmed from the scene data). No new EventSystem is needed. The drag system (`IBeginDragHandler`, `IEndDragHandler`, `IDropHandler`) works through the existing one automatically.

---

## 10. Quick sanity checklist before Play

| # | Check |
|---|---|
| 1 | `Scheduling Puzzle Panel` is **inactive** in the Hierarchy |
| 2 | `SchedulingPuzzleUI.dragLayer` points to the **Canvas root**, not the panel |
| 3 | `GanttContainer` and `FeedbackText` deleted (or left; code ignores nulls) |
| 4 | `ExecutionSlot` root Image has **Raycast Target ✓** |
| 5 | `ProcessCard` has both `SchedulingProcessCard` AND `CanvasGroup` components |
| 6 | `SchedulingPuzzleProp.schedulingOverlay` is wired |
| 7 | `SchedulingEventHandler.door` is wired |
| 8 | `PlayerMetricsTracker` is on the same persistent GameObject as `DDAController` |
| 9 | No second `EventSystem` in the scene |
| 10 | `CardPool` uses **Grid Layout Group** with **Fixed Column Count = 4** |
| 11 | `FeedbackPopup` is the **last child** of `Scheduling Puzzle Panel` (renders on top) |
| 12 | `FeedbackPopup` is **inactive** in the Hierarchy at start |
| 13 | Play mode: wrong answer → pop-up appears centre-screen with ✗ icon; OK dismisses it |
| 14 | Wrong answer ×3 → pop-up shows the correct RR schedule as a hint |
| 15 | Correct answer → pop-up shows ✓ + Gantt string; OK closes the puzzle panel |

---

## 11. FeedbackPopup — hierarchy and Inspector setup

Create as the **last child** of `Scheduling Puzzle Panel` (so it renders above all other children).

### FeedbackPopup root GameObject
- **Rename**: `FeedbackPopup`
- **RectTransform**: full-stretch anchor (0,0 → 1,1), Size Delta (0,0), Anchored Pos (0,0)
  — this makes it cover the entire panel so the Blocker inside can intercept all clicks.
- **Add Component → Scripts → SchedulingFeedbackPopup**
- Set **inactive** in the Hierarchy (script's Awake() also enforces this as a safeguard).

### Child 1 — Blocker
- Right-click `FeedbackPopup` → **UI → Image**. Rename to `Blocker`.
- **RectTransform**: full-stretch (0,0 → 1,1), Size Delta (0,0)
- **Image component**: Color = `(0, 0, 0, 0)` (fully transparent) — **Raycast Target ✓**
  This prevents the player from dragging tokens or pressing Submit while the pop-up is open.

### Child 2 — PopupCard
- Right-click `FeedbackPopup` → **UI → Image**. Rename to `PopupCard`.
- **RectTransform**: Anchor middle-centre, Width=**420**, Height=**185**, Anchored Pos (0, 0)
- **Image component**: Color = `(0.10, 0.10, 0.16, 0.97)` — dark panel, high alpha
- **Raycast Target ✓** (blocks clicks on the card itself from falling through to Blocker)

#### PopupCard children

**IconText**
- Right-click `PopupCard` → **UI → Text (TextMeshPro)**. Rename to `IconText`.
- **Text**: leave blank (set at runtime to ✗ or ✓)
- **RectTransform**: Width=60, Height=60, Anchored Pos (0, +52)
- **Anchor**: top-centre (0.5,1 → 0.5,1)
- **Font Size**: 42, Bold, Centre-aligned
- Colour is set at runtime; use white as default.

**MessageText**
- Right-click `PopupCard` → **UI → Text (TextMeshPro)**. Rename to `MessageText`.
- **Text**: leave blank
- **RectTransform**: Width=380, Height=80, Anchored Pos (0, −10)
- **Anchor**: middle-centre (0.5,0.5 → 0.5,0.5)
- **Font Size**: 13, Centre-aligned, word-wrap **on**
- Colour: white

**OkButton**
- Right-click `PopupCard` → **UI → Button (TextMeshPro)**. Rename to `OkButton`.
- **RectTransform**: Width=100, Height=34, Anchored Pos (0, −70)
- **Anchor**: bottom-centre (0.5,0 → 0.5,0)
- **Button label text**: `OK`
- Leave `onClick` list **empty** — `SchedulingFeedbackPopup.OnEnable()` wires it in code.

### Wire SchedulingFeedbackPopup fields
Back on the **FeedbackPopup** root, with `SchedulingFeedbackPopup` selected in the Inspector:

| Field | GameObject to drag |
|---|---|
| `iconText` | `FeedbackPopup / PopupCard / IconText` |
| `messageText` | `FeedbackPopup / PopupCard / MessageText` |
| `okButton` | `FeedbackPopup / PopupCard / OkButton` |

### Wire feedbackPopup in SchedulingPuzzleUI
Back on **Scheduling Puzzle Panel**, in the `SchedulingPuzzleUI` Inspector:

| Field | Value |
|---|---|
| `feedbackPopup` | Drag `FeedbackPopup` GameObject here |

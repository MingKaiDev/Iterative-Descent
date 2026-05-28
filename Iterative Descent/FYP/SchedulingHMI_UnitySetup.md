# Scheduling Puzzle -- HMI Terminal Reskin
### Story 2 supplement to SchedulingPuzzle_UnitySetup.md

This document covers only the changes introduced by the HMI amber terminal theme.
Complete the base wiring in `SchedulingPuzzle_UnitySetup.md` first, then apply every section here on top of it.

---

## Assets delivered this session

| File | Destination |
|---|---|
| `SchedulingTerminal.fbx` | FYP root -- import into Unity, move to Assets/Models/Props/ |
| `SchedulingTerminal.blend` | FYP root -- source file only, not imported into Unity |
| `Assets/Sprites/UI/HMI/hmi_panel_bg.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_scanline_tile.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_btn_inject.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_slot_empty.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_slot_lock.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_slot_cam.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_slot_vent.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_slot_alarm.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_token_lock.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_token_cam.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_token_vent.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_token_alarm.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_warning_amber.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_warning_red.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_warning_ok.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_divider.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_section_label_bg.png` | Already in project |
| `Assets/Sprites/UI/HMI/hmi_time_chip.png` | Already in project |

---

## 1. Sprite import settings (Project window)

Select all files in `Assets/Sprites/UI/HMI/` and apply these defaults in the Inspector:

| Setting | Value |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | 100 |
| Filter Mode | Bilinear |
| Compression | None (these are small UI assets, size is negligible) |
| Alpha Is Transparency | On |

Then apply 9-slice borders to the following sprites individually. Select each, click **Sprite Editor**, set borders, then Apply:

| Sprite | L | B | R | T |
|---|---|---|---|---|
| `hmi_panel_bg.png` | 16 | 16 | 16 | 16 |
| `hmi_btn_inject.png` | 8 | 8 | 8 | 8 |
| `hmi_slot_empty.png` | 6 | 6 | 6 | 6 |
| `hmi_slot_lock.png` | 6 | 6 | 6 | 6 |
| `hmi_slot_cam.png` | 6 | 6 | 6 | 6 |
| `hmi_slot_vent.png` | 6 | 6 | 6 | 6 |
| `hmi_slot_alarm.png` | 6 | 6 | 6 | 6 |
| `hmi_token_lock.png` | 8 | 8 | 8 | 8 |
| `hmi_token_cam.png` | 8 | 8 | 8 | 8 |
| `hmi_token_vent.png` | 8 | 8 | 8 | 8 |
| `hmi_token_alarm.png` | 8 | 8 | 8 | 8 |
| `hmi_warning_amber.png` | 10 | 6 | 6 | 6 |
| `hmi_warning_red.png` | 10 | 6 | 6 | 6 |
| `hmi_warning_ok.png` | 10 | 6 | 6 | 6 |
| `hmi_section_label_bg.png` | 6 | 4 | 6 | 4 |

Sprites that do NOT need 9-slicing (use as Simple):
`hmi_panel_bg.png` (full-panel, not stretched), `hmi_scanline_tile.png`, `hmi_divider.png`, `hmi_time_chip.png`.

---

## 2. Font requirements

All HMI text uses a monospace font to sell the terminal aesthetic. The project does not currently have one imported. Two options:

**Option A (recommended):** Download **Courier Prime** (OFL license, free).
Place the .ttf in `Assets/Fonts/CourierPrime-Regular.ttf`. Create a TMP Font Asset from it:
Window -> TextMeshPro -> Font Asset Creator -> Source Font = CourierPrime-Regular -> Generate Font Atlas -> Save.
Save as `Assets/Fonts/CourierPrime_TMP.asset`.

**Option B (quick placeholder):** Use the TMP default font but set letter-spacing to 2 on all HMI text fields. It will not look monospaced but the colours and sprites still carry the theme.

---

## 3. Scheduling Puzzle Panel -- reskin changes

These changes replace the default Panel look. Do not touch the existing child hierarchy positions -- only change visual properties.

### 3a. Panel root Image

Select `Scheduling Puzzle Panel`.

| Inspector field | Old value | New value |
|---|---|---|
| Image -> Source Image | None (Unity default) | `hmi_panel_bg` (Sliced) |
| Image -> Image Type | Simple | Sliced |
| Image -> Color | (0.1, 0.1, 0.15, 0.92) | White, fully opaque (R:1 G:1 B:1 A:1) -- let the sprite carry the colour |

### 3b. Scanline overlay child

Add a new child Image directly inside `Scheduling Puzzle Panel` (not inside any other child):
- Rename to `Scanline_Overlay`
- RectTransform: full-stretch anchor (0,0 -> 1,1), Size Delta (0,0)
- Image -> Source Image: `hmi_scanline_tile`, Image Type: Tiled
- Image -> Color: White, A = 180 (alpha ~70%)
- Raycast Target: OFF
- In the Hierarchy, drag this child to be the **second child** (just below the panel root, above TitleText) -- render order matters

### 3c. TitleText

| Field | New value |
|---|---|
| Text | `ARBITEX FACILITY OS v4.1 // PROCESS SCHEDULER` |
| Font Asset | CourierPrime_TMP (or default + letter-spacing 2) |
| Font Size | 16 |
| Color | R:0.937 G:0.624 B:0.153 A:1 (amber, hex #EF9F27) |
| Font Style | Normal (not bold -- monospace reads better un-bolded) |
| Alignment | Left |
| RectTransform Width | 760 (unchanged) |
| RectTransform Height | 40 (reduce from 50 -- title is one line) |

Add a second TitleText line. Right-click `Scheduling Puzzle Panel` -> UI -> Text (TextMeshPro). Rename to `SubtitleText`:
- Text: `NODE: CLASSRM-02 // INTRUSION DETECTED -- OVERRIDE AUTHORISED`
- Font Size: 11
- Color: R:0.729 G:0.459 B:0.090 A:1 (dim amber, hex #BA7517)
- RectTransform: Width=760, Height=20, Anchored Pos Y = TitleText Y - 28

Add Divider image between the subtitle and the instruction area:
- Right-click panel -> UI -> Image. Rename to `HeaderDivider`.
- Source Image: `hmi_divider`, Image Type: Simple, Preserve Aspect: OFF
- RectTransform: Width=760, Height=3, Anchored Pos Y = SubtitleText Y - 20
- Color: White, A = 255
- Raycast Target: OFF

### 3d. InstructionText (pool label)

Replace instruction text with a section label background:
- Add Image component: Source Image = `hmi_section_label_bg`, Sliced
- OR: simply update the TMP text color to amber and leave the background transparent

| Field | New value |
|---|---|
| Text | (generated at runtime by InitPuzzle -- no change needed in Inspector) |
| Color | R:0.729 G:0.459 B:0.090 A:1 (dim amber) |
| Font Asset | CourierPrime_TMP |
| Font Size | 12 |

Add a section label above CardPool manually. Right-click panel -> UI -> Text (TMP). Rename `PoolLabel`:
- Text: `AVAILABLE PROCESSES -- DRAG TO SCHEDULE`
- Font Size: 10
- Color: R:0.388 G:0.212 B:0.024 A:1 (very dim amber, hex #633806)
- RectTransform: Width=380, Height=18, Anchored Pos = just above CardPool Y

Add a second section label above SlotRow. Rename `TimelineLabel`:
- Text: `EXECUTION TIMELINE -- RECONSTRUCT ROUND ROBIN ORDER`
- Same styling as PoolLabel, Anchored Pos just above SlotRow Y

### 3e. SubmitButton (INJECT OVERRIDE)

| Inspector field | New value |
|---|---|
| Image -> Source Image | `hmi_btn_inject` |
| Image -> Image Type | Sliced |
| Image -> Color | White fully opaque |
| RectTransform Width | 220 (wider to fit the label) |
| RectTransform Height | 40 |
| Button label text | `[ INJECT OVERRIDE ]` |
| Label font | CourierPrime_TMP, size 13, Color = R:0.980 G:0.780 B:0.459 A:1 (#FAC775) |
| Normal Color | White |
| Highlighted Color | R:1 G:0.85 B:0.5 A:1 (brighter amber on hover) |
| Pressed Color | R:0.6 G:0.35 B:0.05 A:1 (dark pressed) |

### 3f. CloseButton

| Field | New value |
|---|---|
| Image -> Color | R:0.255 G:0.141 B:0.016 A:1 (dark amber bg) |
| Label text | `[ ABORT ]` |
| Label color | R:0.729 G:0.459 B:0.090 A:1 |
| Font | CourierPrime_TMP, size 12 |

---

## 4. ProcessCard prefab -- HMI reskin

Open the `ProcessCard` prefab in the Prefab editor.

### Per-process sprite assignment
The sprite on the root Image is set to one of the four hmi_token_*.png sprites at runtime by `SchedulingPuzzleUI`. To support this, each token sprite must be assigned in the `SchedulingPuzzleUI` Inspector (see section 6 below).

Interim: set the root Image Source Image to `hmi_token_lock` as a visual placeholder. The script will override it at runtime.

Image Type: Sliced. Color: White, fully opaque.

### NameText (process label)

| Field | New value |
|---|---|
| Text | (runtime) |
| Font | CourierPrime_TMP |
| Font Size | 12 |
| Color | R:0.980 G:0.780 B:0.459 A:1 (#FAC775) |
| Font Style | Bold |

### BurstTimeText

| Field | New value |
|---|---|
| Font | CourierPrime_TMP, size 11 |
| Color | R:0.729 G:0.459 B:0.090 A:1 |

### ArrivalTimeText

| Field | New value |
|---|---|
| Font | CourierPrime_TMP, size 10 |
| Color | R:0.388 G:0.212 B:0.024 A:1 |

---

## 5. ExecutionSlot prefab -- HMI reskin

Open the `ExecutionSlot` prefab.

Root Image: Source Image = `hmi_slot_empty`, Image Type: Sliced, Color: White opaque.

The code should swap the Source Image to the appropriate process slot sprite when a card is placed. This requires a code change (see section 8).

**FilledBg child**: disable and leave as-is. The slot sprite itself carries the fill colour so FilledBg is not needed in the HMI theme (can be left in hierarchy but kept disabled).

**SlotLabel** (the T=0, T=3 time labels):

| Field | New value |
|---|---|
| Font | CourierPrime_TMP, size 10 |
| Color | R:0.388 G:0.212 B:0.024 A:1 (#633806) |
| Alignment | Centre |

---

## 6. SchedulingPuzzleUI Inspector -- new serialized fields needed

The following new fields must be added to `SchedulingPuzzleUI.cs` to support sprite swapping at runtime. Add them as serialized fields and wire them in the Inspector:

```csharp
[Header("HMI Token Sprites")]
[SerializeField] private Sprite tokenSpriteLock;
[SerializeField] private Sprite tokenSpriteCam;
[SerializeField] private Sprite tokenSpriteVent;
[SerializeField] private Sprite tokenSpriteAlarm;

[Header("HMI Slot Sprites")]
[SerializeField] private Sprite slotSpriteEmpty;
[SerializeField] private Sprite slotSpriteLock;
[SerializeField] private Sprite slotSpriteCam;
[SerializeField] private Sprite slotSpriteVent;
[SerializeField] private Sprite slotSpriteAlarm;
```

Wire these in the Inspector on `Scheduling Puzzle Panel -> SchedulingPuzzleUI`:

| Field | Sprite asset |
|---|---|
| Token Sprite Lock | `hmi_token_lock` |
| Token Sprite Cam | `hmi_token_cam` |
| Token Sprite Vent | `hmi_token_vent` |
| Token Sprite Alarm | `hmi_token_alarm` |
| Slot Sprite Empty | `hmi_slot_empty` |
| Slot Sprite Lock | `hmi_slot_lock` |
| Slot Sprite Cam | `hmi_slot_cam` |
| Slot Sprite Vent | `hmi_slot_vent` |
| Slot Sprite Alarm | `hmi_slot_alarm` |

---

## 7. FeedbackPopup -- HMI reskin

The three warning sprite variants map directly to the three feedback states.

### PopupCard Image

Remove the solid colour background. Replace with the appropriate warning sprite:
- Wrong attempt 1 or 2: Source Image = `hmi_warning_amber`, Sliced
- Wrong attempt 3+ (reveal): Source Image = `hmi_warning_red`, Sliced
- Correct: Source Image = `hmi_warning_ok`, Sliced

This sprite swap must happen at runtime in `SchedulingFeedbackPopup.Show()`. Add a serialized field:

```csharp
[SerializeField] private Sprite warningAmber;
[SerializeField] private Sprite warningRed;
[SerializeField] private Sprite warningOk;
[SerializeField] private Image popupCardImage;
```

Wire all three sprites and the PopupCard Image in the Inspector.

### Updated feedback strings (copy-paste ready, ASCII-safe)

| State | Icon | Message |
|---|---|---|
| 1st wrong attempt | X | `OVERRIDE REJECTED -- RECHECK SEQUENCE` |
| 2nd wrong attempt | X | `OVERRIDE REJECTED -- FINAL ATTEMPT AUTHORISED` |
| 3rd+ wrong (reveal) | X | `LOCKOUT INITIATED -- DISPLAYING AUTHORISED SEQUENCE` |
| Correct | O | `OVERRIDE ACCEPTED -- LOCK_SYS DISABLED` |

Replace existing icon characters in `SchedulingFeedbackPopup.cs`:
- Wrong icon: the letter `X` (capital X, not Unicode cross)
- Correct icon: the letter `O` (capital O, not Unicode checkmark)

### IconText and MessageText styling (PopupCard children)

| Field | New value |
|---|---|
| Font | CourierPrime_TMP |
| IconText Color (wrong) | R:0.847 G:0.353 B:0.188 A:1 (#D85A30 coral-red) |
| IconText Color (ok) | R:0.114 G:0.620 B:0.459 A:1 (#1D9E75 teal) |
| MessageText Color | R:0.729 G:0.459 B:0.090 A:1 (dim amber) |
| OkButton label | `[ CONFIRM ]` |
| OkButton font | CourierPrime_TMP, size 12, color #FAC775 |

---

## 8. Code changes required in SchedulingPuzzleUI.cs

The following changes in the script are needed to complete the HMI theme. These are Inspector-driven where possible so you do not need to hardcode values.

### 8a. Process names

In `GeneratePuzzle()` or `InitPuzzle()`, replace the process name array:

```csharp
// Old
string[] processNames = new string[] { "P1", "P2", "P3", "P4" };

// New
string[] processNames = new string[] { "LOCK_SYS", "CAM_ARRAY", "VENT_CTRL", "ALARM_NET" };
```

### 8b. Token sprite assignment in card Init

When calling `card.Init(processName, burstTime, ...)`, also pass the correct token sprite based on process index:

```csharp
Sprite[] tokenSprites = { tokenSpriteLock, tokenSpriteCam, tokenSpriteVent, tokenSpriteAlarm };
card.GetComponent<Image>().sprite = tokenSprites[processIndex % 4];
card.GetComponent<Image>().type   = Image.Type.Sliced;
```

### 8c. Slot sprite swap on card drop

In `SchedulingSlot.SetOccupied(SchedulingProcessCard card)`, swap the slot sprite:

```csharp
// Add a public method or event that SchedulingPuzzleUI calls after drop
public void SetHmiSprite(Sprite filled) {
    GetComponent<Image>().sprite = filled;
    GetComponent<Image>().type   = Image.Type.Sliced;
}

public void ClearHmiSprite(Sprite empty) {
    GetComponent<Image>().sprite = empty;
    GetComponent<Image>().type   = Image.Type.Sliced;
}
```

Call `SetHmiSprite(slotSpriteLock/Cam/Vent/Alarm)` after a successful drop, and `ClearHmiSprite(slotSpriteEmpty)` when a card is lifted back out.

### 8d. Submit button label override

In `SchedulingPuzzleUI.OnEnable()` or `InitPuzzle()`, set the button label explicitly:

```csharp
submitButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = "[ INJECT OVERRIDE ]";
```

This ensures the label survives any Unity UI resets.

### 8e. Instruction text reframe

Replace the instruction string generated in `InitPuzzle()`. Find where `instructionText.text` is set and change the prefix line to:

```
ARBITEX PROCESS TABLE -- Q={Q}\n
```

followed by the existing process table content (process name, burst time, arrival time per line). This keeps the educational content intact and adds the HMI framing.

---

## 9. New prop: SchedulingTerminal.fbx

### Import into Unity

1. Copy `SchedulingTerminal.fbx` from the FYP root into `Assets/Models/Props/`.
2. Select the FBX in the Project window. In the Inspector:
   - **Scale Factor**: 1
   - **Convert Units**: ON (file is in metres, Unity default is metres -- this is fine)
   - **Import Materials**: ON, Location = Use External Materials (Legacy)
   - Apply.

3. Unity will create material slots for each named material (M_Term_Casing, M_Term_Screen, etc.). These are plain Principled-equivalent URP/Standard materials. Assign colours manually or leave the auto-imported defaults -- they will be close to the Blender originals.

### Material colour reference (for manual adjustment in Unity)

| Material | Base Color (hex) | Metallic | Smoothness |
|---|---|---|---|
| M_Term_Casing | #0A0601 | 0.75 | 0.55 |
| M_Term_Screen | #050300 | 0.15 | 0.94 |
| M_Term_Bezel | #070401 | 0.60 | 0.50 |
| M_Term_LED | #0D0701 + Emission #EF9F27 x 3.5 | 0.0 | 0.0 |
| M_Term_Button | #060400 | 0.10 | 0.20 |
| M_Term_PwrLight | #0A0000 + Emission #D90D05 x 2.0 | 0.0 | 0.0 |
| M_Term_Bracket | #080501 | 0.90 | 0.70 |
| M_Term_Conduit | #050400 | 0.70 | 0.50 |
| M_Term_Vent | #020101 | 0.30 | 0.20 |
| M_Term_Rivet | #100900 | 0.95 | 0.80 |

Enable **Emission** on M_Term_LED and M_Term_PwrLight. In URP Lit shader: check the Emission checkbox, set Emission Color to the hex above.

### Placement in Classroom 1

The terminal mounts on the interior wall beside the classroom door (the same wall the player faces when entering). Suggested transform:

| Property | Value |
|---|---|
| Position X | Place flush against wall (match wall X boundary) |
| Position Y | 1.0 (bottom of terminal sits 1.0m off floor -- eye level for a seated player) |
| Position Z | Adjust so it sits beside the door frame, not blocking the doorway |
| Rotation Y | 90 or 270 depending on which wall face |
| Rotation X, Z | 0 |
| Scale | (1, 1, 1) |

The terminal's back face (+Y in Blender = -Z in Unity after FBX import with -Z Forward) should be flush against the wall mesh. Nudge in the Z axis until the bracket rivets are inside the wall slightly.

### BoxCollider and interactable setup

Remove `SchedulingPuzzleProp` from `Prop_Whiteboard` (uncheck or remove the component).

On the `SchedulingTerminal` FBX root in the Hierarchy:
1. **Add Component -> Box Collider**
   - Is Trigger: ON
   - Size: (0.65, 0.85, 0.20) -- slightly larger than casing so the trigger is generous
   - Center: (0, 0, 0)
2. **Add Component -> InteractableBase**
   - Interact Prompt: `Use Terminal`
   - Wire `promptPanel` to the shared UI prompt panel (same as other interactables)
3. **Add Component -> InteractableRegistrar**
   - No fields needed
4. **Add Component -> Scripts -> SchedulingPuzzleProp**
   - `schedulingOverlay`: drag `Scheduling Puzzle Panel` from Hierarchy

---

## 10. HMI sanity checklist

Run through these after completing all wiring, in addition to the base doc checklist.

| # | Check |
|---|---|
| 1 | `hmi_panel_bg` appears as the Scheduling Puzzle Panel background (dark with amber border) |
| 2 | `Scanline_Overlay` is second child of panel, Raycast Target OFF, image tiled |
| 3 | TitleText reads `ARBITEX FACILITY OS v4.1 // PROCESS SCHEDULER` in amber monospace |
| 4 | Submit button reads `[ INJECT OVERRIDE ]` with amber text on dark background |
| 5 | Card tokens show hmi_token_*.png sprite matching process index (amber/teal/purple/coral) |
| 6 | Process names in pool read LOCK_SYS / CAM_ARRAY / VENT_CTRL / ALARM_NET |
| 7 | Empty gantt slots show hmi_slot_empty.png (dashed amber border, dark bg) |
| 8 | Filled slot swaps to correct colour sprite on card drop |
| 9 | 1st wrong: popup shows amber warning bg, X icon, `OVERRIDE REJECTED -- RECHECK SEQUENCE` |
| 10 | 2nd wrong: popup shows amber warning bg, X icon, `OVERRIDE REJECTED -- FINAL ATTEMPT AUTHORISED` |
| 11 | 3rd wrong: popup shows red warning bg, X icon, `LOCKOUT INITIATED -- DISPLAYING AUTHORISED SEQUENCE` + reveals correct order |
| 12 | Correct: popup shows teal warning bg, O icon, `OVERRIDE ACCEPTED -- LOCK_SYS DISABLED` |
| 13 | SchedulingTerminal.fbx placed on Classroom 1 wall, BoxCollider trigger active |
| 14 | SchedulingPuzzleProp removed from Prop_Whiteboard |
| 15 | M_Term_LED emission is ON and glows amber in Play mode |
| 16 | M_Term_PwrLight emission is ON and glows red in Play mode |

---

## 11. Blender source file notes

`SchedulingTerminal.blend` contains one collection: **Terminal_HMI** with 32 mesh objects.

| Part group | Objects |
|---|---|
| Casing | Term_Casing |
| Screen | Term_Screen_Bezel, Term_Screen |
| LED strip | Term_LED_Strip |
| Control strip | Term_CtrlStrip, Term_Btn_Power, Term_Btn_Reset, Term_PwrLight |
| Label plate | Term_LabelPlate |
| Mounting brackets | Term_Brk_TL_Back/Wall, TR_Back/Wall, BL_Back/Wall, BR_Back/Wall (8 parts) |
| Bracket rivets | Term_Rivet_TL, TR, BL, BR (4 cylinders) |
| Front rivets | Term_FrontRivet_* (4 cylinders, corner accents) |
| Cable conduit | Term_Conduit |
| Side vents | Term_Vent_L_0/1/2, Term_Vent_R_0/1/2 (6 parts) |

All transforms applied. FBX export settings: Forward = -Z, Up = Y, Scale = FBX Units Scale.

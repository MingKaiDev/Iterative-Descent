# ReagentRouting (Chemistry Lab -- Dijkstra's Algorithm) -- Unity Scene Setup

## What this teaches, and how it differs from DrainPuzzle

DrainPuzzle teaches BFS/DFS: an unweighted graph, where the only thing that
matters is traversal ORDER (queue vs stack). This puzzle teaches Dijkstra's
algorithm: a weighted graph, where the thing that matters is total path
COST. All edge costs are visible up front -- there is no fog-of-war /
hidden-node discovery here, unlike Drain. The player builds a route
node-by-node from INTAKE to CHAMBER, then commits; the route only passes if
its cost equals the true minimum (Dijkstra-computed) cost, not merely a
route that reaches the target.

BKT concept key: `BayesianKnowledgeTracker.Dijkstra` (already defined,
previously unused, prerequisite `BfsDfs` per `bkt-concept-map.md`).

Core algorithm logic (graph generation, Dijkstra, path reconstruction, path
cost) is ported 1:1 from the HTML/JS prototype you playtested, and has been
independently re-verified in C# via a reflection-based test harness against
a brute-force path search: 2500 randomised trials across all 5 tiers, zero
mismatches.

## Files created

| File | Path |
|---|---|
| ReagentRoutingUI.cs | Assets/Scripts/Puzzles/ReagentRouting/ |
| ReagentFeedbackPopup.cs | Assets/Scripts/Puzzles/ReagentRouting/ |
| ReagentRoutingProp.cs | Assets/Scripts/Props/ |
| ReagentRoutingEventHandler.cs | Assets/Scripts/Events/ |
| ReagentRouting_PanelBackground.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_HeaderBar.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_ButtonNormal.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_ButtonPressed.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_NodeValve.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_PipeSegment.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_CostChip.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_PopupPanel.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentRouting_HazardStripe.png | Assets/Sprites/UI/ReagentRouting/ |
| ReagentNodePrefab.prefab | Assets/Prefab/ReagentRouting/ |
| ReagentEdgePrefab.prefab | Assets/Prefab/ReagentRouting/ |
| ReagentCostChipPrefab.prefab | Assets/Prefab/ReagentRouting/ |

All `.cs` files above have matching `.meta` sidecars with fresh GUIDs
already generated (`generate_script_meta.py` / `script_manifest.json`).

## Files modified (existing project files, not new)

| File | Path | Why |
|---|---|---|
| PlayerMetricsTracker.cs | Assets/Scripts/Events/DDA/ | DDA wiring -- see "DDA / BKT wiring" below |
| PuzzleDDAController.cs | Assets/Scripts/Events/DDA/ | DDA wiring -- see "DDA / BKT wiring" below |
| PlayerCombat.cs | Assets/Scripts/Player/ | New `ReduceSettleTime()` method for the barrel attachment reward, plus a deferred-idea TODO comment -- see "ReagentRoutingEventHandler setup" below |
All `.png` files have matching `.meta` sidecars with TextureImporter
settings already generated (`generate_meta.py` / `sprite_manifest.json`),
including 9-slice borders on PanelBackground, ButtonNormal, ButtonPressed,
CostChip, and PopupPanel.

## Visual identity

Amber/copper industrial-chemical schematic look, deliberately distinct from
Drain's green CRT terminal, Stack's charcoal PDU panel, and PacketFilter's
green-on-black CRT: brushed dark-copper panel background with subtle grain
and a bevelled edge, riveted header bar, hazard-stripe accents, warm
amber/bronze buttons with a pressed-state indent. Node and pipe sprites are
generated as near-white/light-gray so the existing runtime tinting
technique (`Image.color`, matching DrainPuzzleUI's `ApplyNodeVisual` /
`StateToColor` pattern) works correctly -- do NOT recolor these sprites
directly in the image, or runtime tinting will look wrong.

Route/graph colours (already implemented in `ReagentRoutingUI.cs`, for
reference when eyeballing the scene):

| State | Color | Hex (approx) |
|---|---|---|
| INTAKE (source) | teal-green | #45AB82 |
| CHAMBER (target) | hazard red | #C74538 |
| Selected (your route) | amber | #E6AB3D |
| Available (clickable next) | bright copper glow | #FFC76E |
| Optimal (revealed on fail) | cyan | #59D1E6 |
| Idle junction | dim copper | #82693D |

## Prefabs already created and verified

All 3 hand-authored using real GUIDs read directly from this project's own
`Tree Search/NodePrefab.prefab` and `ChipPrefab.prefab` (Image script, Button
script, TextMeshProUGUI script, font asset GUIDs) -- not guessed. Verified
with `verify_prefabs.py` (the same technique documented in this project's
memory rule #19 for `Concept Tutorial Panel.prefab`): every `--- !u!TYPE
&fileID` block parsed, every local `{fileID: N}` reference resolved, every
non-GameObject's `m_GameObject` back-reference confirmed present in that
GameObject's `m_Component` list, every RectTransform's `m_Children` /
`m_Father` confirmed to agree in both directions, plus a full
`yaml.safe_load_all` parse. Result: **all 3 files verified clean, zero
errors** (8, 4, and 9 objects respectively).

### ReagentNodePrefab
- Root: Image (72x72, sprite = `ReagentRouting_NodeValve`, tinted at runtime by `ApplyNodeVisual`), Raycast Target ON, Button component (Target Graphic = the root Image)
- Child: `LabelText` -- TextMeshProUGUI, bold, size 12, color `(0.90, 0.86, 0.78, 1)` (warm off-white)

### ReagentEdgePrefab
- Root: Image only (sprite = `ReagentRouting_PipeSegment`, color alpha 0.45, Raycast Target OFF), size 1x14, pivot (0, 0.5) -- `SpawnEdgesAfterLayout()` resizes and rotates it at runtime exactly like DrainPuzzleUI's EdgePrefab

### ReagentCostChipPrefab
- Root: Image (60x28, sprite = `ReagentRouting_CostChip`, Image Type = Sliced, Raycast Target OFF)
- Child: `CostText` -- TextMeshProUGUI, bold, size 13, color `(0.20, 0.13, 0.05, 1)` (dark brown, readable on the amber chip), default text "0"

## Canvas Hierarchy

Build this panel manually in the Editor (per this project's established
convention -- puzzle panels themselves are hand-built from a spec, not saved
as prefabs; only the small reusable widgets above are prefabs).

```
[ReagentRoutingPanel]                -- starts INACTIVE, full-screen overlay
  [Background]                       -- Image, ReagentRouting_PanelBackground, Sliced
  [HazardBorder] (optional)          -- Image, ReagentRouting_HazardStripe, thin strip along top/bottom
  [Header]
    [HeaderBg]                       -- Image, ReagentRouting_HeaderBar
    [TitleText]                      -- TMP "REAGENT ROUTING -- CHEMISTRY LAB"
    [TierBadge]
      [TierBadgeText]                -- TMP "TIER 2 -- NORMAL" (tierBadgeText)
  [GraphSection]
    [EdgeContainer]                  -- RectTransform, NO Image, no Layout Group (edgeContainer)
    [ChipContainer]                  -- RectTransform, NO Image, no Layout Group (chipContainer)
    [NodeContainer]                  -- RectTransform, NO Image, no Layout Group (nodeContainer)
  [InfoPanel]
    [RouteCostText]                  -- TMP "ROUTE COST: 0" (routeCostText)
    [AttemptsText]                   -- TMP "ATTEMPTS: 0" (attemptsText)
    [StatusText]                     -- TMP status line (statusText)
  [Buttons]
    [UndoButton]                     -- Button, child TMP "Undo" (undoButton)
    [ResetButton]                    -- Button, child TMP "Reset Route" (resetButton)
    [CommitButton]                   -- Button, child TMP "Commit Route" (commitButton)
    [CloseButton]                    -- Button, child TMP "Close" (closeButton)
  [WinPanel]                         -- starts INACTIVE (winPanel)
    [WinText]                        -- TMP win message (winText)
  [FeedbackPopup]                    -- ReagentFeedbackPopup, starts INACTIVE (feedbackPopup)
    [Blocker]                        -- Image alpha=0, Raycast Target ON, full stretch
    [PopupCard]                      -- Image, ReagentRouting_PopupPanel, Sliced
      [IconText]                     -- TMP (OK / X icon)
      [MessageText]                  -- TMP body
      [OkButton]                     -- Button
```

Important ordering note (matches Drain): `EdgeContainer` must render behind
`ChipContainer`, which must render behind `NodeContainer`, so list them
top-to-bottom in that order under `GraphSection` in the Hierarchy (Unity UI
draws later siblings on top).

## Canvas component settings

### ReagentRoutingPanel (root)
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Canvas Group (optional) | Blocks Raycasts ON |
| ReagentRoutingUI | (this script) |
| Active in scene | OFF (starts inactive -- prop activates it) |

### Background
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Sprite: ReagentRouting_PanelBackground, Type: Sliced, Raycast Target ON |

### Header
| Component | Setting |
|---|---|
| RectTransform | Anchor: top/stretch, Pivot (0.5, 1), Height 60, Left/Right 0, Top 0 |

### HeaderBg
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Sprite: ReagentRouting_HeaderBar, Raycast Target OFF |

### TitleText
| Component | Setting |
|---|---|
| RectTransform | Anchor: left/stretch, Width 420, Left 16 |
| TextMeshProUGUI | Text: "REAGENT ROUTING -- CHEMISTRY LAB", Size 14, color #F0DCC8, alignment middle-left, Bold |

### TierBadge / TierBadgeText
| Component | Setting |
|---|---|
| RectTransform | Anchor: right/middle, Pivot (1, 0.5), Width 200, Height 32, Right 16 |
| TextMeshProUGUI | Text set at runtime by `GeneratePuzzle()`, Size 12, color #E6AB3D, alignment middle-center, Bold |

### GraphSection
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Top 96, Bottom 120, Left 16, Right 16 |

### EdgeContainer / ChipContainer / NodeContainer
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| No Image, no Layout Group | Script positions all children manually via NormPos (0..1) convention, same as DrainPuzzleUI |

### InfoPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5, 0), Height 56, Left 16, Right 16, Bottom 64 |
| Horizontal Layout Group | Spacing 24, Child Alignment: Middle Left |

### RouteCostText / AttemptsText
| Component | Setting |
|---|---|
| RectTransform | Layout Element, Width 200, Height 24 |
| TextMeshProUGUI | Size 13, color #FFC76E, alignment middle-left, Bold |

### StatusText
| Component | Setting |
|---|---|
| RectTransform | Layout Element, Flexible Width 1, Height 24 |
| TextMeshProUGUI | Size 11, color #C8B89E, alignment middle-left, overflow: Ellipsis |

### Buttons
| Component | Setting |
|---|---|
| RectTransform | Anchor: bottom/stretch, Pivot (0.5, 0), Height 40, Left 16, Right 16, Bottom 16 |
| Horizontal Layout Group | Spacing 12, Child Alignment: Middle Center |

### UndoButton / ResetButton / CommitButton / CloseButton
| Component | Setting |
|---|---|
| RectTransform | Width 140, Height 36 (Layout Element) |
| Image | Sprite: ReagentRouting_ButtonNormal, Type: Sliced, Raycast Target ON |
| Button | Transition: Sprite Swap, Pressed Sprite: ReagentRouting_ButtonPressed |
| Child TMP | Size 12, color #2E1D0F, alignment middle-center, Bold |

### WinPanel
| Component | Setting |
|---|---|
| RectTransform | Anchor: center/center, Width 520, Height 190 |
| Image | Sprite: ReagentRouting_PopupPanel, Type: Sliced |
| Active in scene | OFF |

### WinText
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, Left 24, Right 24, Top 16, Bottom 16 |
| TextMeshProUGUI | Size 13, color #F0DCC8, alignment middle-center, word wrap ON |

### FeedbackPopup
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| ReagentFeedbackPopup | Wire: IconText, MessageText, OkButton (inherited from PuzzleFeedbackPopup -- check its own field names) |
| Active in scene | OFF (never SetActive(false) in Awake -- reset visibility from InitPuzzle instead, per project rule #9; ReagentRoutingUI.InitPuzzle() already does this) |

### FeedbackPopup > Blocker
| Component | Setting |
|---|---|
| RectTransform | Anchor: stretch/stretch, all offsets 0 |
| Image | Color: #000000 alpha 0, Raycast Target ON |

### FeedbackPopup > PopupCard
| Component | Setting |
|---|---|
| RectTransform | Anchor: center/center, Width 460, Height 200 |
| Image | Sprite: ReagentRouting_PopupPanel, Type: Sliced |

### FeedbackPopup > PopupCard > IconText / MessageText / OkButton
Same layout as DrainFeedbackPopup's equivalents (see `DrainPuzzle_UnitySetup.md`) -- reuse those exact RectTransform values, just recolor to the amber/copper palette above (icon/message text #F0DCC8, OK button using ReagentRouting_ButtonNormal/Pressed sprites).

## Inspector wiring (ReagentRoutingUI)

| Field | Assign |
|---|---|
| edgeContainer | EdgeContainer RectTransform |
| chipContainer | ChipContainer RectTransform |
| nodeContainer | NodeContainer RectTransform |
| nodePrefab | ReagentNodePrefab |
| edgePrefab | ReagentEdgePrefab |
| costChipPrefab | ReagentCostChipPrefab |
| tierBadgeText | TierBadgeText TMP |
| routeCostText | RouteCostText TMP |
| attemptsText | AttemptsText TMP |
| statusText | StatusText TMP |
| winPanel | WinPanel GameObject |
| winText | WinText TMP |
| undoButton | UndoButton Button |
| resetButton | ResetButton Button |
| commitButton | CommitButton Button |
| closeButton | CloseButton Button |
| feedbackPopup | FeedbackPopup ReagentFeedbackPopup |

Not a wired reference, but a tunable Inspector value on the same
component: `attemptsBeforeReveal` (int, default 3) -- how many failed
commits on one graph before the optimal path reveals.

## ReagentRoutingProp setup

1. Attach `ReagentRoutingProp.cs` to the Chemistry Lab reagent terminal prop GameObject (or a child trigger collider).
2. Also attach: `BoxCollider`, `InteractableBase`, `InteractableRegistrar`.
3. Set `reagentRoutingOverlay` to the `ReagentRoutingPanel` Canvas child (starts inactive).
4. The Canvas panel needs `ReagentRoutingUI`, and the Canvas itself needs `EventSystem` + `GraphicRaycaster` (same as every other puzzle panel).

## ReagentRoutingEventHandler setup

**Updated a second time -- this is now a permanent barrel attachment, not
an item drop.** After the first round (guaranteed ammo+health, mirroring
MatchingEventHandler) you asked for a bigger reward: a pistol barrel
attachment. On solve, this now calls the new `PlayerCombat.ReduceSettleTime()`
method, permanently shaving `settleTimeReduction` seconds off the player's
aim-settle time -- the reticle reaches full accuracy faster. This REPLACES
the ammo+health drop (confirmed) rather than stacking with it, and is
auto-granted the instant `OnReagentRoutingSolved` fires (confirmed) --
no physical pickup, no new prefab or art needed.

1. Attach to a persistent GameObject in the scene (e.g. GameManager), matching DrainEventHandler / StackEventHandler.
2. Tune `settleTimeReduction` in the Inspector (default 0.5s, i.e. PlayerCombat's default 1.5s settleTime becomes 1.0s).
3. Never add a duplicate instance -- the Debug.Log in `HandleSolved` prints `gameObject.name` and `gameObject.scene.name` so you can spot double-firing if it happens.
4. **Critical guard, more so than for a consumable:** a private `_rewardGranted` flag makes the upgrade apply only once per play session. This is a PERMANENT stat change, not a consumable -- the Chemistry Lab terminal is currently re-enterable/re-solvable (confirmed in conversation; a proper room-level "already solved" gate is a planned future task, not built yet), so without this guard every re-solve during testing would keep shaving time off settleTime, eventually making aim settle instantly. Remove this guard once the room-level gate exists, if you'd rather it be re-grantable (e.g. for a stacking upgrade design).

### New method added to PlayerCombat.cs

`public void ReduceSettleTime(float amount)` -- permanently reduces
`settleTime`, floored at 0.1s so aiming never becomes instant. Added right
after the existing `AddAmmo()` method, following the same pattern (a small
public mutator called by an external reward/pickup source). Verified via
brace-balance check before/after (28 -> 31 pairs, all from the new method
body plus two string-interpolation brace pairs in its log line -- no full
compile was possible for this file since it depends on Animator,
PlayerMovement, ShotgunPellet and other real Unity/project types not worth
stubbing for one small addition).

### Deferred idea -- NOT implemented, flagged for later

You raised tying reticle size (`AccuracyT`, 0 = fresh aim, 1 = fully
settled) to bonus damage -- a tighter, more-settled shot also hitting
harder, not just landing more accurately -- but asked to put it on hold.
Nothing was built for this. A `TODO` comment was added next to
`AccuracyT` in `PlayerCombat.cs` noting that the natural hook, if picked
back up later, is in `FireBullet()` where `damage` is assigned onto the
bullet -- scale it by `AccuracyT` there.

## DDA / BKT wiring (already patched)

`PlayerMetricsTracker.cs` and `PuzzleDDAController.cs` have been patched
(diffs mirror the Drain block exactly, only names/BKT-concept changed):

- `PlayerMetricsTracker`: subscribes to `ReagentRoutingUI.OnReagentRoutingSolved` in `OnEnable`/`OnDisable`; new API block (`LastReagentRoutingWrongAttempts`, `LastReagentRoutingTime`, `TotalReagentRoutingSolved`, `AverageReagentRoutingWrongAttempts`, `AverageReagentRoutingTime`, `NotifyReagentRoutingStarted()`, `HandleReagentRoutingSolved(int)`) calling `UpdateGeneralPool()` and `BKT.UpdateAfterAttempt(BayesianKnowledgeTracker.Dijkstra, ...)`; reset block added to `ResetMetrics()`.
- `PuzzleDDAController`: subscribes `ReagentRoutingUI.OnReagentRoutingSolved += OnGeneralPuzzleSolved;` (and `-=`) alongside the other general-pool puzzles -- no other changes needed, since Reagent Routing pools into the existing general-puzzle signal group rather than adding new observation-vector indices.

Brace-balance sanity check (no full Unity/dotnet compiler available for
these two large files with all their real dependencies): `PlayerMetricsTracker.cs` was 136 open / 136 close braces before the patch, 164 / 164 after (net +28, matching the size of the inserted block) -- consistent, no stray brace introduced. `PuzzleDDAController.cs` is 29 / 29 after its two one-line insertions.

## Optional: ConceptTutorials caption

`ReagentRoutingProp.OpenPuzzle()` already calls
`ConceptTutorials.ShowIfUnseenThenContinue("dijkstra", ...)`. This gracefully
no-ops (invokes the continuation immediately) if no caption text or diagram
is registered for `"dijkstra"` yet, so the puzzle works today without it.
If you want the tutorial popup to actually show something the first time,
add a `"dijkstra"` entry to `ConceptTutorials.cs`'s `_captionText` dictionary
(ASCII only, no em dash, matching the tone of existing entries), and author
a diagram in `ConceptTutorialUI` -- neither has been done yet, since this
is a bonus and not required for the puzzle to function.

## Decisions -- CONFIRMED after first playtest

1. **Exact-match pass condition -- confirmed, no tolerance.** Committing a
   route only succeeds if its cost exactly equals the Dijkstra-optimal
   cost.
2. **No hint button -- confirmed.**
3. **Single-button feedback popup UX -- confirmed.**
4. **Reveal-after-3-wrongs (NEW, added post-playtest).** The optimal path
   used to be revealed on the very first failed commit. It's now only
   revealed once the player has racked up `attemptsBeforeReveal` (Inspector
   field on `ReagentRoutingUI`, default 3) failed commits on the same
   graph. Below that, the popup still shows the cost delta (your cost vs
   the true minimum) but keeps the answer hidden, and tells the player how
   many more misses are left before it reveals.
5. **Undo now works after a failed commit (NEW, added post-playtest).**
   Previously, any commit (pass or fail) locked the path, and the only way
   to try again was Reset Route (back to INTAKE, re-walk the whole thing).
   Undo is now allowed immediately after a non-optimal commit too: it
   removes just the last step and re-opens editing, so the player can
   swap out one bad branch instead of restarting from scratch. Any
   optimal-path reveal already on screen stays visible as a running
   reference while they keep adjusting; it only clears on Reset Route or
   on solving.
6. **Playtest bug fixed: the "no pipe connects there" warning was
   unreachable.** `ApplyNodeVisual()`'s clickable check required
   `IsAdjacent(last, id)`, so Unity's Button component was disabled for
   every non-adjacent node -- the click never registered, so
   `OnNodeClicked()`'s own adjacency check (which shows the warning) never
   ran. Fixed by dropping the adjacency requirement from what makes a node
   clickable; non-adjacent junctions now register the click and show the
   warning, while still rendering in the dim "Junction" color (that part
   of the logic, in `GetNodeState()`, was untouched) so the visual
   guidance toward valid next steps is unchanged.

All three code changes above were re-verified: the 4 puzzle scripts still
compile cleanly against the stub harness, and the ported Dijkstra/graph
logic was re-run through the 2500-trial brute-force test with zero
mismatches (unaffected, since only click-handling/UI-state code changed,
not the algorithm).

## Decision -- RESOLVED (twice): Chemistry Lab reward

**Round 1** settled on a guaranteed ammo + health spawn mirroring
`MatchingEventHandler` (no new infrastructure, consistent with an existing
puzzle reward, reasonable given no combat immediately follows this room).

**Round 2 (final, current implementation):** you asked for a bigger
reward -- a pistol barrel attachment. This replaces the ammo+health drop
entirely (confirmed) and is auto-granted on solve (confirmed). Mechanically
it permanently reduces `PlayerCombat.settleTime` via the new
`ReduceSettleTime()` method, so the aim reticle reaches full accuracy
faster. See "ReagentRoutingEventHandler setup" above for the full
implementation, the one-time-grant guard, and the deferred
reticle-size-affects-damage idea that was explicitly put on hold and not
built.

## Testing checklist

- [x] Tested across 4 tiers
- [x] Edge costs are visible immediately, no fog-of-war
- [x] Clicking a non-adjacent node now shows the "no pipe connects there" status message (bug found in first playtest, fixed -- see above)
- [x] Undo removes the last step
- [x] Commit with a non-optimal path works (popup shows cost delta; optimal path + edges now only reveal after `attemptsBeforeReveal` failures, per the new decision above)
- [x] Commit with the optimal path works: win panel shows, OnReagentRoutingSolved fires
- [x] Reset Route works
- [x] Close button works and restores timeScale / cursor state
- [x] DDA tier reflects PuzzleDDAController.GetTierForConcept(BayesianKnowledgeTracker.Dijkstra)
- [ ] Re-test: non-adjacent click now shows the warning (fix just applied, not yet playtested)
- [ ] Re-test: Undo works immediately after a failed/non-optimal commit without needing Reset Route first
- [ ] Re-test: optimal path stays hidden on attempts 1-2, reveals on attempt 3 (or your configured `attemptsBeforeReveal`), and the popup's "N more attempts" countdown text is correct each time
- [ ] Tier 4 (the one tier not yet covered in the first pass)

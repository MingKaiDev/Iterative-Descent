# Story 18 -- Painting Puzzle Unity Setup

Hallway 2 overworld ordering puzzle. No UI overlay -- everything is world-space.

---

## Files Created

| File | Purpose |
|---|---|
| `Assets/Scripts/Puzzles/Painting/PaintingPuzzleManager.cs` | Scene manager; holds state, validates order, fires event |
| `Assets/Scripts/Props/PaintingInteractable.cs` | IInteractable on each painting's button |
| `Assets/Scripts/Props/PaintingConfirmTerminal.cs` | IInteractable on the confirm button |
| `Assets/Scripts/Events/PaintingEventHandler.cs` | Unlocks Hallway 2 exit on solve |
| `Assets/Art/Props/PaintingButton.fbx` | Button prop (base plate + red cap + rim ring) |
| `Assets/Sprites/UI/PaintingPuzzle/label_*.png` | 8 state-name label sprites (512x128 PNG) |

---

## Correct Painting Order

| Slot | State | CorrectSlot value |
|---|---|---|
| 1 | NEW | 1 |
| 2 | READY | 2 |
| 3 | RUNNING | 3 |
| 4 | BLOCKED | 4 |
| 5 | READY-SUSPENDED | 5 |
| 6 | BLOCKED-SUSPENDED | 6 |
| 7 | ZOMBIE | 7 |
| 8 | TERMINATED | 8 |

---

## Scene Hierarchy per Painting (x8)

```
Painting_01_NEW              (existing painting frame GO)
  LabelQuad                  (Quad, Mesh Renderer, label_new.png sprite/material)
  PaintingButton_01          (PaintingButton.fbx instance)
    RankText                 (TextMeshPro - World Space, shows current rank number)
    -- components on PaintingButton_01 GO: --
    [PaintingInteractable]
    [InteractableBase]
    [InteractableRegistrar]
    [BoxCollider]
```

Repeat for all 8 paintings, incrementing the name and setting correctSlot accordingly.

---

## Confirm Terminal

```
PaintingConfirmTerminal      (any prop -- reuse PaintingButton.fbx or a wall panel)
  -- components: --
  [PaintingConfirmTerminal]
  [InteractableBase]
  [InteractableRegistrar]
  [BoxCollider]
```

Place at the end of the painting corridor. The InteractLabel shown to the player is "Confirm Order".

---

## PaintingPuzzleManager GO

Attach to any persistent or scene-level empty GO (e.g. Hallway2_Puzzles).

Inspector fields:
- **Paintings** (size 8): drag in all 8 PaintingInteractable GOs
- **Wrong Order Dialogue**: optional DialogueData asset for ARBITEX wrong-order taunt
- **Solved Dialogue**: optional DialogueData asset for ARBITEX solve line

---

## PaintingEventHandler GO

Attach to any GO in the scene.

- **Exit Door**: drag the Hallway 2 exit DoorController here

---

## PaintingInteractable Inspector per Painting

| Field | Value |
|---|---|
| Correct Slot | 1-8 (see table above) |
| Rank Text | Drag in the child RankText TextMeshPro |
| Label Quad | Drag in the child LabelQuad GO |

---

## Label Quad Setup

For each painting:
1. Create a child Quad, name it LabelQuad.
2. Create a Material using the Unlit/Texture shader (or URP Unlit).
3. Set the texture to the correct `label_*.png` from `Assets/Sprites/UI/PaintingPuzzle/`.
4. Scale the quad to roughly 0.5 x 0.12 x 1 and position it below the painting frame.
5. Enable Alpha Clipping if the PNG transparent border is visible.

---

## RankText (TextMeshPro - World Space) Setup

1. Add a child GameObject named RankText to the button GO.
2. Add TextMeshPro - World Space component.
3. Set font size ~2.5, Bold, centered.
4. Position it slightly in front of the button face (Y offset -0.025 from button surface).
5. Default text: leave blank -- PaintingInteractable.Start() sets it.

---

## DDA Behaviour

| Tier | Labels | Starting Order |
|---|---|---|
| 0/1 | Visible | Correct order (rank = correctSlot) |
| 2 | Hidden | Correct order |
| 3/4 | Hidden | Randomised ranks on Start |

No additional Inspector setup needed -- PaintingPuzzleManager.ApplyDDASettings() handles this on Start.

---

## Testing Checklist

- [ ] All 8 PaintingInteractable GOs registered in PaintingPuzzleManager.paintings[]
- [ ] Each PaintingInteractable.correctSlot set to the right value (1-8, no duplicates)
- [ ] Rank number visible on each button in Play mode
- [ ] E on a button cycles the number 1->2->...->8->1
- [ ] Confirm terminal responds to E
- [ ] Wrong order: ARBITEX dialogue fires, no door unlock
- [ ] Correct order: door unlocks, ARBITEX solved dialogue fires
- [ ] Tier 0/1: label quads visible; Tier 2+: label quads hidden (test by setting DDA tier manually via DDADisplayHUD or editing starting tier in PuzzleDDAController)
- [ ] Tier 3/4: ranks start shuffled
- [ ] Console shows [Metrics] Painting puzzle solved with correct wrong-attempt count
- [ ] Console shows [PuzzleDDA] tier re-evaluation after solve

# Classroom 1 Quiz 2 - Unity Setup Guide

## What was delivered

| File | Change |
|------|--------|
| `Assets/StreamingAssets/quiz_data_2.json` | New 15-question bank: hash_tables (5), trees_bst (5), basic_sorts (5) |
| `Assets/Scripts/Props/PuzzleProp.cs` | Added `UnityEvent onCompleted` - fires when the player clicks Close after finishing |

The `onCompleted` UnityEvent replaces the need for a dedicated `Quiz2EventHandler` script.
Wire whatever outcome you want (door unlock, enemy spawn, etc.) directly in the Inspector
once you decide what the quiz should gate.

---

## Step 1 - Duplicate the Quiz Panel

The existing `PuzzleProp` in Classroom 1 already points at the "Quiz Panel" in your Canvas.
Quiz 2 needs its OWN panel - two PuzzleProps cannot safely share one PuzzleUI instance.

1. In the Hierarchy, expand your Canvas and find **Quiz Panel**
2. Duplicate it (Ctrl+D) and rename it **Quiz Panel 2**
3. Leave it inactive (the PuzzleProp will activate it on open)
4. No component changes needed - PuzzleUI is stateless between Setup() calls

---

## Step 2 - Create the prop GameObject in Classroom 1

Create a new GameObject (or use an existing desk/computer mesh) inside the Classroom 1
root and add the following components in order:

| Component | Notes |
|-----------|-------|
| `BoxCollider` | Size to fit the prop geometry; not a trigger |
| `InteractableBase` | Set `highlightRadius`, assign `propCenter` (can be the same Transform), assign `promptPanel` and `promptLabel` from your shared Prompt Panel |
| `InteractableRegistrar` | No fields to set; auto-registers on Enable |
| `PuzzleProp` | See Step 3 |

Name the GameObject something like **Quiz_Prop_CL1_02** to make it identifiable in the scene.

---

## Step 3 - Configure PuzzleProp on the new prop

In the Inspector on the new PuzzleProp:

| Field | Value |
|-------|-------|
| Load From JSON | Checked |
| Json File Name | `quiz_data_2.json` |
| Questions Per Session | 5 |
| Puzzle Overlay | Drag in **Quiz Panel 2** from the Canvas |
| On Completed (UnityEvent) | Leave empty for now - wire up when you decide the outcome |

The `InteractLabel` is hardcoded to "Read Note" in PuzzleProp. If you want a different
label (e.g. "Inspect Worksheet"), change the property in `PuzzleProp.cs`:
```csharp
public string InteractLabel => "Inspect Worksheet";
```

---

## How the BKT selection works for Quiz 2

`QuestionSelector` applies the same `FirstSessionPlan` (3x arrays_and_lists + 2x complexity_big_o)
on the first interaction. Since `quiz_data_2.json` has neither of those concepts, the first-session
plan returns 0 questions and falls back to a random sample of 5 from the new bank. This is the
intended fallback and gives the player an initial spread across hash_tables, trees_bst, and basic_sorts.

On subsequent interactions, BKT picks the weakest concept across the full session history
(BKT state is on PlayerMetricsTracker which persists across scenes). Both quiz props share
the same BKT instance, so answers on Quiz 1 and Quiz 2 all update the same knowledge model.

---

## Wiring the outcome later

When you decide what Quiz 2 gates, drag the target component into the `On Completed` event
slot on PuzzleProp. Example - unlock a door:

1. In Inspector, click + under **On Completed**
2. Drag the DoorController GameObject into the object slot
3. Set the function to **DoorController.Unlock()**

No new scripts needed.

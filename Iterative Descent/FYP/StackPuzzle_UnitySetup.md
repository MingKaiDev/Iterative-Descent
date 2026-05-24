# Stack Puzzle -- Unity Setup Guide

## Canvas Hierarchy

```
Canvas
  Stack Panel                (inactive by default; StackPuzzleUI.cs here)
    Background               Image -- dark terminal panel
    InstructionText          TextMeshProUGUI
    MovesText                TextMeshProUGUI (hidden at tier 0/1 by StackPuzzleUI)
    ContentRow               Horizontal Layout Group
      TargetColumn           Vertical Layout Group
        TargetLabel          TextMeshProUGUI -- "TARGET STATE"
        TargetCellParent     Vertical Layout Group
                               reverseArrangement = true (TOS appears at top visually)
                               -- assign to targetCellParent
      StackColumn            Vertical Layout Group
        StackLabel           TextMeshProUGUI -- "CURRENT STACK"
        StackCellParent      Vertical Layout Group
                               reverseArrangement = true (TOS appears at top visually)
                               -- assign to stackCellParent
      TokenColumn            Vertical Layout Group
        TokenLabel           TextMeshProUGUI -- "AVAILABLE TOKENS"
        TokenButtonParent    Grid/HLG -- assign to tokenButtonParent
    ButtonRow                Horizontal Layout Group
      PopButton              Button -- "POP"
      SubmitButton           Button -- "SUBMIT"
      CloseButton            Button -- "CLOSE"
    FeedbackPopup            StackFeedbackPopup (starts INACTIVE)
      Blocker                Image, full-stretch, alpha=0, Raycast Target ON
      PopupCard              Image (dark panel)
        IconText             TextMeshProUGUI
        MessageText          TextMeshProUGUI
        OkButton             Button
```

## StackPuzzleUI Inspector Fields

| Field              | Assign to                                    |
|--------------------|----------------------------------------------|
| stackCellParent    | StackCellParent RectTransform                |
| targetCellParent   | TargetCellParent RectTransform               |
| tokenButtonParent  | TokenButtonParent RectTransform              |
| popButton          | PopButton                                    |
| submitButton       | SubmitButton                                 |
| closeButton        | CloseButton                                  |
| instructionText    | InstructionText TMP                          |
| movesText          | MovesText TMP                                |
| feedbackPopup      | FeedbackPopup (StackFeedbackPopup component) |
| stackCellPrefab    | See prefab spec below                        |
| tokenButtonPrefab  | See prefab spec below                        |

## Prefab Specs

### StackCellPrefab
- Root: Image (assign to prefab root)
  - Preferred Height: 48
  - Layout Element: flexible width
- Child: TextMeshProUGUI named exactly `ValueText`
  - Anchors: stretch/stretch, padding 8

### TokenButtonPrefab
- Root: Button
  - Preferred Width: 80, Height: 48
- Child: TextMeshProUGUI named exactly `Label`
  - Text Alignment: Centre

## Prop Setup (StackPuzzleProp)

1. Attach to a terminal mesh in the scene (or child collider):
   - BoxCollider
   - InteractableBase
   - InteractableRegistrar
   - StackPuzzleProp
2. Set `stackOverlay` to the Stack Panel in the Canvas.

## DDA Tiers at a Glance

| Tier  | Capacity | Target Depth | Junk | Move Limit | Values  |
|-------|----------|--------------|------|------------|---------|
| 0/1   | 4        | 2            | 0    | None       | Decimal |
| 2     | 5        | 3            | 1    | 8 moves    | Decimal |
| 3/4   | 6        | 3-4          | 2    | 7 moves    | Hex     |

## BKT / DDA Wiring (already done in code)

- StackPuzzleUI reads `PuzzleDDAController.GetTierForConcept(BayesianKnowledgeTracker.StacksAndQueues)`
- PlayerMetricsTracker subscribes to `StackPuzzleUI.OnStackSolved` in OnEnable/OnDisable
- BKT replays wrong attempts then correct solve on `stacks_and_queues` concept
- Observation vector extended to 25 values (indices 20-24 = stack metrics)
- ObservationSize constant updated to 25 -- update DirectorAgent Space Size when PPO is built (Story 7)

## Testing Checklist

- [ ] Open terminal at Tier 0 -- stack starts empty, 2 target values, no move counter visible
- [ ] Push tokens in reverse order of target -- verify Submit passes
- [ ] Push wrong value then pop -- verify stack updates correctly
- [ ] Try to push past capacity -- verify overflow warning appears
- [ ] Open at Tier 3 -- verify hex display (0xNN) and move counter visible
- [ ] Wrong submit twice -- verify feedback shows target on 3rd wrong attempt
- [ ] H key DDA HUD -- verify stack metrics appear after a solve

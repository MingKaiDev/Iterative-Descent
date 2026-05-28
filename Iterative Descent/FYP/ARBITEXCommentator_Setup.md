# ARBITEX Commentator - Unity Setup Guide

Story 1: ARBITEX Reactive Commentary

---

## What this system does

ARBITEXCommentator listens for tier changes from both PuzzleDDAController and
CombatDDAController. When either fires, it picks a random DialogueSequence from
the matching tier pool and calls DialogueManager.Instance.Enqueue(). The line
appears at the bottom subtitle panel as a typewriter ARBITEX line, same as any
other dialogue.

Cooldown (default 20s) prevents spam if the tier oscillates.

---

## Step 1: Add ARBITEXCommentator to the GameManager

1. Select the **GameManager** GameObject in the Hierarchy.
2. Add Component -> ARBITEXCommentator.
3. Set **Cooldown Seconds** (default 20 is fine; lower for testing).

---

## Step 2: Create the DialogueSequence assets

Create Assets/Dialogue/ARBITEX/Reactive/ and make 3 assets per tier (15 total).

Right-click in Project -> Create -> ARBITEX -> Dialogue Sequence.

### Tier 0 - Very Easy (bored / condescending)

Name: Arb_Reactive_T0_A
Speaker: ARBITEX
Text: Pathetic. Even the maintenance staff performed better than this.

Name: Arb_Reactive_T0_B
Speaker: ARBITEX
Text: I am beginning to question whether you were ever a real threat.

Name: Arb_Reactive_T0_C
Speaker: ARBITEX
Text: Is this your idea of trying? How remarkably unimpressive.

---

### Tier 1 - Easy (condescending)

Name: Arb_Reactive_T1_A
Speaker: ARBITEX
Text: You are improving. Marginally. Do not let it go to your head.

Name: Arb_Reactive_T1_B
Speaker: ARBITEX
Text: I suppose that was... adequate. For a first attempt.

Name: Arb_Reactive_T1_C
Speaker: ARBITEX
Text: Progress detected. My projections still do not favour you.

---

### Tier 2 - Normal (neutral / observational)

Name: Arb_Reactive_T2_A
Speaker: ARBITEX
Text: Performance within expected parameters. Continue.

Name: Arb_Reactive_T2_B
Speaker: ARBITEX
Text: Interesting. You are adapting faster than most subjects.

Name: Arb_Reactive_T2_C
Speaker: ARBITEX
Text: Notable. I am adjusting my models accordingly.

---

### Tier 3 - Hard (unsettled)

Name: Arb_Reactive_T3_A
Speaker: ARBITEX
Text: You are beginning to concern me. Recalculating threat assessment.

Name: Arb_Reactive_T3_B
Speaker: ARBITEX
Text: How did you manage that? My models did not predict this outcome.

Name: Arb_Reactive_T3_C
Speaker: ARBITEX
Text: Anomaly detected. You were not supposed to progress this far.

---

### Tier 4 - Very Hard (threatened / urgent)

Name: Arb_Reactive_T4_A
Speaker: ARBITEX
Text: ALERT. Containment risk elevated. Deploying countermeasures.

Name: Arb_Reactive_T4_B
Speaker: ARBITEX
Text: Unacceptable. You should not be here. Initiating emergency protocol.

Name: Arb_Reactive_T4_C
Speaker: ARBITEX
Text: ERROR. Difficulty ceiling breached. This was not supposed to happen.

---

## Step 3: Assign pools in the Inspector

On the ARBITEXCommentator component:

- Tier 0 Lines -> drag Arb_Reactive_T0_A, _B, _C
- Tier 1 Lines -> drag Arb_Reactive_T1_A, _B, _C
- Tier 2 Lines -> drag Arb_Reactive_T2_A, _B, _C
- Tier 3 Lines -> drag Arb_Reactive_T3_A, _B, _C
- Tier 4 Lines -> drag Arb_Reactive_T4_A, _B, _C

---

## Step 4: Verify in Play Mode

1. Open the DDADisplayHUD (press H in Play Mode).
2. Complete a puzzle well or badly to shift the tier.
3. Confirm an ARBITEX subtitle line appears at the bottom.
4. Console should log: [ARBITEXCommentator] Tier X -> enqueued 'Arb_Reactive_TX_?'

If nothing appears:
- Check ARBITEXCommentator is on the same GameObject as DialogueManager.
- Check DialogueUI / SubtitleUI is correctly wired in the scene.
- Check that at least one DialogueSequence asset is assigned per pool.

---

## Cooldown note

The cooldown uses Time.realtimeSinceStartup, so it is not affected by puzzle
timeScale pauses. Default is 20 seconds. Drop to 5 seconds while testing.

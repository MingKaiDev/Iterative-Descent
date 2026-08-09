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

**Story 10 addition (2026-08-09):** before this system, ARBITEX could speak its
first-ever line mid-way through the very first quiz in Main Hall, with zero
introduction -- a disembodied voice suddenly commenting on the player with no
context for who or what it is. `ARBITEXCommentator` now also plays a one-time
`introSequence` on the first enemy death of the session (`EnemyBase.OnAnyEnemyDied`),
normally the Combat 1 breach enemy, and suppresses ALL tier-change commentary
entirely until that intro has fired. Any tier change that happens before the
intro is silently dropped, not queued.

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

## Step 2b: Create the Intro sequence (Story 10)

Create Assets/Dialogue/ARBITEX/Arb_Intro.asset (already created on disk this
session -- confirm it shows up after Unity reimports, this step is just for
reference / re-authoring if needed).

Right-click in Project -> Create -> ARBITEX -> Dialogue Sequence, name it
`Arb_Intro`, sibling to the Reactive/ folder (not inside it -- this is a
one-time story beat, not part of the random-pick reactive pool).

Entries (speaker changes mid-sequence on purpose -- the reveal moment):

Speaker: ???
Text: Well. That's new. Most test subjects don't survive first contact.

Speaker: ARBITEX
Text: I am ARBITEX. I administer this facility, and everything inside it. Including you.

Speaker: ARBITEX
Text: Let's see if you're worth the rest of my attention.

`SubtitleUI`'s speaker-colour map has no entry for `???`, so it falls back to
the default (white) -- then flips to ARBITEX's red on the second line, which
visually reinforces the reveal. No SubtitleUI changes needed for this to work.

---

## Step 3: Assign pools in the Inspector

On the ARBITEXCommentator component:

- Intro Sequence -> drag Arb_Intro
- Tier 0 Lines -> drag Arb_Reactive_T0_A, _B, _C
- Tier 1 Lines -> drag Arb_Reactive_T1_A, _B, _C
- Tier 2 Lines -> drag Arb_Reactive_T2_A, _B, _C
- Tier 3 Lines -> drag Arb_Reactive_T3_A, _B, _C
- Tier 4 Lines -> drag Arb_Reactive_T4_A, _B, _C

---

## Step 4: Verify in Play Mode

1. From a fresh scene load, shift the DDA tier (e.g. do well/badly on Quiz 1)
   BEFORE killing any enemy. Confirm NO ARBITEX line appears yet -- this is
   correct, tier commentary is suppressed until the intro fires.
2. Solve the Linked List puzzle, let Combat 1 fire, kill the breach enemy.
   Confirm the Arb_Intro sequence plays (??? -> ARBITEX, 3 lines). Console
   should log: [ARBITEXCommentator] Intro fired on first enemy death --
   reactive commentary now active.
3. Open the DDADisplayHUD (press H in Play Mode).
4. Shift the tier again (any puzzle or combat). Confirm an ARBITEX subtitle
   line now appears at the bottom.
5. Console should log: [ARBITEXCommentator] Tier X -> enqueued 'Arb_Reactive_TX_?'

If nothing appears:
- Check ARBITEXCommentator is on the same GameObject as DialogueManager.
- Check DialogueUI / SubtitleUI is correctly wired in the scene.
- Check that at least one DialogueSequence asset is assigned per pool.
- Check Intro Sequence is assigned -- if it's None, a warning logs and
  reactive commentary stays suppressed forever (by design, this is the
  simplest way to guarantee no reactive line ever plays without the intro
  having fired first).

---

## Cooldown note

The cooldown uses Time.realtimeSinceStartup, so it is not affected by puzzle
timeScale pauses. Default is 20 seconds. Drop to 5 seconds while testing.

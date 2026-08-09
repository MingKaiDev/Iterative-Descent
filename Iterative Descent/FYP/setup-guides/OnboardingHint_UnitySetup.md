## Onboarding Control Hints -- Setup Checklist (Story 31)

Scene: Assets/Scenes/Level 1.unity

Code is done: `Assets/Scripts/UI/OnboardingHintUI.cs` (new), plus one new call each
in `EnemyEventBridge.cs` (Combat 1 -- Main Hall door breach, the actual first
encounter any player reaches) and `EncounterTrigger.cs` (Combat 2/3 -- Classroom 1
entry and Hallway 1 pre-shutter, in case a build ever lets the player reach those
before Combat 1 somehow). `ShowCombatHintOnce()` is idempotent, so both call sites
existing is safe -- whichever fires first is the one that actually shows. The
Canvas panel itself is NOT hand-authored in the scene YAML -- build it in the
Editor following the steps below, same as Story 27/28's panels were.

---

## What this does

Two short, non-blocking text hints, ASCII only (no unicode arrows/dashes per
CLAUDE.md):

- On spawn: "WASD Move | Mouse Look | E Interact | ESC Pause" -- disappears the
  moment the player presses a movement key, or after 12s if they never do.
- On the first enemy encounter the player actually reaches -- normally Combat 1
  (Main Hall door breach, via `EnemyEventBridge`), with Combat 2/3 (`EncounterTrigger`)
  as a fallback hook in case a build path skips Combat 1: "RMB Aim | LMB Shoot |
  R Reload | 1/2 Swap Weapon" -- disappears after 6s.

Neither hint pauses the game, unlocks the cursor, or blocks input. They are pure
HUD text, same layer as the health bar. Both are strictly one-shot per play session
(not per scene reload vs per level -- if you die and the scene reloads, they will
show again, same as any other session-scoped state in this game).

---

## Step 1 -- Create the panel

1. Under the main Canvas, add a new empty GameObject: `Onboarding Hint Panel`.
   Suggested placement: top-center of screen, safely clear of the subtitle panel
   (bottom) and the interact prompt (usually center-low) so they never overlap.
2. Add a child `TextMeshProUGUI` object, e.g. `HintLabel`. Center-aligned, readable
   size (24-32pt), maybe a semi-transparent dark backing panel behind it for
   legibility over bright rooms.
3. Attach `OnboardingHintUI` to `Onboarding Hint Panel`.
4. Assign `Hint Label` in the Inspector to the TMP text child.
5. **`Onboarding Hint Panel` itself must stay ACTIVE (checked) in the Inspector at
   all times.** Do not uncheck it. Only `HintLabel` toggles visibility -- the
   script does this automatically in code (`Awake()` hides it, `Display()`/
   `Update()` show/hide it). If the parent panel is ever left unchecked, the whole
   script goes dormant: `Awake()`/`OnEnable()` never run, so it never subscribes
   to the spawn event and nothing will ever appear. (This bit the first pass --
   see project gotcha #9 in memory/feedback-unity-gotchas.md. `HintLabel` itself
   is fine left either state, the script forces it inactive at start regardless.)

## Step 2 -- Confirm defaults

The two hint strings and their timers are set in the Inspector with sensible
defaults (see script header). Adjust wording/timing here if needed -- no code
change required for text/timing tweaks.

## Step 3 -- Playtest

- Load into Level 1 fresh. Confirm the movement hint appears within a frame or two
  of spawn, and disappears the instant you press W/A/S/D (or after 12s idle).
  Confirm it does NOT visually collide with the intro dialogue subtitle line
  ("Ugh my head... Where am I?...").
- Solve the Linked List puzzle to trigger the Main Hall door breach (Combat 1) --
  this is the real first encounter in a normal playthrough. Confirm the combat
  hint appears once, a couple seconds after the door opens (matches
  `EnemyEventBridge.activationDelay`). Then continue to Classroom 1 / Hallway 1 and
  confirm it does NOT reappear on either of those later encounters.
- Open a quiz terminal or any puzzle overlay while a hint is still showing --
  confirm the hint panel disappears immediately (PlayerInteractor.IsPaused check)
  and does not bleed through behind the puzzle UI.
- Die and let the scene reload -- confirm both hints are eligible to show again
  from a clean state.

## Known non-goals (explicitly out of scope for this pass)

- Not a blocking/forced tutorial -- a player can still walk away or interact before
  the hint times out. That was a separate open question (Story 31) the user chose
  not to force for now.
- Does not touch `PuzzleProp.InteractLabel` (still hardcoded to "Read Note" on every
  quiz terminal) or the Unicode arrow/em-dash characters in `SelectionPromptUI.cs`
  line ~52. Both are real, already-identified bugs, deliberately left unfixed this
  pass -- user chose to scope this session to the on-screen control prompt only.

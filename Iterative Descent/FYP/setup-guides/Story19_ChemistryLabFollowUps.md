Story 19 -- Chemistry Lab (Reagent Routing) Follow-Ups
Unity Scene Setup Checklist

======================================================
OVERVIEW
======================================================

Three follow-up tasks flagged during and after the Reagent Routing
(Chemistry Lab / Dijkstra's Algorithm) puzzle build, playtest (tiers 0-4,
all passed), and completion. None of these block the puzzle working
today -- they are polish/completeness items raised in conversation.

Puzzle: Reagent Routing (Chemistry Lab, Dijkstra's Algorithm)
Room: GameObject "Chemistry Labortary" (typo) in Assets/Scenes/Level 1.unity
Related files: ReagentRoutingUI.cs, ReagentRoutingProp.cs,
ReagentRoutingEventHandler.cs, PlayerCombat.cs, ConceptTutorials.cs

NOTE ON PERSISTENCE: no save/load system exists anywhere in this project
yet (checked -- no SaveManager, no PlayerPrefs usage found). Anything
below described as "already solved" or a "permanent" state only holds for
the current play session, same as every other static-flag pattern already
in the game (e.g. PasswordEventHandler's FolderPuzzleSolved). True
cross-launch persistence would be a separate, much larger undertaking
affecting every puzzle in the game, not scoped here.


======================================================
TASK 1 -- ALREADY-SOLVED GATE
======================================================

Problem: the Chemistry Lab terminal is currently re-enterable and
re-solvable indefinitely. ReagentRoutingEventHandler's _rewardGranted flag
stops the barrel attachment from re-applying, but the puzzle itself can
still be opened and re-solved endlessly, which:
  - lets the player re-roll a fresh graph on demand at no cost
  - keeps firing OnReagentRoutingSolved into PlayerMetricsTracker/BKT every
    time, which may not be wanted once the concept is "learned"

Scope (session-lifetime only, per the persistence note above):
  1. Add a flag marking the puzzle solved -- either a static bool on
     ReagentRoutingUI (e.g. HasBeenSolved), or an instance flag on
     ReagentRoutingProp. Same pattern as PasswordEventHandler's
     FolderPuzzleSolved.
  2. Set it inside ReagentRoutingUI.EndPuzzle() alongside the existing
     OnReagentRoutingSolved?.Invoke(_attempts) call.
  3. ReagentRoutingProp.Interact() / OpenPuzzle() checks the flag first.

OPEN QUESTION (need your call before building): once solved, should the
terminal be
  (a) fully locked out -- InteractLabel changes to something like
      "Terminal Offline", no further interaction, or
  (b) still openable to review/replay, but skip re-firing
      OnReagentRoutingSolved and re-granting anything?
These are meaningfully different UX. Once this exists, _rewardGranted on
ReagentRoutingEventHandler likely becomes redundant and can be removed --
it only exists today to patch over this exact gap.


======================================================
TASK 2 -- DIJKSTRA TUTORIAL
======================================================

Problem: ReagentRoutingProp already calls
ConceptTutorials.ShowIfUnseenThenContinue("dijkstra", ...), but "dijkstra"
has no entry in ConceptTutorials.cs's _captionText dictionary, so the call
currently no-ops silently (falls straight to onDone, no panel ever shows).

Two parts, different sizes:

1. Caption text -- small, self-contained. Add a "dijkstra" entry to
   _captionText, ASCII only, no em dash, matching the tone/length of the
   existing entries (closest match: "bfs_dfs", since Reagent Routing is
   the direct sequel concept to Drain). Draft for your review, not final:

     "Not every path costs the same. Dijkstra's algorithm finds the
     cheapest route through a network by always expanding the nearest
     unvisited point first, locking in its true cost before moving on.
     Wander down the wrong branch and you simply backtrack -- the
     algorithm never has to guess twice about a point once its cost is
     settled."

2. Diagram -- bigger, NOT scoped in this task. Per ConceptTutorials.cs's
   own header comment, only "arrays_and_lists" has a diagram built into
   the ConceptTutorialUI prefab right now. Every other concept, including
   "bfs_dfs" which Drain already uses, has caption text ready but is ALSO
   currently skipped for the exact same reason (no diagram child = no
   panel shown). This is a pre-existing project-wide gap, not something
   new to Reagent Routing -- adding a "dijkstra" diagram in isolation
   without deciding how the other already-skipped concepts get theirs
   might be worth a single project-wide pass instead of a one-off here.


======================================================
TASK 3 -- RETICLE REDUCTION / GUN BARREL (damage scaling)
======================================================

Problem: raised while designing the barrel-attachment reward, then
explicitly put on hold. Idea: scale bullet damage by AccuracyT (how
settled/tight the reticle currently is), so a fully-settled shot also hits
harder, not just lands more accurately.

Status: nothing built. A TODO comment already marks the intended hook
point next to the AccuracyT property in PlayerCombat.cs.

Scope when picked back up:
  1. Decide the scaling curve/range -- e.g. Lerp from 1x damage at
     AccuracyT = 0 up to some multiplier at AccuracyT = 1. Needs a design
     number from you.
  2. Apply it in PlayerCombat.FireBullet(), where `bullet.damage = damage;`
     is currently a flat assignment. Change to something like
     `bullet.damage = damage * damageMultiplier;` where damageMultiplier is
     derived from AccuracyT per the chosen curve.
  3. Decide whether this becomes part of the existing barrel attachment
     reward (i.e. ReduceSettleTime also unlocks this), or is a fully
     separate mechanic/unlock.

## Quiz Briefing HUD + Password Reveal + 60% Threshold -- Playtest Checklist
Scene: Assets/Scenes/Level 1.unity

Everything is already built -- code, the Quiz Briefing Panel prefab, and the scene
wiring. Nothing left to build by hand in the Editor. This doc is now just what to check
when you next open Unity.

---

## What's already done (no action needed)

- `PasswordEventHandler.HandlePuzzleFinished()`: 60% pass threshold (was 100%).
- `PuzzleUI` completion screen shows "Access granted. Password: X" when passed, for
  quizzes that have a password wired.
- `PuzzleProp` gained `briefingMessage` and `passwordScreenToReveal` fields.
- `Prop_Desk_Folder` (the actual folder/password quiz prop -- not `Quiz1EventHandler.cs`,
  which is dead code unattached to anything in this scene) already has both fields set:
  `briefingMessage` = "Solve the quiz to get the password", `passwordScreenToReveal` ->
  the scene's `PasswordScreenUI` component. You wired this yourself in the Editor.
- `Assets/Prefab/UI/Quiz Briefing Panel.prefab` exists (hand-authored, verified) and is
  already instanced as a child of the Canvas in `Level 1.unity`.

---

## Before you start -- read this

The folder quiz is not the only quiz that opens the shared "Quiz Panel". Two other props
in the scene (both named `Prop_Noticeboard_Paper_2`, pinned to `processes_threads`) reuse
the exact same panel and fire the exact same completion event. `PasswordEventHandler`
listens to that event globally, not per-prop. This means passing either of those two
noticeboard quizzes at 60% or higher will ALSO unlock the password auto-type on the
computer, exactly as passing the folder quiz would -- this was already true before this
change (it required 100% instead of 60%), the lower threshold just makes it easier to
trigger by accident. The password text on the completion screen itself will NOT show for
those two props, and neither will the briefing notice, since only `Prop_Desk_Folder` has
`briefingMessage`/`passwordScreenToReveal` set.

If this cross-triggering is not what you want, say so and it can be scoped down to
`Prop_Desk_Folder` specifically.

---

## Step 1 -- Open Unity and let it reimport

Since the prefab and scene were edited outside the Editor, open the project and let
Unity finish importing/recompiling before doing anything else. Check the Console for
errors on load -- there shouldn't be any, but confirm before testing.

## Step 2 -- Sanity-check the panel once, visually

Select `Quiz Briefing Panel` under the Canvas in the Hierarchy. Confirm in the Inspector:
- `QuizBriefingUI` component has `Message Text` and `Continue Button` both assigned (not
  None/Missing).
- The panel is inactive by default (unchecked box next to its name).

## Step 3 -- Playtest the full flow

Interact with `Prop_Desk_Folder` from fresh. Confirm:
- The briefing notice appears first: "Solve the quiz to get the password", cursor
  unlocked, game paused.
- Clicking Continue closes it and the quiz (or any unseen concept tutorial, if queued)
  opens immediately after -- no stray frame of unpaused gameplay.
- Pressing Esc while the briefing is open closes it the same way Continue does.
- Answer 3 of 5 questions correctly. Completion screen should read "Access granted.
  Password: password" (the scene's actual configured password).
- Answer fewer than 3 correctly on a separate attempt: completion screen should fall back
  to "Finished! X / 5 correct." with no password shown.
- Open the PC and the password panel: it should auto-type `password` and log in
  successfully.

## Step 4 -- Confirm the two noticeboard props still behave normally

Interact with either `Prop_Noticeboard_Paper_2` prop. Confirm it skips the briefing
entirely and goes straight to (concept tutorial, then) its quiz, same as before this
change -- and that its completion screen never shows password text.

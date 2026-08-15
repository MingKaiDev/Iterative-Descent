# Boss Fight Polish - Unity Setup

Covers the wiring for three changes made in code:

1. ARBITEX line on arena entry (replaces the "smooth the trigger" ask - see note below).
2. Boss sound effects (attacks, phase 2, death, encounter stinger, footsteps).
3. Boss death unlocks the front (courtyard) gate and shows an end-of-demo panel.

None of this is playtested yet. Treat every step below as unverified until you've
run it in the editor.

## Note on the trigger smoothing ask

The original ask was to make BossEncounterTrigger / the Idle-to-Combat snap feel
smoother. That was dropped in favor of an ARBITEX line firing on arena entry
instead (see step 2 below). If the instant HUD pop-in or the instant Idle-to-Combat
snap still feels rough after the dialogue line is in, that's a separate follow-up,
not something this pass touches.

## 1. Boss SFX - no audio assets exist yet

Assets/Audio currently has folders for Gun, Enemy, Player, Event, Main Menu, and
Voice. There is no Boss folder and no boss SFX clips anywhere in the project.
Every AudioClip field below defaults to null and will log a warning (or just stay
silent, for the per-attack clips) until you assign something. I have not
fabricated or guessed at placeholder audio - you need to source or record clips
and import them before any of this makes sound.

Suggested folder: `Assets/Audio/Boss/`

## 2. BossAudio component (DROID-7 root GameObject)

1. Select the DROID-7 root GameObject (same object as BossStateMachine, BossHealth,
   BossAttackRegistry, and Animator).
2. Add Component -> BossAudio. This requires an AudioSource - Unity will add one
   automatically via `[RequireComponent]`.
3. Assign clips as you get them:
   - `Encounter Stinger Clip`
   - `Footstep Clip` (see step 5 for wiring this one up)
   - `Phase Two Clip`
   - `Death Clip`
   - `Boss Music Clip` (looping fight music - see step 2b)
4. Tune the five volume sliders if needed (all default to sensible levels).

## 2b. Boss music (new)

`BossAudio` now has a `Boss Music Clip` field. It does NOT get its own
AudioSource for this - boss music is just a different track on the scene's
existing BGM source, owned by `GameAudioManager` (the same component that
plays ambient noise). `BossAudio` only holds the clip reference and forwards
to `GameAudioManager.PlayBossMusic()` / `StopBossMusic()`.

1. Nothing extra to add on the DROID-7 root for this - no second AudioSource
   component needed.
2. Assign `Boss Music Clip` on `BossAudio` (same Inspector panel as the other
   boss clips) and tune `Music Volume` (default 0.6) if needed.
3. Make sure a `GameAudioManager` exists in the scene (it already should, for
   ambient noise) - if `GameAudioManager.Instance` is null, boss music logs a
   warning and silently does nothing, same as `bossAudio` being unassigned.

Behavior:

- `BossEncounterTrigger` calls `bossAudio.PlayBossMusic()` at the same moment
  it plays the encounter stinger. If `bossAudio` isn't assigned on the
  trigger, or `Boss Music Clip` isn't assigned on `BossAudio`, this is a no-op
  (logs a warning) - same "silent until you assign a clip" pattern as
  everything else in this file.
- Boss music hard-swaps `GameAudioManager`'s single BGM AudioSource (no
  crossfade): the ambient clip stops and the boss clip plays in its place
  immediately, and it swaps back to the ambient clip automatically when the
  boss dies (`HandleBossDeath()` calls `StopBossMusic()`).
- No Phase 2 music swap yet - single track for the whole fight.
- No boss music asset exists yet - same as the SFX clips, you need to source
  or import one into `Assets/Audio/Boss/` and drag it in.

## 3. Per-attack windup / hitbox-open clips

`BossAttackBase` now has two optional AudioClip fields that every attack
component inherits: `Windup Clip` (plays the instant the attack starts) and
`Hitbox Open Clip` (plays at the moment the attack becomes dangerous - the same
frame damage is dealt). Both are silent if left unassigned.

Set these per attack component on the DROID-7 root:

- SlashAttack
- LeftPunchAttack
- StabAttack
- BladeSweepAttack
- GroundSlamAttack
- PounceAttack
- CoreOverloadAttack
- SprintChargeAttack

SprintChargeAttack is a special case: its `OnHitboxOpen()` override is
deliberately empty (left that way on purpose, for damage-broadcast reasons, not
touched by this pass). Its `Hitbox Open Clip` now plays from inside the charge
coroutine instead, at the exact frame the player is hit - same field, different
call site, no separate Inspector field to worry about.

CoreOverloadAttack is also a special case: the laser beam is sustained over
several seconds (extend + hold), not an instant hit like the others, so a
one-shot clip would end on its own before the beam visually does. It now has a
`Laser Loop Sound` section with its own `laserLoopClip` field, played on a
**separate AudioSource that must live on the LaserOrigin child GameObject**
(the same one that holds the LineRenderer) - not the shared boss AudioSource -
so it can be stopped independently the instant the beam ends, without cutting
off any other boss sound playing at the same time.

1. Select the LaserOrigin child GameObject (child of the DROID-7 head bone).
2. Add Component -> Audio Source if it doesn't have one yet.
3. On the CoreOverloadAttack component, assign `Laser Loop Clip`.

If you skip this, CoreOverloadAttack logs a warning at Start() and the beam
stays silent except for the one-shot `Hitbox Open Clip` - nothing breaks.

## 4. ARBITEX boss-intro dialogue

A new DialogueSequence asset was hand-authored at
`Assets/Dialogue/ARBITEX/Arb_BossIntro.asset`, following the exact same format
as `Arb_Intro.asset`. Single line:

> ARBITEX: "This is it. There is no escape for you."

1. Open the scene with BossEncounterTrigger (the arena-entrance trigger collider).
2. Select it and find the new `Boss Intro Dialogue` field.
3. Drag in `Arb_BossIntro.asset`.
4. Optionally also assign the new `Boss Audio` field (drag the BossAudio
   component from the DROID-7 root) if you want the encounter stinger to play
   alongside the line.
5. Verify DialogueManager exists in the scene (it should already, on
   GameManager) - if `DialogueManager.Instance` is null the trigger logs a
   warning and the line silently won't play.

This line does not pause the player - it has no choices, so it types out and
auto-dismisses while combat starts underneath it, same as any other narration
line without choices.

## 5. Boss footsteps (optional, needs an Animation Event)

`BossAudio.PlayFootstep()` is a public method meant to be called from an
Animation Event, exactly like `EnemyChaserAudio.PlayWalkStep()`:

1. Open the boss's walk/run animation clip in the Animation window.
2. Add an Animation Event at each footfall frame.
3. Set the event's function to `PlayFootstep`.

Skip this if you don't have a footstep clip yet - nothing else depends on it.

## 6. Front gate unlock + end-of-demo panel

### Confirm the gate target

Per your call, the gate to unlock is the existing courtyard gate - the
`Door_South` GameObject that's already wired to `GateDisableProp` (the
Hallway 2 control panel puzzle). `Door_South` already has a `DoorController`
component (`isLocked: 1`, swing-open animation, optional `doorBreakClip`) - this
is the one `BossDeathSequence` should call `Unlock()` on. It's already being
hidden by an unrelated puzzle elsewhere in the game (GateDisableProp just calls
SetActive(false) on it directly) - that's a separate system and this change
doesn't touch it. If the player already solved the Hallway 2 panel puzzle before
reaching the boss, the gate GameObject may already be inactive; `DoorController.Unlock()`
on an inactive GameObject is harmless (it just won't be visible), but flag this
to me if it turns out to be a problem in playtesting - it weakens the "boss
death reveals the gate" beat.

### Create the End Of Demo panel

1. On your main UI Canvas (or a new dedicated Canvas), create a panel: background,
   a TMP text ("Demo Complete - Thanks for playing!"), and a "Return to Menu"
   button. Set it inactive by default, same as the Death Screen panel.
2. Attach `EndOfDemoUI` to the panel (or its Canvas - assign `panel` if the
   script isn't on the panel itself).
3. Wire the button's On Click() to `EndOfDemoUI.ReturnToMenu()`.

### Create BossDeathSequence

1. Attach `BossDeathSequence` to any persistent GameObject (DROID-7 root or
   GameManager both work - it just subscribes to the static `BossHealth.OnBossDeath`
   event, no direct reference needed either way).
2. Assign `Front Gate`: the `DoorController` component on `Door_South`.
3. Assign `End Of Demo UI`: the `EndOfDemoUI` component from step above.
4. Default timing is 3s before the gate unlocks, then 1.5s more before the panel
   shows - tune both if the death animation needs more room to breathe.

## 7. Playtest checklist

- [ ] Enter the boss arena - HUD appears, ARBITEX line plays, stinger plays (if assigned).
- [ ] Boss music starts on arena entry (if assigned) and the ambient loop cuts out.
- [ ] Boss death: ambient loop resumes once boss music stops.
- [ ] Each attack's windup/impact sounds play (if clips assigned) and don't cut
      each other off oddly.
- [ ] Phase 2 transition plays its sound at the HP threshold.
- [ ] Boss death: death sound plays, then gate opens ~3s later, then the
      end-of-demo panel appears ~1.5s after that.
- [ ] "Return to Menu" button loads scene 0 correctly.
- [ ] Confirm scene 0 is actually still your Main Menu scene in Build Settings -
      this was assumed from DeathScreenUI's existing convention, not re-verified here.

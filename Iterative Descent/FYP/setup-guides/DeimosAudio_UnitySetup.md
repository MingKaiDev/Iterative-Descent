# Deimos Audio - Unity Setup

Deimos's equivalent of `FYP/setup-guides/BossPolish_UnitySetup.md` section 1-3/5 (ARES's boss
audio pass), scoped to what Deimos actually has right now: Move 1 (Idle/Combat/Walk FSM) and
Move 2 (MMAKick + Dropkick attacks) are code-complete but not yet playtested -- see
`project-deimos-boss.md`. None of this is playtested either. Treat every step below as
unverified until you've run it in the editor.

## 1. Deimos SFX - no audio assets exist yet

Same situation as ARES was before `BossPolish_UnitySetup.md`: `Assets/Audio` has no Deimos
folder and no Deimos SFX clips anywhere in the project. Every AudioClip field below defaults to
null and will log a warning (or just stay silent, for the per-attack clips) until you assign
something.

Suggested folder: `Assets/Audio/Deimos/`

## 2. Per-attack windup / hitbox-open clips -- ALREADY WIRED, just needs clips

Unlike ARES, this part doesn't need any new code. `DeimosAttackBase` already carries the same
two optional AudioClip fields `BossAttackBase` gives every ARES attack: `Windup Clip` (plays the
instant the attack starts) and `Hitbox Open Clip` (plays at the moment the attack becomes
dangerous -- the same frame damage is dealt). Both are silent if left unassigned, and every
Deimos attack inherits them automatically.

Set these per attack component on the Deimos root:

- MMAKickAttack
- DropkickAttack

Any future attack (JumpBack, JumpForward, Superhuman Choke Lift, Flying Knee) gets the same two
fields for free the moment it's added as a `DeimosAttackBase` subclass -- nothing else to wire.

## 3. DeimosAudio component (Deimos root GameObject)

A new script, `Assets/Scripts/Audio/DeimosAudio.cs`, mirrors `BossAudio.cs` for the non-attack
cues -- combat-start stinger, footsteps, phase 2, death, combat music.

1. Select the Deimos root GameObject (same object as DeimosStateMachine, DeimosHealth,
   DeimosAttackRegistry, and Animator).
2. Add Component -> DeimosAudio. This requires an AudioSource -- Unity will add one
   automatically via `[RequireComponent]`.
3. Assign clips as you get them:
   - `Combat Stinger Clip`
   - `Footstep Clip` (see step 5 for wiring this one up)
   - `Phase Two Clip`
   - `Death Clip`
   - `Combat Music Clip` (looping fight music -- see step 4)
4. Tune the five volume sliders if needed (all default to sensible levels).

**No Inspector wiring needed beyond this** -- this is the one place Deimos's setup is actually
simpler than ARES's. ARES needs `BossEncounterTrigger` because entering its arena is its own
scripted moment (HUD, sealed doors, intro dialogue, Packet Filter gating all hang off that same
trigger collider), so `BossAudio.PlayEncounterStinger()`/`PlayBossMusic()` have to be called
from there explicitly. Deimos has no equivalent arena-entry trigger -- it just detects the
player itself via `DeimosStateMachine.detectionRadius` and walks into Combat -- so instead
`DeimosStateMachine` now fires a new static `OnCombatStart` event the instant that happens (same
moment the "FightStarted" Animator trigger fires), and `DeimosAudio` self-subscribes to it, the
same way it already self-subscribes to `DeimosHealth.OnPhaseTwo`/`OnBossDeath`. Add the
component, assign clips, done.

## 4. Combat music

Same `GameAudioManager` hard-swap pattern ARES uses -- Deimos doesn't get its own AudioSource
for this either. `DeimosAudio` only holds the `Combat Music Clip` reference and forwards to
`GameAudioManager.PlayBossMusic()` / `StopBossMusic()` (reusing ARES's method names rather than
adding Deimos-specific ones -- the two bosses never fight at the same time, so one shared BGM
hard-swap is enough).

1. Nothing extra to add on the Deimos root for this -- no second AudioSource component needed.
2. Assign `Combat Music Clip` on `DeimosAudio` and tune `Music Volume` (default 0.6) if needed.
3. Make sure a `GameAudioManager` exists in the scene Deimos's arena lives in (it already should,
   for ambient noise) -- if `GameAudioManager.Instance` is null, combat music logs a warning and
   silently does nothing, same as `combatMusicClip` being unassigned.

Behavior:

- `DeimosStateMachine` fires `OnCombatStart` the instant Deimos enters Combat; `DeimosAudio`
  reacts by playing the stinger and starting combat music together, same pairing as ARES's
  `BossEncounterTrigger` call site.
- Combat music hard-swaps `GameAudioManager`'s single BGM AudioSource (no crossfade): the
  ambient clip stops and the combat clip plays in its place immediately, and it swaps back to
  the ambient clip automatically when Deimos dies (`HandleBossDeath()` calls `StopCombatMusic()`).
- No Phase 2 music swap -- single track for the whole fight, same as ARES.
- No Deimos combat music asset exists yet -- same as the SFX clips, source or import one into
  `Assets/Audio/Deimos/` and drag it in.

## 5. Deimos footsteps (optional, needs an Animation Event)

`DeimosAudio.PlayFootstep()` is a public method meant to be called from an Animation Event,
exactly like `BossAudio.PlayFootstep()` / `EnemyChaserAudio.PlayWalkStep()`:

1. Open the `Walk` clip in the Animation window (the one tagged "Locomotion" alongside `Idle`).
2. Add an Animation Event at each footfall frame.
3. Set the event's function to `PlayFootstep`.

Skip this if you don't have a footstep clip yet -- nothing else depends on it.

## 6. Playtest checklist

- [ ] Walk into detection range -- Deimos enters Combat, combat stinger plays (if assigned).
- [ ] Combat music starts the same moment and the ambient loop cuts out.
- [ ] Deimos death: ambient loop resumes once combat music stops.
- [ ] MMAKick and Dropkick windup/impact sounds play (if clips assigned) and don't cut each
      other off oddly -- also confirm the Animation Events these attacks still need
      (`OnHitboxOpen` / `OnAttackAnimEnd` on each clip, see `project-deimos-boss.md`) are in
      place, since without them the attacks currently deal no damage regardless of audio.
- [ ] Phase 2 transition plays its sound at the HP threshold (currently just a sound cue --
      DeimosHealth's Phase 2 event has no behavioural reaction yet, same as the sound itself is
      forward-looking until clips exist).
- [ ] Deimos death: death sound plays.
- [ ] Footsteps play on Walk, once wired.

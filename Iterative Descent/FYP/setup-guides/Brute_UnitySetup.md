# Brute Enemy — Unity Setup Guide

Companion to `Assets/Scripts/Enemy/EnemyBrute.cs` and `Assets/Scripts/Enemy/BruteAttack.cs`.
Mirrors the existing CyberSoldier (`EnemyChaser`/`EnemyAttack`) and Rusher
(`EnemyRusher`/`RusherAttack`) wiring pattern — read `MeleeHitbox_UnitySetup.md` and
`CyberSoldier_AttackAnimation_UnitySetup.md` first if anything below is unfamiliar.

Assumes a fresh, unmodified Mixamo humanoid rig (confirmed with Ming Kai) — standard
bone names, separate left/right hand bones available.

---

## 1. Rename the root GameObject

The Brute's root is currently named **"Unarmed Idle"** (the Mixamo idle clip name — this
is what a freshly-imported/dragged-in rig defaults to before anyone renames it). Rename it
to something like `Brute` or `EnemyBrute_01` before wiring anything else, so it doesn't get
confused with the actual "Unarmed Idle" *animation clip* inside the Animator Controller.

## 2. Components on the root GameObject

Same layout as CyberSoldier/Rusher:

- `NavMeshAgent` (required by `EnemyBase`)
- `EnemyBrute` (requires `BruteAttack` automatically)
- `BruteAttack`
- `Animator` — either on the root or found via `GetComponentInChildren<Animator>()` on a
  child rig object, same as the other enemies.

Set `NavMeshAgent.Speed`/`Angular Speed`/`Stopping Distance` however you like in the
Inspector — `EnemyBrute.Awake()` overwrites `speed` and `stoppingDistance` at runtime
anyway (`chaseSpeed`, `attackRange - 0.1`), so the Inspector values only matter before Play.

## 3. Animator Controller — parameters

Create a new Animator Controller for the Brute (or duplicate CyberSoldier's/Rusher's as a
starting point) with these parameters:

| Parameter | Type | Fired by |
|---|---|---|
| `Speed` | Float | `EnemyBrute` every frame (drives locomotion blend) |
| `IsDead` | Bool | `EnemyBrute.OnDie()` |
| `Scream` | Trigger | `EnemyBrute.WakeUpRoutine()` — once per `Activate()` |
| `SwipeA_Hit1` | Trigger | `BruteAttack` — Combo A, hit 1 |
| `SwipeA_Hit2` | Trigger | `BruteAttack` — Combo A, hit 2 |
| `SwipeB_Hit1` | Trigger | `BruteAttack` — Combo B, hit 1 |
| `SwipeB_Hit2` | Trigger | `BruteAttack` — Combo B, hit 2 |
| `Grab` | Trigger | `BruteAttack` — instant-kill grab |

All eight names match the `[SerializeField]` defaults on both scripts exactly — if you
rename any of them in the Animator Controller, update the matching Inspector field too
(they're all exposed, not hardcoded).

## 4. Animator Controller — states and transitions

Add one state per clip:

| State | Clip |
|---|---|
| Idle | Unarmed Idle |
| Mutant Walk | Mutant Walk (or whatever your locomotion clip is named) |
| Scream | Zombie Scream |
| SwipeA_Hit1 | Mutant Swipe |
| SwipeA_Hit2 | Mutant Swipe (2) |
| SwipeB_Hit1 | Mutant Swipe (1) |
| SwipeB_Hit2 | Mutant Swipe (3) |
| Grab | Thrusting Attack |
| Death | (your Brute death clip, if you have one — `EnemyBase.OnDie()` doesn't require a specific clip, just the `IsDead` bool) |

**Two DIFFERENT wiring patterns apply here — do not mix them up:**

**A) Locomotion (Idle ↔ Mutant Walk) — a normal DIRECT transition pair, NOT `Any State`.**
Wire `Idle -> Mutant Walk` with condition `Speed > 0.1`, and `Mutant Walk -> Idle` with
condition `Speed < 0.1`. Both: no Exit Time, short transition duration (~0.1–0.15s) for a
clean blend. Do **not** route locomotion through `Any State` — see section 11 below for
what goes wrong if you do (this bit the first playtest).

Also check the `Mutant Walk` clip's import settings (select the FBX, Animation tab, the
specific clip) and confirm **Loop Time is checked**. If it's unchecked, the clip plays once
and holds on its last frame instead of cycling — same "stuck" symptom as the `Any State`
mis-wiring, so if the transition fix in section 11 doesn't fully resolve it, check this too.

**B) The six attacks (Scream + 4 swipes + Grab) — `Any State -> [that state]`, as before.**
This part of the original guidance is unchanged and still correct — it's specifically for
states that need to interrupt whatever the Brute is currently doing (Idle OR Walking,
unpredictably) the instant a trigger fires. This is not a stylistic preference — it's a
documented bug fix. CyberSoldier's first Punch1/Punch2 wiring attempt (`Walking -> Punch1`)
froze after a single punch because `EnemyChaser.StopAgent()` drops `NavMeshAgent.speed` to 0
the instant the enemy enters attack range, which flips the Animator to Idle via the existing
`Speed < 0.1` transition *before or while* the attack trigger is still waiting to fire from
`Walking`. Since `EnemyBrute.TickChase()`/`StopAgent()` does the exact same thing on entering
`Attacking` state, every one of its attack triggers is exposed to the identical failure mode.
`Any State ->` sidesteps it entirely (same fix already applied to CyberSoldier, see
`project-enemy-system.md` 2026-08-10).

Exit transitions (each attack state back to locomotion) can use Exit Time = clip length,
no exit time in — same pattern as the existing enemies. Note that because these ARE `Any
State` transitions, Unity's default **"Can Transition To Self" stays checked but is
harmless here** — each attack fires once via `SetTrigger`, which auto-consumes, so it
can't continuously re-fire the way a held Bool/threshold condition can (see section 11).

## 5. Fist hitboxes (swipe combos)

Under the hand bones on the rig:

```
CharacterRig (Animator)
└── ... → Hand_R bone
    └── RightFistHitbox   (empty GameObject + BoxCollider)
└── ... → Hand_L bone
    └── LeftFistHitbox    (empty GameObject + BoxCollider)
```

On each `BoxCollider`:
- `Is Trigger` = **true**
- Size/position roughly matching the fist — these are never enabled at runtime
  (`BruteAttack.Awake()` disables them), they're purely a position/size reference for
  `Physics.OverlapBox`.

Assign `RightFistHitbox` → `BruteAttack.rightFistBox`, `LeftFistHitbox` →
`BruteAttack.leftFistBox` in the Inspector.

Reminder of the combo → hand mapping (matches the "Left+Right" / "Right+Left" naming
literally):

- Combo A ("Left+Right swipe"): Hit 1 = **left** hand (Mutant Swipe) → Hit 2 = **right**
  hand (Mutant Swipe (2))
- Combo B ("Right+Left swipe"): Hit 1 = **right** hand (Mutant Swipe (1)) → Hit 2 =
  **left** hand (Mutant Swipe (3))

## 6. Grab hitbox

Thrusting Attack is a two-handed lunge, not a single-hand swing, so it gets its own
hitbox rather than reusing a fist bone — a child of the spine/chest or hips bone works
well, centred on the Brute's forward reach:

```
CharacterRig (Animator)
└── ... → Spine/Chest bone
    └── GrabHitbox   (empty GameObject + BoxCollider, Is Trigger = true)
```

Assign to `BruteAttack.grabHitbox`.

## 7. Layers

Same convention as every other enemy in this project:
- `BruteAttack.hitableLayers` → **Player** layer only.
- The Brute's own colliders (capsule, hurtbox, etc.) should be on the **Enemy** layer.

## 8. Animation Events (optional but recommended)

Add these Animation Events at the true impact frame of each clip for frame-accurate hits
instead of relying on the guessed `*Delay` fields:

| Clip | Event method |
|---|---|
| Mutant Swipe | `OnComboHit1Frame` |
| Mutant Swipe (1) | `OnComboHit1Frame` |
| Mutant Swipe (2) | `OnComboHit2Frame` |
| Mutant Swipe (3) | `OnComboHit2Frame` |
| Thrusting Attack | `OnGrabHitFrame` |

(Same method fires for both combos' hit 1 / hit 2 — `BruteAttack` already knows which
combo is in progress internally and picks the correct hitbox.) If you skip this step
entirely, the coroutine fallback timing (`comboAHit1Delay` etc.) still works — just less
precisely until tuned. Watch the `[BruteAttack] HIT CHECK ACTIVE` console log against the
swing in Play mode, same tuning technique used for CyberSoldier/Rusher.

## 9. Balance defaults — please playtest and retune

These are starting guesses, not validated numbers:

| Field | Default | Notes |
|---|---|---|
| `grabCooldown` | 16s | Main lever keeping Grab rare — a miss still burns the full cooldown |
| `grabChanceBase` | 0.12 (12%) | Chance to attempt Grab instead of a swipe when player HP is healthy |
| `grabChanceLowHealth` | 0.7 (70%) | Chance once player HP fraction ≤ `playerLowHealthThreshold` — weighted, not guaranteed, per your call |
| `playerLowHealthThreshold` | 0.3 (30% HP) | Threshold that switches between the two chances above |
| `wakeupLockDuration` | 1.6s | Should roughly match the Zombie Scream clip length |
| swipe/grab hit delays | 0.4–0.5s | All guesses — tune per clip once imported |
| `comboAHit1Delay` / `comboBHit1Delay` | 1.43s each | Real data as of 2026-08-29 — impact frame 43 of 80 (30fps) on `Mutant Swipe (1)`, confirmed by user in the Animation import inspector. `comboAHit1Delay` currently COPIES this value on the assumption that `Mutant Swiping.fbx` (Combo A Hit 1) has the same timing — it shares the identical 80-frame length and identical 0.90625 exit time in `Phobos.controller`, but its own impact frame has not been visually confirmed yet. Check it and correct if it looks off. |
| `comboAGapDuration` / `comboBGapDuration` | 0.98s each | Derived from the same real data (see section 11e) — NOT a short "beat between hits," see the Hit2-fires-early bug (11b/11d) for why |

Combo A vs Combo B is a straight coin flip each decision (your call) — the Brute can
repeat the same combo back-to-back, unlike Rusher's strict alternation.

## 10. What was deliberately left out

- **No proximity/shot auto-activation** (the feature CyberSoldier has — see
  `project-enemy-system.md` "Auto-activation" section). Not asked for here; the Brute
  currently only wakes via whatever explicit `Activate()` call you wire up
  (`EncounterTrigger`/`EnemyEventBridge`, same as Rusher). Easy to port over later if
  wanted — flag it if you want that added.
- **No windup rotation telegraph** (the feature CyberSoldier has for its punch). Rusher
  doesn't have this either, and the Brute follows Rusher's pattern here for the swipes.

## 11. Bugs found in first playtest (2026-08-29) and their fixes

Three issues surfaced the first time this was actually run in Play mode. Two were script
fixes (already applied, delivered to the repo — recompile and you have them), one is an
Animator Controller wiring fix you still need to make by hand.

### 11a. Walk animation looked like a limp, then froze after walking stopped — Animator wiring, not code

**Cause:** `Mutant Walk` had been wired the same way as the attack states — as an `Any
State -> Mutant Walk` transition — rather than as a direct `Idle <-> Mutant Walk` pair (see
section 4A above, which now calls this out explicitly). Two separate symptoms, same root
cause:

- **Limping/stutter while walking:** `Any State` transitions default to "Can Transition To
  Self" = checked. If `Mutant Walk`'s condition (e.g. `Speed > 0.1`) stays true every frame
  while walking, Unity keeps re-entering `Mutant Walk` and restarting the clip from frame 0
  instead of letting one continuous stride play out — reads exactly as a limp.
- **Frozen pose after walking stops:** with `Mutant Walk` only reachable via `Any State` and
  no direct `Mutant Walk -> Idle` transition wired, there was no path back to Idle when
  `Speed` dropped to 0 — the only thing that could move the Animator out of `Mutant Walk` at
  all was an attack trigger firing (which, being `Any State`-based, can interrupt anything).
  That's why it looked "stuck until it attacks."

**Fix (Editor-only, not yet done — do this by hand):** delete the `Any State -> Mutant Walk`
transition, wire the direct `Idle <-> Mutant Walk` pair described in section 4A instead, and
confirm `Mutant Walk`'s Loop Time is checked in its import settings. No code changes needed
for this — `EnemyBrute` already drives the `Speed` float every frame the same way
CyberSoldier/Rusher do.

### 11b. Combo A's second swipe hitbox fired before the first swipe's animation had finished — code fix, already applied

**Cause:** `BruteAttack.ComboSequence()` waited `comboAHit1Delay` (the fist hitbox timing)
then a single shared `comboGapDuration` (0.25s default) before firing the `SwipeA_Hit2`
trigger — but `comboGapDuration` was meant to cover the REST of Hit 1's clip after its
hitbox point, and 0.25s was just a guess, almost certainly shorter than the real remainder
of the `Mutant Swipe` clip. Since `SwipeA_Hit2` is (correctly) wired as `Any State ->`, it's
allowed to interrupt whatever's currently playing the instant its trigger fires — so an
undertimed gap cut `Mutant Swipe` off early and started `Mutant Swipe (2)` while the first
swing was still visibly mid-animation. Worse, the single `comboGapDuration` was shared
between Combo A and Combo B, but `Mutant Swipe` and `Mutant Swipe (1)` (their respective
Hit 1 clips) are very unlikely to be the same length — the exact same "one shared delay
can't fit two different clip lengths" mistake this project already hit once with
CyberSoldier's Punch1/Punch2 (see `project-enemy-system.md` 2026-08-10).

**Fix, already applied to `BruteAttack.cs`:** split into `comboAGapDuration` and
`comboBGapDuration` (both default 0.6s, still a guess — needs real tuning against your
clips). **How to tune them:** in Play mode, note the time the `[BruteAttack] Starting
Left+Right swipe combo.` (or `Right+Left`) log line prints, watch `Mutant Swipe` (or
`Mutant Swipe (1)`) visually finish, then set `comboAGapDuration` (or `comboBGapDuration`)
to `(that elapsed time) - comboAHit1Delay` (or `- comboBHit1Delay`). If you add the
`OnComboHit1Frame`/`OnComboHit2Frame` Animation Events from section 8, the *hit detection*
timing stops depending on these delays at all — but the gap still governs when the Hit2
*trigger* fires, so it still needs tuning either way.

### 11c. Attack animation getting cut off if the player left range mid-swing — code fix, already applied

Not separately reported as a bug, but fixed at the same time: `EnemyBrute.TickAttack()` used
to call `ResumeChase()` (which un-stops the `NavMeshAgent`) the instant the player stepped
outside `attackRange * 1.25`, even mid-swing — visually the Brute would start sliding away
while a swipe or grab animation was still playing. Fixed by adding `BruteAttack.IsAttacking`
and gating the chase resume on it (`dist > attackRange * 1.25f && !_attack.IsAttacking`), so
whatever swing or grab is already committed to finishes playing before the Brute breaks off.

### 11d. Hitbox STILL firing before the animation finishes after 11b — real cause found: 0.25s blend on every Any State attack transition

You reported this a second time after 11b's fix landed (`comboAGapDuration`/
`comboBGapDuration` still at the 0.6s default — you hadn't retuned them yet), so I went and
read the actual `Phobos.controller` asset directly instead of guessing again. Found it:
**every one of the six `Any State -> [attack state]` transitions** (`SwipeA_Hit1`,
`SwipeA_Hit2`, `SwipeB_Hit1`, `SwipeB_Hit2`, `Grab`, and `Scream`) still has Unity's
**default Transition Duration of 0.25 seconds**, with **Has Exit Time unchecked**. Nobody
touched this when the transitions were created — it's what Unity fills in automatically.

**Why this breaks hit timing no matter how well the *Delay/*Gap fields are tuned:** "Has
Exit Time = off" means the transition CAN start blending in the instant its trigger fires
(good, that's what you want for responsiveness) — but "Transition Duration = 0.25s" means it
then spends a quarter second CROSS-FADING from whatever was playing into the new clip,
rather than cutting to it instantly. During that 0.25s, the outgoing clip is still partially
blended in and visually influencing the pose. So even a perfectly-tuned `comboAGapDuration`
only controls when the *trigger fires* — the actual clip swap still smears over the next
0.25s on top of that, which reads as exactly what you're seeing: the next swing's hitbox
(fired by `comboXHit2Delay` after the trigger) can register while the blend is still washing
out the previous pose.

**Fix (Editor-only — do this by hand, same reasoning as 11a):** for each of the six
`Any State -> [attack state]` transitions in the Brute's Animator Controller (click the
transition arrow, not the state, to see these fields in the Inspector):
- Set **Transition Duration** to **0** (drag the blend bar all the way left, or type `0`
  directly into the duration field). Combat hit-reactions should snap, not crossfade — this
  is standard practice for reactive/attack states, not specific to this project.
- Leave **Has Exit Time** unchecked — that part was already correct.

This removes one whole axis of uncertainty from the timing puzzle. It does **not** replace
tuning `comboAGapDuration`/`comboBGapDuration`/the delay fields — you'll still want to do
that (section 11b's method) — but it makes the numbers you land on actually mean what the
tooltips say, instead of fighting an invisible quarter-second blend on top.

**Stronger fix, worth prioritizing now given this is the third round of hitbox-timing
reports:** wire the `OnComboHit1Frame`/`OnComboHit2Frame`/`OnGrabHitFrame` Animation Events
from section 8 onto the actual impact frame of each clip. Once those are wired, hit
detection stops depending on any guessed delay entirely — it fires exactly when the clip
says it should, immune to transition blending, immune to clip-length mismatches between
Combo A and B, immune to anything except the clip itself. The `*Delay` fields become a pure
fallback that never actually triggers. Given delay-tuning alone has now taken two rounds to
even partially work, this is the one that actually stops the guessing.

## 12. Swipe knockback (added 2026-08-29)

Both swipe combo hits (Hit 1 and Hit 2, either combo) now push the player directly away
from the Brute on landing. Grab does not knock back — it's an instant kill, there's nothing
to push.

**No Editor wiring needed** — this reuses the existing `PlayerHealth`/`PlayerMovement`
lookup (`BruteAttack.Awake()` now also caches `PlayerMovement` off the same GameObject as
`PlayerHealth`) and routes through a new `PlayerMovement.ApplyKnockback(Vector3)` public
method, which layers an external velocity on top of normal WASD movement via the
`CharacterController` (decays back to zero automatically, doesn't fight input movement).

New Inspector fields:

| Field | Component | Default | Notes |
|---|---|---|---|
| `knockbackForce` | `BruteAttack` | 6 m/s | Horizontal push speed on a landed swipe hit |
| `knockbackUpwardKick` | `BruteAttack` | 1 m/s | Small vertical component for stagger feel — set to 0 for a perfectly horizontal push |
| `knockbackRecoverySpeed` | `PlayerMovement` | 6 | How fast the push decays to zero (Lerp factor, not flat per-second) — higher = snappier |

If `PlayerHealth` and `PlayerMovement` are ever split onto different GameObjects (they
currently live on the same Player root), the knockback silently no-ops with a one-time
warning logged from `BruteAttack.Awake()` rather than throwing — check the console if hits
land but nothing pushes.

## 11e. Combo A/B Hit 1 timing tuned from real data (2026-08-29)

`comboAHit1Delay`/`comboBHit1Delay` and `comboAGapDuration`/`comboBGapDuration` were guesses
until now. Replaced with real numbers derived from the actual clips and the actual
`Phobos.controller` transitions, not another round of guessing:

- User found the true impact frame for `Mutant Swipe (1)` (Combo B, Hit 1) by scrubbing the
  clip in its Animation import inspector: **frame 43** of 80 total, at 30fps → `43 / 30 =
  1.43s`. Set as `comboBHit1Delay`.
- Read `Phobos.controller` directly (again) for the state's own return-to-idle exit
  transition (the automatic, condition-less one, not the incoming `Any State ->` trigger
  transition) — `Mutant Swipe 1`'s exit transition has `m_ExitTime: 0.90625`, i.e. it
  transitions out at 90.625% of the clip = frame 72.5 of 80. So the clip is considered
  "done" by the Animator at `72.5 / 30 = 2.417s`. `comboBGapDuration` is the remainder after
  the hit lands: `(72.5 - 43) / 30 = 0.98s`.
- `Mutant Swiping.fbx` (Combo A, Hit 1) turns out to share the **exact same** 80-frame length
  (`firstFrame: 0` / `lastFrame: 80` in its `.meta`) and the **exact same** `0.90625` exit
  time in the controller as `Mutant Swipe (1)`. Given that, `comboAHit1Delay`/
  `comboAGapDuration` were set to the **same** `1.43s`/`0.98s` as a working assumption — same
  clip length and same exit point is decent evidence the swing timing is similar, but this
  has **not** been visually confirmed frame-by-frame the way Combo B was. **Check it**: scrub
  `Mutant Swiping.fbx` (no number suffix) in its own Animation import inspector and confirm
  the fist actually connects around frame 43. If it's off, tell me the real frame and I'll
  recompute the same way.

Hit 2 delays (`comboAHit2Delay`/`comboBHit2Delay`, both still the 0.4s guess) were not part
of this request and remain untuned — `Mutant Swiping (2).fbx`/`Mutant Swiping (3).fbx` (Hit 2
for each combo) are shorter clips, 50 frames each per their `.meta` files, so do not assume
they share Hit 1's timing when you get to tuning those.

**Still the better long-term fix, not done yet:** wire the actual `OnComboHit1Frame`
Animation Event on both `Mutant Swipe (1)` and `Mutant Swiping.fbx` at their real impact
frames (you're already in the right Inspector panel for Combo B — the Events track was
visible and empty in your screenshot). Once wired, hit detection stops depending on
`comboXHit1Delay` being right at all; the numbers above are the fallback in case events
never get added, not a replacement for them.

## 13. Tuning telemetry: Anim Start/End events + Grab roll logging (added 2026-08-29)

### 13a. OnAnimStart / OnAnimEnd -- wire these on all six attack clips

Two new diagnostic-only receivers on `BruteAttack` (no gameplay effect, safe anywhere):
`OnAnimStart(string clipLabel)` and `OnAnimEnd(string clipLabel)`. On each of the six attack
clips (`Zombie Scream`, `Mutant Swipe`, `Mutant Swipe (2)`, `Mutant Swipe (1)`,
`Mutant Swipe (3)`, `Thrusting Attack`):

- Animation Event at **frame 0** → Function `OnAnimStart`, String parameter = the clip's own
  name (e.g. `Mutant Swipe (1)`).
- Animation Event at the **last frame** → Function `OnAnimEnd`, same String parameter.

Once wired, a single swing prints three real timestamps in the console:

```
[BruteAttack] ANIM START -- Mutant Swipe (1) | time=12.401
[BruteAttack] HIT CHECK ACTIVE -- Combo B Hit 1 (Mutant Swipe (1)) | time=13.834
[BruteAttack] ANIM END -- Mutant Swipe (1) | time=14.818
```

Send me those three numbers (or just the deltas) for whichever clip you're tuning and I'll
recompute the matching `comboXHit1Delay`/`comboXGapDuration`/`comboXHit2Delay` directly —
this replaces the raw-frame-math approach used for the current values (section 11e), which
can't account for Any State transition blending, Animator playback speed, or anything else
that only shows up at runtime. Real START/END timestamps are strictly better data.

### 13b. Grab roll now logs every decision, not just successful ones

You reported the swipes seem to be "denying" the Grab. I read through `TryAttack()` /
`ShouldAttemptGrab()` / the two cooldown timers line by line and didn't find a structural
bug — the mechanism looks like it's working as designed: `_grabCooldownTimer` only ever gets
set when Grab actually fires, so it stays at 0 (available) for every decision until the first
successful Grab, meaning every attack opportunity gets one independent 12% roll (or 70% once
the player is at ≤30% HP). At a 12% base chance that's expected to lose ~7 times out of 8 —
which can easily read as "never happens" over a short session even with nothing wrong.

Since that's a claim I can't fully verify without watching it play, `ShouldAttemptGrab()` now
logs **every** roll, pass or fail:

```
[BruteAttack] Grab roll (base) -- 0.734 vs chance 0.12 -> swipe
[BruteAttack] Grab roll (base) -- 0.041 vs chance 0.12 -> GRAB
[BruteAttack] Grab roll (LOW HEALTH) -- 0.812 vs chance 0.70 -> swipe
```

Play a session and check the console:

- If it prints regularly but almost always says `-> swipe`, that's the 12% base chance
  behaving exactly as designed (your own "weighted, not guaranteed" call) — the fix there is
  raising `grabChanceBase`/`grabChanceLowHealth` if it feels too rare, not a code change.
- If `-> GRAB` prints but no Grab animation/kill actually happens, the bug is downstream of
  the decision (Animator `Grab` trigger/transition), not `ShouldAttemptGrab()` itself.
- If the log barely prints at all (i.e. `TryAttack()`/`ShouldAttemptGrab()` isn't being
  reached often), that points at something upstream -- e.g. the Brute rarely reaching
  `State.Attacking`, or `_isAttacking` getting stuck true. Send me that observation and I'll
  dig into `EnemyBrute.TickAttack()` next.

### 13c. Hit2 / Grab delays updated from clip frame counts (2026-08-29) -- estimates, not confirmed

You gave the total frame counts for the three remaining attack clips: Hit1 = 80 (already
known, matches earlier data), Hit2 = 50 (matches the .fbx.meta files), Grab = 100 (new --
wasn't previously recorded). I pulled each state's own condition-less exit transition
straight from `Phobos.controller` for real exit-time percentages:

| State | Clip | Frames | Exit time (controller) |
|---|---|---|---|
| SwipeA/B_Hit1 | Mutant Swiping / Mutant Swipe (1) | 80 | 0.90625 |
| SwipeA/B_Hit2 | Mutant Swiping 2 / Mutant Swiping 3 | 50 | 0.85 |
| Grab | Thrusting Attack | 100 | 0.92574257 |

I do **not** have a real confirmed hit-impact frame for Hit2 or Grab -- only Combo B Hit1's
frame 43 is confirmed (you found it by scrubbing the clip after it killed you). What I did:
took that confirmed frame as a fraction of its clip (43/80 = 53.75%) and applied the *same
fraction* to the Hit2 and Grab clips as a starting estimate, since I had nothing better than
the flat 0.4s/0.5s guesses that were there before. Updated in `BruteAttack.cs`:

- `comboAHit2Delay` / `comboBHit2Delay`: 0.4s -> **0.9s** (frame 27 of 50)
- `grabHitboxDelay`: 0.5s -> **1.8s** (frame 54 of 100)

Confidence, honestly assessed:
- Hit2 estimate is medium -- it's the same swipe motion type as Hit1, just a shorter clip, so
  a similar anticipation-to-impact ratio is a reasonable guess. Its exit-time (0.85) is lower
  than Hit1's (0.90625) though, so the pacing isn't identical either.
- Grab estimate is weak and I want to flag this clearly rather than let it sit quietly as if
  it were tuned: Grab is a grapple/thrust, a completely different motion from a swipe, so
  there's no real reason its impact timing should follow a swipe's fraction. It's also the
  instant-kill move that already caused the original "hit fired before the animation" bug
  report -- getting it wrong is the highest-stakes mistake in this whole system.

**Before you trust `grabHitboxDelay` in a build**, do the same thing you did for Combo B Hit1:
scrub `Thrusting Attack.fbx` (or watch the console once you've wired `OnAnimStart`/`OnAnimEnd`
plus a temporary manual event at the frame that looks like real contact) and tell me the real
frame. I'll swap the estimate for real data the same way I did for Combo B.

## 14. Grab (and Hit2) getting cut off mid-animation -- fixed 2026-08-29

You reported the Grab attack keeps getting cut off. Found it in `BruteAttack.cs`:
`GrabSequence()` was clearing `_isAttacking` the instant the hit check ran (right after
`grabHitboxDelay`, ~1.8s), not after the Thrusting Attack clip actually finished playing
(the clip's real "done" point, per `Phobos.controller`'s own exit-time, is ~3.09s in).

`EnemyBrute.TickAttack()` calls `_attack.TryAttack()` every single frame, and `TryAttack()`
is a no-op ONLY while `_isAttacking` is true. So the moment `_isAttacking` went false --
which was happening roughly 1.3 seconds before the Grab clip was actually done -- the Brute
was free to immediately fire a new attack trigger. Since all six Any State attack
transitions now have Transition Duration 0 (the earlier fix for hitbox-before-animation),
that new trigger doesn't blend in, it cuts the still-playing Grab clip instantly. That is
exactly what "cut off" looks like.

**Fix:** added `grabRecoveryDuration` (estimate: 1.29s, from the controller's real exit-time
for Grab minus the `grabHitboxDelay` estimate) -- `GrabSequence()` now waits this out AFTER
the hit check before releasing `_isAttacking`. Applied the identical fix to Combo Hit2
(`comboAHit2RecoveryDuration`/`comboBHit2RecoveryDuration`, ~0.52s each) since it had the
exact same bug, just a much shorter and less noticeable tail (~0.5s vs Grab's ~1.3s) -- you
likely hadn't noticed it yet, but it was there.

**Still an estimate, same caveats as section 13c:** `grabRecoveryDuration` is only as good as
`grabHitboxDelay` underneath it (both extrapolated from Combo B Hit1's confirmed fraction,
not a real measured Grab hit frame). Once you scrub `Thrusting Attack.fbx` for the real
contact frame, both `grabHitboxDelay` and `grabRecoveryDuration` should be recomputed
together from the real frame and the controller's exit-time (`0.9257 * 100 frames = 92.57`,
`/30fps = 3.09s` total) rather than adjusted independently.

## 15. Brute never walks again once it starts attacking -- fixed 2026-08-29

You reported that once the Brute starts attacking, it just keeps attacking and never goes
back to walking. Real root cause, in `BruteAttack.cs`: `_swipeCooldownTimer` (and
`_grabCooldownTimer`) were being set the INSTANT the attack started (`StartComboSwipe()` /
`StartGrab()`), not when it actually finished. `attackCooldown` (1.8s) is shorter than a full
swipe combo now correctly takes to play out (~3.8s with the section-14 recovery fix), so by
the time the combo animation was actually done and `_isAttacking` cleared, the cooldown timer
had ALREADY run out. `TryAttack()` could fire again the very next frame with zero real gap --
and since the player is usually still within `attackRange * 1.25` right after a swing,
`EnemyBrute.TickAttack()` never calls `ResumeChase()` either, so the Brute just chains attack
after attack forever and the Walk animation never plays again.

**Fix:** `_swipeCooldownTimer` and `_grabCooldownTimer` are now set at the true END of
`ComboSequence()` / `GrabSequence()` (right before `_isAttacking` is released), not at the
start. This gives the full `attackCooldown` (1.8s) / `grabCooldown` (16s) as REAL downtime
after the animation finishes, matching what their Inspector tooltips already said. You should
now see the Brute pause (facing the player, Idle) for a beat between combos instead of
chaining them with no gap. If the player stays within melee range the whole time, the Brute
will still not walk during that pause -- it has no reason to, it's not moving away -- but it
will stop being a nonstop attack blender. If you want the Brute to periodically break off and
re-approach even while the player stays close (a more "deliberate" combat pacing, matching the
same theme discussed for CyberSoldier), that's a separate design decision -- let me know and
I can add a stand-off/reposition beat on top of this fix.

## 16. Death animation -- Loop Time fix + death SFX hook (2026-09-03)

The `Death` state, its `Any State -> Death` transition (`IsDead` bool, Transition Duration 0,
no Exit Time -- same convention as the other five reactive states, see section 11d), and the
`Zombie Death` clip assignment already existed in `Phobos.controller` from an earlier pass, but
two pieces were still incomplete:

**Bug fixed -- Loop Time was checked on the death clip.** `Zombie Death.fbx`'s `Death` clip
(`Assets/Art/Characters/hulking-cyborg/source/Zombie Death.fbx`, 90 frames) had **Loop Time**
checked, the same category of mistake as the `Mutant Walk` mis-wiring in section 11a, just the
opposite symptom: instead of getting stuck, a *looping* death clip restarts the collapse motion
from frame 0 every ~3s instead of holding the final collapsed pose. Since `EnemyBrute.OnDie()`
calls `Destroy(gameObject, 3f)`, a mistimed loop could read as the Brute twitching back upright
right as it disappears. Fixed by unchecking **Loop Time** on the `Death` clip in its Animation
import inspector (Loop Time is now off, matching every other one-shot reactive clip in this
controller -- Scream/swipes/Grab are all non-looping by default since they were imported as
Trigger-driven one-offs, not locomotion).

**Added -- death SFX hook.** `EnemyBrute` had `IsDead`/animation wired but never played a sound
on death (unlike `EnemyChaser`, which calls `_audio?.PlayDeath()`). Added, mirroring
`EnemyChaserAudio`'s convention exactly:
- `BruteAudio.deathClip` (`AudioClip`, optional -- same null-safe pattern as `grabConnectClip`/
  `neckSnapClip`) + `deathVolume` (default 1).
- `BruteAudio.PlayDeath()` -- `PlayOneShot(deathClip, deathVolume)`, logs a warning and no-ops if
  unassigned.
- `EnemyBrute.OnDie()` now calls `_audio?.PlayDeath()` right after `SetAnimBool(_deadHash, true)`,
  same spot `EnemyChaser.OnDie()` calls its own `PlayDeath()`.

**Still needed (Editor-only, by hand):** `BruteAudio.deathClip` is unassigned -- drag a clip onto
it in the Inspector (the Phobos GameObject's `BruteAudio` component). No death SFX exists yet
under `Assets/Audio/Enemy/Phobos/` -- either add one there, or point `deathClip` at
`Assets/Audio/Enemy/BasicEnemy/Enemy Death.mp3` (CyberSoldier's death clip) as a placeholder
until a Brute-specific one is sourced.

**Known non-issue, flagging anyway:** the `Death` state's own return transition (`fileID:
5187588500647248705`, condition-less, Exit Time ~0.916 of the clip) sends it back to `Idle`
once the clip finishes playing, same as every attack state's auto-return. For a corpse this
looks wrong in principle, but `Destroy(gameObject, 3f)` fires before or right around when that
transition would complete for a 90-frame/30fps (~3s) clip, so it isn't visually reachable in
practice. If the death clip length or the 3s delay ever changes independently, re-check this --
the cleanest long-term fix would be removing that return transition entirely (a dead Brute has
no reason to ever leave `Death`), just not required by anything asked so far.

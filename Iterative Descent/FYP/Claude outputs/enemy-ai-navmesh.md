# ARBITEX Normal Enemy AI — NavMesh Decision Systems

**Scope:** how the three regular NavMesh enemy types (Chaser, Rusher, Brute) decide what to do — activation, targeting, chase/attack state transitions, the stagger interrupt. Attack mechanics, hit-detection geometry, and animation/moveset design are a separate document.
**Source files verified against:** `EnemyBase.cs`, `IEnemy.cs`, `EnemyChaser.cs`, `EnemyRusher.cs`, `EnemyBrute.cs`, `EnemyAttack.cs`, `RusherAttack.cs`, `BruteAttack.cs`, `EnemyDirector.cs`, `EncounterTrigger.cs`, `HashTableEnemyEventBridge.cs` (via `project-hashtable-puzzle.md`), `project-boss-fight.md`, `project-deimos-boss.md`.

---

## 1. Overview: one shared framework, three decision policies

Every regular enemy in the game is an `EnemyBase` subclass implementing `IEnemy`. `EnemyBase` owns everything generic — health, damage, death, the DPS-threshold stagger/flinch system, and checkpoint-retry reset — and three concrete subclasses each own their own decision policy on top of it:

| Type | Class | Core shape |
|---|---|---|
| Chaser | `EnemyChaser.cs` | 2-distance-band FSM: chase until in range, then attack; can also self-activate |
| Rusher | `EnemyRusher.cs` | 3-distance-band FSM: sprint / walk / walk-and-attack simultaneously |
| Brute | `EnemyBrute.cs` | Chaser's shape plus a scripted wake-up beat and a weighted swipe-vs-grab decision |

**On Phobos:** confirmed via `HashTableEnemyEventBridge.cs` (the Hash Table puzzle's corpse-to-ambush system) — Phobos is a plain, unmodified `EnemyBrute` prefab instance. The bridge just calls `Activate()` on it directly (revealing a hidden GameObject and swapping out a corpse prop) instead of `EncounterTrigger`'s usual "player walks through a collider" path. Everything in §4.3 below (Brute's wake-up scream, chase, and the swipe/grab decision) applies to Phobos exactly as written — the only difference is what triggers `Activate()`, not what happens after.

**On Deimos — this is where your assumption doesn't hold.** Deimos is *not* a NavMesh-chaser enemy and is not an `EnemyBase` subclass at all. It has its own hand-built `DeimosStateMachine.cs` / `DeimosHealth.cs`, architecturally the same category as ARES's pre-PPO heuristic boss FSM (`BossStateMachine.cs`/`BossHealth.cs`) — a dedicated state machine per boss, with its own 8-move roster (MMAKick, Dropkick, Flying Knee, a grab-and-throw, two no-damage repositioning jumps, a ported flinch reaction, and death), self-triggering attacks (the "jump behind the player" counter watches for encirclement on its own timer, independent of the normal attack-picker), and its own stagger port rather than a shared one. It reuses *some* of the same NavMesh plumbing this document describes (e.g. it independently discovered and pre-emptively fixed the same "sample the target position onto the mesh before `SetDestination()`" issue described in §5.3 below), but its decision structure belongs with ARES's document, not this one. I've flagged this rather than folding Deimos in here, since treating it as "just another chaser" would misdescribe how it actually works.

---

## 2. Shared framework (`EnemyBase.cs`)

### 2.1 Lifecycle: dormant until `Activate()`

Every enemy starts fully inert: `Awake()` sets `_agent.enabled = false` and `enabled = false` (the whole `MonoBehaviour`, so `Update()` never runs at all while dormant). `_baseMaxHealth` is snapshotted before any DDA scaling, animator `applyRootMotion` is forced off (the NavMeshAgent owns position — root motion and the agent fighting each other was an early, project-wide source of walking-on-the-spot bugs, see `feedback-unity-gotchas.md`).

`Activate(Transform target)` (from `IEnemy`) re-enables the agent and the component, calls the subclass's `OnActivate()` hook, and — critically — re-applies `EnemyDirector` DDA scaling every single time, not just once:

```
maxHealth      = baseMaxHealth * EnemyDirector.Instance.EnemyHealthMultiplier
currentHealth  = maxHealth   (full heal to the newly-scaled max)
agent.speed    = baseSpeed   * EnemyDirector.Instance.SpeedMultiplier
attack damage *= EnemyDirector.Instance.DamageMultiplier
```

This is what makes a tier change mid-game apply correctly: `EnemyDirector`'s getters read `CombatDDAController.CurrentTier` live, so whichever tier is current *at the moment this specific enemy activates* is the one it gets — a tier that changed after one encounter doesn't retroactively affect enemies already mid-fight, but it does apply cleanly to the next one. See the Combat DDA math document for the tier table itself; this document is about what each enemy *does*, not how hard it hits.

`Deactivate()` pauses the state machine (subclass hook `OnDeactivate()`) without destroying anything — `Activate()` can resume it later.

### 2.2 Stagger (DPS-threshold flinch), shared by all three types

`TakeDamage()` records every hit's timestamp and amount, prunes anything older than `staggerWindowSeconds` (default 1s), and triggers a stagger if either the rolling in-window damage-per-second crosses `staggerDpsThreshold` (default 40) *or* a single hit alone crosses `staggerBigHitDamage` (a bypass for a hypothetical one-shot-flinch weapon; disabled by default at 99999). On trigger: the NavMeshAgent is stopped, `IsStaggered` goes true, the subclass's `OnStaggerStart()` hook fires (each type uses this to cancel whatever it was mid-doing — see §4), and after `staggerDuration` (default 1s) the agent resumes and `OnStaggerEnd()` fires (each type forces itself back into a fresh Chasing state rather than trying to guess what it was doing before the interrupt).

Repeated staggers in the same fight need proportionally more DPS each time (`staggerResistanceMultiplier *= staggerResistanceGrowth`, default 1.5×), decaying back toward 1× after `staggerResistanceDecayDelay` seconds of no new hits — so persistent point-blank fire can't permanently flinch-lock an enemy, but also isn't a one-and-done.

This same system was later ported field-for-field into `DeimosHealth.cs` (see §1) since Deimos isn't an `EnemyBase` subclass and couldn't inherit it directly — same math, same defaults, same intent, just re-implemented rather than shared by inheritance.

### 2.3 Death, corpse handling, and checkpoint retry

`OnDie()` (subclass hook) plays the death state and calls `HandleCorpseDespawn(delay)`. If the enemy's `Retryable` flag is set (set by `EncounterTrigger` per-enemy at scene start, gated on that encounter's own `resetOnCheckpointRespawn` toggle — see the Combat DDA math doc §2.1 for the player-death interaction), the corpse is `SetActive(false)` after the delay rather than `Destroy()`ed, so `ResetForRetry()` can revive the exact same GameObject later. A non-retryable enemy is destroyed normally.

`ResetForRetry(spawnPosition, spawnRotation)` — called by `EncounterTrigger.ResetEncounter()` after a checkpoint respawn — cancels any pending corpse-deactivate, clears all stagger/hit-history state, re-enables the GameObject at full health at its original spawn transform, `Rebind()`s the Animator (so it doesn't come back stuck in a terminal "Dead" pose), and calls the subclass's `OnResetForRetry()` hook to reset its own state-machine enum back to Idle. Applies unconditionally to every enemy in the encounter, dead or still mid-fight — a rolled-back encounter means everyone refights from scratch, never a mix of fresh and battle-damaged survivors.

---

## 3. Activation paths

Two independent ways a dormant enemy wakes up:

1. **`EncounterTrigger`** (scripted, room-wide): the player walks through a trigger collider, or a puzzle event bridge calls `ActivateEncounter()` directly (e.g. `HashTableEnemyEventBridge` for Phobos). Every enemy assigned to that trigger activates together, regardless of DDA tier — count-based DDA scaling was removed project-wide in the 2026-09 combat revamp (see the Combat DDA math doc §6.2); only per-enemy toughness scales now.
2. **`EnemyChaser`'s own proximity/shot auto-activation** (Chaser only, not Rusher or Brute): see §4.1.

---

## 4. Per-type decision policy

### 4.1 EnemyChaser

```
[Idle] --Activate()--> [Chasing] --dist <= attackRange--> [Attacking]
                            ^                                   |
                            +---- dist > attackRange*1.25 ------+
        [Dead] <-- health == 0, from any state
```

**Chasing (`TickChase`):** path recalculation is throttled to once per `pathUpdateRate` (default 0.2s) rather than every frame — `SetDestination()` every frame is expensive and unnecessary at this granularity. The moment distance drops to `attackRange` (default 2m), the agent is fully stopped (`isStopped = true` + `ResetPath()`, not just a speed-zero) and the state flips to Attacking.

**Attacking (`TickAttack`):** faces the player every frame via a manual `Slerp` (*unless* a windup rotation is currently in progress — see below), and calls `EnemyAttack.TryAttack()` every frame, which is itself cooldown-gated internally (a no-op call while its own `_cooldownTimer > 0`) — so "attack" here really means "keep offering to attack; the attack script decides if it's actually time." If the player gets more than `attackRange * 1.25` away, `ResumeChase()` fires immediately — the 1.25× band exists so the enemy doesn't flicker between the two states right at the boundary.

**Auto-activation — the one Chaser-specific decision the other two types don't have.** A dormant Chaser can wake itself up two ways, both deliberately bypassing `EncounterTrigger`'s scripted, DDA-scaled activation count (this is framed as a direct player action against *this* enemy, not a room-wide encounter start):
- **Proximity + line-of-sight:** polled via `InvokeRepeating` (not `Update()`, since a dormant enemy's `MonoBehaviour` is disabled and `Update()` simply never runs — `InvokeRepeating` keeps firing regardless) every `autoActivateCheckInterval` (default 0.25s). If the player is within `autoActivateRadius` (default 8m) *and* a `Physics.Linecast` at chest height against the "Level 1 Obstacle" layer comes back clear, it wakes up. The line-of-sight check specifically stops a dormant enemy in the next room from waking through a wall just because it's numerically within radius.
- **Being shot:** `OnHit()` checks `_state == Idle && _currentHealth > 0` (skipped on a killing blow — no point activating something about to die) and activates immediately, no range limit — any hit landing means the player is already engaged.

**Windup rotation:** cosmetic but decision-relevant — while an attack's windup is playing, `WindupRotate()` takes over `transform.rotation` from the normal face-the-player `Slerp`, rotating to a fixed offset angle (`windupRotationDegrees`) timed to finish exactly when the hit resolves, and holds that pose until the windup ends. This is why `TickAttack()` explicitly skips its own `FaceTarget()` call while `_isWindingUp` is true — two systems fighting over `transform.rotation` on the same frame was exactly the kind of bug this project had already hit elsewhere (see `feedback-unity-gotchas.md`).

### 4.2 EnemyRusher

```
                    beyond 12m
      +------------------------------+
      |                              v
  [Sprinting] <----------------  [Walking] <---+
      |         within 12m           |          |
      | within 6m                    | within 6m|  beyond 6m
      v                              v          |
  [Mauling] (walk + attack simultaneously) ------+
  [Dead] <-- health == 0, from any state
```

Three concrete distance bands re-evaluated every frame (`rushDistance` default 12m, `maulDistance` default 6m), not a single in-range/out-of-range split like Chaser:

- **Sprinting** (beyond 12m): `agent.speed = walkSpeed * rushMultiplier` (default 5 × 3 = 15).
- **Walking** (6–12m): normal `walkSpeed`.
- **Mauling** (within 6m): the distinctive behavior — the agent keeps closing distance *while* `RusherAttack.TryAttack()` is called every frame, rather than fully stopping to attack the way Chaser/Brute do. Only once within `stopDistance` (default 1.8m, deliberately just under `maulDistance`) does it freeze (`isStopped = true`) so it stops physically pushing the player; above that it keeps a `stoppingDistance`-gated approach going.

Rotation is fully manual here — `agent.updateRotation = false` is set explicitly in `Awake()`, and `RotateToward()` drives facing at a different turn speed per state (`walkTurnSpeed` 240°/s, `sprintTurnSpeed` 480°/s, `maulTurnSpeed` 720°/s — deliberately fastest while mauling, so it doesn't visibly lag behind a circle-strafing player mid-swing). This is the one type where the code explicitly disables agent-driven rotation; see §5.2 for why Chaser/Brute's rotation ownership is less certain from source alone.

State re-selection happens fresh every `OnActivate()` via `ChooseStateForDistance()`, so a Rusher that starts an encounter already close to the player skips straight to Mauling rather than always beginning at Sprinting.

### 4.3 EnemyBrute

```
[Idle] --Activate()--> [WakingUp] --wakeupLockDuration--> [Chasing] --dist<=attackRange--> [Attacking]
                                                               ^                                |
                                                               +---- dist > attackRange*1.25 ----+
        [Dead] <-- health == 0, from any state
```

Structurally Chaser's FSM (identical `TickChase`/`TickAttack` shape, same 1.25× hysteresis band on breaking off) with one state prepended: **WakingUp.** Every single `Activate()` call — including a re-activation after `Deactivate()`, not just the very first time — locks the agent in place (`isStopped = true`, `ResetPath()`), fires a scream animator trigger, and holds for `wakeupLockDuration` (default 1.6s, meant to match the scream clip's length) before releasing into Chasing. This is a deliberate "it wakes up and roars" beat, not a bug — it replays every time by design.

**The swipe-vs-grab decision (`BruteAttack.cs`) is the one place a "normal" enemy makes a weighted random choice rather than a pure state-machine transition.** While in Attacking and off cooldown, `TryAttack()` rolls against a grab chance that is *not* fixed: `grabChanceBase` (default 12%) normally, but replaced with `grabChanceLowHealth` (default 70%) once the player's HP fraction drops below `playerLowHealthThreshold` (default 30%). In other words, the Brute becomes far more likely to go for its heavy grab specifically when the player is already low — a genuine predator-behavior decision, not just an animation pick. (Grab's actual mechanics — freeze, damage timing, instant-kill framing — are moveset detail and out of scope here.)

`OnStaggerStart()` cancels the wake-up-scream coroutine if the DPS threshold is crossed mid-roar (so a stagger visibly interrupts even the intro beat, not just combat), but deliberately does **not** cancel an in-progress swipe/grab — that resolves on its own timer regardless; only new attacks and movement are blocked for the stagger's duration. The code comment flags this as an intentional scope limit: cancelling a mid-grab stagger would additionally require releasing the player's movement lock to avoid a soft-lock, which hasn't been needed yet.

---

## 5. NavMesh mechanics and a known open risk

### 5.1 Common pattern across all three types

Every type follows the same shape: cache a `NavMeshAgent` reference in `EnemyBase.Awake()`, throttle `SetDestination()` calls to a fixed interval (`pathUpdateRate`, 0.2s across all three) rather than every frame, and explicitly `isStopped = true` + `ResetPath()` on entering an attack/wake state rather than relying on `stoppingDistance` alone (which only slows approach, it doesn't guarantee the agent stops pathing).

### 5.2 Rotation ownership — confirmed for Rusher, not confirmed in code for Chaser/Brute

`EnemyRusher` explicitly sets `agent.updateRotation = false` in code and drives all facing manually via `RotateToward()`. `EnemyChaser` and `EnemyBrute` both call a manual `FaceTarget()` `Slerp` every frame but **never set `agent.updateRotation` in either script** — meaning either it's set to `false` on the prefab in the Inspector (not visible from source), or the agent's own rotation and the manual `Slerp` are both active and only one visibly wins depending on Inspector config. I'm flagging this rather than asserting either way, since I could only verify it against the two `.cs` files, not the prefab's serialized Inspector values.

### 5.3 A known, unresolved risk: `SetDestination()` and elevated target positions

This is worth including because it's a specific, documented finding from this same codebase, not speculation. While building and debugging ARES's PPO training (`BossStateMachine.UpdateCombat()`), the team found that Unity's `NavMeshAgent.SetDestination()` silently rejects a target position that sits meaningfully above the actual walkable mesh surface (e.g. a capsule collider's pivot, ~1 unit above ground) — far more strictly than `NavMesh.SamplePosition()`'s own search radius. The fix there was to sample the target onto the mesh first: `NavMesh.SamplePosition(target.position, out hit, radius, NavMesh.AllAreas)` and feed `hit.position` to `SetDestination()` instead of the raw transform position.

`project-boss-fight.md`'s own writeup explicitly flags that `EnemyChaser.cs` and `EnemyRusher.cs` (this document's subject matter) call `_agent.SetDestination(_target.position)`/`_target.position` with the same *unsampled* raw-position pattern that caused ARES to never chase at all until it was fixed — and that this was **never fixed or even confirmed broken** for the live-game enemies, because Level 1 hadn't been playtested yet at the time that finding was written up. `EnemyBrute.cs` (verified directly in this pass) has the identical pattern (`_agent.SetDestination(_target.position)` in `TickChase()`). If the player's own collider pivot sits elevated the same way the boss's training-dummy opponents' did, this same silent-rejection failure could affect Chaser/Brute/Rusher chasing in the live game and hasn't been ruled out. Worth a deliberate playtest check (does an enemy that starts at rest actually close distance reliably) before assuming this class of bug can't recur here.

---

## 6. `IEnemy` contract (for reference)

```csharp
void Activate(Transform target);  // wake up, told who to pursue
void Deactivate();                // pause without destroying
void Die();                       // idempotent — safe to call twice
bool IsDead { get; }
```

`EnemyBase` provides the shared implementation; every subclass hook described above (`OnActivate`/`OnDeactivate`/`OnDie`/`OnHit`, plus the stagger and retry hooks) is where the three types actually diverge.

---

## 7. Summary table

| | EnemyChaser | EnemyRusher | EnemyBrute |
|---|---|---|---|
| States | Idle → Chasing → Attacking → Dead | Idle → Sprinting/Walking/Mauling → Dead | Idle → WakingUp → Chasing → Attacking → Dead |
| Distance bands | 1 (attackRange) | 2 (rushDistance, maulDistance) | 1 (attackRange) |
| Moves while attacking? | No — fully stops | Yes, in Mauling (until stopDistance) | No — fully stops |
| Rotation control | Manual `Slerp`, `updateRotation` not set in code | Manual `RotateTowards`, `updateRotation = false` explicit | Manual `Slerp`, `updateRotation` not set in code |
| Self-activation | Yes (proximity + on-hit) | No | No |
| Attack decision | Single attack type, cooldown-gated | Alternating L/R swipe, cooldown-gated | Weighted swipe-vs-grab roll, shifts toward grab at low player HP |
| Special state | Windup rotation (cosmetic, blocks FaceTarget) | None | WakingUp scream lock, replayed every Activate() |
| Confirmed real-world instance | — | — | Phobos (Hash Table Brute), activated via event bridge instead of EncounterTrigger |

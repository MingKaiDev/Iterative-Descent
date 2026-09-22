# ARES (DROID-7) AI — PPO-Driven Boss Director

**Scope:** how ARES decides what to do — its movement FSM, its two interchangeable attack-selection systems (a heuristic fallback and a trained PPO policy), the reward design and training history behind the policy, and the puzzle-gated toggle between the two. Attack mechanics, hitbox geometry, and moveset/animation design are a separate document; this one is about the decision-making layer.
**Source files verified against:** `BossStateMachine.cs`, `BossHealth.cs`, `BossAttackBase.cs`, `BossAttackRegistry.cs`, `Training/BossAgent.cs`, `AresAttackDisableManager.cs` (referenced), `BossEncounterTrigger.cs` (referenced via project docs).
**Project docs cross-referenced:** `project-boss-fight.md` (training/bug history), `project-ares-attack-disable-reward.md` (the reward-room mechanic), `project-remaining-work.md` (the smart/dumb toggle), `project-ai-architecture.md` (overall two-system framing).

---

## 1. Overview

ARES is the only reinforcement-learning-driven AI in the project. Everything else — Puzzle DDA, Combat DDA, the regular Chaser/Rusher/Brute enemies, and Deimos — is heuristic (rule-based FSMs, weighted random rolls, exponential moving averages). ARES's movement is *also* heuristic (a plain FSM, structurally the same shape as Deimos's), but **which attack it throws** is decided by a PPO policy trained with Unity ML-Agents, running in inference-only mode against the live player. This split — heuristic movement, learned attack selection — is deliberate and load-bearing for how the whole system is built, so it's worth stating up front before the detail below.

ARES actually has **two interchangeable attack-selection systems** wired onto the same GameObject at all times:

1. **`BossAttackRegistry`** — the original heuristic system: poll periodically, pick a uniformly random attack that's off cooldown and in range. This existed before any PPO training happened and still exists as a fully functional, independently reachable fallback.
2. **`BossAgent`** — the trained PPO policy (`BossAgent_v3.onnx`, currently deployed in Inference Only mode on the live boss). Replaces the registry's uniform-random pick with a learned choice, using a 16-float observation of the fight state.

Both run simultaneously, every frame, on the deployed boss. A single boolean (`_smartModeActive`) decides which one's decision actually executes — see §6. This is why the codebase and the project docs both describe this as a "smart / dumb" toggle rather than two separate boss configurations: nothing is ever un-wired or destroyed, only one system's output is used at a time.

---

## 2. Movement: `BossStateMachine` (heuristic FSM, not RL)

```
[Idle] --player within detectionRadius--> [Combat] --EnterAttacking()--> [Attacking]
                                              ^                               |
                                              +---------- ExitAttacking() ----+
   [Combat] --HP <= phaseThreshold--> [PhaseTransition] --pause, then--> [Combat]
   [Dead] <-- BossHealth.OnBossDeath, from any state
```

`BossStateMachine` (`[RequireComponent(NavMeshAgent, Animator, BossHealth)]`) owns movement and never delegates it to either attack-selection system — `BossAgent`'s own header comment is explicit about this: "this Agent only decides WHICH attack fires, never where the boss moves." The two attack systems only ever call `EnterAttacking()`/`ExitAttacking()`, which stop/resume the chase; they never touch the NavMeshAgent directly.

- **Idle → Combat:** plain distance check against `detectionRadius` (default 15m), polled every `Update()`.
- **Combat:** chases via `NavMesh.SamplePosition(player.position, ...)` → `SetDestination(hit.position)`. **This ground-projection step was added specifically because raw `SetDestination(player.position)` silently failed** — the code comment documents the exact investigation: the player capsule's pivot sits above the walkable mesh surface, and `NavMeshAgent.SetDestination()` has a much stricter off-mesh tolerance than `NavMesh.SamplePosition()`'s own search radius, so an unprojected target was rejected on every single call, `hasPath` staying false forever. This fix is *already baked into ARES's code today* — contrast with the regular enemies (Chaser/Rusher/Brute), where the equivalent code was flagged as carrying the identical unfixed risk and never confirmed either way (see the normal-enemy-AI document, §5.3).
- **Attacking:** `EnterState(Attacking)` calls `_agent.ResetPath()` — movement fully stops while an attack plays; whichever attack-selection system is active owns the rest of that beat.
- **PhaseTransition:** fires once, automatically, on `BossHealth.OnPhaseTwo` (HP crossing `phaseThreshold`, default 50%). Pauses movement, sets the `isPhase2` animator bool, waits `phaseTransitionDuration` (default 2s), then re-enters Combat. This is the *only* phase-aware behavior currently wired up — the observation vector both attack systems see (§4.1) carries a phase-2 flag, but nothing currently changes attack-selection tempo or unlocks new moves on it; it's an unused hook, not a bug (see §9).
- **Dead:** permanent — `_agent.enabled = false` and never re-enabled from this path (checkpoint respawn goes through a dedicated method, not a state re-entry — see below).

**Checkpoint respawn** (`ResetForCheckpointRespawn()`, called by `CheckpointManager` when the player dies and respawns at a checkpoint reached before the ARES encounter, boss still alive) is its own explicit sequence, distinct from the training-only episode reset: cancel any attack currently mid-execution on **every** `BossAttackBase` on the GameObject (`CancelAttack()` — see §5 for why this specifically exists), warp the agent back to its captured spawn position/rotation, reset health via `BossHealth.ResetHealth()`, then re-arm the state machine straight into Combat (skipping Idle's detection-radius wait, since the player is already right there). No-op if the boss is already dead.

---

## 3. Health: `BossHealth`

Plain `IDamageable` HP component, 500 HP default, with three static events any listener can subscribe to independently (`OnHealthChanged`, `OnPhaseTwo`, `OnBossDeath`) plus a fourth (`OnHealthReset`) fired only by `ResetHealth()`. `TakeDamage()` clamps at zero, checks the phase threshold, and fires `Die()` once, guarded against double-firing. `ResetHealth()` (training episode reset, and the live checkpoint-respawn path above) deliberately does **not** replay `OnPhaseTwo`/`OnBossDeath` — those are one-shot story beats, not something a reset should re-trigger — but does fire `OnHealthReset` specifically so a listener like the HUD can undo any one-shot visual state (the phase-2 red tint) that `OnPhaseTwo` left behind.

---

## 4. The attack framework (`BossAttackBase`)

Every concrete attack (Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload) extends this shared base, which is what both selection systems actually call into. It isn't itself a decision-maker, but its state is exactly what both systems' decisions are gated on, so it belongs in this document rather than the moveset one.

- **Cooldown:** flat `cooldown` (default 3s across all 8 attacks, currently untouched — see §9's GroundSlam finding for why this flatness matters), tracked via `IsOnCooldown`.
- **Variety cooldown (`varietyCooldown`):** a *second*, independent timer, read only by `BossAgent`'s action masking (§4.2) — `BossAttackRegistry`'s uniform-random picker never reads it at all. This is what stops the trained policy from picking the same attack twice in a row even once its plain cooldown has cleared, without touching the dumb-mode fallback's behavior at all.
- **Timeout safety net:** `attackTimeoutDuration` (default 5s) — if the attack's Animation Event (`OnAttackAnimEnd`) never fires (a missing event on the clip), a coroutine force-ends the attack anyway and logs a warning. Primary signal is always the Animation Event; this is only a backstop.
- **`CancelAttack()`:** a `virtual` hook, overridden by any attack that seizes manual control mid-flight (SprintCharge/GroundSlam disconnect the NavMeshAgent; Pounce hides renderers; CoreOverload runs a telegraph). This exists specifically because of a real bug found during training (§8, bug 2/Cause B): if the boss died mid-attack with no cancellation path, the attack's coroutine kept running on stale data after the world had already reset around it, which could drag the agent off the NavMesh permanently. `ResetForCheckpointRespawn()` (§2) and `BossTrainingEnv.ResetEpisode()` (training-only) both call `CancelAttack()` on every attack as the first step of any reset, for exactly this reason.

---

## 5. Attack selection, system 1: `BossAttackRegistry` ("dumb mode")

The original, pre-RL system, and still what runs whenever `_smartModeActive` is false (§6). Simple and fully deterministic in shape: every `attackDecisionInterval` seconds (default 2s) while `BossStateMachine.CurrentState == Combat`, collect every attack that is off cooldown, within `maxRange` of the player, and not permanently disabled (§7), then `Random.Range` a uniform pick from that set and `Execute()` it. No memory of what was picked last, no weighting — variety, if any, is incidental. Subscribes to every attack's `OnAttackEnded` to call `BossStateMachine.ExitAttacking()` once a swing finishes, which is also how `BossAgent`'s own choices return to Combat — the registry's event subscription runs regardless of which system is actually deciding what to execute.

---

## 6. Attack selection, system 2: `BossAgent` (PPO policy, "smart mode")

### 6.1 Observation vector — 16 floats, fixed order

| Index | Observation | Detail |
|---|---|---|
| 0 | Distance to opponent | raw magnitude |
| 1–2 | Opponent velocity relative to boss, X/Z | world-space, boss doesn't rotate the observation frame |
| 3 | Opponent angular velocity around the boss | signed rad/s — tangential component of opponent velocity ÷ distance; positive = counter-clockwise viewed from above. Flagged in the code itself as the author's own interpretation of a locked spec, not independently re-confirmed against a second source |
| 4 | Time since opponent last fired | normalized against a 10s horizon, clamped [0,1] |
| 5 | Time since opponent last moved | same normalization |
| 6 | Boss HP | normalized [0,1] against `maxHealth` |
| 7 | Phase-2 flag | 0/1 — see §2, currently unconsumed by anything downstream |
| 8–15 | Time-since-last-used, per attack | `TimeSinceUsedNormalized` (§4), fixed order: Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload |

**Two different data sources feed this vector depending on where ARES is running**, and this branch is a real, deliberate piece of the design, not an inconsistency:
- **Training (`trainingEnv != null`, Training.unity):** velocity/fire-timing/move-timing come directly from the active scripted opponent's own tracked fields (`PlayerAgentBase.Velocity`/`TimeSinceLastFired`/`TimeSinceLastMoved`).
- **Live deployment (`trainingEnv == null`, Level 1.unity — see §6.5):** there is no `PlayerAgentBase` for the real player, so velocity is approximated from frame-to-frame position deltas between successive `CollectObservations()` calls (`ComputeFallbackVelocity()`, zeroed on the first sample or after a >1s gap), and the two timing observations are defaulted to a neutral **0.5** rather than 0 — 0 would read as "opponent just fired / just moved," a constant false signal the policy never actually saw during training.

### 6.2 Action space — 9 discrete values, fixed order

`0` = Idle (always unmasked — ML-Agents requires at least one enabled action per branch; Idle is a safe fallback since the boss keeps chasing via `BossStateMachine.Combat` regardless of what this branch picks). `1–8` = Slash, LeftPunch, Stab, BladeSweep, GroundSlam, Pounce, SprintCharge, CoreOverload, in an order wired explicitly on the `attacks[8]` Inspector array — the code comment is emphatic that this order must never change without also updating the trained checkpoint's semantics, since index meaning is baked into the model weights, not re-derivable from anything else.

### 6.3 Action masking (`WriteDiscreteActionMask`)

An attack is selectable only when **all** of: `BossStateMachine.CurrentState == Combat`; `!IsOnCooldown`; `!IsOnVarietyCooldown`; `distance <= attack.maxRange`; and not permanently disabled by the reward-room mechanic (§7). This is a strict superset of `BossAttackRegistry`'s own filter (which only checks cooldown + range) — the variety-cooldown and permanent-disable checks exist only here.

### 6.4 Reward function

```
Terminal (fired once, on episode end via BossTrainingEnv):
    BossWon   -> +1.0   (terminalWinReward)
    BossLost  -> -1.0   (terminalLoseReward)
    TimedOut  -> +0.3   (terminalTimeoutReward — boss survived without a clean kill either way)

Shaping (fired continuously, off health-change events):
    hit landed      = +0.5 * (damage dealt / opponent max HP)        (hitLandedRewardScale)
    damage taken    = -0.5 * (damage taken / boss max HP)            (damageTakenPenaltyScale)
    variety bonus   = +0.1, ADDED on top of a hit-landed reward,
                      only if the attack that landed had
                      TimeSinceUsedNormalized >= 0.5 at the moment
                      it was executed                                 (varietyBonusReward / varietyBonusFreshnessThreshold)
```

All five constants are `[SerializeField]` Inspector fields on `BossAgent`, tunable without retraining the shape of the reward (though changing them does require a fresh training run to actually take effect on the policy).

**A structural property worth naming explicitly, because it shapes how to read the training curves in §8:** the damage-taken penalty is a weak per-action training signal, because the 9-action space has no defensive/dodge/block action at all. Whether the boss takes a hit on a given frame is driven almost entirely by the three scripted opponents' own independent attack cadence, not by which attack the boss just chose — from the policy's perspective this term is close to uncontrollable noise moment-to-moment. The real credit-assignment path is episode-level (a better attack choice → faster kill → less total exposure time → less total damage taken over the whole episode), not frame-level avoidance. This was flagged explicitly during development: don't expect Cumulative Reward to climb close to zero purely from "damage avoidance" reasoning — the boss structurally cannot dodge, so a persistently negative-but-stabilizing average reward can still represent a converged, competent policy. Adding real damage-avoidance agency would need a dodge/block action added to the roster, which hasn't been done.

### 6.5 Level 1 deployment fallback

`BossAgent` was originally built and trained purely against Training.unity's three scripted opponents (`BossTrainingEnv.ActiveOpponent`). Deploying the trained checkpoint onto the real Level 1.unity boss required a real code change, not just Inspector wiring — `CollectObservations()` and `WriteDiscreteActionMask()` both originally resolved the opponent exclusively through `trainingEnv.ActiveOpponent`, which is `null` on Level 1.unity (there is no `BossTrainingEnv` there); left as-is, this would have fed an all-zero observation vector and — more seriously — permanently masked every attack (distance would compute as `Mathf.Infinity` with no opponent, failing every `maxRange` check), leaving the boss stuck in Idle forever.

The fix, `ResolveOpponentTransform()`, is a two-branch helper called from both `CollectObservations()` and `WriteDiscreteActionMask()`: return `trainingEnv.ActiveOpponent.transform` unchanged when `trainingEnv != null` (Training.unity behavior is completely untouched), otherwise fall back to a cached `FindFirstObjectByType<PlayerHealth>()` lookup for the real player — the same fallback pattern several individual attack scripts (SprintCharge/GroundSlam/Pounce/CoreOverload) already used for their own target resolution before this fix existed. `trainingEnv` is deliberately left **empty** in the Level 1.unity prefab's Inspector fields specifically to trigger this fallback path.

### 6.6 `Heuristic()` is not a stand-in policy

`Heuristic()` (ML-Agents' manual-testing hook, active when Behavior Type = Heuristic Only) is hardcoded to always return Idle. The code comment is explicit that this is deliberate — it exists to sanity-check observations/masking in Play mode without a trained model attached, not to provide a playable fallback AI. This mattered directly during debugging: early in development, a component lifecycle bug (§8, bug 3) meant `Initialize()` never actually ran, so `BossAttackRegistry`'s random-heuristic fallback was silently driving 100% of observed attacks for an extended period, and the visually-plausible combat this produced was initially and incorrectly read as evidence the RL agent must be working.

---

## 7. Smart/dumb mode toggle and the puzzle-gated attack-disable reward

### 7.1 Smart/dumb toggle

Design decision (2026-09-09, confirmed working 2026-09-10): if the player has **not** solved the Packet Filter puzzle before reaching ARES, the boss fights at full strength on the trained policy (`_smartModeActive = true`, the default). If the player **has** solved it ("cutting ARBITEX's connection to ARES"), ARES is deliberately weakened: `SetSmartModeActive(false)` sets `bossAttackRegistry.enabled = true` and makes `OnActionReceived()` discard the trained policy's choice with an early return — but the policy keeps running underneath regardless (still collecting observations, still requesting decisions every `DecisionRequester` period), just silently. This is a deliberate design choice, not an optimization shortcut: toggling `BossAgent.enabled` itself would re-trigger `Initialize()`/`OnEnable()`, which is exactly the class of lifecycle re-entrancy bug that already caused real problems once before (§8, bug 3).

The trigger condition needed no new plumbing — `PacketFilterEventHandler.ServerNodeDisabled` (a static bool, set once the puzzle is solved) already existed as a dead hook (originally intended for an ARES Phase 2 buff that was never built) before this toggle became its first real consumer. `BossEncounterTrigger` calls `aresAgent.SetSmartModeActive(!PacketFilterEventHandler.ServerNodeDisabled)` on every trigger entry.

**A real bug was found and fixed here, worth including since it's a genuine "one-shot trigger swallows a value that needed live re-evaluation" gotcha:** the first implementation placed the mode-set call *inside* `BossEncounterTrigger`'s existing one-shot `_triggered` guard (which gates the HUD/door-seal/dialogue setup that should only ever run once). An early, incidental walk-in before the puzzle was solved latched "smart" permanently for that play session — solving the puzzle afterward and re-entering hit the one-shot guard and never reached the mode-check line again. Fixed by splitting the two concerns: one-time setup stays behind `_triggered`, but the mode-check call now runs unconditionally on every trigger entry. Confirmed by the user afterward via a clean behavioral signal: the trained policy has a strong, consistent GroundSlam-opening bias (§8's dominance finding), so a visibly *varying* opening move after solving the puzzle was good independent evidence the uniform-random fallback was actually the one executing, not just the same policy behaving differently.

### 7.2 The attack-disable reward room

A separate mechanic, downstream of both attack-selection systems rather than a third one: eight machines (`AresAttackDisableMachineProp`, one per attack, `IInteractable`), where interacting with any one **permanently** disables that specific attack for the rest of the playthrough and immediately locks the player out of interacting with any of the other seven — exactly one attack can ever be disabled. `AresAttackDisableManager` is a persistent singleton tracking the disabled attack by **`Type`** (`typeof(GroundSlamAttack)` etc.), not by array index — this is a deliberate choice, not an arbitrary one: `BossAttackRegistry`'s internal array (built via `GetComponents<BossAttackBase>()`, i.e. Unity's component declaration order) and `BossAgent`'s `attacks[8]` array (a separate, hand-wired Inspector order — §6.2) are independently ordered over the same eight components, so an index chosen against one array has no guaranteed meaning against the other. Both `BossAttackRegistry.TryExecuteRandomAttack()` and `BossAgent.WriteDiscreteActionMask()` independently check `IsPermanentlyDisabled(attack)` by type before considering an attack available — so whichever system is currently live (§7.1), a permanently-disabled attack stays off in both. Masking on `BossAgent`'s side is inference-time only; the trained checkpoint itself is never retrained or altered by this mechanic.

---

## 8. Training history and real bugs found (why the current policy behaves the way it does)

This is included because it's directly relevant to interpreting what `BossAgent_v3` actually is — a case study, not just a deployment footnote.

**v1 (discarded):** an earlier checkpoint (11 actions/18 observations, includes 4 attacks that no longer exist in the current 8-attack roster) spammed a single move repeatedly. Root cause: no memory of the boss's own recent actions in the observation vector, and a pure hit/win-lose reward with no repetition penalty — training entropy collapsed from 2.25 to 0.75. This directly motivated the variety-cooldown/variety-bonus design (§4, §6.4) in the v2 redesign.

**v2 (16-obs/9-action redesign, one full 2,000,065-step run, showed no convergence):** checkpoint rewards oscillated flat/noisy around -0.4 to -0.5 for the whole run, never climbing. Investigating why surfaced two real, independent, compounding bugs, both found via live Console debugging rather than guessed:

- **Bug — reentrant `EndEpisode()` (found 2026-08-28):** whenever the *opponent* landed the killing blow, `BossHealth.Die()` fired synchronously from deep inside the opponent's own `Update()`/`TickFire()` call stack, cascading synchronously through the whole episode-end chain into `Agent.EndEpisode()` (ML-Agents) — reentrant with respect to ML-Agents' own expected call context. This threw a `NullReferenceException` inside ML-Agents' internal `UpdateSensors()` on effectively every single boss-loss episode of the entire v2 run, very likely silently corrupting that episode's trajectory data before the trainer finished recording it. An earlier try/catch fix (bug 1, the "boss never revives" symptom) had stopped this from blocking the world-reset visually, but did nothing to stop the underlying reentrancy or the exception itself from firing. Real fix: `BossTrainingEnv` no longer calls `EndEpisode()` directly from inside the death-handling callback; it now only latches a pending result and processes it from its own next normal `Update()` frame — removing the reentrancy rather than catching its symptom.
- **Bug — `BossStateMachine.UpdateCombat()` feeding `SetDestination()` an unprojected, elevated target (found 2026-08-28, immediately after the above):** exactly the NavMesh issue described in §2 — confirmed, after a lengthy elimination process (NavMesh bake/layer checks, `Warp()` diagnostics, a direct `NavMesh.CalculatePath()` sanity check, decisive re-`SetDestination()` test against a pre-sampled point), to be a target-tolerance issue rather than a coverage/bake problem. **This meant the boss could never actually chase via ordinary NavMesh pathing for its entire training history up to this point** — only its two manual gap-closer attacks (SprintCharge, Pounce) ever produced real movement, which is exactly why it looked like "the boss doesn't track, only uses gap-closers."

A third bug, found between these two (**`OnEnable()`/`OnDisable()` declared without the `override` keyword**, so `Agent.OnEnable()`'s call to `LazyInitialize()`/`Initialize()` — which is what disables `BossAttackRegistry` — never ran at all), meant that for an unknown stretch of earlier testing, **every attack anyone observed in Training.unity was actually `BossAttackRegistry`'s old random-heuristic fallback**, not `BossAgent`'s policy, discovered only by pausing Play mode and directly checking the registry's `enabled` checkbox live.

**v3 (same config and reward as v2, both above bugs fixed, one full run):** checkpoint rewards clearly improved at every measured point except one (+0.536 at 1.4M steps — the first time any run touched positive average reward at all — versus v2's best of -0.089; final -0.138 versus v2's final -0.592), with Cumulative Reward trending up overall across the run (not just a temporary bump that fully regressed, unlike v2) and Episode Length trending down (faster, more decisive resolutions). Not a smooth monotonic climb — there's a real, still-unexplained dip between the 1.4M and 1.6M checkpoints (-0.449) that would need win/loss/timeout-by-opponent-type instrumentation (not built) to properly diagnose rather than guess at from the reward curve alone. **Conclusion drawn at the time, and worth stating plainly for the report: the same reward design and hyperparameters that produced v2's flat non-convergence produced real (if unstable) learning once the two blocking bugs were fixed — the reward function itself was never the problem.**

**Deployment (Step 7, 2026-08-29):** `BossAgent_v3.onnx` wired onto the live `DROID_Boss.prefab` (Behavior Type: Inference Only) via the fallback path in §6.5. User-confirmed working in Play mode on Level 1.unity — the first time the live game (not just the training scene) had been playtested at all in this project.

**The GroundSlam finding (2026-08-29, not a bug — a legitimate reward-exploit case study):** once deployed, the boss was observed repeatedly favoring GroundSlam over every other attack. Pulling the actual tuning values off the prefab confirmed why: every attack shares an identical flat 3s cooldown regardless of damage, range, or AOE size, and GroundSlam is strictly dominant under that flat cost — highest damage (45), among the longest range (15m), and the only 5m-radius AOE, for the same "price" as, say, a 10-damage melee punch. `hitLandedRewardScale * (damage / opponentMaxHP)` scales directly with raw damage and comfortably outweighs the flat `varietyBonusReward` once damage gets large enough, so the freshness bonus was never strong enough to make the trained policy give up a reliable high-damage AOE hit for a weaker attack. **This is presented in the project's own documentation explicitly as a textbook "policy correctly maximizes a subtly misspecified reward" finding** — three fixes were discussed (rebalance GroundSlam's cost directly; reward-shape a v4 retrain; or leave it and document it as a finding) and the user's explicit decision was to leave it and document it, not to treat it as a defect.

---

## 9. Current status and open items (AI-decision-relevant only)

- **Deployed and confirmed working:** `BossAgent_v3.onnx`, Inference Only, live on Level 1.unity's `DROID_Boss`, smart/dumb toggle confirmed working via the opening-move-variance signal (§7.1).
- **GroundSlam dominance:** confirmed root cause (§8), explicitly deferred by the user — not fixed, not scheduled, documented as a finding rather than a bug. Do not start rebalancing it unprompted.
- **The 1.4M→1.6M v3 reward dip:** unexplained, low priority, no instrumentation built to investigate it properly.
- **No defensive/dodge action:** the 9-action space is purely offensive (Idle + 8 attacks); the damage-taken reward term is structurally weak as a result (§6.4). Adding real damage-avoidance agency would be a design change (new action, retrain), not something the current policy can express.
- **Phase 2 is movement-only right now:** `BossStateMachine` pauses and tints on crossing the HP threshold, and the observation vector carries the flag, but nothing downstream (attack tempo, unlocked moves) currently reads it — an intentionally unused hook, not a bug.
- **`BossAttackRegistry` balance:** untouched since `BossAgent` took over as the primary system; combat has been rebalanced elsewhere (medkits, damage numbers) since the registry was last the live path, and it's now reachable again in dumb mode via the Packet Filter toggle — flagged as worth a sanity pass, not urgent.
- **Report framing, stated explicitly in the project's own docs and worth repeating here:** ARES is the *only* RL/PPO-driven system in the entire AI-in-education-game design — Puzzle DDA and Combat DDA are both BKT/heuristic, and Deimos (despite superficially looking like "another boss") is also a hand-built heuristic FSM, not RL. Keep this framing consistent across whichever document discusses the project's AI systems as a whole.

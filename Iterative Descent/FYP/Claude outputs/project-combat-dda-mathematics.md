# ARBITEX Combat DDA — Complete Mathematical Framework

**Last Updated:** 2026-09-15
**Status:** Heuristic DDA only (no BKT blend, no PPO hook yet — see §5)
**Source files verified against:** `CombatDDAController.cs`, `PlayerMetricsTracker.cs`, `TierProfile.cs`, `EnemyDirector.cs`, `EnemyBase.cs`, `PlayerCombat.cs`, `PlayerHealth.cs`

---

## 1. OVERVIEW

Combat DDA evaluates player performance once per **encounter** (the span from the first enemy activating in a room to the last active enemy dying) and updates a single global `CurrentTier` (0–4) that `EnemyDirector` exposes to every enemy as scaling multipliers. Unlike Puzzle DDA, Combat DDA:

- has **no Bayesian Knowledge Tracing component** — it is purely heuristic signal aggregation
- has **no PPO/RL override hook** wired up yet (Puzzle DDA has a `scoreOverride` delegate; Combat DDA does not)
- scales enemy **toughness and aggression**, not enemy **count** (count-scaling was removed in the 2026-09 revamp — every pre-placed enemy in a room is always activated; the tier instead makes each one tankier/harder-hitting)
- as of 2026-09-15, also reacts to **player death** as a separate hard-penalty path alongside the per-encounter evaluation below — see §3.6

This keeps the report framing clean: **only ARES (the boss) is RL/PPO-driven; both DDA systems (Puzzle and Combat) are BKT/heuristic.**

---

## 2. ENCOUNTER LIFECYCLE

An encounter is tracked by `PlayerMetricsTracker` via an active-enemy counter, not a fixed timer:

```
NotifyEnemyActivated()   // called from EnemyBase.Activate()
  _activeEnemyCount++
  if _activeEnemyCount == 1:            // first enemy of a fresh encounter
      ShotsFired  = 0
      ShotsLanded = 0
      _encounterStartTime = Time.realtimeSinceStartup

NotifyEnemyKilled()      // called from EnemyBase.Die()
  _activeEnemyCount = max(0, _activeEnemyCount - 1)
  if _activeEnemyCount == 0:            // last enemy in the encounter just died
      LastEncounterDuration    = Time.realtimeSinceStartup - _encounterStartTime
      LastEncounterShotsFired  = ShotsFired
      LastEncounterShotsLanded = ShotsLanded
      fire OnEncounterEnd → CombatDDAController.Evaluate()
```

`Time.realtimeSinceStartup` is used for the same reason as Puzzle DDA — a wall-clock measure that can't be distorted by any `Time.timeScale` changes elsewhere in the game.

**Player death is not part of this lifecycle.** If the player dies before the last enemy in the room is killed, `_activeEnemyCount` never reaches 0 through this path and `OnEncounterEnd` never fires — see §3.6 for how death is scored instead, and §2.1 for how the counter gets cleaned up afterward.

### 2.1 Interaction with checkpoint respawn

`CheckpointManager.RespawnPlayer()` calls `EncounterTrigger.ResetEncounter()` for whichever encounter(s) the current checkpoint is ahead of. `ResetEncounter()` calls `Activate()` again on **every** enemy assigned to that trigger — dead or still alive — which unconditionally re-fires `NotifyEnemyActivated()` for each one (`EnemyBase.Activate()` only guards against `_isDead`, not against being reactivated while already alive). Left alone, this double-counts any enemy that survived the player's death, since that enemy was never `Die()`'d and so never decremented the counter.

`PlayerMetricsTracker.ResetActiveEncounter()` (added 2026-09-15) zeroes `_activeEnemyCount` and is called from the death handler in §3.6, immediately on death, before any respawn/reset happens. This makes the subsequent re-activation rebuild the count from a clean 0 rather than stacking on top of the pre-death count — this was a latent bug in the checkpoint-rollback path that was only exposed once death started being handled at all.

---

## 3. SIGNAL COMPUTATION

### 3.1 Accuracy [weight 35%]

```
accuracy = Clamp01(LastEncounterShotsLanded / LastEncounterShotsFired)
```

`ShotsFired`/`ShotsLanded` are pooled across all three player weapons, but "landed" is counted differently per weapon:

- **Pistol / Rifle:** one `NotifyShotFired()` per trigger pull, one `NotifyShotLanded()` the instant that single projectile registers a hit (binary hit/miss).
- **Shotgun:** one `NotifyShotFired()` per trigger pull, but the shot only counts as landed once at least `pelletHitFractionForDDA` (default **40%**) of that shot's pellets connect — resolved once every pellet in the volley has hit or expired.

Active only if `LastEncounterShotsFired > 0`.

### 3.2 Normalised health [weight 30%]

```
healthSignal = Clamp01(CurrentHealth / maxHealth)
```

Read from `PlayerHealth` at the moment the encounter ends (`maxHealth` default = 100). Active only if a `PlayerHealth` reference is available.

### 3.3 Combat time [weight 20%]

```
timeSignal = Clamp01(1 - InverseLerp(fastCombatTime, slowCombatTime, LastEncounterDuration))

// fastCombatTime = 15s → timeSignal = 1.0 (fast kill, harder player)
// slowCombatTime = 90s → timeSignal = 0.0 (slow kill, easier player)
```

Active only if `LastEncounterDuration > 0`.

### 3.4 Normalised ammo [weight 15%]

```
TotalAmmoNormalised = InverseLerp(minAmmoCount, ammoCombatSoftCap, currentMag + spareAmmo)

// minAmmoCount       = 5   → treated as the "0" end of the range (a floor, not true zero)
// ammoCombatSoftCap  = 48  → treated as the "1" end of the range
```

This uses a floor instead of raw zero (2026-09 revamp) so a player sitting on a handful of rounds isn't scored the same as a player with none. Active only if a `PlayerCombat` reference is available.

### 3.5 Weighted aggregation (partial-data safe)

Exactly the same active-coverage renormalisation pattern as Puzzle DDA — a missing signal is dropped rather than dragging the score toward zero:

```
score = 0, totalWeight = 0
for each active signal i:
    score       += weightᵢ × signalᵢ
    totalWeight += weightᵢ

rawScore = Clamp01(score / totalWeight)     // if totalWeight == 0, keep previous tier (no update)
```

Weights: accuracy = 0.35, health = 0.30, time = 0.20, ammo = 0.15 (sum to 1.0 when all four signals are active).

This weighted-aggregation path only ever runs on `OnEncounterEnd` (§2). Player death does not go through it — see §3.6.

### 3.6 Death penalty (added 2026-09-15)

Player death (`PlayerHealth.OnPlayerDied`) is scored independently of §3.5, not as a fifth weighted signal blended in at encounter end. Two reasons: dying doesn't necessarily coincide with an encounter ending (the player can die mid-fight, before the last enemy is killed, in which case `OnEncounterEnd` never fires for that encounter at all — see §2), and a death is meant to read as a hard, unambiguous failure rather than something that can be partially offset by good accuracy or a fast partial kill count earlier in the same fight.

```
CombatDDAController.OnPlayerDied():
    rawScore     = deathPenaltyScore                              // default 0 — worst possible score
    CurrentScore = Lerp(rawScore, CurrentScore, deathSmoothing)    // default 0.2 (history weight)
    CurrentTier  = ScoreToTier(CurrentScore)
    fire OnCombatTierChanged if tier moved
    PlayerMetricsTracker.Instance.ResetActiveEncounter()           // see §2.1
```

`deathSmoothing` (default **0.2**) is deliberately much lower than the normal `scoreSmoothing` (default **0.5**, §4) — a lower history weight means the new value dominates more, so a death pulls `CurrentScore` sharply toward 0 in one step rather than being eased in gradually like a normal encounter result. Same Lerp convention as §4 (history-weight, not new-value-weight — see the naming caveat there).

**Worked example:** player is at Tier 3 (`CurrentScore = 0.75`) and dies. `CurrentScore = Lerp(0, 0.75, 0.2) = 0.2×0.75 + 0.8×0 = 0.15` → Tier 0 (Very Easy). A single death can drop the player three full tiers in one step, versus the normal per-encounter path where even a worst-case encounter (`rawScore = 0` at `scoreSmoothing = 0.5`) would only halve the score.

Both `deathPenaltyScore` and `deathSmoothing` are `[SerializeField]` Inspector fields on `CombatDDAController`, tunable independently of the four encounter-signal weights and of `scoreSmoothing`.

---

## 4. SMOOTHING

**Inspector field:** `scoreSmoothing`, default **0.5**, range [0, 0.95].

```
CurrentScore(t) = Lerp(rawScore(t), CurrentScore(t-1), scoreSmoothing)
                = (1 − scoreSmoothing) × rawScore(t) + scoreSmoothing × CurrentScore(t-1)
```

**Note on naming vs. Puzzle DDA:** Puzzle DDA's α is the *new-value* weight (`α × raw + (1−α) × history`, default α = 0.3). Combat DDA's `scoreSmoothing` is the *history* weight — it's the `t` argument to `Mathf.Lerp(raw, previous, t)`. At the default of 0.5 the two systems behave identically (50/50), but they are not the same parameter and should not be transcribed with the same symbol in the report. In Combat DDA's own terms: `scoreSmoothing = 0` → instant response to the latest encounter; `scoreSmoothing = 0.95` → very gradual, history-dominated.

Update only happens on `OnEncounterEnd`, so this is smoothing across successive **encounters**, not a per-frame filter. The death path (§3.6) uses its own separate `deathSmoothing` constant instead of this one.

---

## 5. TIER MAPPING

```
Tier 0 (Very Easy): score ∈ [0.00, 0.20)
Tier 1 (Easy):      score ∈ [0.20, 0.40)
Tier 2 (Normal):    score ∈ [0.40, 0.60)
Tier 3 (Hard):      score ∈ [0.60, 0.80)
Tier 4 (Very Hard): score ∈ [0.80, 1.00]
```

Implemented as an explicit if-chain in `ScoreToTier()` (functionally identical to `floor(score × 5)` clamped to [0,4] — same boundaries as Puzzle DDA's tier mapping).

**Default starting state** (before any encounter has completed): `CurrentScore = 0.30`, `CurrentTier = 1` (Easy) — a deliberately gentle first encounter rather than starting neutral at Tier 2.

`OnCombatTierChanged(int newTier)` fires only when the tier actually changes value, for HUD/audio hooks to subscribe to. This fires from both the §3.5 encounter-end path and the §3.6 death path.

---

## 6. TIER → GAMEPLAY PARAMETERS (`TierProfile` / `EnemyDirector`)

Each tier is a `TierProfile` ScriptableObject exposing five multipliers, read by `EnemyDirector` from whichever tier `CombatDDAController.CurrentTier` currently reports:

| Field | Applied where | Effect |
|---|---|---|
| `speedMultiplier` | `EnemyChaser`/etc. on `Activate()` | scales NavMeshAgent chase speed |
| `damageMultiplier` | `EnemyAttack` | scales melee damage |
| `enemyHealthMultiplier` | `EnemyBase.Activate()` | `maxHealth = baseMaxHealth × healthMult`, then fully heals to that new max |
| `damageMitigation` | `EnemyBase.TakeDamage()` | `effectiveDamage = incomingDamage × (1 − mitigation)`, applied before health loss, hit reactions, and stagger-DPS all read the same reduced number |
| `ammoSpawnMultiplier` / `healthSpawnMultiplier` | reserved, not yet consumed anywhere | future use |

**Effective HP formula** (the real "shots needed to kill" multiplier once mitigation stacks on top of the raw health increase):

```
effectiveHP = enemyHealthMultiplier / (1 − damageMitigation)
```

### 6.1 Current tier table (2026-09 revamp — placeholder values, not yet balance-passed)

| Tier | Speed | Damage | Health | Mitigation | Effective HP |
|---|---|---|---|---|---|
| 0 (Very Easy) | 0.90× | 0.80× | 0.80× | 0% | **0.80×** |
| 1 (Easy) — default start | 1.00× | 1.00× | 1.00× | 0% | **1.00×** |
| 2 (Normal) | 1.05× | 1.05× | 1.15× | 8% | **1.25×** |
| 3 (Hard) | 1.10× | 1.10× | 1.30× | 15% | **1.53×** |
| 4 (Very Hard) | 1.20× | 1.15× | 1.50× | 25% | **2.00×** |

Worked example (Tier 3): `1.30 / (1 − 0.15) = 1.30 / 0.85 = 1.529…` → shown as 1.53×.

### 6.2 Why toughness, not count

Prior to the 2026-09 revamp, higher tiers spawned more enemies (`enemyCountMultiplier`). This was removed: `EncounterTrigger` now always activates every pre-placed enemy in a room regardless of tier, and difficulty is expressed entirely through the toughness/aggression multipliers above — a harder tier costs the player more ammo per enemy rather than throwing more enemies at them simultaneously.

Because `EnemyDirector`'s multiplier getters read `CombatDDAController.CurrentTier` live (not cached), a tier change from one encounter doesn't retroactively affect enemies already mid-fight — it takes effect the next time `EnemyBase.Activate()` runs (i.e., the next encounter).

---

## 7. INTEGRATION FLOW (end-to-end)

```
EncounterTrigger fires → activates every pre-placed enemy in the room
  → EnemyBase.Activate() per enemy
       maxHealth = baseMaxHealth × EnemyDirector.Instance.EnemyHealthMultiplier
       currentHealth = maxHealth
       PlayerMetricsTracker.NotifyEnemyActivated()   // resets ShotsFired/Landed on 0→1

Player fires weapon → PlayerCombat.HandleFiring() → NotifyShotFired()
Hit registers        → NotifyShotLanded()            // per-weapon rule, see §3.1

Enemy.TakeDamage(amount)
  effectiveDamage = amount × (1 − EnemyDirector.Instance.DamageMitigation)
  currentHealth -= effectiveDamage
  if currentHealth <= 0: Die()

EnemyBase.Die() → PlayerMetricsTracker.NotifyEnemyKilled()
  if last active enemy: snapshot Duration/ShotsFired/ShotsLanded → fire OnEncounterEnd

CombatDDAController.OnEncounterEnd → Evaluate()
  compute rawScore (§3.5) → smooth into CurrentScore (§4) → ScoreToTier (§5)
  → OnCombatTierChanged(newTier) if it moved

── in parallel, independent of the above ──

PlayerHealth.Die() → OnPlayerDied
  → CombatDDAController.OnPlayerDied()   // §3.6
       force rawScore = deathPenaltyScore → smooth with deathSmoothing → ScoreToTier
       → OnCombatTierChanged(newTier) if it moved
       → PlayerMetricsTracker.ResetActiveEncounter()   // §2.1

Next encounter's EnemyBase.Activate() calls read the (possibly new) tier's TierProfile
```

---

## 8. TUNING PARAMETERS & CALIBRATION NOTES

### 8.1 Current thresholds
- **Fast combat time:** 15s (`fastCombatTime`)
- **Slow combat time:** 90s (`slowCombatTime`)
- **Min ammo floor:** 5 rounds (`minAmmoCount`)
- **Ammo soft cap:** 48 rounds (`ammoCombatSoftCap`)
- **Shotgun pellet-hit threshold:** 40% of pellets in a volley (`pelletHitFractionForDDA`)
- **Score smoothing:** 0.5 (history weight — see §4 for the naming caveat)
- **Death penalty score:** 0.0 (`deathPenaltyScore`) — worst possible raw score
- **Death smoothing:** 0.2 (`deathSmoothing`) — deliberately lower than `scoreSmoothing` so death drops are large and immediate

### 8.2 Known open items (as of 2026-09-15, from project tracking)
- `scoreSmoothing` jitter has not been reviewed for Combat DDA specifically
- No asymmetric tier-boundary bands (hysteresis) implemented — a score sitting right on a boundary can flip tiers back and forth between encounters
- No minimum-attempts guard before reaching Tier 2+ (a single encounter's result can jump the tier immediately) — the same is now true of a single death (§3.6), by design
- The 15s/90s fast/slow combat-time thresholds are called out as still needing review, separately from the toughness table itself
- The Tier 0–4 toughness table (§6.1) is explicitly a first-pass placeholder awaiting its own balance pass — cite it in the report as "current values," not tuned/validated ones
- The death penalty (§3.6) is implemented but not yet playtested — `deathPenaltyScore`/`deathSmoothing` values are a first pass, not a balance-tested pair
- Repeated deaths in quick succession will each independently pull `CurrentScore` toward 0 (no cooldown/debounce between death evaluations) — worth playtesting before citing a specific "tiers dropped per death" figure in the report, since it depends on how close together deaths can occur

---

## 9. FILES & CODE REFERENCES

| File | Purpose |
|---|---|
| `CombatDDAController.cs` | Signal weighting, smoothing, tier mapping, death-penalty handling, `OnCombatTierChanged` event |
| `PlayerMetricsTracker.cs` | Encounter lifecycle (`NotifyEnemyActivated`/`NotifyEnemyKilled`), shot counters, `OnEncounterEnd` event, `ResetActiveEncounter()` |
| `TierProfile.cs` | Per-tier ScriptableObject: speed/damage/health/mitigation/spawn multipliers |
| `EnemyDirector.cs` | Reads `CombatDDAController.CurrentTier`, exposes live multipliers to enemies |
| `EnemyBase.cs` | Applies `EnemyHealthMultiplier` on `Activate()`, `DamageMitigation` on `TakeDamage()`, calls the two `PlayerMetricsTracker` notify methods |
| `PlayerCombat.cs` | `TotalAmmoNormalised`, shot-fired/landed notifications per weapon |
| `PlayerHealth.cs` | `CurrentHealth`/`maxHealth`, `OnPlayerDied` event consumed by §3.6 |
| `CheckpointManager.cs` / `EncounterTrigger.cs` | Checkpoint respawn / encounter reset — see §2.1 for the interaction with the death penalty |

---

## 10. MATH SUMMARY TABLE

| Operation | Formula | Purpose |
|---|---|---|
| **Accuracy signal** | `ShotsLanded / ShotsFired` | 35% of score |
| **Health signal** | `CurrentHealth / maxHealth` | 30% of score |
| **Time signal** | `1 − InverseLerp(15, 90, duration)` | 20% of score |
| **Ammo signal** | `InverseLerp(5, 48, mag + spare)` | 15% of score |
| **Weighted raw score** | `Σ(signalᵢ × weightᵢ) / Σ(activeWeightsᵢ)` | combine with partial-data safety |
| **Smoothing** | `(1−s)×raw(t) + s×CurrentScore(t−1)`, s = 0.5 | smooth tier transitions across encounters |
| **Death penalty** | `Lerp(0, CurrentScore, 0.2)` | hard, immediate tier drop on player death (§3.6) |
| **Tier mapping** | boundaries at 0.20/0.40/0.60/0.80 (≡ `floor(score×5)`) | convert [0,1] → tier [0,4] |
| **Effective HP** | `enemyHealthMultiplier / (1 − damageMitigation)` | real difficulty multiplier per tier |
| **Damage after mitigation** | `incomingDamage × (1 − damageMitigation)` | applied in `TakeDamage()` |

---

## 11. KEY CONTRASTS WITH PUZZLE DDA (useful for the report's comparison section)

| Aspect | Puzzle DDA | Combat DDA |
|---|---|---|
| Knowledge model | Blends heuristic score with BKT `P(knows)` (50/50) | Heuristic only, no BKT |
| PPO/RL hook | `scoreOverride` delegate present (unused, PPO-ready) | No override hook implemented |
| Difficulty expressed via | Puzzle content/complexity per tier (card counts, table sizes, etc.) | Enemy toughness + aggression multipliers (not enemy count) |
| Update cadence | Per puzzle solve | Per encounter (first-activation → last-death), plus an independent hard-penalty path on player death |
| Smoothing convention | α = new-value weight (default 0.3) | `scoreSmoothing` = history weight (default 0.5); death path uses a separate, lower `deathSmoothing` (0.2) — inverse convention, same 50/50 only coincidentally when set that way |
| Tier boundaries | 0.20/0.40/0.60/0.80 | Identical: 0.20/0.40/0.60/0.80 |
| Failure state (player death) | N/A — puzzles have no fail/death state | Explicit hard-penalty path (§3.6), independent of the normal signal weighting |

---

## 12. NEXT STEPS FOR REPORT

1. **Signal justification:** explain why accuracy (35%) and health (30%) outweigh time (20%) and ammo (15%) — the design intent is that *how* the player fought (skill, survivability) matters more than *how fast* or *how efficiently*.
2. **Effective HP derivation:** show the worked Tier 3 example (§6.1) to demonstrate that mitigation compounds multiplicatively with raw health, not additively.
3. **Calibration section:** be explicit that the Tier 0–4 toughness table and the 15s/90s time thresholds are first-pass, untuned values (§8.2) — this is honest framing that a marker will respect more than presenting them as final.
4. **Comparison table:** §11 above is close to report-ready as a "Puzzle DDA vs. Combat DDA" comparison table.
5. **Limitations:** no hysteresis/minimum-attempts guard means the current system is more reactive (and more prone to oscillation) than the Puzzle DDA side — worth naming as a known limitation rather than letting a marker discover it independently. The death penalty (§3.6) sharpens this same limitation: a single death with no debounce can now swing three tiers in one step, which is worth citing as a deliberate design trade-off (strong signal, no smoothing safety net) once it's been playtested.

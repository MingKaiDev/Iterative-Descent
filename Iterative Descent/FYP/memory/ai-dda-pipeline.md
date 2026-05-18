# AI & DDA Pipeline

## Overview
The project uses two decoupled AI systems. The existing `DDAController` + `PlayerMetricsTracker` infrastructure is shared foundation, but each system adapts a different game domain.

**System 1 — Puzzle AI (educational layer):** BKT (Bayesian Knowledge Tracing) models player CS knowledge state per concept. A supervised DDA policy (trained offline, deployed via Sentis) takes BKT state + performance metrics and outputs puzzle parameters. This is the Sprint 2–3 priority.

**System 2 — Combat AI (engagement layer):** Heuristic DDA for the basic chaser enemy (`EnemyChaser`) now. An RL-trained Stalker enemy (Alien Isolation / Mr. X style) in a future phase. The two systems are decoupled; puzzle performance does not directly drive combat difficulty.

The `DDAController` heuristic currently runs in **display-only mode**. The `AgentScoreOverride` delegate hook remains in place for when the supervised Puzzle DDA policy is ready.

---

## PlayerMetricsTracker — COMPLETE

- Singleton on persistent `GameManager` (`DontDestroyOnLoad`)
- Subscribes to puzzle events via `OnEnable` / `OnDisable`
- All puzzle timers use `Time.realtimeSinceStartup` (immune to `Time.timeScale = 0f`)

### Why realtimeSinceStartup
Puzzles set `Time.timeScale = 0f`, which freezes all `deltaTime`-based clocks. `NotifyQuizStarted()` is called from `PuzzleUI.Setup()` and `NotifyLinkedListStarted()` from `LinkedListPuzzleUI.InitPuzzle()` — both self-contained, no prop dependency.

---

## RoomTrigger.cs
- Attach to trigger `BoxCollider` child inside each room
- Calls `EnterRoom` / `ExitRoom` on `PlayerMetricsTracker`
- Handles spawn-inside edge case via `Physics.OverlapBox` in `Start()`
- Draws Scene gizmo with room label

**Configured rooms:** `MainHall_Horror`, `Washroom`, `Hallway1`, `ClassRoom1`

---

## Director Observation Vector (15 values, normalised [0, 1])

| Index | Metric |
|---|---|
| 0 | Total session time |
| 1 | Current room time |
| 2 | Average completed room time |
| 3 | Previous room time |
| 4 | Rooms visited |
| 5 | Last quiz score |
| 6 | Last quiz time |
| 7 | Last quiz passed |
| 8 | Average quiz score |
| 9 | Total quiz attempts |
| 10 | Last linked list wrong attempts |
| 11 | Last linked list solve time |
| 12 | Total linked list puzzles solved |
| 13 | Average linked list wrong attempts |
| 14 | Average linked list solve time |

---

## DDA Signal Computation (Heuristic Mode)

### Quiz Signals — 60% combined weight
| Signal | Weight | Computation |
|---|---|---|
| Average quiz score | 35% | Taken directly as 0–1 |
| Last quiz speed | 15% | `InverseLerp(20s fast, 90s slow)`, inverted (fast = 1.0) |
| Pass rate | 10% | `TotalQuizPassed / TotalQuizAttempts` |

### Linked List Signals — 40% combined weight
| Signal | Weight | Computation |
|---|---|---|
| Accuracy | 25% | `1 - (avgWrongAttempts / 8)` — fewer wrongs = higher signal |
| Speed | 15% | `InverseLerp(20s, 120s)`, inverted (fast = 1.0) |

**Normalisation:** Score normalised by active weight so partial data doesn't drag score down.  
**Smoothing:** Exponential moving average (default α = 0.5).

---

## Difficulty Tiers

| Score Range | Tier | Label |
|---|---|---|
| 0.00–0.19 | 0 | Very Easy |
| 0.20–0.39 | 1 | Easy |
| 0.40–0.59 | 2 | Normal |
| 0.60–0.79 | 3 | Hard |
| 0.80–1.00 | 4 | Very Hard |

---

## AgentScoreOverride Hook

`DDAController` exposes a delegate property `AgentScoreOverride` (`Func<float[], float>`). When the supervised Puzzle DDA policy is ready:
1. Assign the delegate from the Sentis inference wrapper
2. The heuristic computation is bypassed entirely
3. No other changes needed anywhere else

---

## DDADisplayHUD.cs
- `OnGUI` overlay — no Canvas required
- Toggle with **H key**
- Displays: tier badge (colour-coded), difficulty score bar, raw vs smoothed score, per-signal breakdown bars with weights, live quiz metrics, live linked list metrics

---

## Sprint 2–3 — AI Pending Items

| Item | Description |
|---|---|
| BKT model | `BKTModel.cs` — per-concept knowledge state, updates on puzzle events |
| Concept tagging | MCQ questions and linked list puzzles tagged with CS concept IDs |
| Supervised DDA policy | Python training → .onnx export → Sentis inference wrapper |
| `EnemyDirector.cs` | Reads `DDAController.CurrentTier` → applies to `EnemyChaser` speed / spawn delay |
| `DifficultyProfile` | ScriptableObjects for per-tier parameter presets |

---

## Observation Vector — Revision Needed

The current 15-value vector (indices 0–4 = room timing) has weak signal for a trained model. Planned revision before Sentis deployment:

| Index | Replace with |
|---|---|
| 0 | Player health (normalised) |
| 1 | Recent quiz delta (last score − previous score) |
| 2 | Puzzle retry count this session |
| 3 | Enemy encounters without death (normalised) |
| 4 | Time since last puzzle (normalised 0–300s) |

Indices 5–14 (quiz + linked list metrics) remain unchanged — they carry strong signal.

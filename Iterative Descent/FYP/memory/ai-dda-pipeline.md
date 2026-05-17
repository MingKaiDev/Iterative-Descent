# AI & DDA Pipeline

## Overview
The Dynamic Difficulty Adjustment (DDA) system feeds a 15-value normalised observation vector to a PPO reinforcement learning agent (DirectorAgent). In Sprint 1, the system runs in **heuristic mode** (display only, no gameplay effect). The PPO agent is the Sprint 2 priority.

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

## PPO Hook (AgentScoreOverride)

`DDAController` exposes a delegate property `AgentScoreOverride`. When `DirectorAgent.cs` is ready:
1. Assign the delegate in `DirectorAgent.cs`
2. The heuristic computation is bypassed entirely
3. No other changes needed anywhere else

---

## DDADisplayHUD.cs
- `OnGUI` overlay — no Canvas required
- Toggle with **H key**
- Displays: tier badge (colour-coded), difficulty score bar, raw vs smoothed score, per-signal breakdown bars with weights, live quiz metrics, live linked list metrics

---

## Sprint 2 — AI Pending Items

| Item | Description |
|---|---|
| `DirectorAgent.cs` | PPO agent consuming the 15-value observation vector |
| Reward function | Design, action space definition, training configuration |
| `EnemyDirector.cs` | Reads `CurrentTier` to drive enemy spawn rate / speed |
| `DifficultyProfile` | ScriptableObjects for per-tier configuration |
| Sentis inference | Load trained .onnx model at runtime via Unity Sentis |

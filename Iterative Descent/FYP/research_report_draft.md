# FYP Research Report — Informal Draft
**Project:** AI-Enabled Serious Game — "Iterative Descent"
**Author:** Ming Kai
**Last updated:** 2026-05-18

> **Note:** This is an informal working draft to sanity-check understanding before writing the formal thesis. The tone is explanatory and direct — written to myself first, the examiner second. Sections marked `[TODO]` need research citations or further fleshing out.

---

## 1. Introduction & Motivation

### What is the problem?

Computer Science education has a well-documented engagement problem. Traditional instructional methods — lectures, worksheets, textbook exercises — struggle to keep learners engaged, particularly when covering abstract topics like data structures (linked lists, trees, graphs). Students often encounter these concepts passively, without any real feedback loop to tell them whether they actually *understand* or are just memorising surface-level answers.

Serious games — games designed primarily for a purpose other than entertainment — offer a compelling alternative. They situate learning within active, goal-directed play, creating conditions where students are intrinsically motivated to engage with the material. The question isn't whether games can teach (there's substantial evidence they can), but whether we can make them *adaptive* — responsive to the individual learner's current knowledge state, rather than delivering the same difficulty curve to everyone.

This is the core problem this project addresses: **how do we build a serious game that genuinely adapts its educational content and threat level to an individual player, in real time, using AI?**

### Why is this interesting / worth doing?

Most educational games fall into one of two failure modes:

1. **Too easy / static** — the game is engaging but the difficulty never pushes the learner beyond what they already know. There's no zone-of-proximal-development stretch.
2. **Too hard / unadaptive** — the game is frustrating because it doesn't account for prior knowledge, leading learners to disengage or simply memorise the "correct" answers without internalising concepts.

Dynamic Difficulty Adjustment (DDA) in commercial games (e.g. Resident Evil 4's AI Director, Left 4 Dead's Director system) solves the engagement problem for entertainment games. The idea is that an invisible director modulates challenge in real time to keep the player in a state of flow — not too easy, not too overwhelming. Applying this principle to a *learning* context is more nuanced because there are two orthogonal dimensions to optimise: **engagement** (is the player having fun?) and **learning** (is the player actually acquiring knowledge?).

The hypothesis of this project is that a well-designed AI system — combining established cognitive modelling techniques (Bayesian Knowledge Tracing) with modern machine learning approaches (PPO reinforcement learning, supervised inference) — can navigate this dual optimisation effectively within a horror-themed rogue-like game.

The horror genre is a deliberate choice. Horror games are inherently high-stakes: limited resources, persistent threat, time pressure, environmental storytelling. These properties create a context where CS puzzle-solving feels *meaningful* — you're not doing a dry exercise, you're solving a linked-list puzzle to unlock a door before the enemy reaches you. The cognitive load introduced by fear and urgency actually mirrors conditions under which retention is strongest, because emotional arousal strengthens memory consolidation. [TODO: cite neuroscience of arousal + memory, e.g. McGaugh 2004]

### What does this project actually build?

Concretely, the deliverables are:
- A playable Unity 3D horror game set across a multi-floor school building
- A modular puzzle system (MCQ quiz, linked-list pointer-rewiring, password entry) that teaches data structures and CS concepts
- An AI-driven DDA system that adapts puzzle difficulty based on measured player performance
- An enemy AI system (currently: heuristic NavMesh chaser; future: RL-trained Stalker enemy)
- A full FYP research report documenting the design, implementation, and evaluation

---

## 2. System Design & Architecture

### The core design decision: two separate AI systems

Early in the design process, a critical architecture choice was made: this project does **not** use a single AI system to control everything. Instead, there are two entirely separate, loosely-coupled AI subsystems:

1. **Puzzle AI** — controls the educational layer (what CS topic to present, how hard to make the puzzle)
2. **Combat AI** — controls the threat layer (how aggressive/intelligent the enemy feels)

This separation is deliberate and important. Conflating the two would mean that a player who is struggling with a linked-list puzzle might also be given a weaker enemy — which doesn't necessarily make sense, and could produce training signal noise for either AI system. A player can be a fast, confident puzzle solver but terrible at evading enemies, or vice versa.

The two systems share a single **global stress signal** — a single float representing combined player distress. If both systems independently detect that the player is overwhelmed (failing puzzles *and* taking heavy enemy damage), both soften simultaneously. This prevents the game from pile-driving the player when multiple failure axes align. But outside of this shared signal, the systems operate independently.

### System 1: Puzzle AI (Educational Layer)

The puzzle AI has two components that work together:

**Bayesian Knowledge Tracing (BKT)**

BKT is a probabilistic cognitive model originally developed for intelligent tutoring systems (Corbett & Anderson, 1994). [TODO: get full citation] It maintains a probability estimate for each concept the learner might or might not know:

```
P(knows_k | attempt_n)
```

Given a learner's correct/incorrect answer on an exercise involving concept k, BKT updates its belief using four parameters:
- `P(L0)` — prior probability the learner already knew the concept before the exercise
- `P(T)` — probability of learning (transitioning from "not knowing" to "knowing") after an exercise
- `P(G)` — probability of guessing correctly despite not knowing
- `P(S)` — probability of slipping (answering incorrectly despite knowing)

In the context of this game, each CS topic (e.g. "linked list reversal", "array indexing", "pointer arithmetic") has its own BKT model. After each puzzle attempt, BKT is updated and used to decide:
- Which concept to present next (select the concept where the learner is most uncertain, to maximise information gain)
- Whether to increase or decrease puzzle complexity

The BKT update runs locally in C# using standard Bayes' rule. It does not require a network call or trained model — it is a hand-specified probabilistic computation. [TODO: write out the math properly]

**Supervised DDA (Difficulty Parameterisation)**

BKT tells us *what* to teach. The supervised DDA model tells us *how hard to make it*. Given the current BKT state across all concepts plus session-level performance metrics (average quiz score, solve time, retry count), a small neural network (trained offline via supervised learning on simulated player trajectories) outputs a **difficulty parameter vector** — things like:
- Number of nodes in the linked-list puzzle
- Whether hints are available
- Which question tier to pull from

This model is trained offline and exported as an `.onnx` file, then run at runtime via **Unity Sentis** (Unity's inference backend). The training data is synthetic: we simulate players with varying skill profiles and generate (state → difficulty parameter) pairs by running human-expert-tuned difficulty schedules against each profile. [TODO: flesh out training methodology in more detail — this needs more thought]

The current implementation has the heuristic DDA in place (a hand-written scoring function that mirrors what the supervised model will eventually do) with a clean handoff point — `DDAController.AgentScoreOverride` can be assigned to plug in the model without changing anything else.

**Current Implementation State (Sprint 1)**

The metrics collection, DDA controller, and HUD overlay are all complete. The system tracks:
- Per-concept quiz scores and timing (15-value observation vector)
- Linked-list puzzle: wrong attempts before solve, solve time
- Room dwell times (for future use in pacing decisions)
- A 5-tier difficulty scale: Very Easy → Easy → Normal → Hard → Very Hard

The DDA tiers are already wired to the linked-list puzzle: `LinkedListPuzzleUI.NodeCountForCurrentTier()` reads `DDAController.Instance.CurrentTier` and scales the number of nodes from 3 (Very Easy) to 6 (Very Hard).

What's still to be built: the BKT module, the offline training pipeline for the supervised DDA model, and the Unity Sentis deployment.

### System 2: Combat AI (Engagement Layer)

**Current (Sprint 1–2): Heuristic DDA for EnemyChaser**

The current enemy is `EnemyChaser` — a zombie-like cannon fodder enemy that chases the player and attacks at melee range. It uses a NavMeshAgent for pathfinding and a simple 3-state FSM: Idle → Chasing → Attacking.

The DDA component for combat is simple at this stage: `EnemyDirector.cs` (to be built) will read `DDAController.CurrentTier` and tune `EnemyChaser` parameters accordingly:
- `chaseSpeed` — how fast the enemy moves
- `activationDelay` — how long after a puzzle trigger before the enemy spawns
- Eventually: number of enemies, patrol patterns

The `EnemyChaser` is not RL-trained and never will be. It is deliberately kept simple — fast to implement, easy to tune, reliable enough for playtesting. It is cannon fodder, not the centrepiece AI.

**Future: RL Stalker Enemy**

The design ambition for the primary threat is an intelligent, persistent Stalker — inspired by the Alien in Alien: Isolation (Creative Assembly, 2014) and Mr. X in Resident Evil 2 (Capcom, 2019). The key design goal is that the enemy feels *smart* and *relentless*, not just fast.

The Alien Isolation alien is a well-known example of emergent intelligent behaviour in games. It uses a **dual-director pattern**: a high-level Director AI that decides where to deploy the alien based on the player's location and behaviour, and a lower-level alien brain that executes pursuit/search/ambush behaviours. The alien never "cheats" — it doesn't have direct access to the player's position. It must *infer* location from audio cues, footsteps, and visual contact. This is what makes it terrifying.

The planned Stalker enemy will be trained using **Proximal Policy Optimisation (PPO)** via Unity ML-Agents. The agent will learn:
- A pursuit policy: how to close distance on the player efficiently given a NavMesh graph
- A frustration system: the agent becomes "more aggressive" (shorter search patterns, less hiding) the longer it's been since it last saw the player — preventing tedium if the player hides forever
- A perception model: the agent only "knows" the player's location when it has line-of-sight or is within hearing range; otherwise it must reason from last-known position

The observation space will include: relative position (last known), distance to last known position, time since last contact, current health of enemy, room context (open/tight corridor), DDA tier signal.

The action space is a discrete set of navigational commands (set destination, rotate, wait, search nearby room).

Training will occur entirely in Unity Editor using ML-Agents' self-play or scripted-player-opponent setup. Inference will be deployed via Sentis.

This is a future phase — it begins once the puzzle AI system is complete and the game has a fully playable loop to train against.

### Level Architecture

The game is set across multiple floors of a school building. Completed rooms:

- Main Hall (spawn, double door south)
- Classroom 1 (puzzle: MCQ + linked list + password chain)
- Hallway 1 (connecting corridor)
- Washroom (exploration, horror props)
- Stairs 1 (scissors staircase connecting floors, complete with graffiti and horror atmosphere)

Pending: Classroom 2, Computer Lab, Classroom 3, General Office, Hallway 2, Stairs 2–3, Courtyard, Shack.

Each room is modelled in Blender (modular, 4m grid), exported as FBX, and assembled in Unity. All architectural assets follow a strict grid (4m × 4m cells, 3.5m floor-to-floor height) to ensure NavMesh baking is clean and enemy pathfinding has no degenerate geometry.

---

## 3. Implementation

### Interaction & Puzzle System

The interaction system is built on a clean interface-component pattern. Any GameObject in the scene becomes interactable by attaching three components:

- `InteractableBase` — owns the outline highlight, prompt UI, and in-range state
- `InteractableRegistrar` — self-registers/unregisters into `PlayerInteractor`'s static list (no `FindObjectsByType` calls at runtime)
- An `IInteractable` implementation — defines what actually happens on interaction (`PuzzleProp`, `ComputerScreenProp`, `PickupProp`)

`PlayerInteractor` runs entirely off its static list, scanning for objects within radius each frame, sorting nearest-first, and handling arrow-key cycling between multiple nearby objects.

Crucially, `PlayerInteractor` is *paused* (via `PlayerInteractor.Pause()`) whenever a puzzle UI is open. This prevents the player from accidentally triggering other interactables while mid-puzzle, and it ensures `Time.timeScale = 0f` (set by the puzzle props) doesn't create interaction jank.

**The puzzle chain in Classroom 1** is a three-step sequence:
1. The player finds a folder/note with the MCQ quiz (PuzzleProp → PuzzleUI). The quiz tests basic CS knowledge. A perfect score sets `PasswordScreenUI.FolderPuzzleSolved = true` (static flag, persists regardless of UI state).
2. The player interacts with the computer (ComputerScreenProp → PasswordScreenUI). If the folder was solved perfectly, the password auto-types itself character by character.
3. Logging in swaps the password panel for the linked-list puzzle (LinkedListPuzzleUI). Solving the linked list fires `OnLinkedListSolved`, which: (a) unlocks the door via `LinkedListEventHandler`, and (b) triggers the enemy break-in sequence via `EnemyEventBridge`.

This chain is deliberately one-directional and event-driven. No polling, no `Update()` state-checking. Each system fires events that the next system in the chain listens to, keeping everything loosely coupled.

### DDA Metrics & Observation Vector

`PlayerMetricsTracker` is a persistent singleton (DontDestroyOnLoad) that tracks 15 normalised values forming the PPO Director's observation vector. All puzzle timers use `Time.realtimeSinceStartup` (immune to `Time.timeScale = 0f`, which is set when puzzles open — using `Time.deltaTime`-based clocks here would give wrong timings).

The 15 observations cover three domains:
- **Room / time metrics** (indices 0–4): session time, current room dwell time, average completed room time, previous room time, rooms visited
- **Quiz metrics** (indices 5–9): last quiz score, last quiz time, last quiz passed (binary), average score, total attempts
- **Linked list metrics** (indices 10–14): last wrong attempts, last solve time, total puzzles solved, average wrong attempts, average solve time

`DDAController` computes a heuristic difficulty score by weighting these signals:
- Quiz group: 60% combined (avg score 35%, speed 15%, pass rate 10%)
- Linked list group: 40% combined (accuracy 25%, speed 15%)

The raw score is smoothed via exponential moving average (default α = 0.5) before being snapped to one of 5 tiers. When the PPO Director model is ready, `DDAController.AgentScoreOverride` receives the model's output, and the heuristic is bypassed without any other changes to the system.

### Enemy System Architecture

The enemy system is designed for extension without modification. `EnemyBase` is an abstract MonoBehaviour that owns: health, death, NavMeshAgent, Animator, and the `IDamageable` interface. Subclasses override four abstract hooks: `OnActivate`, `OnDeactivate`, `OnDie`, `OnHit`.

`EnemyChaser` extends `EnemyBase` and adds a 4-state FSM (Idle → Chasing → Attacking → Dead). Path recalculation is throttled to every 0.2s (configurable) to avoid the performance cost of calling `SetDestination` every frame.

`EnemyAttack` is a separate component handling the melee attack timing, fist hitbox management, and animation events. The fist hitbox (`FistHitboxRelay` on the fist bone) relays `OnTriggerEnter` up to `EnemyAttack.OnFistHit`, which applies damage to any `IDamageable` hit.

The enemy is activated by `EnemyEventBridge`, which listens for `LinkedListPuzzleUI.OnLinkedListSolved` and calls `enemy.Activate(playerTransform)` after a configurable delay (defaults to 2s — enough time for the door animation to complete before the enemy breaks through). The bridge uses a one-shot guard (`_triggered = true`) to prevent double-firing.

### Known Issues (as of Sprint 1 close)

Two bugs are confirmed and need fixing before the next sprint:

1. **`LinkedListPuzzleUI`**: `Invoke(nameof(ClosePanel), 1.5f)` will not fire because `Time.timeScale = 0f` while the puzzle is open. `Invoke` respects timeScale. Needs to be a `StartCoroutine` with `WaitForSecondsRealtime`.

2. **Double `NotifyQuizStarted()`**: Both `PuzzleProp.OpenPuzzle()` and `PuzzleUI.Setup()` call `NotifyQuizStarted()`, causing `TotalQuizAttempts` to be incremented twice per quiz. The call in `PuzzleProp.OpenPuzzle()` should be removed.

Other lower-priority items: `RoomTrigger.Start()` spawn-inside detection is commented out; `InteractableBase` outline colour mismatch (blue vs. documented yellow); mixed input system usage between `PlayerInteractor` (legacy) and `PlayerMovement`/`PlayerCombat` (New Input System).

---

## What's Next

Sprint 2 priorities:
- Fix the two critical bugs above
- Build `EnemyDirector.cs` to wire DDA tier → EnemyChaser parameters
- Begin BKT module design — define the concept set (CS topics covered by puzzles) and the update equations
- Start offline training pipeline design for supervised DDA model
- Continue level construction: Classroom 2, Computer Lab

[TODO: Literature Review section — DDA survey, BKT origins, serious games research, Alien Isolation as AI case study, RE2 Mr. X design analysis]

[TODO: Evaluation plan — how to measure if the DDA actually improves learning outcomes vs. static difficulty]

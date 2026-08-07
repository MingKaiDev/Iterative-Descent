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

---

## 2. Literature Review

### 2.1 Serious games and educational game design

The term "serious games" was coined by Clark Abt (1970), who defined them as games with "an explicit and carefully thought-out educational purpose and are not intended to be played primarily for amusement." Abt was writing about board games and policy simulations, but the principle transfers: games impose structure, goals, and feedback loops that can be deliberately shaped to build knowledge rather than just entertain.

Prensky (2001) made the case for digital serious games directly. His argument was that the properties that make games engaging, things like clear rules, adaptive feedback, and meaningful goals, are the same properties that make learning stick. Learners who resist a worksheet will voluntarily grind through a difficult game level, because the game frames the work as something worth doing. That matters because engagement is not incidental to learning; it is a prerequisite. A student who checks out halfway through retains very little, since later material builds on earlier material they never properly absorbed.

Gee (2003) went further and looked at how games structure knowledge acquisition rather than just motivating it. He identified 36 learning principles in well-designed commercial games, including learning in context rather than in the abstract, being allowed to experiment and fail without catastrophic consequence, and receiving information at the moment it becomes relevant. His conclusion was that good games already function as good tutors, and the implication for educational game design is that content should be woven into the mechanics, not added on top.

Several researchers formalised this into models. Garris, Ahlers, and Driskell (2002) proposed an input-process-output model where instructional content and game characteristics produce a game cycle of user behaviour and feedback, which in turn drives learning outcomes. The model treats motivational states like interest and challenge as the mechanism that converts game activity into actual learning: if the player is not engaged, the cycle runs but nothing sticks. Plass, Homer, and Kinzer (2015) added that no single learning theory covers everything, because games operate across cognitive, behavioural, affective, and sociocultural dimensions simultaneously.

The evidence that serious games work is real, though it is thinner than advocates sometimes imply. Connolly et al. (2012) reviewed 129 empirical studies and found consistent positive effects on knowledge acquisition and engagement, with complex multi-mechanic genres outperforming simpler ones. The catch is that many studies lacked control groups or ran too briefly to test retention rather than just immediate recall. The honest summary is that well-designed serious games can match conventional instruction on learning outcomes while clearly outperforming it on engagement, and that engagement advantage matters because it predicts whether students persist and return to the material.


**References cited in section 2.1:**
- Abt, C. C. (1970). *Serious Games*. Viking Press.
- Connolly, T. M., Boyle, E. A., MacArthur, E., Hainey, T., & Boyle, J. M. (2012). A systematic literature review of empirical evidence on computer games and serious games. *Computers & Education, 59*(2), 661-686.
- Garris, R., Ahlers, R., & Driskell, J. E. (2002). Games, motivation, and learning: A research and practice model. *Simulation & Gaming, 33*(4), 441-467.
- Gee, J. P. (2003). *What Video Games Have to Teach Us About Learning and Literacy*. Palgrave Macmillan.
- Plass, J. L., Homer, B. D., & Kinzer, C. K. (2015). Foundations of game-based learning. *Educational Psychologist, 50*(4), 258-283.
- Prensky, M. (2001). *Digital Game-Based Learning*. McGraw-Hill.

### 2.2 Flow theory, Zone of Proximal Development and engagement

Two theoretical constructs sit behind most of the serious game design literature when it comes to engagement: Csikszentmihalyi's flow theory and Vygotsky's Zone of Proximal Development. They come from different traditions but point at the same practical problem, which is how to keep a learner working at a level that is challenging enough to produce learning without being so hard that they give up.

Flow, as Csikszentmihalyi (1990) described it, is the subjective state of complete absorption in a task. It occurs when the demands of the activity are closely matched to the person's current skill level. When challenge significantly outpaces skill, the result is anxiety. When skill significantly outpaces challenge, the result is boredom. The narrow corridor between these two states is where flow lives, and it is characterised by focused attention, loss of self-consciousness, distorted time perception, and intrinsic motivation to continue. Nakamura and Csikszentmihalyi (2002) later identified the specific conditions that reliably produce flow: clear and proximate goals, immediate feedback on progress, and a perceived balance between challenge and personal capability. These conditions are not unique to games, but games are unusually good at satisfying all three simultaneously.

*[Figure: Csikszentmihalyi flow channel diagram -- challenge on the vertical axis, skill on the horizontal axis, with the flow corridor running diagonally, anxiety above and boredom below.]*

Vygotsky's Zone of Proximal Development (ZPD) addresses a related but distinct question. Where flow theory describes the experiential state a learner needs to be in, ZPD describes where their knowledge needs to be. Vygotsky (1978) defined the ZPD as "the distance between the actual developmental level as determined by independent problem solving and the level of potential development as determined through problem solving under adult guidance." In plain terms, the ZPD is the band of difficulty just above what a learner can already do unassisted. Tasks below the ZPD are too easy to produce learning. Tasks above it are inaccessible without support. The concept of scaffolding, formalised by Wood, Bruner, and Ross (1976) [insert citation here], extends this by describing how a more capable guide can temporarily support a learner through tasks that sit above their current independent capability, then gradually withdraw that support as competence grows.

The practical overlap between flow and ZPD is significant. Both frameworks require the difficulty of the task to track the learner's current level of capability. A fixed difficulty level satisfies neither: a learner who improves will pass through the optimal window and into boredom, while a learner who struggles will pass through it in the other direction into anxiety. This is the theoretical basis for Dynamic Difficulty Adjustment in educational games, explored in the next section. DDA is, in effect, an automated attempt to keep the player simultaneously in flow and inside their ZPD.

**References cited in section 2.2:**
- Csikszentmihalyi, M. (1990). *Flow: The Psychology of Optimal Experience*. Harper & Row.
- Nakamura, J., & Csikszentmihalyi, M. (2002). The concept of flow. In C. R. Snyder & S. J. Lopez (Eds.), *Handbook of Positive Psychology* (pp. 89-105). Oxford University Press.
- Vygotsky, L. S. (1978). *Mind in Society: The Development of Higher Psychological Processes*. Harvard University Press.

### 2.3 Dynamic Difficulty Adjustment

Dynamic Difficulty Adjustment (DDA) refers to a game automatically modifying its own challenge in response to observed player behaviour, rather than relying on a difficulty setting chosen once at the start of a session. The best documented commercial example is Valve's "AI Director" in Left 4 Dead, described by lead designer Michael Booth at GDC 2009. The Director does not control enemies directly. Instead it tracks aggregate player state, health, position, pacing since the last intense encounter, and adjusts the probability and intensity of future encounters to produce a deliberate rhythm of tension and relief rather than a flat difficulty curve. This "dramatic arc" model, where the game intentionally lets tension fall after a peak instead of stacking peaks indefinitely, is one of the clearest articulations of why DDA is not simply "get harder when the player is doing well."

Resident Evil 4 (Capcom, 2005) is frequently cited as a second commercial precedent, running a difficulty system that quietly adjusts enemy aggression, damage, and item drops based on recent player performance without ever surfacing this to the player. This claim is well corroborated in game-industry reporting (Engadget, 2015) but, unlike the Left 4 Dead Director, was never formally documented by Capcom in an academic or conference source, so it should be treated as strong anecdotal precedent rather than a citable technical specification.

The academic literature on DDA is broader than either single example. Zohaib (2018) and, more recently, Mortazavi, Moradi and Vahabie (2024) both survey the field and find that most DDA implementations fall into rule-based approaches, player-modelling approaches that infer skill or affect from telemetry, and increasingly machine-learning-driven approaches, with consistent positive effects reported on flow, motivation, and engagement across genres.

DDA in serious games specifically introduces a complication that the entertainment-only literature does not have to solve. Seyderhelm and Blackmore (2021), reviewing DDA in serious games and simulation training specifically, note that the field is fragmented across single-metric adaptation strategies (most commonly adapting one game parameter off one performance signal) and that only a minority of reviewed systems attempt to adapt difficulty using a broader model of the learner's cognitive state rather than raw performance alone. This is the central tension this project's literature sits in: an entertainment DDA system only has to answer "is the player still having fun," while an educational DDA system also has to answer "is the player still learning," and those two questions do not always move together. This tension is the direct motivation for treating puzzle difficulty and combat threat as two decoupled systems rather than a single difficulty dial, discussed further in Chapter 3.

**References cited in section 2.3:**
- Booth, M. (2009). *The AI Systems of Left 4 Dead*. Game Developers Conference (GDC) 2009, Valve Corporation.
- Engadget (2015). Resident Evil 4 secretly adjusted its difficulty for you. [INSERT CITATION HERE: confirm exact article author/byline before final submission]
- Mortazavi, F., Moradi, H., & Vahabie, A. H. (2024). Dynamic difficulty adjustment approaches in video games: a systematic literature review. *Multimedia Tools and Applications, 83*, 83227-83274.
- Seyderhelm, A. J. A., & Blackmore, K. (2021). Systematic Review of Dynamic Difficulty Adaption for Serious Games: The Importance of Diverse Approaches. [INSERT CITATION HERE: confirm final publication venue, currently sourced as an SSRN preprint]
- Zohaib, M. (2018). Dynamic Difficulty Adjustment (DDA) in Computer Games: A Review. *Advances in Human-Computer Interaction, 2018*, Article 5681652.

### 2.4 Intelligent Tutoring Systems and Bayesian Knowledge Tracing

Intelligent Tutoring Systems (ITS) are computer systems that model an individual learner's knowledge state and adapt instruction to it, rather than delivering fixed content to every learner. VanLehn (2011) conducted a meta-analysis comparing human tutoring, ITS, and untutored instruction, and found that step-based and substep-based ITS achieved effect sizes close to human one-on-one tutoring, a result that is frequently cited as the justification for treating ITS-style adaptive modelling as a credible substitute for individual attention in domains, like this project's, where one-on-one human tutoring does not scale.

Bayesian Knowledge Tracing (BKT) is the dominant student-modelling technique underlying ITS, introduced by Corbett and Anderson (1994) in the context of the ACT Programming Tutor. BKT represents each skill or concept as a binary latent state, "known" or "not known," and updates the probability that the learner knows it after every observed attempt using four parameters: the prior probability the learner already knew the skill (P(L0)), the probability of transitioning from not-knowing to knowing after an attempt (P(T)), the probability of guessing correctly despite not knowing (P(G)), and the probability of slipping, answering incorrectly despite knowing (P(S)). The appeal of this model for a real-time game context is that the update is a closed-form Bayesian calculation with no training loop and no external dependency, which is why it was chosen over a black-box classifier for the concept-tracking layer of this project.

Standard BKT is not without documented weaknesses. The base model assumes P(G) and P(S) are fixed constants for a given skill regardless of which specific question is asked, an assumption that does not always hold in practice. Baker, Corbett and Aleven (2008) addressed this by proposing a contextual estimation method that infers slip and guess probabilities per-question from surrounding performance rather than fixing them globally, and reported improved prediction accuracy over the standard model as a result. Separately, BKT is known in the literature to face an identifiability concern, where a given sequence of correct and incorrect answers can be fit equally well by more than one combination of parameters, though the exact scope of this issue is still debated and this project has not verified which specific paper should be credited as the original source. [INSERT CITATION HERE: primary source for the BKT parameter identifiability issue, distinct from Baker et al. 2008] A more recent systematic review by Šarić-Grgić, Grubišić and Gašpar (2024), covering twenty-five years of BKT research, confirms that contextual and related extensions remain an active area of refinement, and that fixed, hand-tuned parameters, as used in this project's default P(L0)=0.3, P(T)=0.1, P(S)=0.1, P(G)=0.25 configuration, represent a simplified but well-precedented starting point rather than the current state of the art. This is a reasonable trade-off for a single-semester project scope, but it is a limitation worth stating plainly rather than implying the model is more sophisticated than it is.

**References cited in section 2.4:**
- Baker, R. S. J. d., Corbett, A. T., & Aleven, V. (2008). More accurate student modeling through contextual estimation of slip and guess probabilities in Bayesian knowledge tracing. In *Proceedings of the 9th International Conference on Intelligent Tutoring Systems (ITS 2008)* (pp. 406-415). Springer.
- Corbett, A. T., & Anderson, J. R. (1994). Knowledge tracing: Modeling the acquisition of procedural knowledge. *User Modeling and User-Adapted Interaction, 4*(4), 253-278.
- Šarić-Grgić, I., Grubišić, A., & Gašpar, A. (2024). Twenty-five years of Bayesian knowledge tracing: a systematic review. *User Modeling and User-Adapted Interaction, 34*, 1127-1173.
- VanLehn, K. (2011). The relative effectiveness of human tutoring, intelligent tutoring systems, and other tutoring systems. *Educational Psychologist, 46*(4), 197-221.

### 2.5 Reinforcement Learning and AI Agents in Games

Reinforcement learning (RL) trains an agent to select actions that maximise cumulative reward through trial-and-error interaction with an environment, rather than being given labelled correct answers. Proximal Policy Optimisation (PPO), introduced by Schulman, Wolski, Dhariwal, Radford and Klimov (2017), is a policy-gradient method that constrains how far each update can move the policy, giving much of the stability of trust-region methods with a substantially simpler implementation. This combination of stability and simplicity is the reason PPO, rather than an older method like vanilla policy gradient or DQN, was the algorithm selected for this project's planned RL work, and it is also why PPO is the most widely used RL algorithm in applied game AI work generally, including implementations built on Unity ML-Agents.

The application of RL to games extends well beyond training an opponent to fight the player. Shao, Tang, Zhu, Li and Zhao's (2019) survey of deep RL in video games documents its use across arcade, first-person, and real-time-strategy genres, at both single-agent and multi-agent scale, and notes that the same core techniques used for opponent AI have also been applied to procedural content generation and adaptive game systems, which is the framing under which this project originally considered PPO as a candidate for the DDA director itself, before that approach was set aside (see Chapter 3.3.2).

Alien: Isolation (Creative Assembly, 2014) is the design reference most directly cited by this project's planned Stalker enemy. Creative director Alistair Hope's GDC 2015 talk describes a two-layer architecture: a "Director" AI that tracks player location and manages an internal "menace" gauge, periodically directing the Alien toward the player when tension needs to rise and pulling it away when the gauge indicates the player needs a break, and a separate, lower-level "Alien" behaviour layer that senses the player only through simulated hearing and line-of-sight rather than reading the player's true position directly. This perception restriction, not omniscience, is repeatedly identified as the reason the Alien is perceived as intelligent rather than simply relentless, and it is the specific design property this project's planned Stalker aims to reproduce.

Despite this precedent for RL-driven game agents, using RL as a difficulty controller rather than an opponent controller is considerably harder in practice, and the literature on RL reproducibility supports why. Henderson, Islam, Bachman, Pineau, Precup and Meger (2018) demonstrated that reported deep RL results are highly sensitive to implementation details, random seed, and hyperparameter choice, to the point that headline comparisons between algorithms are frequently not statistically meaningful without careful controlled evaluation. Combined with the sparse and delayed nature of any reward signal that is meant to represent "the player learned something" rather than "the player pressed the right button," this instability is the concrete, literature-supported basis for this project's finding, detailed in Chapter 3.3.2, that PPO was not viable as the primary controller for the educational difficulty layer and was instead retained only for the combat layer, where the reward signal is dense and immediate.

**References cited in section 2.5:**
- Henderson, P., Islam, R., Bachman, P., Pineau, J., Precup, D., & Meger, D. (2018). Deep reinforcement learning that matters. *Proceedings of the AAAI Conference on Artificial Intelligence, 32*(1).
- Hope, A. (2015). *Building Fear in Alien: Isolation*. Game Developers Conference (GDC) 2015, Creative Assembly.
- Schulman, J., Wolski, F., Dhariwal, P., Radford, A., & Klimov, O. (2017). Proximal Policy Optimization Algorithms. *arXiv:1707.06347*.
- Shao, K., Tang, Z., Zhu, Y., Li, N., & Zhao, D. (2019). A Survey of Deep Reinforcement Learning in Video Games. *arXiv:1912.10944*.

### 2.6 Evaluation of Serious Games

Evaluating whether a serious game works is harder than evaluating whether it is enjoyable, because "did learning occur" requires a defensible measurement of knowledge change, not just a satisfaction score. Connolly, Boyle, MacArthur, Hainey and Boyle (2012), already introduced in section 2.1, found consistent positive effects across 129 studies but also found that a large share of the underlying literature used weak designs, no control group, no pre/post comparison, or a single short session too brief to distinguish real learning from novelty effects. Boyle et al.'s (2016) update to that same review, covering more recent literature, found the picture had improved but not resolved: only 18 of the studies identified used a randomised controlled trial, while 72 used a quasi-experimental design, typically a pre-test/post-test around a single play session with an intact classroom or lab group rather than randomised assignment. This is presented not as a flaw specific to weaker studies but as a realistic constraint of the field, since randomised recruitment at scale is rarely available to a single research or student project.

All, Castellar and Van Looy (2016) argue that knowledge gain alone is an incomplete evaluation target for a digital game-based learning intervention, and propose a conceptual framework that also accounts for usability and engagement as necessary preconditions rather than secondary metrics. Their reasoning is that a game which fails on usability or fails to hold attention cannot produce a fair test of its pedagogical content, because the player never engaged with that content in the intended way; a null result on learning gain in a game nobody wanted to keep playing does not tell you the pedagogy failed, it may only tell you the game failed. Mayer (2014) makes a related methodological distinction between research designs, including value-added designs, which isolate the effect of a specific feature by comparing a base version of a game against the same game plus that one feature, and cognitive-consequences designs, which compare extended play against no play at all. The value-added framing is the more relevant precedent for this project, since the object of interest is specifically whether the DDA layer improves outcomes relative to the same game and the same puzzle content running at fixed difficulty, not whether the game as a whole teaches CS content better than no game.

Taken together, this literature indicates that a credible evaluation plan for this project should measure engagement and usability alongside, not instead of, learning outcome, should use the game's own DDA-on and DDA-off states as the comparison condition rather than attempting a full randomised trial that is out of scope for a single-semester student project, and should be explicit about the resulting limits on how strongly any result can be generalised. The full evaluation methodology is developed in Chapter 5 and is out of scope for the current interim submission, which covers Introduction, Literature Review, and Design and Ideation only.

**References cited in section 2.6:**
- All, A., Castellar, E. P. N., & Van Looy, J. (2016). Assessing the effectiveness of digital game-based learning: Best practices. *Computers & Education, 92-93*, 90-103.
- Boyle, E. A., Hainey, T., Connolly, T. M., Gray, G., Earp, J., Ott, M., Lim, T., Ninaus, M., Ribeiro, C., & Pereira, J. (2016). An update to the systematic literature review of empirical evidence of the impacts and outcomes of computer games and serious games. *Computers & Education, 94*, 178-192.
- Connolly, T. M., Boyle, E. A., MacArthur, E., Hainey, T., & Boyle, J. M. (2012). A systematic literature review of empirical evidence on computer games and serious games. *Computers & Education, 59*(2), 661-686.
- Mayer, R. E. (2014). *Computer Games for Learning: An Evidence-Based Approach*. MIT Press.

[TODO: Chapter 5 Evaluation plan, how to measure if the DDA actually improves learning outcomes vs. static difficulty. Out of scope for the interim report; revisit once Chapter 4 Implementation is underway.]

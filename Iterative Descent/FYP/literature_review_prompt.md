# Literature Review Prompt — FYP (Paste into Normal Claude Chat)

---

You are helping me write Chapter 2 (Literature Review) of my Final Year Project report. Below is everything you need to know about the project and what has been built.

---

## Project Summary

**Title:** ARBITEX: Adaptive Difficulty Through an Intelligent Game Director
**Type:** AI-enabled serious game (Unity 3D, C#)
**Genre:** Horror rogue-like set in a school building controlled by a rogue AI warden named ARBITEX

The game teaches undergraduate-level Computer Science concepts (data structures, algorithms, OS concepts, networking) through interactive puzzles embedded in gameplay. A player must solve CS puzzles to unlock doors and progress while evading enemies.

---

## What Has Been Built (relevant to Literature Review)

**Two-system AI architecture (deliberately decoupled):**

### System 1 -- Puzzle AI (Educational Layer)
- **Bayesian Knowledge Tracing (BKT):** Corbett-Anderson model implemented in C#. Maintains P(knows) per CS concept. 11 active concepts (linked_lists, cpu_scheduling, stacks_and_queues, bfs_dfs, networking_ports, network_security, etc.). Updates after every puzzle attempt via 4-parameter model (P_L0, P_T, P_S, P_G).
- **Supervised DDA:** Heuristic signal pipeline (quiz scores, solve times, retry counts) feeding a 5-tier difficulty system (Very Easy to Very Hard). Exponential moving average smoothing. BKT global P(knows) blended at 0.5 weight with heuristic score.
- **Question selection:** QuestionSelector targets the concept with lowest P(knows) for MCQ sessions.

### System 2 -- Combat AI (Engagement Layer)
- **Heuristic DDA for EnemyChaser:** NavMesh state machine enemy. DDA tunes chase speed and activation delay based on current tier.
- **EnemyDirector:** Reads CurrentTier, applies to enemy parameters.
- **Future stretch goal:** PPO-trained Stalker enemy (Unity ML-Agents + Sentis .onnx inference). Inspired by Alien Isolation's alien AI (Director + alien brain dual pattern).

### Puzzle Types Implemented (all with BKT wiring and DDA tier scaling)
- MCQ Quiz terminal (standard Corbett-Anderson BKT update)
- Linked List pointer-rewiring puzzle (drag-and-connect nodes)
- CPU Scheduling (Round Robin Gantt drag-fill)
- Stack push/pop terminal
- BFS/DFS drain pipe traversal puzzle (fog of war, protocol switch mechanic)
- Port matching puzzle
- VLSM subnet allocation puzzle
- Packet filter / firewall rule puzzle (3-phase, lords objective)
- Exam paper page-flip MCQ
- OS Process lifecycle painting arrangement

### ARBITEX Commentary System
AI warden provides reactive narration when DDA tier changes. Fires on tier-change events from both DDA controllers.

### FPS combat system
Player has pistol + shotgun. EnemyChaser cannon fodder. Boss fight (DROID-7, PPO) is a stretch goal.

---

## Report Structure (confirmed)

- Chapter 1. Introduction
- **Chapter 2. Literature Review** (this is what you are helping write)
  - 2.1 Serious Games and Educational Game Design
  - 2.2 Flow Theory, Zone of Proximal Development, and Engagement
  - 2.3 Dynamic Difficulty Adjustment
  - 2.4 Intelligent Tutoring Systems and Bayesian Knowledge Tracing
  - 2.5 Reinforcement Learning and AI Agents in Games
  - 2.6 Evaluation of Serious Games
- Chapter 3. Design and Ideation
- Chapter 4. Game Implementation
- Chapter 5. Testing and Evaluation
- Chapter 6. Conclusion

---

## Writing Instructions

**Tone and style:**
- Academic but readable. This is a final year project report, not a PhD thesis.
- Write in prose paragraphs, not bullet points.
- Be direct. Avoid filler phrases like "it is worth noting that" or "it goes without saying."
- Do not use em dashes or Unicode special characters -- use plain hyphens or commas instead.
- Do not use emojis.

**Citation handling (critical):**
- Do NOT invent citations. If you reference a paper, author, or finding, you must be confident it exists.
- If you are uncertain whether a citation exists or have approximate details, write [INSERT CITATION HERE] with a note describing what the citation should be (e.g. [INSERT CITATION HERE: original BKT paper by Corbett and Anderson, 1994]).
- Real citations I already have confirmed: Corbett & Anderson (1994) for BKT. Left 4 Dead Director (Valve). RE4 AI Director (Capcom). McGaugh (2004) for arousal and memory consolidation.
- For everything else, flag it rather than fabricate.

**Scope of each section:**
- 2.1 covers what serious games are, key definitions, design principles, evidence for learning efficacy.
- 2.2 covers Csikszentmihalyi's Flow theory, Vygotsky's ZPD, how these apply to game-based learning.
- 2.3 covers DDA in commercial games (L4D, RE4), academic DDA approaches, the dual optimisation problem of engagement + learning in educational games.
- 2.4 covers ITS history, BKT as the dominant student model, the 4-parameter model, limitations, and how BKT fits into game contexts.
- 2.5 covers RL fundamentals (PPO specifically), how RL has been applied to game AI (not just enemy AI -- also adaptive game systems), the Alien Isolation dual-AI architecture as a design reference, and why RL-as-DDA is difficult (training instability, reward design, the finding in this project that PPO was explored and rejected as the primary DDA method in favour of supervised/heuristic approaches).
- 2.6 covers how serious games are evaluated -- learning outcomes, usability, engagement metrics, and appropriate study designs for a student playtesting context.

---

## What to do

Write Chapter 2 in full, all 6 sections. Aim for roughly 200-400 words per section. Use [INSERT CITATION HERE] wherever a specific claim needs a real citation that you are not certain about.

Start with section 2.1.

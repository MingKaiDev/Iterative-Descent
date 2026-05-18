# Project Overview — AI-Enabled Serious Game (FYP)

## Identity
| Property | Value |
|---|---|
| Title | AI-Enabled Serious Game |
| Type | Horror Serious Game |
| Engine | Unity 3D (C#) |
| AI Approach | Multi-system: any paradigm (supervised, unsupervised, probabilistic, RL, LLM) chosen by fit |
| Runtime Inference | Unity Sentis (.onnx) for trained model deployment |
| 3D Assets | Modular Blender models exported as FBX |
| UI Framework | TextMeshPro, Unity New Input System, Quick Outline asset |

## Deliverables
1. **FYP Report / Thesis** — high quality, substantial research
2. **Completed video game** — functional AI systems required; graphics/gameplay polish is optional

## AI Architecture (Two Systems)
| System | Domain | Approach | Status |
|---|---|---|---|
| Puzzle AI | Educational layer | BKT + supervised DDA | Sprint 2–3 |
| Combat AI | Engagement layer | Heuristic DDA (chaser now); RL Stalker (future) | Chaser DDA: Sprint 2–3 |

## Priorities (in order)
1. **Puzzle AI** — BKT + supervised DDA for puzzle content and difficulty adaptation
2. **Combat DDA** — heuristic difficulty tuning for basic chaser enemy
3. Core gameplay loop (player health, game loop, room traversal)
4. Level design / assets
5. Report write-up
6. RL Stalker enemy — future phase, not before DDA is complete

## Tech Stack
- **Language**: C#
- **Engine**: Unity 3D (Unity 6 LTS assumed)
- **AI/ML**: BKT (probabilistic), supervised learning (Python/PyTorch → .onnx → Sentis), RL for Stalker (future)
- **3D Authoring**: Blender (FBX export, -Z Forward / Y Up)
- **UI**: TextMeshPro, New Input System

## Sprint Status
| Sprint | Status | Summary |
|---|---|---|
| Sprint 1 | COMPLETE | Interactable system, all puzzle types, DDA metrics tracker (display only), Blender Stairs1 asset |
| Sprint 1 close-out | COMPLETE | Linked List puzzle fully redesigned — pointer-rewiring drag-and-drop (tests real CS knowledge), DDA tier scaling (3–6 nodes), 6 new scripts (LLArrow, LLNodeBlock, LLNextHandle, LLHeadPointer, LLNullTerminal, LinkedListPuzzleUI rewrite) |
| Sprint 2 | IN PROGRESS | Enemy system complete (chaser + melee attack + event bridge + NavMesh); Puzzle AI + Combat DDA pending |

## Sprint 2 — Completed
- Full enemy AI system: `IEnemy`, `EnemyBase`, `EnemyChaser`, `EnemyAttack`, `FistHitboxRelay`, `EnemyEventBridge`
- NavMesh baked via `NavMeshSurface` on scene-root empty GO (Physics Colliders mode)
- Enemy triggers on `OnLinkedListSolved` — door opens then enemy activates with delay
- Enemy stops on attack, resumes chase when player escapes (ResetPath fix)

## Sprint 2–3 — Remaining (AI)
- **Puzzle AI**: BKT knowledge model per CS concept, supervised DDA policy (Python → .onnx → Sentis)
- **Combat DDA**: `EnemyDirector.cs` reads `DDAController.CurrentTier` → tunes `EnemyChaser` speed / spawn delay
- `DifficultyProfile` ScriptableObjects (per-tier parameter presets)
- Player health system (receive damage from `EnemyAttack`)
- Player combat (kill enemy — both ranged and melee planned)

## Enemy Classification (important)
- `EnemyChaser` = cannon fodder. Basic NavMesh chaser. DDA tunes parameters only. No RL.
- Future **Stalker enemy** (Alien Isolation / Mr. X style) = RL-trained intelligent threat. Out of scope until DDA is complete.

## Linked List Puzzle — Next Improvements (backlog)
- Add a second operation type at Very Hard tier (e.g. insert a node, delete a node)
- Visual polish: horror-themed node colours, animated arrows, sound feedback
- Reset button to restore original forward wiring without closing the panel

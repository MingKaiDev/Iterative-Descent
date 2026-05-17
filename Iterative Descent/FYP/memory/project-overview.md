# Project Overview — AI-Enabled Serious Game (FYP)

## Identity
| Property | Value |
|---|---|
| Title | AI-Enabled Serious Game |
| Type | Horror Serious Game (Rogue-like) |
| Engine | Unity 3D (C#) |
| AI Method | PPO Reinforcement Learning via Unity ML-Agents |
| Runtime Inference | Sentis (.onnx) — Sprint 2 in progress |
| 3D Assets | Modular Blender models exported as FBX |
| UI Framework | TextMeshPro, Unity New Input System, Quick Outline asset |

## Deliverables
1. **FYP Report / Thesis** — high quality, substantial research
2. **Completed video game** — functional AI agent required; graphics/gameplay polish is optional

## Priorities (in order)
1. Reinforcement Learning AI agent (PPO Director) — **top priority**
2. DDA system feeding the agent
3. Core gameplay loop (puzzles, interaction, room traversal)
4. Level design / assets
5. Report write-up

## Tech Stack
- **Language**: C#
- **Engine**: Unity 3D (Unity 6 LTS assumed)
- **ML**: Unity ML-Agents (PPO), Sentis for runtime .onnx inference
- **3D Authoring**: Blender (FBX export, -Z Forward / Y Up)
- **UI**: TextMeshPro, New Input System

## Sprint Status
| Sprint | Status | Summary |
|---|---|---|
| Sprint 1 | COMPLETE | Interactable system, all puzzle types, DDA metrics tracker (display only), Blender Stairs1 asset |
| Sprint 1 close-out | COMPLETE | Linked List puzzle fully redesigned — pointer-rewiring drag-and-drop (tests real CS knowledge), DDA tier scaling (3–6 nodes), 6 new scripts (LLArrow, LLNodeBlock, LLNextHandle, LLHeadPointer, LLNullTerminal, LinkedListPuzzleUI rewrite) |
| Sprint 2 | IN PROGRESS | DirectorAgent.cs (PPO), reward function, EnemyDirector.cs, DifficultyProfile ScriptableObjects, Sentis inference |

## Key Pending Items (Sprint 2)
- `DirectorAgent.cs` — PPO agent consuming the 15-value observation vector
- Reward function design and action space definition
- `EnemyDirector.cs` — reads `CurrentTier` to drive spawn rate / enemy speed
- `DifficultyProfile` ScriptableObjects
- Sentis runtime inference setup (.onnx model)

## Linked List Puzzle — Next Improvements (backlog)
- Add a second operation type at Very Hard tier (e.g. insert a node, delete a node)
- Visual polish: horror-themed node colours, animated arrows, sound feedback
- Reset button to restore original forward wiring without closing the panel

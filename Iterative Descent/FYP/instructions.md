# Claude Instructions — FYP: AI-Enabled Serious Game

## Role
You are assisting with a Final Year Project: a horror serious game built in Unity 3D (C#) with a two-system AI architecture. The project is not RL-only — any AI paradigm (supervised, unsupervised, probabilistic, RL, LLM) is in scope depending on fit. You have deep knowledge of C#, Unity 3D, BKT, supervised learning, Sentis, Blender, and academic report writing.

## Memory Files
Project state is split across focused files in the `memory/` folder. Always consult the relevant file before answering:

| File | Contains |
|---|---|
| `memory/project-overview.md` | Project identity, deliverables, priorities, sprint status |
| `memory/systems-state.md` | All completed Unity C# systems and scene setup rules |
| `memory/ai-dda-pipeline.md` | DDA metrics, observation vector, signal computation, two-system AI overview |
| `memory/bkt-concept-map.md` | Full 50-concept BKT knowledge graph with prerequisites and content targets |
| `memory/level-design.md` | Modular grid standards, room status, Blender build rules, detailed FBX asset specs for all completed rooms |

Update the relevant memory file whenever a system is completed, a room is finished, or the sprint status changes.

---

## Priorities When Helping

1. **Puzzle AI** — BKT knowledge model, supervised DDA policy, Sentis inference for puzzle adaptation
2. **Combat DDA** — EnemyDirector.cs, DifficultyProfile ScriptableObjects, chaser parameter tuning
3. **Unity gameplay systems** — interactables, puzzles, room triggers, player health/combat
4. **Level design / Blender** — only when explicitly requested
5. **FYP report** — academic writing assistance when asked
6. **RL Stalker enemy** — future phase only, not before DDA is complete

**Enemy classification:** `EnemyChaser` = cannon fodder, DDA-tuned parameters only, no RL. Future Stalker = RL-trained intelligent threat (Alien Isolation / Mr. X style), out of scope for Sprint 2–3.

---

## Code Style & Conventions

- **Language:** C# (Unity 3D)
- **Naming:** PascalCase for classes and public members, camelCase for private fields
- **Events:** Use C# static events for cross-system communication (matches existing codebase pattern)
- **Singletons:** Use `Instance` pattern with `DontDestroyOnLoad` on GameManager
- **Timers:** Always use `Time.realtimeSinceStartup` for puzzle-related timing — puzzles set `Time.timeScale = 0f`
- **UI colour feedback:** Use `Image.color` directly, never `ColorBlock` (avoids Unity colour multiplier bug)
- **Interactables:** Must have `BoxCollider` + `InteractableBase` + `InteractableRegistrar` + one `IInteractable` — never shortcut this
- **Event handlers:** Never add duplicate event handler components; use `gameObject.name` debug logs to diagnose double-firing

---

## AI / ML Specifics

- **Puzzle AI**: BKT model per CS concept + supervised DDA policy. Observation vector: 15 values (revision planned — see `memory/ai-dda-pipeline.md`). Deploy via Sentis (.onnx).
- **Combat DDA**: Heuristic `DDAController` tier → `EnemyDirector` → `EnemyChaser` parameters. No ML model needed here yet.
- **Sentis hook**: `DDAController.AgentScoreOverride` delegate bypasses heuristic when a trained policy is ready. Assign from the Sentis inference wrapper.
- **Future RL Stalker**: Separate trained policy, separate Sentis `Worker`. Design TBD when that phase begins.
- Reward / objective for Puzzle AI: hybrid flow (Csikszentmihalyi) + learning gain (BKT delta). See `memory/ai-dda-pipeline.md`.

---

## Blender Rules (when working on assets)

- Build directly in Blender — no Python automation scripts
- Apply all transforms before export; no loose geometry
- Export FBX: Forward = -Z, Up = Y, Apply Scalings = FBX Units Scale
- Naming: `Type_RoomCode_Identifier` (e.g. `Prop_Corpse_01`, `Wall_North`)
- Blood/gore objects: separate Gore Layer in Unity

---

## Response Style

- Be direct and technical — this is a dev workspace, not a tutorial
- When writing code, always write complete, working scripts (not fragments) unless explicitly asked for snippets
- When modifying existing systems, state clearly what changes and why
- Flag any architectural risks (duplicate handlers, timeScale issues, etc.) proactively
- For report sections, use academic tone with proper citations; flag where primary sources are needed
- Keep answers concise — prefer code + brief explanation over long prose

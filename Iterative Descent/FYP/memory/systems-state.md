# Unity Systems State — Sprint 1 Complete

## Interactable System — COMPLETE

### Architecture
Modular, interface-based. Any GameObject becomes interactable by attaching three components:
- `InteractableBase` — outline highlight, prompt display, state management
- `InteractableRegistrar` — auto-registers/deregisters into `PlayerInteractor`'s static list on Enable/Disable
- One `IInteractable` implementation — defines the specific behaviour

### Interaction Flow
Proximity → outline (white = in range, yellow = selected) → arrow keys cycle objects → E calls `Interact()` → `PlayerInteractor.Pause()/Resume()` disables interaction during active UI overlays

### Core Scripts
| Script | Role |
|---|---|
| `IInteractable.cs` | Interface: `InteractLabel` + `Interact(GameObject interactor)` |
| `InteractableBase.cs` | Outline highlight, prompt show/hide, state |
| `InteractableRegistrar.cs` | Self-registers into PlayerInteractor's static list |
| `PlayerInteractor.cs` | Proximity detection, nearest-first default, arrow-key cycling, E to interact, Pause/Resume |
| `SelectionPromptUI.cs` | Shared hint panel: "◄ ► 1/2 — Use Computer" when multiple in range |

---

## Puzzle System — COMPLETE

| Script | Role |
|---|---|
| `QuestionData.cs` | Serializable MCQ data: 4 answers + correct index |
| `PuzzleUI.cs` | MCQ UI; per-question colour feedback via `Image.color` (NOT ColorBlock); auto-advances after 1.4s; fires `OnPuzzleFinished(int correct, int total)`; calls `PlayerMetricsTracker.Instance?.NotifyQuizStarted()` in `Setup()` |
| `PuzzleProp.cs` | `IInteractable` for puzzle props; opens/closes puzzle overlay; pauses `PlayerInteractor`; restores cursor and timescale on close |
| `PuzzleEventHandler.cs` | Listens to `OnPuzzleFinished`; calls `door.Unlock()` if correct ≥ 3 |
| `DoorController.cs` | Easing-curve door animation; locked state; `Unlock()` opens door |

---

## Password / Computer System — COMPLETE

| Script | Role |
|---|---|
| `ComputerScreenProp.cs` | `IInteractable` for computer screens; opens/closes PC Password Panel; mirrors PuzzleProp pattern |
| `PasswordScreenUI.cs` | Password entry UI; player can type manually; if `FolderPuzzleSolved == true`, auto-types correct password char-by-char; correct password fires `OnPC1LoggedIn`; wrong password shows ACCESS DENIED + clears field |
| `PasswordEventHandler.cs` | Always-awake; sets `FolderPuzzleSolved = true` on perfect puzzle score; subscribes to `OnPC1LoggedIn` to trigger next puzzle |

**Note:** `FolderPuzzleSolved` is a **static flag** — auto-type works regardless of order computer/folder puzzle is opened.

---

## Linked List Puzzle — COMPLETE (redesigned Sprint 1 close-out)

**Concept:** Player reverses a linked list by physically rewiring pointer arrows via drag-and-drop. Tests actual CS knowledge (pointer manipulation), not just sorting. DDA scales node count (3–6) based on current tier.

### Scripts

| Script | Role |
|---|---|
| `LinkedListPuzzleUI.cs` | Master controller. Spawns nodes, HEAD, NULL terminal, arrows. Handles all drag callbacks. Checks solution by walking list from HEAD and comparing to reversed values. Fires `OnLinkedListSolved(int wrongAttempts)`. DDA tier → node count (0/1→3, 2→4, 3→5, 4→6). |
| `LLNodeBlock.cs` | Fixed-position node box. Displays value, holds `NextNode` reference, exposes `HandleRect` (right-edge handle) as arrow source. |
| `LLNextHandle.cs` | Drag-event relay (child of node or HEAD box). Delegates `OnBeginDrag/OnDrag/OnEndDrag` to `LinkedListPuzzleUI`. `isHead` flag distinguishes HEAD handle from node handles. |
| `LLHeadPointer.cs` | HEAD pointer box. Fixed position above node row. Has its own `LLNextHandle` child. Tracks `TargetNode`. |
| `LLNullTerminal.cs` | NULL endpoint. Fixed position to the right of all nodes. RaycastTarget ON — detected via `GetComponentInParent` in hit-test. |
| `LLArrow.cs` | Dynamic arrow: stretches a UI Image between two RectTransforms each `LateUpdate`. Supports floating mode (drag ghost). Pivot `(0,0.5)`, anchor `(0.5,0.5)` on parent so `anchoredPosition` maps to `InverseTransformPoint` coords. |
| `LinkedListEventHandler.cs` | Listens to `OnLinkedListSolved`; calls `door.Unlock()`; debug log prints `gameObject.name` + scene name to catch duplicate handler bugs. |

### Old scripts (deprecated, safe to delete)
- `LinkedListNodeCard.cs` — replaced by `LLNodeBlock.cs`
- `NodeSlot.cs` — replaced by drop logic inside `LinkedListPuzzleUI`

### Puzzle Flow
1. `InitPuzzle(onClose)` called by prop → notifies metrics tracker → `GeneratePuzzle()`
2. N unique values (1–20) generated; `_solution = values.Reversed()`
3. Nodes laid out horizontally in `nodeContainer`, wired forward (0→1→2→…→null)
4. HEAD placed above node 0; NULL terminal placed to the right of last node
5. `RebuildArrows()` creates one `LLArrow` per pointer (HEAD + each node)
6. Player drags handles to rewire; each successful drop calls `RebuildArrows()`
7. Submit → walk list from HEAD, detect cycles, compare traversal to `_solution`
8. Correct: fires event + closes after 1.5s. Wrong: shows traversal vs target, board NOT reset

### Inspector Setup (LinkedListPuzzleUI)
| Field | Value |
|---|---|
| `nodePrefab` | Node prefab (LLNodeBlock + LLNextHandle child) |
| `headPointerPrefab` | HEAD prefab (LLHeadPointer + LLNextHandle child) |
| `nullTerminalPrefab` | NULL prefab (LLNullTerminal + Image, RaycastTarget ON) |
| `arrowPrefab` | Arrow prefab (LLArrow + Image, solid white) |
| `nodeContainer` | RectTransform — **NO Layout Group; pivot must be (0.5, 0.5)** |
| `headYOffset` | ~70–80px to place HEAD above node row |
| `nodeSpacing` | 160px default |

### Node Prefab Hierarchy
```
Node (root) — LLNodeBlock
  ├── Image          (background rect, NO sprite / UI Default)
  ├── ValueText      (TextMeshProUGUI, white text)
  └── NextHandle     (LLNextHandle + Image Knob sprite, 20×20, anchor right-middle)
```

### Arrow Coordinate Note
`LLArrow` uses `_panelRect.InverseTransformPoint(rectTransform.position)` for both source and target, and `LinkedListPuzzleUI.ScreenToContainer` uses `RectTransformUtility.ScreenPointToLocalPointInRectangle` — both resolve to `nodeContainer` local space and are consistent for Overlay and Camera canvases.

---

---

## Enemy System — COMPLETE (Sprint 2 partial)

### Architecture
Interface-driven for future enemy types. Two interfaces + abstract base + two behaviour components + one event bridge.

| Script | Location | Role |
|---|---|---|
| `IEnemy.cs` | `Scripts/Enemy/` | Interface: `Activate(Transform)`, `Deactivate()`, `Die()`, `IsDead` |
| `EnemyBase.cs` | `Scripts/Enemy/` | Abstract MonoBehaviour implementing `IEnemy` + `IDamageable`. Owns health, death, NavMeshAgent ref, Animator ref. Sets `applyRootMotion = false` on Awake to prevent walk-on-spot conflict with NavMeshAgent. Guards all `isStopped` calls with `isOnNavMesh`. |
| `EnemyChaser.cs` | `Scripts/Enemy/` | Concrete enemy. State machine: Idle → Chasing → Attacking → Dead. Throttles `SetDestination` every 0.2s. Calls `StopAgent()` (isStopped + ResetPath) on enter attack, `ResumeChase()` on exit. |
| `EnemyAttack.cs` | `Scripts/Enemy/` | Periodic melee. Coroutine drives `_isAttacking` reset — animation events are optional. `hitboxDelay` + `hitboxActiveWindow` control fist collider open window. Trigger: `"Attack"`. |
| `FistHitboxRelay.cs` | `Scripts/Enemy/` | Attach to fist bone child. Relays `OnTriggerEnter` → `EnemyAttack.OnFistHit()`. Required because Unity's trigger callbacks only fire on the Collider's own GameObject. |
| `EnemyEventBridge.cs` | `Scripts/Events/` | Listens to `LinkedListPuzzleUI.OnLinkedListSolved`. Waits `activationDelay` seconds (match to door anim duration + suspense), then calls `enemy.Activate(playerTransform)`. |

### Adding a new enemy type
1. Create `class MyEnemy : EnemyBase`
2. Override `OnActivate`, `OnDeactivate`, `OnDie`, `OnHit`
3. Write your own Update / movement logic
4. `EnemyEventBridge`, `IDamageable`, `TakeDamage` all work unchanged

### Fist Hitbox Hierarchy
```
EnemyRoot  (EnemyChaser + EnemyAttack + NavMeshAgent)
└── CharacterRig  (Animator — applyRootMotion = false)
    └── ... → Hand_R bone
        └── FistHitbox  (BoxCollider IsTrigger=true + FistHitboxRelay)
                            ↑ assign to EnemyAttack.fistHitbox in Inspector
```

### Animator Parameters Required
| Name | Type | Driven by |
|---|---|---|
| `Speed` | Float | EnemyChaser — agent velocity magnitude |
| `Attack` | Trigger | EnemyAttack.TryAttack() |
| `IsDead` | Bool | EnemyChaser.OnDie() |

### Animator Transition Rules
- `Damaged Walking → zombie attack`: Has Exit Time OFF, Condition: Attack trigger
- `zombie attack → Damaged Walking`: Has Exit Time ON at 1.0 (full clip), no conditions
- **Do NOT exit zombie attack before the clip ends** — early exit caused `_isAttacking` to lock up (fixed by coroutine, but still bad practice)

### NavMesh Setup
- Package: `com.unity.ai.navigation` — already installed
- Place `NavMeshSurface` on a **scene-root empty GameObject** (`NavMesh_Manager`), NOT on any rotated floor/wall object
- Use Geometry: **Physics Colliders** (more robust for Blender FBX imports with flipped normals)
- Collect Objects: All Game Objects, Include Layers: Environment
- Root cause of "bakes on walls not floor": Blender FBX floor had Rotation X: -89.98 — NavMesh evaluated geometry in warped local space. Fix: Apply All Transforms in Blender before FBX export, OR keep NavMeshSurface on scene root

### Key Behavioural Notes
- `applyRootMotion = false` is **mandatory** — root motion + NavMeshAgent both drive world position and cancel each other out (walk-on-spot bug)
- Always guard `_agent.isStopped` and `_agent.SetDestination` with `_agent.isOnNavMesh` — throws errors otherwise
- Use `_agent.ResetPath()` when stopping to attack — `isStopped = true` alone leaves cached path intact and causes drift
- `attackRange` inspector field controls stop distance. Default 2f is too small for most humanoid models — use 2.5f or higher
- `stoppingDistance = Mathf.Max(0f, attackRange - 0.1f)` — stops agent at attack range edge, not inside it

### Enemy Trigger Flow
```
LinkedList puzzle solved
  ├── LinkedListEventHandler → door.Unlock()
  └── EnemyEventBridge → wait activationDelay → enemy.Activate(player)
        → State: Chasing → SetDestination every 0.2s
        → dist ≤ attackRange → StopAgent() → State: Attacking
        → EnemyAttack.TryAttack() → coroutine → SetTrigger("Attack")
        → hitboxDelay → fist collider enabled → FistHitboxRelay.OnTriggerEnter
        → EnemyAttack.OnFistHit → IDamageable.TakeDamage(20, hitPoint)
        → dist > attackRange*1.25 → ResumeChase() → State: Chasing
```

### Pending (stubs to wire up later)
- `OnHit`: hit-stagger animation + blood VFX at hitPoint
- `OnDie`: ragdoll / proper death anim (currently Destroy after 3s)
- `EnemyEventBridge`: real SFX + camera shake (currently Debug.Log stub)
- DDA integration: `EnemyChaser.chaseSpeed` driven by `DDAController.CurrentTier`
- Player damage reception: wire `EnemyAttack.meleeDamage` into player health system

---

## Stub

| Script | Role |
|---|---|
| `PickupProp.cs` | `IInteractable` stub for collectibles — **not yet implemented** |

---

## Unity Scene Setup Rules

### Canvas (all children inactive by default)
- Prompt Panel
- Cycle Hint Panel (`SelectionPromptUI`)
- Puzzle Panel (`PuzzleUI`)
- PC Password Panel (`PasswordScreenUI`)

### Component Requirements Per Interactable Prop
`BoxCollider` + `InteractableBase` + `InteractableRegistrar` + one `IInteractable` script

### Persistent GameObjects
| Component | Attached To |
|---|---|
| `PlayerInteractor` | Player GameObject |
| `PasswordEventHandler` | Persistent GameObject in scene |
| `PlayerMetricsTracker` | GameManager (DontDestroyOnLoad) |
| `DDAController` | GameManager (same as PlayerMetricsTracker) |
| `DDADisplayHUD` | GameManager or any scene GameObject; toggle with **H key** |

---

## Critical Behavioural Notes
- Puzzle UI uses `Image.color` directly — avoids Unity's ColorBlock colour multiplier issue
- `Time.timeScale = 0f` while any puzzle is open — **all puzzle timers must use `Time.realtimeSinceStartup`**, never `Time.deltaTime`-based clocks
- All UI overlays hide the selection prompt and pause `PlayerInteractor` while open
- Never add duplicate event handler components — use `LinkedListEventHandler`'s `gameObject.name` debug log to identify duplicates if double-firing occurs

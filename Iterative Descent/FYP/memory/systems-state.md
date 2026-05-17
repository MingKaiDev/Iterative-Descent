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

## Linked List Puzzle — COMPLETE

| Script | Role |
|---|---|
| `LinkedListPuzzleUI.cs` | Drag-and-drop node sorting; generates 4 random unique values; player sorts ascending; tracks wrong submissions; fires `OnLinkedListSolved(int wrongAttempts)` on correct solve; auto-closes after 1.5s; calls `PlayerMetricsTracker.Instance?.NotifyLinkedListStarted()` in `InitPuzzle()` |
| `LinkedListEventHandler.cs` | Listens to `OnLinkedListSolved`; calls `door.Unlock()`; debug log prints `gameObject.name` + scene name to catch duplicate handler bugs |

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

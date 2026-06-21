# Pause Menu + ESC Overlay Close -- Unity Setup

Stories 29 and 27. Done 2026-06-21.

---

## What was implemented

**Story 29 -- ICloseable / centralised ESC**

- `Assets/Scripts/Interfaces/ICloseable.cs` -- single `void Close()` method.
- `PlayerInteractor` extended:
  - `RegisterCloseable(ICloseable)` / `DeregisterCloseable()` -- called by props on open/close.
  - `EscConsumedThisFrame` static bool -- set when ESC was used to close a puzzle; prevents same frame re-opening the pause menu.
  - `Update()` now handles ESC while paused: if `_activeCloseable != null`, calls `Close()` and sets the flag.
  - `LateUpdate()` resets `EscConsumedThisFrame`.
- All 10 puzzle props now implement `ICloseable`:
  - PuzzleProp, ComputerScreenProp, SchedulingPuzzleProp, StackPuzzleProp, DrainPuzzleProp, MatchingPuzzleProp, SubnetPuzzleProp, PacketFilterProp, ExamPaperProp, PadlockProp.
  - Each calls `PlayerInteractor.RegisterCloseable(this)` just before setting `Time.timeScale = 0f`.
  - Each calls `PlayerInteractor.DeregisterCloseable()` at the top of its close method.
  - For PuzzleProp and ExamPaperProp (which have `ClosePuzzle(bool completed)`), `Close()` calls `ClosePuzzle(false)` -- ESC exits without quiz credit.
- ESC removed from `NumberLockUI.Update()` -- was redundant; centralised handler now covers it.

**Story 27 -- PauseMenuUI**

- `Assets/Scripts/UI/PauseMenuUI.cs` -- singleton, DontDestroyOnLoad not needed (stays in game scene).
- Opens: `PlayerInteractor.Pause()`, `Time.timeScale = 0f`, cursor unlocked.
- Closes: `PlayerInteractor.Resume()`, `Time.timeScale = 1f`, cursor locked.
- Buttons: Resume, Quit to Main Menu, How to Play (greyed out until Story 28).

---

## Scene Wiring

### Step 1 -- PauseMenuUI script

Attach `PauseMenuUI` to the GameManager (or any persistent scene object).

### Step 2 -- Pause Panel

Create a new Canvas child panel: `UI > Panel > rename to "Pause Panel"`.

Set it **inactive** by default.

Recommended layout (dark semi-transparent background, centred VLG):

```
Pause Panel
  Background Image (alpha ~0.8, black)
  Content VLG (VerticalLayoutGroup, centered)
    Title Text (TMP) -- "PAUSED"
    Resume Button
    How to Play Button (greyed out, non-interactable)
    Quit Button
```

### Step 3 -- Inspector wiring on PauseMenuUI

| Field | Assign |
|-------|--------|
| Pause Panel | The "Pause Panel" GameObject |
| Resume Button | Resume Button child |
| How To Play Button | How to Play Button child |
| Quit Button | Quit Button child |
| Main Menu Scene Name | "MainMenu" (must match your Build Settings scene name) |

Button onClick events are wired in Start() via code -- no Inspector onClick setup needed.

### Step 4 -- Test checklist

- [ ] ESC during gameplay opens pause panel; game is frozen (timeScale=0).
- [ ] ESC again (or Resume button) closes panel; game resumes.
- [ ] Open any puzzle overlay -> press ESC -> overlay closes; game resumes. Pause menu does NOT open on the same ESC press.
- [ ] Quit to Main Menu loads the main menu scene correctly.
- [ ] Weapon fire, movement, and enemy AI are all frozen while paused (timeScale=0 covers this).

---

## Notes for Story 28

When TutorialUI is implemented, wire `howToPlayButton`:

```csharp
howToPlayButton.interactable = true;
howToPlayButton.onClick.AddListener(() =>
    TutorialUI.Instance.Show(onClose: () => OpenPause()));
```

Call `ClosePause()` before showing the tutorial so the pause panel hides cleanly while the tutorial is up.

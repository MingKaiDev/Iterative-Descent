# Sensitivity Settings Menu -- Unity Setup

Written 2026-08-14 in response to playtest feedback: default mouse sensitivity felt too high.

---

## What was implemented

- `Assets/Scripts/UI/SettingsManager.cs` -- static PlayerPrefs wrapper (no scene object needed). Holds `MouseSensitivity` (get/set, clamped 0.2-5), `DefaultMouseSensitivity` (1.5, down from the old hardcoded 3), `MinMouseSensitivity`, `MaxMouseSensitivity`.
- `Assets/Scripts/UI/SettingsPanelUI.cs` -- scene-local singleton + `ICloseable`, same pattern as `HelpPanelUI`. Has a `Slider` and optional `TMP_Text` value label. Saves to `SettingsManager` on every slider change and, in the gameplay scene, live-applies the new value to the active `PlayerMovement` immediately (no need to close/reopen the panel to feel the change).
- `PlayerMovement.cs` -- `Start()` now overwrites `mouseSensitivity` with `SettingsManager.MouseSensitivity` at spawn. The Inspector field is now just an editor-time fallback; the saved/default value always wins at runtime.
- `PauseMenuUI.cs` -- new `settingsButton` field, wired in `Start()` to `SettingsPanelUI.Instance?.Show()`.
- `MainMenu.cs` -- `OpenOptions()` stub replaced with `SettingsPanelUI.Instance?.Show()`.

**On the 1.5 default:** this is a starting guess, not a playtested number. The old value (3, with an extra internal `* 10f` multiplier in `HandleLook()`) is what playtesters called too high; halving it is a reasonable first cut but you should confirm it feels right once the slider is wired up, and adjust `SettingsManager.DefaultMouseSensitivity` if not.

---

## Scene Wiring (repeat in both MainMenu.unity and the gameplay scene)

### Step 1 -- Settings Panel

Create a new Canvas child panel: `UI > Panel > rename to "Settings Panel"`. Set it **inactive** by default.

Recommended layout:

```
Settings Panel
  Background Image (alpha ~0.8, black)
  Content VLG (VerticalLayoutGroup, centered)
    Title Text (TMP) -- "SETTINGS"
    Sensitivity Row
      Label Text (TMP) -- "Mouse Sensitivity"
      Sensitivity Slider (Slider)
      Value Text (TMP) -- shows current number, e.g. "1.5"
    Back Button
```

### Step 2 -- Slider setup

On the Slider component itself: `Whole Numbers` OFF. Min/Max Value are overwritten in code (`Start()` sets them from `SettingsManager.MinMouseSensitivity` / `MaxMouseSensitivity`), so the Inspector values don't strictly matter, but set them to 0.2 / 5 anyway so the slider looks correct in the editor before Play.

### Step 3 -- Attach script

Attach `SettingsPanelUI` to the "Settings Panel" GameObject.

### Step 4 -- Inspector wiring on SettingsPanelUI

| Field | Assign |
|-------|--------|
| Sensitivity Slider | The Slider child |
| Value Label | The Value Text child (optional -- leave empty if you skip it) |
| Close Button | Back Button child |

### Step 5 -- Hook up the entry point

**Gameplay scene:** on the `PauseMenuUI` component (on GameManager or wherever it lives), assign the new `Settings Button` field to a "Settings" button placed in the Pause Panel (next to Resume / How to Play / Quit).

**Main menu scene:** find whatever button currently calls `MainMenu.OpenOptions()` (or add one if it doesn't exist yet) and confirm its `onClick` still points at `OpenOptions()` -- the method body changed but the signature didn't, so existing wiring should keep working.

### Step 6 -- Test checklist

- [ ] Pause menu -> Settings button opens the panel; slider reflects the currently saved value.
- [ ] Dragging the slider in-game changes look sensitivity immediately, before closing the panel.
- [ ] Close/Back button and ESC both close the panel without closing the pause menu underneath.
- [ ] Value persists: change it, quit to main menu, quit the game entirely, relaunch -- slider should show the same value.
- [ ] Main menu Settings/Options button opens the same panel; slider still reflects the saved value even though no `PlayerMovement` exists in that scene yet (live-apply is skipped there, save still happens).
- [ ] Fresh install (no PlayerPrefs yet) starts at 1.5, not the old 3.

---

## Open question for playtesting

Is 1.5 actually the right default, or still too high/too low? [insert playtest data here once available] -- this needs real player feedback before the report can claim the "sensitivity too high" complaint is resolved, not just that a slider now exists.

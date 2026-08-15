# Volume Settings (Music / SFX) -- Unity Setup

Written 2026-08-15 in response to a request to add volume control to the Settings panel. Mirrors `FYP/setup-guides/SensitivitySettings_UnitySetup.md` in structure.

---

## What was implemented (code side, already done)

- `Assets/Scripts/UI/SettingsManager.cs` -- added `MusicVolume` / `SFXVolume` (linear 0-1, PlayerPrefs-backed, same pattern as `MouseSensitivity`), `LinearToDecibel()` (converts a linear slider value to the dB scale `AudioMixer.SetFloat` expects, floored at -80 dB instead of -Infinity for 0), and `ApplyVolumeToMixer(AudioMixer)` (pushes both saved values onto a mixer's exposed parameters).
- `Assets/Scripts/UI/SettingsPanelUI.cs` -- added `musicSlider` / `sfxSlider` (+ optional `musicValueLabel` / `sfxValueLabel`) and an `audioMixer` field. Dragging either slider saves to `SettingsManager` and calls `AudioMixer.SetFloat` immediately (no `FindFirstObjectByType` needed -- the mixer is a shared asset, not a scene object, so this works identically in both scenes).
- `Assets/Scripts/Audio/GameAudioManager.cs` and `Assets/Scripts/Main Menu/MainMenuAudioManager.cs` -- both now call `SettingsManager.ApplyVolumeToMixer(audioMixer)` in `Awake()`, so the saved volume applies on scene load even if the player never opens Settings. Both have a new `audioMixer` Inspector field, currently unassigned.

**What is NOT done, and can't be done from code:** the AudioMixer asset itself does not exist yet. Nothing routes audio through it. Until the steps below are done in the Unity Editor, the sliders will save values correctly (check PlayerPrefs / the value labels) but you will hear no volume change.

---

## Why AudioMixer instead of a simple multiplier

This project's other audio scripts (`GameAudioManager`, `BossAudio`, `EnemyChaserAudio`, `PlayerAudioController`, `ShotgunAudio`, `WeaponSounds`, `MainMenuAudioManager`, etc.) each hardcode their own per-clip volume floats (e.g. `footstepVolume`, `fireVolume`) and call `AudioSource.volume =` or `PlayOneShot(clip, volume)` directly -- there was no central mixer before this. AudioMixer was chosen deliberately over adding a global multiplier in each script because it is the standard Unity approach and leaves room for later additions (ducking, snapshots, limiting) without touching every audio script again. The tradeoff is the manual routing work below, which a script cannot do for you -- `AudioSource.outputAudioMixerGroup` needs a serialized reference to a sub-asset that only exists after you build the mixer.

---

## Editor Setup

### Step 1 -- Create the AudioMixer asset

`Assets > Create > Audio Mixer`. Name it e.g. `MainMixer`. Put it in `Assets/Audio/` (create the folder if it doesn't exist).

### Step 2 -- Create Music and SFX groups

Open the Audio Mixer window (double-click the asset, or `Window > Audio > Audio Mixer`). Under the `Master` group, right-click > `Add child group`, create two: `Music` and `SFX`.

### Step 3 -- Expose the volume parameters

For each group (`Music`, `SFX`):
1. Select the group, find its `Volume` slider in the Inspector/mixer strip.
2. Right-click the slider > `Expose 'Volume (of Music)' to script`.
3. Go to the mixer's `Exposed Parameters` list (top-right of the Audio Mixer window).
4. Rename the exposed entries to match the code exactly: `MusicVolume` and `SFXVolume`. These names must match `SettingsManager.MixerParamMusicVolume` / `MixerParamSFXVolume` character-for-character or `SetFloat` silently fails.

### Step 4 -- Route every existing AudioSource

Every AudioSource in the project currently has its Output set to "Master" (Unity default) or left unset. Each needs its `Output` field (Inspector, AudioSource component) set to the `Music` or `SFX` child group. This is the actual "meter" wiring -- without it, the sliders control a mixer nothing is plugged into.

**Music group** (looping background audio):

| Script | Field | Notes |
|---|---|---|
| `GameAudioManager.cs` | `_ambientSource` (the single `AudioSource` on the GameAudioManager GameObject) | Also carries boss music when `PlayBossMusic()` swaps its clip -- one Output routing covers both. |
| `MainMenuAudioManager.cs` | `bgmSource` | Main menu scene only. |

**SFX group** (everything else -- one-shots, footsteps, weapon fire, UI clicks):

| Script | Field |
|---|---|
| `MainMenuAudioManager.cs` | `sfxSource` |
| `BossAudio.cs` | shared boss `AudioSource` (`GetComponent<AudioSource>()` on the boss root) |
| `BossAttackBase.cs` | `_sfxSource` (same shared boss AudioSource as above -- confirm it's the same component, not a duplicate) |
| `CoreOverloadAttack.cs` | `_laserAudioSource` (separate AudioSource on the `LaserOrigin` child object) |
| `EnemyChaserAudio.cs` | `_audioSource` (per-enemy prefab) |
| `PlayerAudioController.cs` | `_audioSource` (Player GameObject) |
| `ShotgunAudio.cs` | `_source` (Player GameObject) |
| `WeaponSounds.cs` | `audioSource` (Player GameObject) |
| `DoorController.cs` | `_audioSource` (per door prefab/instance) |
| `PaintingInteractable.cs` | `audioSource` (painting prop) |
| `ShutterController.cs` | `_audioSource` (per shutter instance) |

Because several of these are on prefabs (enemy, weapons, doors, shutters), routing the prefab's AudioSource once should propagate to all instances -- but check at least one instance of each in every scene it appears in, in case a scene has a non-prefab override.

### Step 5 -- Assign the mixer asset in the Inspector

In both `MainMenu.unity` and the gameplay scene:
- On the `SettingsPanelUI` component: assign `audioMixer` = the `MainMixer` asset.
- On `GameAudioManager` (gameplay scene): assign `audioMixer` = the same asset.
- On `MainMenuAudioManager` (main menu scene): assign `audioMixer` = the same asset.

### Step 6 -- Add the sliders to the Settings panel

Inside the existing "Settings Panel" (see `SensitivitySettings_UnitySetup.md` for its layout), add two more rows following the same shape as the sensitivity row:

```
Settings Panel
  Content VLG
    ...
    Sensitivity Row (existing)
    Music Volume Row
      Label Text (TMP) -- "Music Volume"
      Music Slider (Slider, Min 0 / Max 1, Whole Numbers off)
      Music Value Text (TMP) -- shows e.g. "50%"
    SFX Volume Row
      Label Text (TMP) -- "SFX Volume"
      SFX Slider (Slider, Min 0 / Max 1, Whole Numbers off)
      SFX Value Text (TMP) -- shows e.g. "100%"
```

On `SettingsPanelUI`, assign `musicSlider` / `musicValueLabel` / `sfxSlider` / `sfxValueLabel` to the new children.

### Step 7 -- Test checklist

- [ ] Settings panel shows Music/SFX sliders at their saved (or default 50% / 100%) position on open.
- [ ] Dragging Music slider changes ambient/boss music volume immediately, in both scenes.
- [ ] Dragging SFX slider changes footstep/weapon/enemy/UI sound volume immediately, in both scenes.
- [ ] Dragging Music slider does NOT affect SFX volume, and vice versa.
- [ ] Values persist: change both, quit to main menu, quit the game entirely, relaunch -- sliders and actual audio volume should match the saved values on first frame, without opening Settings.
- [ ] Set SFX to 0% -- game should have no gunfire/footstep/hit audio, but music still plays normally.
- [ ] Set Music to 0% -- ambient/boss music should be silent, but gunfire/footsteps/UI clicks still play normally.
- [ ] Boss fight: confirm boss music (which hard-swaps `GameAudioManager`'s ambient source) still respects the Music slider after the swap.

---

## Open item

No AudioMixer asset exists in the repo as of this writing -- Steps 1-6 above have not been performed yet. [confirm once done -- this doc should be updated to note the asset's path and that routing is complete]

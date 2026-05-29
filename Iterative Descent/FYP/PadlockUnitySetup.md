# Padlock Unity Setup

Story 11 -- Trophy case padlock (number lock UI).

---

## Sprite

`padlock_sprite.png` is in the FYP root. Import into Unity:

- Texture Type: Sprite (2D and UI)
- Filter Mode: Bilinear
- Compression: None (or High Quality)
- Alpha Is Transparency: ON

Assign as the source image on the padlock prop in the scene (or on a world-space quad
if you want a 3D billboard effect).

---

## Canvas hierarchy (Number Lock UI)

The lock UI must NOT be a full-screen panel. It is a compact floating overlay.

```
Canvas (Screen Space - Overlay)
  └── NumberLockPanel          [RectTransform: 420x280, anchored centre]
        ├── Background         [Image, dark semi-transparent, rounded corners via Sprite]
        ├── PadlockImage       [Image, padlock_sprite.png, top-centre, ~120x120]
        ├── TitleText          [TMP "COMBINATION LOCK", small caps, dim colour]
        ├── DigitsRow          [HorizontalLayoutGroup, spacing 8]
        │     ├── DigitSlot_0  [Image background + TMP digit child]
        │     ├── DigitSlot_1
        │     ├── DigitSlot_2
        │     └── DigitSlot_3
        ├── FeedbackText       [TMP, hidden by default, "ACCESS DENIED" etc.]
        └── InstructionText    [TMP, "A/D - Select Digit    W/S - Change    Enter - Submit    Esc - Close"]
```

Each DigitSlot should be:
- Width: 64, Height: 80
- Image (background highlight)
- Child TMP text, font size 36, bold, centred

---

## NumberLockUI Inspector wiring

| Field             | Value                                     |
|-------------------|-------------------------------------------|
| Digit Texts (4)   | TMP children inside DigitSlot_0..3        |
| Digit Highlights  | Image components on DigitSlot_0..3        |
| Feedback Text     | FeedbackText TMP                          |
| Normal Colour     | (64,56,46,255)                            |
| Selected Colour   | (204,166,51,255) -- amber                 |
| Denied Colour     | (204,26,26,255)                           |
| Unlocked Colour   | (51,179,64,255)                           |

---

## PadlockProp Inspector wiring

| Field            | Value                                                       |
|------------------|-------------------------------------------------------------|
| Number Lock UI   | Drag in the NumberLockPanel's NumberLockUI component        |
| Correct Code     | Leave EMPTY for always-deny (fill in a future sprint)       |
| Unlocked Object  | Optional -- any GameObject to enable on correct code entry  |

PadlockProp also needs:
- BoxCollider (trigger, sized to prop mesh)
- InteractableBase
- InteractableRegistrar

---

## InteractableBase.SetInteractable

PadlockProp calls `SetInteractable(false)` on unlock. Make sure InteractableBase
exposes this method. If it does not exist yet, add:

```csharp
public void SetInteractable(bool value)
{
    enabled = value;
    // also disable the outline if present
}
```

---

## Testing checklist

- [ ] Padlock sprite visible in scene on trophy case prop
- [ ] Press E near padlock -- NumberLockPanel appears, cursor unlocked, game paused
- [ ] A/D shifts amber highlight between 4 digits
- [ ] W/S spins selected digit 0-9 (wraps)
- [ ] Press Enter with wrong code -- "ACCESS DENIED" in red
- [ ] Press Escape -- panel closes, cursor locks, game resumes
- [ ] With correctCode set to "1234" in Inspector: entering 1234 and pressing Enter shows "UNLOCKED"
- [ ] After correct code, prop becomes non-interactable

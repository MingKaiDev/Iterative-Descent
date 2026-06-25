# Story 25 -- Readable Notes: Unity Setup Guide

## Files Created

| File | Location |
|------|----------|
| NoteData.cs | Assets/Scripts/Pickups/ |
| NoteProp.cs | Assets/Scripts/Props/ |
| NoteUI.cs | Assets/Scripts/UI/ |

---

## Step 1 -- Create the NotePanel in the Canvas

In the Canvas hierarchy (same Canvas as all other overlays):

```
Canvas
  NotePanel              <-- new GameObject, starts INACTIVE
    Background           <-- Image, semi-transparent dark panel
    NoteFrame            <-- Image (optional notebook sprite)
    TitleText            <-- TextMeshProUGUI
    AuthorRow            <-- GameObject (hide when author is empty)
      AuthorText         <-- TextMeshProUGUI
    ScrollRect           <-- for long notes
      Viewport           <-- Mask component
        BodyText         <-- TextMeshProUGUI (Content of ScrollRect)
    CloseHint            <-- TextMeshProUGUI ("[ E ] Close")
```

Add NoteUI component to NotePanel. Wire Inspector fields:
- _titleText    -> TitleText
- _authorText   -> AuthorText
- _authorRow    -> AuthorRow
- _bodyText     -> BodyText
- _closeHintText -> CloseHint (optional)

NotePanel must start INACTIVE -- NoteUI.Awake() calls SetActive(false).

---

## Step 2 -- Create NoteData assets

Right-click in Project window -> Create -> ARBITEX -> Note Data.

One asset per note. Suggested naming: Note_FacultyMemo, Note_SecurityLog, etc.

Fields:
- title: header text displayed on the overlay
- author: optional attribution line (leave blank to hide the AuthorRow)
- body: full note text, supports newlines
- noteType: Lore / Tutorial / Code (for your reference -- no gameplay effect yet)

---

## Step 3 -- Place a note prop in the scene

For each note in the level:

1. Create or pick a mesh GameObject (piece of paper, clipboard, sticky note on wall, etc.).
2. Add components:
   - InteractableBase
   - InteractableRegistrar
   - NoteProp
3. In NoteProp Inspector:
   - noteData: assign the NoteData asset for this note
   - noteOverlay: assign the NotePanel from the Canvas
   - canReadAgain: true (default) -- false if you want one-read-only notes
   - interactLabel: customise per note ("Read Memo", "Read Letter", "Examine Screen")

---

## Step 4 -- Verify ESC handling

NoteUI.Show() calls NoteProp which calls PlayerInteractor.RegisterCloseable(this).
ESC is routed by PlayerInteractor -> NoteProp.Close() -> NoteProp.CloseNote() -> NoteUI.Hide().
E key is handled directly in NoteUI.Update() -> same CloseNote() callback.

No extra wiring needed -- the ICloseable system handles it automatically.

---

## Testing Checklist

- [ ] Approach note prop -- outline appears, prompt shows interact label
- [ ] Press E -- NotePanel appears with correct title, author, body
- [ ] Scroll body text if content is long
- [ ] Press E again -- panel closes, player control restores
- [ ] Press ESC while note is open -- panel closes (ESC route)
- [ ] canReadAgain = false -- prop disables itself after first close, no further interaction
- [ ] Pause menu does NOT open while note is open (EscConsumedThisFrame flag)

---

## Notes on NoteType

The NoteType enum (Lore / Tutorial / Code) is on NoteData for your authoring convenience.
No gameplay systems read it yet. Future uses:
- Tutorial: could trigger a BKT hint update
- Code: could cross-reference with a trophy/unlock system

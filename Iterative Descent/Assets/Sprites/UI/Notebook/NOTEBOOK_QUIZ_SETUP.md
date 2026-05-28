# Notebook Quiz Panel Setup

Applies to: "Quiz Panel" and "Quiz Panel 2" in Level 1.unity
Starting point: sprites imported, nothing else changed yet.

---

## Step 1 -- Sprite import settings

You must do this before assigning sprites or the 9-slice will not work.

In the Project panel, navigate to Assets/Sprites/UI/Notebook/

Select notebook_bg.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Bilinear  (bg has grain, bilinear looks softer)
- Compression: None
- Click Apply

Select notebook_spiral.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply

Select notebook_btn.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Open the Sprite Editor (button in the Inspector)
- In the Sprite Editor, set the Border values: L 8 | B 8 | R 8 | T 8
  (this tells Unity where the unstretched corners end -- the wobbly ink border stays sharp)
- Click Apply, close Sprite Editor
- Click Apply on the import settings

---

## Step 2 -- Quiz Panel background

Do this for both "Quiz Panel" and "Quiz Panel 2".

1. In the Hierarchy, expand the Canvas and find "Quiz Panel"
2. Select it
3. In the Inspector, find the Image component (it is on the root panel object itself)
4. Change the Source Image field: assign notebook_bg.png
5. Set Image Type to: Simple
6. Tick "Preserve Aspect": OFF
7. Set Color to: R 255, G 255, B 255, A 255  (fully opaque white -- the sprite provides all colour)

The ruled paper will now fill the panel background.

---

## Step 3 -- Add the spiral binding strip

Do this for both "Quiz Panel" and "Quiz Panel 2".

1. Right-click "Quiz Panel" in the Hierarchy > UI > Image
2. Rename the new child to: NotebookSpiral
3. In the RectTransform, set anchor preset to: left-stretch
   (hold Alt while clicking the anchor preset so it also sets position)
4. Set these values:
   - Pos X: 0
   - Left: 0,  Right: (leave as is / 0)
   - Width: 50
   - Top: 0,  Bottom: 0
4. Image component settings:
   - Source Image: notebook_spiral.png
   - Image Type: Simple
   - Preserve Aspect: OFF  (let it stretch vertically to fill the panel height)
   - Raycast Target: OFF
5. In the Hierarchy, drag NotebookSpiral to be the FIRST child of Quiz Panel
   (it renders beneath the text and buttons)

---

## Step 4 -- Button backgrounds

Do this for all 4 answer buttons in both panels.
The buttons are direct children of the Quiz Panel named things like "Button", "Button (1)" etc.

For each answer button:
1. Select the button GameObject
2. Find its Image component
3. Source Image: notebook_btn.png
4. Image Type: Sliced
5. Fill Center: ON
6. Color: leave as white -- PuzzleUI.cs will tint this at runtime for correct/wrong feedback

Do NOT change the Button component's ColorBlock -- PuzzleUI.cs already sets those to pure
white in Awake() to avoid Unity's color multiplier fighting with the Image color.

---

## Step 5 -- PuzzleUI colour fields

Select "Quiz Panel" and find the PuzzleUI component.
Change these four colour fields (click the colour swatch):

Default Colour  -- the button colour when unanswered
  R: 255  G: 250  B: 235  A: 255   (warm cream paper)

Correct Colour  -- correct answer highlight
  R: 180  G: 230  B: 175  A: 255   (soft green ink wash)

Wrong Colour    -- wrong answer highlight
  R: 230  G: 160  B: 155  A: 255   (soft red ink wash)

Text Colour     -- all button label text
  R: 28   G: 22   B: 42   A: 255   (dark ink navy)

Repeat for "Quiz Panel 2".

---

## Step 6 -- Text colours

The question text, progress text, and feedback text are separate TMP_Text objects.
They are children of the Quiz Panel: look for objects named "Question", "Progress",
"Feedback" or similar.

For each one:
1. Select the TMP_Text object
2. In TextMeshPro component, set Vertex Color to: R:28 G:22 B:42  (same dark ink navy)

For the Close button label: same dark ink navy.

---

## Step 7 -- Panel background color check

The Quiz Panel root Image you set in Step 2 uses the notebook_bg sprite, which already
has its own edge vignette and grain baked in. Make sure there is no extra Image component
underneath it fighting with it. If you see a second Image on the root, either remove it
or set it to Color (0,0,0,0) transparent.

---

## Final hierarchy for each Quiz Panel

Quiz Panel
  NotebookSpiral    <-- Image, notebook_spiral.png, left-stretch, width 50, Raycast OFF
  Progress          <-- TMP_Text, dark ink colour
  Question          <-- TMP_Text, dark ink colour
  Button            <-- answer 0, Image = notebook_btn.png Sliced
  Button (1)        <-- answer 1
  Button (2)        <-- answer 2
  Button (3)        <-- answer 3
  Feedback          <-- TMP_Text, dark ink colour
  Close Button      <-- Image + TMP_Text label, dark ink

---

## Colour reference summary

Paper bg (already baked into notebook_bg.png -- no need to set separately):
  Cream base: #F2EAD2
  Ruled lines: faint blue
  Margin line: red

Button states via PuzzleUI:
  Unanswered:  #FFFAEB  (cream)
  Correct:     #B4E6AF  (green wash)
  Wrong:       #E6A09B  (red wash)

All text:      #1C1629  (dark ink navy)

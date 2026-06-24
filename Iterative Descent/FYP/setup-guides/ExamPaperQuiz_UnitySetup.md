## Exam Paper Quiz 2 -- Unity Setup Guide
Scene: Assets/Scenes/Level 1.unity

---

## Step 1 -- Sprite import settings

In the Project panel navigate to Assets/Sprites/UI/ExamPaper/

Select exam_paper_bg.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Bilinear
- Compression: None
- Click Apply

Select exam_spiral.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply

Select exam_option_bubble.png and exam_option_bubble_selected.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply (do each separately)

Select exam_answer_box.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply

Select exam_tick.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply

Select exam_cross.png:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point
- Compression: None
- Click Apply

---

## Step 2 -- Build the Canvas hierarchy

Open Level 1.unity. In the Hierarchy, find your existing Canvas (the one that contains
Quiz Panel and PC Password Panel). All new objects go inside that same Canvas.

Right-click Canvas > Create Empty, rename it: ExamPaperPanel
This is the full-screen backdrop object. Set these values on its RectTransform:

  Anchor Preset:  stretch-stretch (hold Alt when clicking so it also sets position)
  Left: 0   Right: 0   Top: 0   Bottom: 0
  Pivot: 0.5, 0.5

Add a CanvasGroup component to ExamPaperPanel (Add Component > UI > Canvas Group).
Leave all values at default. This lets you toggle the whole panel on/off cleanly.

Now add a semi-transparent backdrop image directly on ExamPaperPanel:
Add Component > UI > Image
  Source Image: None (leave blank)
  Color: R 0  G 0  B 0  A 160   (dark overlay behind the paper)
  Raycast Target: ON  (blocks clicks from reaching the world)

---

## Step 3 -- Create PaperSheet

Right-click ExamPaperPanel > Create Empty, rename it: PaperSheet

RectTransform values:
  Anchor Preset: middle-centre (no stretching)
  Width:  450
  Height: 620
  Pos X: 0   Pos Y: 0   Pos Z: 0

CRITICAL -- Pivot:
  In the RectTransform header, set Pivot X: 0.5  Y: 1.0
  (top-centre pivot is what makes the flip curl from the top edge)
  The sheet will shift down visually when you do this -- that is expected.
  Re-centre it by setting Pos Y: 310 so it sits centred on screen.

---

## Step 4 -- SpiralStrip

Right-click PaperSheet > UI > Image, rename it: SpiralStrip

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 0   Right: 0
  Height: 40
  Pos Y: 0

Image component:
  Source Image: exam_spiral.png
  Image Type: Simple
  Preserve Aspect: OFF
  Color: R 255  G 255  B 255  A 255
  Raycast Target: OFF

In the Hierarchy, drag SpiralStrip to be the FIRST child of PaperSheet
so it renders underneath everything else.

---

## Step 5 -- PaperBg

Right-click PaperSheet > UI > Image, rename it: PaperBg

RectTransform:
  Anchor Preset: stretch-stretch (hold Alt)
  Left: 0   Right: 0   Top: 40   Bottom: 0
  (Top: 40 leaves room for the spiral strip above)

Image component:
  Source Image: exam_paper_bg.png
  Image Type: Simple
  Preserve Aspect: OFF
  Color: R 255  G 255  B 255  A 255
  Raycast Target: OFF

---

## Step 6 -- Header

Right-click PaperSheet > Create Empty, rename it: Header

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 50
  Pos Y: -48   (sits just below the spiral)

Add Component > Layout > Horizontal Layout Group:
  Child Alignment: Upper Left
  Control Child Size Width: ON   Height: OFF
  Child Force Expand Width: ON   Height: OFF
  Spacing: 0

Right-click Header > UI > Text - TextMeshPro, rename it: TitleText
  TMP component:
    Text: Quiz
    Font Size: 18
    Font Style: Bold
    Vertex Color: R 28  G 22  B 42  A 255
    Alignment: Left + Top
  RectTransform:
    Flexible Width: 1  (via Layout Element component -- Add Component > Layout > Layout Element, Flexible Width = 1)

Right-click Header > UI > Text - TextMeshPro, rename it: InstitutionText
  TMP component:
    Text: Nanyang Technological University
    Font Size: 10
    Font Style: Normal
    Vertex Color: R 28  G 22  B 42  A 255
    Alignment: Right + Top
  Add Component > Layout > Layout Element:
    Flexible Width: 1

---

## Step 7 -- Divider line

Right-click PaperSheet > UI > Image, rename it: Divider

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 2
  Pos Y: -100

Image component:
  Color: R 28  G 22  B 42  A 200
  Raycast Target: OFF

---

## Step 8 -- MetaRow

Right-click PaperSheet > Create Empty, rename it: MetaRow

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 36
  Pos Y: -108

Add Component > Layout > Horizontal Layout Group:
  Child Alignment: Upper Left
  Control Child Size Width: ON   Height: OFF
  Child Force Expand Width: ON   Height: OFF
  Spacing: 8

Create four TMP_Text children of MetaRow:

  NameText:
    Text: Name:  Aaron
    Font Size: 11
    Vertex Color: R 28  G 22  B 42
    Alignment: Left + Bottom
    (Add Layout Element, Flexible Width: 1)

  ScoreText:
    Text: Score:  0 / 5
    Font Size: 11
    Vertex Color: R 28  G 22  B 42
    Alignment: Left + Bottom
    (Add Layout Element, Flexible Width: 1)

  DateText:
    Text: Date:  __/__/____
    Font Size: 11
    Vertex Color: R 28  G 22  B 42
    Alignment: Left + Bottom
    (Add Layout Element, Flexible Width: 1)

  TimeText:
    Text: Time:  ____
    Font Size: 11
    Vertex Color: R 28  G 22  B 42
    Alignment: Left + Bottom
    (Add Layout Element, Flexible Width: 1)

---

## Step 9 -- QuestionLabel

Right-click PaperSheet > UI > Text - TextMeshPro, rename it: QuestionLabel

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 20
  Pos Y: -152

TMP component:
  Text: Question 1 of 5
  Font Size: 11
  Font Style: Normal
  Vertex Color: R 100  G 94  B 82  A 255
  Alignment: Left + Middle

---

## Step 10 -- QuestionText

Right-click PaperSheet > UI > Text - TextMeshPro, rename it: QuestionText

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 70
  Pos Y: -178

TMP component:
  Text: (leave blank -- filled at runtime)
  Font Size: 14
  Font Style: Bold
  Vertex Color: R 28  G 22  B 42  A 255
  Alignment: Left + Top
  Enable Word Wrapping: ON
  Overflow: Overflow

---

## Step 11 -- OptionsContainer

Right-click PaperSheet > Create Empty, rename it: OptionsContainer

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 180
  Pos Y: -254

Add Component > Layout > Vertical Layout Group:
  Child Alignment: Upper Left
  Control Child Size Width: ON   Height: OFF
  Child Force Expand Width: ON   Height: OFF
  Spacing: 6

Now create four identical OptionRow children. Repeat these steps four times,
naming them OptionRow_A, OptionRow_B, OptionRow_C, OptionRow_D:

  Right-click OptionsContainer > Create Empty, rename: OptionRow_A
  RectTransform Height: 38
  Add Component > Layout > Horizontal Layout Group:
    Child Alignment: Middle Left
    Control Child Size Width: OFF   Height: ON
    Child Force Expand Width: OFF   Height: ON
    Spacing: 8
  Add Component > Layout > Layout Element:
    Min Height: 38   Preferred Height: 38

  Inside OptionRow_A, create BubbleButton_A:
    Right-click OptionRow_A > UI > Button - TextMeshPro, rename: BubbleButton_A
    RectTransform Width: 38   (fixed width -- do NOT stretch)
    Add Component > Layout > Layout Element:
      Min Width: 38   Preferred Width: 38   Flexible Width: 0
    Button component:
      Transition: None   (we colour the Image directly at runtime, like PuzzleUI)
    Image component (on the Button object itself):
      Source Image: exam_option_bubble.png
      Image Type: Simple
      Color: R 245  G 240  B 228  A 255
    Delete the default child TMP text that Unity adds to the Button.
    Create a new child: Right-click BubbleButton_A > UI > Text - TextMeshPro, rename: BubbleLabel_A
      Text: A
      Font Size: 14
      Font Style: Bold
      Vertex Color: R 28  G 22  B 42
      Alignment: Centre + Middle
      RectTransform: stretch-stretch, all offsets 0

  Inside OptionRow_A, create OptionLabel_A:
    Right-click OptionRow_A > UI > Text - TextMeshPro, rename: OptionLabel_A
    TMP component:
      Text: (blank -- filled at runtime)
      Font Size: 13
      Vertex Color: R 28  G 22  B 42
      Alignment: Left + Middle
      Enable Word Wrapping: ON
    Add Component > Layout > Layout Element:
      Flexible Width: 1

  Repeat identically for OptionRow_B (label "B"), OptionRow_C ("C"), OptionRow_D ("D").

---

## Step 12 -- AnswerBoxPanel

Right-click PaperSheet > UI > Image, rename it: AnswerBoxPanel

RectTransform:
  Anchor Preset: top-stretch (hold Alt)
  Left: 20   Right: 20
  Height: 56
  Pos Y: -442

Image component:
  Source Image: exam_answer_box.png
  Image Type: Simple
  Color: R 255  G 254  B 245  A 255
  Raycast Target: OFF

Inside AnswerBoxPanel create AnswerFillText:
  Right-click AnswerBoxPanel > UI > Text - TextMeshPro, rename: AnswerFillText
  RectTransform:
    Anchor Preset: stretch-stretch (hold Alt)
    Left: 12   Right: 56   Top: 4   Bottom: 8
  TMP component:
    Text: (blank)
    Font Size: 13
    Font Style: Bold
    Vertex Color: R 28  G 22  B 42
    Alignment: Left + Bottom

Inside AnswerBoxPanel create VerdictImage:
  Right-click AnswerBoxPanel > UI > Image, rename: VerdictImage
  RectTransform:
    Anchor Preset: right-middle
    Width: 40   Height: 40
    Pos X: -8   Pos Y: 0
  Image component:
    Source Image: exam_tick.png   (will be swapped to exam_cross.png at runtime)
    Color: R 255  G 255  B 255  A 255
    Raycast Target: OFF
  IMPORTANT: in the Hierarchy, set VerdictImage to INACTIVE (untick the checkbox
  at the top of the Inspector). ExamPaperPuzzleUI enables it only after an answer.

---

## Step 13 -- NextButton

Right-click PaperSheet > UI > Button - TextMeshPro, rename: NextButton

RectTransform:
  Anchor Preset: bottom-right
  Width: 100   Height: 32
  Pos X: -20   Pos Y: 20

Button component:
  Interactable: OFF (ExamPaperPuzzleUI enables it after an answer)
  Transition: Color Tint (leave default Unity values)

Image component:
  Color: R 28  G 22  B 42  A 255

Child TMP text (rename it NextLabel):
  Text: NEXT >
  Font Size: 13
  Font Style: Bold
  Vertex Color: R 245  G 240  B 228  A 255

---

## Step 14 -- CompletionPanel

Right-click PaperSheet > Create Empty, rename: CompletionPanel
Set INACTIVE in the Hierarchy (untick the checkbox at the top of Inspector).

RectTransform:
  Anchor Preset: stretch-stretch (hold Alt)
  Left: 0   Right: 0   Top: 40   Bottom: 0

Add Component > UI > Image:
  Color: R 245  G 240  B 228  A 245
  Raycast Target: ON

Right-click CompletionPanel > UI > Text - TextMeshPro, rename: CompletionText
  RectTransform:
    Anchor Preset: middle-centre
    Width: 360   Height: 80
    Pos Y: 30
  TMP component:
    Text: Completed!  0 / 5 correct.
    Font Size: 20
    Font Style: Bold
    Vertex Color: R 28  G 22  B 42
    Alignment: Centre + Middle

Right-click CompletionPanel > UI > Button - TextMeshPro, rename: CloseButton
  RectTransform:
    Anchor Preset: middle-centre
    Width: 120   Height: 36
    Pos Y: -40
  Image component:
    Color: R 28  G 22  B 42  A 255
  Child TMP label:
    Text: Close
    Font Size: 14
    Vertex Color: R 245  G 240  B 228  A 255

---

## Step 15 -- Add components to PaperSheet

Select PaperSheet in the Hierarchy.

Add Component > search "PageFlipAnimator", add it.
  Flip Duration: 0.45
  Flip Curve: click the curve field, choose EaseInOut from the presets
  Page Rect: drag PaperSheet itself into this field

Add Component > search "ExamPaperPuzzleUI", add it.
Now fill every field (drag objects from the Hierarchy into each slot):

  Flip Animator        drag PageFlipAnimator component (click the target icon, pick PaperSheet)
  Title Text           drag TitleText
  Institution Text     drag InstitutionText
  Name Text            drag NameText
  Score Text           drag ScoreText
  Date Text            drag DateText
  Time Text            drag TimeText
  Question Label       drag QuestionLabel
  Question Text        drag QuestionText
  Option Buttons
    Element 0          drag BubbleButton_A
    Element 1          drag BubbleButton_B
    Element 2          drag BubbleButton_C
    Element 3          drag BubbleButton_D
  Option Labels
    Element 0          drag OptionLabel_A
    Element 1          drag OptionLabel_B
    Element 2          drag OptionLabel_C
    Element 3          drag OptionLabel_D
  Option Bubbles       (the Image component on each BubbleButton -- use the target icon)
    Element 0          click target icon > pick BubbleButton_A > select its Image component
    Element 1          BubbleButton_B Image
    Element 2          BubbleButton_C Image
    Element 3          BubbleButton_D Image
  Answer Fill Text     drag AnswerFillText
  Verdict Image        drag VerdictImage
  Tick Sprite          drag exam_tick.png from Project panel (Assets/Sprites/UI/ExamPaper/)
  Cross Sprite         drag exam_cross.png from Project panel
  Next Button          drag NextButton
  Completion Panel     drag CompletionPanel
  Completion Text      drag CompletionText
  Close Button         drag CloseButton
  Default Name         Aaron   (type in the field)

  Colours (leave as defaults set in code -- change only if you want different shades):
    Bubble Default Colour  R 245  G 240  B 228  A 255
    Bubble Correct Colour  R 204  G 240  B 210  A 255
    Bubble Wrong Colour    R 245  G 209  B 209  A 255
    Bubble Selected Colour R 217  G 217  B 191  A 255

---

## Step 16 -- Create ExamPaperProp on the desk

In the Hierarchy, find or create the desk/clipboard prop in your scene
(the GameObject that should trigger Quiz 2).

Add these four components to it:

  1. BoxCollider -- size it to cover the interactable surface
  2. InteractableBase -- leave all defaults
  3. InteractableRegistrar -- leave all defaults
  4. ExamPaperProp -- fill fields:

     Load From JSON:        ON
     Json File Name:        quiz_data_2.json
     Questions Per Session: 5
     Exam Paper Overlay:    drag ExamPaperPanel from the Hierarchy
     On Completed:          wire whatever you need (door unlock, next trigger, etc.)

The Interact Label is already set to "Pick Up Paper" in code.

---

## Step 17 -- Event handler for door/outcome

ExamPaperPuzzleUI fires its OWN static event (ExamPaperPuzzleUI.OnPuzzleFinished),
NOT the PuzzleUI.OnPuzzleFinished that PuzzleEventHandler listens to.

Create a new empty GameObject in the scene, name it: ExamPuzzleEventHandler
Add a new script to it, or simply wire the outcome through the
On Completed UnityEvent on the ExamPaperProp Inspector (Step 16) --
that is the simplest option and requires no extra script.

If you want the same BKT-gated 3-correct threshold logic, create a script that subscribes:
  ExamPaperPuzzleUI.OnPuzzleFinished += (correct, total) => { if (correct >= 3) door.Unlock(); };

---

## Step 18 -- Sanity check before Play Mode

In the Hierarchy, confirm:
  ExamPaperPanel    active: NO  (must start hidden)
  CompletionPanel   active: NO  (inside PaperSheet, hidden until quiz ends)
  VerdictImage      active: NO  (inside AnswerBoxPanel, shown after each answer)
  NextButton interactable: OFF (enabled after each answer)

---

## Step 19 -- Smoke test in Play Mode

1. Walk up to the desk prop -- outline appears, prompt shows "Pick Up Paper"
2. Press E -- ExamPaperPanel appears
   Name shows: Aaron
   Institution shows: Nanyang Technological University
   Title shows: Quiz
   Date fills automatically with today's date
3. Question 1 of 5 appears (hash tables topic)
4. Click any bubble -- bubble turns green (correct) or red (wrong)
   Answer box fills with the chosen letter and text
   Tick or X appears on the right side of the answer box
   NEXT > button becomes clickable
5. Click NEXT > -- page flips upward from the top edge, new question appears
6. After question 5 -- CompletionPanel appears with "Completed! X / 5 correct."
7. Click Close -- cursor locks, timeScale returns to 1, player can move again
8. Press H -- DDA HUD shows quiz attempt count incremented by 1

---

## Colour reference

Paper bg:          #F5F0E4  (baked into exam_paper_bg.png)
All dark text:     #1C1629  (R 28  G 22  B 42)
Bubble unanswered: #F5F0E4
Bubble correct:    #CCF0D2  (green wash)
Bubble wrong:      #F5D1D1  (red wash)
NEXT button:       #1C1629 background, #F5F0E4 text

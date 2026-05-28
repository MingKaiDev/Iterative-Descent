# CRT Quiz Panel Setup Guide

Two files to drop in, two Paint sprites to draw, and a few Inspector tweaks.

---

## Part 1 -- Paint Assets to Draw

Draw two PNG files. No transparency needed -- these are positioned at the panel edges
and never overlap the quiz content.

### Sprite A: crt_bezel_h.png  (horizontal strip -- top and bottom of screen)

Canvas size: 512 x 56 pixels

Step by step in MS Paint:
1. File > New > resize canvas to 512 wide x 56 tall (in pixels, not percentage)
2. Set Edit Colors custom color 1 to R:28 G:34 B:28 (#1C221C) -- dark gray-green, the main bezel body
3. Fill the entire canvas with that color (paint bucket on the white background)
4. Set color to R:8 G:8 B:8 (#080808) -- near black
5. Draw a 2-pixel tall horizontal line along the very bottom edge of the image
   (this is the inner edge that faces the screen -- creates a recessed shadow look)
6. Set color to R:42 G:50 B:42 (#2A322A) -- slightly lighter gray-green
7. Draw a 1-pixel tall line just above the black line (row 53, counting from top)
   (this faint highlight above the shadow creates the bevel illusion)
8. Optional vent marks (give it a real monitor look):
   Set color to R:20 G:26 B:20 (#141A14)
   Draw 3 groups of 2 short horizontal lines near the TOP of the image (rows 8-9, 13-14, 18-19)
   Each group: starts at x=16, ends at x=40. Leave gaps between groups.
9. Save as crt_bezel_h.png

### Sprite B: crt_bezel_bottom.png  (bottom strip -- same as A but with power LED)

Same as crt_bezel_h.png EXCEPT:
- The shadow line goes along the TOP edge (row 0-1) not the bottom
  (because the inner edge now faces upward toward the screen)
- Add a power LED dot:
  Set color to R:0 G:255 B:65 (#00FF41) -- phosphor green
  Draw a 6x6 filled square at position x=456, y=22 (right side of strip, vertically centered)
  Then draw a 1px black border around it to make it pop
  This simulates the monitor power indicator LED

Save as crt_bezel_bottom.png

### Sprite C: crt_bezel_v.png  (vertical strip -- left and right sides)

Canvas size: 56 x 512 pixels

Same color fills as Sprite A, but:
- The shadow line (2px, near-black) runs along the RIGHT edge (innermost edge, faces screen)
- The faint highlight runs 1px to the left of the shadow
- Optional vent marks: draw 3 groups of short VERTICAL lines near the left edge instead
  (rows start at y=16, each mark is 2px wide x 16px tall, x positions: 8-9, 13-14, 18-19)

Save as crt_bezel_v.png

---

## Part 2 -- Unity Materials

### Create the CRT Overlay Material
1. In Project panel, right-click > Create > Material
2. Name it: CRTScanlineOverlay
3. In the Shader dropdown, select: Custom/UI/CRTScanlineVignette
4. Set properties (tweak to taste):
   - Scanline Darkness: 0.25
   - Vignette Strength: 0.65
   - Vignette Radius: 0.5
   - Noise Flicker: 0.03

---

## Part 3 -- Quiz Panel Hierarchy Setup

Your Puzzle Panel should look like this after setup. The order matters -- Unity UI
renders children from top to bottom in the hierarchy (higher index = rendered on top).

```
[Puzzle Panel]  <-- CanvasGroup + PuzzleCRTController here
  [BG]          <-- Image, color #050D05 (near-black, slight green tint)
  [Content]     <-- your existing question text, buttons, etc.
  [CRTOverlay]  <-- NEW: full-panel transparent Image with CRTScanlineOverlay material
  [Bezel_Top]   <-- NEW: Image using crt_bezel_h.png sprite
  [Bezel_Bottom] <-- NEW: Image using crt_bezel_bottom.png sprite
  [Bezel_Left]  <-- NEW: Image using crt_bezel_v.png sprite
  [Bezel_Right] <-- NEW: Image using crt_bezel_v.png sprite
```

### CRTOverlay Image settings
- Add a child Image GameObject named CRTOverlay
- Set its RectTransform to stretch-fill the full panel:
  Anchor Min (0,0) Max (1,1), Left/Right/Top/Bottom offsets all 0
- Material: CRTScanlineOverlay
- Source Image: None (leave blank)
- Color: white (RGBA 1,1,1,1) -- the shader drives everything
- Raycast Target: OFF (important -- otherwise it blocks button clicks)

### Bezel Image settings (all four strips)

Import the PNG sprites first:
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point (no blur -- CRT pixels should be sharp)
- Compression: None

Bezel_Top:
- Source Image: crt_bezel_h.png
- Anchor: top-stretch (Min X=0, Max X=1, Min Y=1, Max Y=1)
- Height: 56
- Pivot Y: 1
- Offset Top: 0, Left: 0, Right: 0

Bezel_Bottom:
- Source Image: crt_bezel_bottom.png
- Anchor: bottom-stretch
- Height: 56
- Pivot Y: 0

Bezel_Left:
- Source Image: crt_bezel_v.png
- Anchor: left-stretch
- Width: 56
- Set Top and Bottom offsets to 56 each (so it does not overlap the horizontal strips at corners)

Bezel_Right:
- Same as Left but anchor to right, flip the sprite horizontally using Scale X = -1

---

## Part 4 -- PuzzleUI Inspector Colors (phosphor green scheme)

Select your PuzzleUI component and change these Colours fields:

| Field           | Hex value | R/G/B (0-1)             |
|-----------------|-----------|-------------------------|
| Default Colour  | #0D190D   | R:0.05 G:0.10 B:0.05    |
| Correct Colour  | #0DBF26   | R:0.05 G:0.75 B:0.15    |
| Wrong Colour    | #BF2000   | R:0.75 G:0.12 B:0.00    |
| Text Colour     | #33FF33   | R:0.20 G:1.00 B:0.20    |

Also set the Panel BG Image color to #050D05 (R:0.02 G:0.05 B:0.02).

### TMP Text phosphor glow (optional but looks great)
For each TMP_Text in the quiz (questionText, progressText, feedbackText, button labels):
1. Select the TMP GameObject
2. In the TextMeshPro component, click the font Material
3. Enable Underlay:
   - Color: #00FF00 at 80% alpha
   - Offset X: 0, Offset Y: 0
   - Dilate: 0.3
   - Softness: 0.4
This adds a soft green bloom around the text, like phosphor glow on a real CRT.

---

## Part 5 -- PuzzleCRTController

Add PuzzleCRTController to the Puzzle Panel root GameObject.
It requires a CanvasGroup -- Unity will add one automatically if missing.

Default settings are fine. If flicker feels too harsh, lower Flicker Depth toward 0.05.

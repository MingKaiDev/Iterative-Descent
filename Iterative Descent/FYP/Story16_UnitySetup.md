Story 16 -- Computer Lab Server Room Door Puzzle
Unity Scene Setup Checklist

======================================================
OVERVIEW
======================================================

Puzzle: subnet allocation gate (VLSM / CCNA style)
Location: Computer Lab, exterior wall of server room door
Unlocks: server room door
BKT concept: computer_networks
Files created:
  Assets/Scripts/Puzzles/Subnet/SubnetPuzzleUI.cs
  Assets/Scripts/Puzzles/Subnet/SubnetServerRow.cs
  Assets/Scripts/Puzzles/Subnet/SubnetFeedbackPopup.cs
  Assets/Scripts/Props/SubnetPuzzleProp.cs
  Assets/Scripts/Events/SubnetEventHandler.cs


======================================================
STEP 1 -- CREATE THE CONCEPTPROFILE ASSET
======================================================

1. In Project window: Assets > Create > ARBITEX > Concept Profile
2. Name it: ConceptProfile_ComputerNetworks
3. Set fields:
     Concept Key:   computer_networks
     Display Name:  Computer Networks
     Prior Known:   0.25
     Learning Rate: 0.10
     Slip Rate:     0.08
     Guess Rate:    0.20
4. Save the asset to Assets/Data/ (or wherever other ConceptProfile assets live)
5. On the GameManager > PlayerMetricsTracker component, add this asset to the
   Concept Profiles array


======================================================
STEP 2 -- BUILD THE CANVAS HIERARCHY
======================================================

SubnetPanel is a child of the main scene Canvas (same Canvas as other puzzle panels).
It starts INACTIVE in the Inspector.

UI Theme: Cisco Packet Tracer aesthetic
  Background colour:  #1A2332  (dark navy)
  Accent text colour: #00C8FF  (CPT cyan)
  Row background:     #1E2D3D
  Font: monospaced (Courier New or Liberation Mono via TMP font asset)


SubnetPanel                   [RectTransform, Image (#1A2332), CanvasGroup]
  Background                  [Image (#1A2332), stretch-fill anchor]
    HeaderRow                 [HorizontalLayoutGroup, spacing 16, padding 20]
      TitleText               [TMP, "ARBITEX NETWORK CONSOLE v2.1", colour #00C8FF, bold, size 16]
      StatusText              [TMP, "[LOCKED]", colour #FF4444, bold, size 16, right-aligned]
    SubText                   [TMP, "Assign the correct subnet prefix to restore each server link.",
                                colour #8899AA, size 13, italic]
    Divider                   [Image, height 1, colour #2A4060, horizontal stretch]
    ReferenceTable            [VerticalLayoutGroup, padding 12, background #162030]
      RefTitle                [TMP, "SUBNET REFERENCE", colour #00C8FF, size 12]
      RefText                 [TMP (see content below), colour #AABBCC, size 11]
    Divider2                  [Image, height 1, colour #2A4060]
    TableHeader               [HorizontalLayoutGroup] (see column widths below)
      HeaderServer            [TMP, "SERVER", colour #00C8FF, size 12, width 200]
      HeaderHosts             [TMP, "HOSTS REQ.", colour #00C8FF, size 12, width 120]
      HeaderPrefix            [TMP, "CIDR PREFIX", colour #00C8FF, size 12, width 160]
      HeaderStatus            [TMP, "STATUS", colour #00C8FF, size 12, width 60]
    ServerRow_0               [SubnetServerRow] -- see STEP 3
    ServerRow_1               [SubnetServerRow]
    ServerRow_2               [SubnetServerRow]
    ServerRow_3               [SubnetServerRow]
    Divider3                  [Image, height 1, colour #2A4060]
    ButtonRow                 [HorizontalLayoutGroup, spacing 20, padding top 10]
      SubmitButton            [Button, Image (#1A6B3A), TMP "APPLY CONFIG", colour #00FF88]
      CloseButton             [Button, Image (#3A2020), TMP "EXIT", colour #FF6666]
  FeedbackPopup               [SubnetFeedbackPopup -- starts ACTIVE in Inspector]
    Blocker                   [Image, full-stretch, alpha 0, Raycast Target ON]
    PopupCard                 [Image (#1E2D3D), centre anchor, ~420 x 185]
      IconText                [TMP -- large OK / X, wired in Inspector]
      MessageText             [TMP -- body copy, wired in Inspector]
      OkButton                [Button + TMP "OK"]


REFERENCE TABLE content (RefText):
  /25  =  126 usable hosts  (2^7 - 2)
  /26  =   62 usable hosts  (2^6 - 2)
  /27  =   30 usable hosts  (2^5 - 2)
  /28  =   14 usable hosts  (2^4 - 2)
  /29  =    6 usable hosts  (2^3 - 2)
  /30  =    2 usable hosts  (2^2 - 2)


======================================================
STEP 3 -- BUILD EACH SERVER ROW
======================================================

Each ServerRow_N is a child of the ServerTable VerticalLayoutGroup.
Attach SubnetServerRow to it.
All four rows are identical in structure.

Row height: ~48px. Background Image #1E2D3D. HorizontalLayoutGroup, spacing 8, padding 6.

Inside each row:
  StatusDot      [Image, 12x12, colour #708090 (neutral), LayoutElement width 20]
  ServerLabel    [TMP, monospace, left-aligned, colour #E0EAF0, LayoutElement width 200, flexible 0]
  HostsText      [TMP, right-aligned, colour #AABBCC, size 12, LayoutElement width 120]
  PrefixDisplay  [TMP, centre-aligned, colour #00FF88, bold, size 16, LayoutElement width 80]
  LeftButton     [Button, Image (#2A4060), TMP "<", size 14, LayoutElement width 30]
  RightButton    [Button, Image (#2A4060), TMP ">", size 14, LayoutElement width 30]

Wire in SubnetServerRow Inspector:
  Status Dot     -> StatusDot Image
  Server Label   -> ServerLabel TMP
  Hosts Text     -> HostsText TMP
  Prefix Display -> PrefixDisplay TMP
  Left Button    -> LeftButton Button
  Right Button   -> RightButton Button


======================================================
STEP 4 -- WIRE SubnetPuzzleUI
======================================================

On SubnetPanel > SubnetPuzzleUI component:
  Server Rows[0..3] -> ServerRow_0 .. ServerRow_3 (drag each in order)
  Status Text       -> HeaderRow > StatusText
  Reference Table   -> ReferenceTable GameObject
  Submit Button     -> ButtonRow > SubmitButton
  Close Button      -> ButtonRow > CloseButton
  Feedback Popup    -> FeedbackPopup (SubnetFeedbackPopup component)


======================================================
STEP 5 -- PROP SETUP ON THE TERMINAL OBJECT
======================================================

On the server room wall panel mesh (or an empty GO at its position):
  - BoxCollider (trigger), sized to front face
  - InteractableBase
  - InteractableRegistrar
  - SubnetPuzzleProp
    -> Subnet Overlay: SubnetPanel (Canvas child)

InteractableBase settings:
  Interact Label: "Access Server Room Panel"


======================================================
STEP 6 -- EVENT HANDLER
======================================================

On the persistent GameManager GameObject:
  Add SubnetEventHandler component
  -> Server Room Door: wire the DoorController on the server room door

Do NOT add duplicate SubnetEventHandler components.


======================================================
STEP 7 -- INTERACTABLE INDICATOR (optional)
======================================================

Add InteractableIndicator as a child GO of the terminal prop.
Set bobAxis to Vector3.up.
The indicator will auto-hide when the player solves and closes.


======================================================
STEP 8 -- LOCKED DOOR
======================================================

On the server room door:
  - DoorController (already standard)
  - LockedDoorProp (IInteractable, so player sees "Locked -- find the access panel")
  - Wire SubnetEventHandler.serverRoomDoor to this DoorController

The server room door should start LOCKED (DoorController.isLocked = true in Inspector).


======================================================
TESTING CHECKLIST
======================================================

[ ] SubnetPanel is inactive in Inspector on play
[ ] Interacting with the terminal prop opens the panel correctly
[ ] All four server rows appear with correct host requirements
[ ] Cycling < > changes the prefix display
[ ] Tier 0/1: reference table is visible; Tier 2+: hidden (force tier via DDADisplayHUD H key)
[ ] Wrong submission highlights red rows and shows feedback popup
[ ] After 3 wrong submissions the hint formula appears in popup
[ ] Correct submission shows success popup with "ACCESS GRANTED" status
[ ] On popup dismiss: door unlocks, panel closes, cursor re-locks, timeScale = 1
[ ] BKT updates: check DDADisplayHUD H for computer_networks P(mastery) change
[ ] No duplicate SubnetEventHandler fires (check Debug.Log count)

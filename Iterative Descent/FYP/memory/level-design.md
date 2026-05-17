# Level Design State

## Modular Grid Standards

| Element | Specification |
|---|---|
| Grid unit | 4m W × 4m D × 3.5m H |
| Door (single) | 1m wide × 2.1m tall |
| Door (double) | 2m wide × 2.1m tall |
| Hallway width | 4m |
| Alignment | All rooms snap to 4m grid; all door openings flush with wall faces |

---

## Room Status

| Room | Dimensions | Status | Notes |
|---|---|---|---|
| Main Hall | 12m × 8m | DONE | South double door (spawn), East/West singles |
| Classroom 1 | 8m × 16m | DONE | East single to Hallway 1; enemy encounter, furniture maze, 2 corpses, blood splatter |
| Hallway 1 Segment | 4m × 16m | DONE | SW/SE singles; boarded window, collapsed shelf, 1 corpse |
| Washroom | 12m × 8m | DONE | NW female, NE male; sinks, cubicles, urinals, horror props |
| Stairs 1 | 4m × 8m | DONE | Scissors staircase — see blender-stairs1.md |
| Classroom 2 | — | PENDING | — |
| Computer Lab | — | PENDING | — |
| Classroom 3 | — | PENDING | — |
| Hallway 2 | — | PENDING | — |
| General Office | — | PENDING | — |
| Stairs 2 | — | PENDING | Up only |
| Stairs 3 | — | PENDING | Up only |
| Courtyard | — | PENDING | Exterior low poly |
| Shack | — | PENDING | Standalone in courtyard |

---

## Blender Build Rules

- Build directly in Blender — no Python scripts
- All transforms applied; no loose geometry
- Export as FBX, Unity-ready
- **Naming convention:** `Type_RoomCode_Identifier`
  - Examples: `Wall_North`, `Prop_Corpse_01`, `Light_Fluorescent_H1_01`
- Blood/gore objects on a separate **Gore Layer** in Unity

---

## FBX Export Settings (all assets)

| Setting | Value |
|---|---|
| Forward | -Z Forward |
| Up | Y Up |
| Apply Scalings | FBX Units Scale |
| Transforms | All applied, no loose geometry |

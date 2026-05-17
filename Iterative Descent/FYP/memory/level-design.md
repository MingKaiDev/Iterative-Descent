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
| Main Hall | 12m × 8m | DONE | South double door (spawn), East/West singles — see detail below |
| Classroom 1 | 8m × 16m | DONE | East single to Hallway 1; enemy encounter, furniture maze, 2 corpses, blood splatter — see detail below |
| Hallway 1 Segment | 4m × 16m | DONE | SW/SE singles; boarded window, collapsed shelf, 1 corpse — see detail below |
| Washroom | 12m × 8m | DONE | NW female, NE male; sinks, cubicles, urinals, horror props — see detail below |
| Stairs 1 | 4m × 8m | DONE | Scissors staircase — see detail below |
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

- Build directly in Blender — no Python automation scripts
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

---

# Completed Room Asset Details

---

## Main Hall

**Files:** `MainHall.blend` / `MainHall.fbx`

### Geometry & Dimensions

| Property | Value |
|---|---|
| Footprint | 12m wide (X) × 8m deep (Y) |
| Grid units | 3 × 2 |
| Floor-to-ceiling | 3.5m |

### Door Openings

| Opening | Wall | Width | Notes |
|---|---|---|---|
| South double door | South wall, centred | 2m wide × 2.1m tall | Spawn point — player enters here |
| East single door | East wall | 1m wide × 2.1m tall | Connects to adjacent area |
| West single door | West wall | 1m wide × 2.1m tall | Secondary connection |

### Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floor slab, walls (N/S/E/W), ceiling, door arch cutouts |
| Props | Furniture, atmospheric dressing |
| Lights | Overhead fluorescents, horror point lights |
| Gore | Blood splatter objects (Unity Gore Layer) |

---

## Classroom 1

**Files:** `Classroom1.blend` / `Classroom1.fbx`

### Geometry & Dimensions

| Property | Value |
|---|---|
| Footprint | 8m wide (X) × 16m deep (Y) |
| Grid units | 2 × 4 |
| Floor-to-ceiling | 3.5m |

### Door Openings

| Opening | Wall | Width | Notes |
|---|---|---|---|
| East single door | East wall | 1m wide × 2.1m tall | Connects to Hallway 1 |

### Key Props & Layout

- **Furniture maze** — desks/chairs arranged as traversal obstacles along the room length
- **2 corpses** — positioned within maze area; on Gore Layer
- **Blood splatter** — wall and floor decals; on Gore Layer
- Enemy encounter zone — open entry area before the furniture maze begins

### Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floor slab, walls, ceiling, door arch cutout |
| Furniture | Desks, chairs arranged as maze obstacles |
| Props | Atmospheric dressing, horror details |
| Lights | Fluorescent panels (some flickering/broken), horror point lights |
| Gore | 2 corpse meshes, blood splatter decals (Unity Gore Layer) |

---

## Hallway 1 Segment

**Files:** `Hallway1.blend` / `Hallway1.fbx`

### Geometry & Dimensions

| Property | Value |
|---|---|
| Footprint | 4m wide (X) × 16m deep (Y) |
| Grid units | 1 × 4 |
| Floor-to-ceiling | 3.5m |

### Door Openings

| Opening | Wall | Width | Notes |
|---|---|---|---|
| SW single door | South wall, west side | 1m wide × 2.1m tall | South entry |
| SE single door | South wall, east side | 1m wide × 2.1m tall | Secondary south entry |

### Key Props & Layout

- **Boarded window** — planks nailed across a long-wall window opening
- **Collapsed shelf** — fallen shelving unit partially blocking hallway width
- **1 corpse** — positioned near collapsed shelf; on Gore Layer

### Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floor slab, walls, ceiling, door arch cutouts |
| Props | Boarded window boards, collapsed shelf, atmospheric debris |
| Lights | Sparse fluorescents, horror point lights |
| Gore | 1 corpse mesh (Unity Gore Layer) |

---

## Washroom

**Files:** `Washroom.blend` / `Washroom.fbx`

### Geometry & Dimensions

| Property | Value |
|---|---|
| Footprint | 12m wide (X) × 8m deep (Y) |
| Grid units | 3 × 2 |
| Floor-to-ceiling | 3.5m |

### Layout Split

| Zone | Position | Contents |
|---|---|---|
| Female section | NW quadrant | Sinks along north wall, toilet cubicles |
| Male section | NE quadrant | Sinks along north wall, urinals, toilet cubicles |

### Key Props & Layout

- **Sinks** — wall-mounted along north wall in both sections
- **Toilet cubicles** — partitioned stalls, doors ajar or broken
- **Urinals** — male section, north wall
- **Horror props** — blood, overturned fixtures, graffiti, broken mirrors

### Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floor slab, walls, ceiling, internal partition walls, door arch cutouts |
| Fixtures | Sinks, toilet bowls, urinals, cubicle partitions and doors |
| Props | Mirrors, horror atmospheric dressing |
| Lights | Fluorescent panels (flickering), horror point lights |
| Gore | Blood splatter, horror-specific props (Unity Gore Layer) |

---

## Stairs 1

**Files:** `Stairs1.blend` / `Stairs1.fbx`

### Geometry & Dimensions

| Property | Value |
|---|---|
| Room footprint | 4m wide (X) × 8m deep (Y) |
| Stair type | Scissors / U-turn (two independent flights sharing mid-landings) |
| Floor levels | L0 (0m), L0A (1.75m), L1 (3.5m), L1A (5.25m), L2 (7.0m) |
| Floor-to-floor height | 3.5m |
| Steps per flight | 10 |
| Rise per step | 175mm |
| Tread depth | 300mm |
| Flight clear width | 2m (each occupies one half of the 4m room) |
| Mid-landing depth | 2m at back (west) wall |
| East landing buffer | 2m gap between stair end and east wall arches |
| L2 room | Same 4×8m footprint on top; 3.5m tall; own centred arch |

### Flight Sequence

| Flight | Side | From → To | Y Direction | Z Direction |
|---|---|---|---|---|
| F1 | LEFT (X: -2→0) | L0A (1.75m) → L0 (0m) | Walks east (+Y), Y=-1.5→+1.5 | Descends |
| F2 | RIGHT (X: 0→+2) | L0A (1.75m) → L1 (3.5m) | Walks east (+Y), Y=-1.5→+1.5 | Ascends |
| F3 | LEFT (X: -2→0) | L1 (3.5m) → L1A (5.25m) | Walks west (-Y), Y=+1.5→-1.5 | Ascends |
| F4 | RIGHT (X: 0→+2) | L1A (5.25m) → L2 (7.0m) | Walks east (+Y), Y=-1.5→+1.5 | Ascends |

### East Wall Openings (all centred, X: -1 to +1, 2m wide)

| Opening | Z Range | Connects To |
|---|---|---|
| L0 arch | Z = 0 → 2.4m | Ground floor level |
| L1 arch | Z = 3.5 → 5.9m | L1 floor / Hallway 1 (main entry) |
| L2 arch | Z = 7.0 → 9.4m | L2 room / upper floor |

### Structural Elements

- `Floor_L0`, `Floor_L1` (F2 hole cut, full right-half width), `Floor_L2` (F4 hole cut) — 200mm thick slabs
- `Land_L0A`, `Land_L1A` — full 4m wide × 2m deep mid-landing slabs at back (west) wall
- `Wall_West`, `Wall_North`, `Wall_South` — full height 7m perimeter walls
- `Wall_Div` (back section, Y=-4→-1.5) + `Div_East` (stair zone only, Y=-1.5→+1.5) — centre divider at X=0
- Divider has pass-through holes at L0A and L1A Z levels for mid-landing slabs
- `Ceil_Stair` — ceiling slab at top of stair shell (Z=7.0)
- L2_Room collection: `L2_Wall_West`, `L2_Wall_North`, `L2_Wall_South`, `L2_Ceil` (Z=10.5)

### Props & Details

**Handrails**
- Horizontal Y-direction pipes at `rail_h` = 900mm above each tread top
- Wall side: X=±1.95 (parallel to Wall_North / Wall_South)
- Divider side: X=±0.05 (parallel to Div_East)
- Horizontal guard rails over mid-landings (L0A, L1A) and east landing zones at L1, L2 level
- Vertical square spindles (20mm), 2 per step per side, connecting tread top to rail

**Lighting**
- Fluorescent panel meshes + area lights on underside of `Floor_L1` (Z=3.28), `Floor_L2` (Z=6.78), `L2_Ceil` (Z=10.48)
- 6 panels per slab: left stair run, right stair run, left mid-landing zone, right mid-landing zone, left east buffer, right east buffer
- 4 orange horror point lights at L0A and L1A mid-landings for atmosphere

**Graffiti**
- 6 text props: `"EXIT →"`, `"TURN BACK"`, `"RUN"`, `"NO WAY OUT"`, `"HELP"`, `"↑ ROOF"`
- Placed at eye level near mid-landings and entry points
- Semi-transparent materials: green (`M_Graff1`), red (`M_Graff2`)

### Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floors, walls, east wall arch pieces, divider sections |
| L2_Room | L2 room walls and ceiling |
| Stairs | All tread slabs, nosing strips, mid-landing slabs |
| Rails | All handrail pipes, spindles, guard rails |
| Lights | Fluorescent panel meshes, area lights, horror point lights |
| Props | Graffiti text objects |

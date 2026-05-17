# Blender Asset — Stairs 1

**Files:** `Stairs1.blend` / `Stairs1.fbx`

---

## Geometry & Dimensions

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

---

## Flight Sequence

| Flight | Side | From → To | Y Direction | Z Direction |
|---|---|---|---|---|
| F1 | LEFT (X: -2→0) | L0A (1.75m) → L0 (0m) | Walks east (+Y), Y=-1.5→+1.5 | Descends |
| F2 | RIGHT (X: 0→+2) | L0A (1.75m) → L1 (3.5m) | Walks east (+Y), Y=-1.5→+1.5 | Ascends |
| F3 | LEFT (X: -2→0) | L1 (3.5m) → L1A (5.25m) | Walks west (-Y), Y=+1.5→-1.5 | Ascends |
| F4 | RIGHT (X: 0→+2) | L1A (5.25m) → L2 (7.0m) | Walks east (+Y), Y=-1.5→+1.5 | Ascends |

---

## East Wall Openings (all centred, X: -1 to +1, 2m wide)

| Opening | Z Range | Connects To |
|---|---|---|
| L0 arch | Z = 0 → 2.4m | Ground floor level |
| L1 arch | Z = 3.5 → 5.9m | L1 floor / Hallway 1 (main entry) |
| L2 arch | Z = 7.0 → 9.4m | L2 room / upper floor |

---

## Structural Elements

- `Floor_L0`, `Floor_L1` (F2 hole cut, full right-half width), `Floor_L2` (F4 hole cut) — 200mm thick slabs
- `Land_L0A`, `Land_L1A` — full 4m wide × 2m deep mid-landing slabs at back (west) wall
- `Wall_West`, `Wall_North`, `Wall_South` — full height 7m perimeter walls
- `Wall_Div` (back section, Y=-4→-1.5) + `Div_East` (stair zone only, Y=-1.5→+1.5) — centre divider at X=0
- Divider has pass-through holes at L0A and L1A Z levels for mid-landing slabs
- `Ceil_Stair` — ceiling slab at top of stair shell (Z=7.0)
- L2_Room collection: `L2_Wall_West`, `L2_Wall_North`, `L2_Wall_South`, `L2_Ceil` (Z=10.5)

---

## Props & Details

### Handrails
- Horizontal Y-direction pipes at `rail_h` = 900mm above each tread top
- Wall side: X=±1.95 (parallel to Wall_North / Wall_South)
- Divider side: X=±0.05 (parallel to Div_East)
- Horizontal guard rails over mid-landings (L0A, L1A) and east landing zones at L1, L2 level
- Vertical square spindles (20mm), 2 per step per side, connecting tread top to rail

### Lighting
- Fluorescent panel meshes + area lights on underside of `Floor_L1` (Z=3.28), `Floor_L2` (Z=6.78), `L2_Ceil` (Z=10.48)
- 6 panels per slab: left stair run, right stair run, left mid-landing zone, right mid-landing zone, left east buffer, right east buffer
- 4 orange horror point lights at L0A and L1A mid-landings for atmosphere

### Graffiti
- 6 text props: `"EXIT →"`, `"TURN BACK"`, `"RUN"`, `"NO WAY OUT"`, `"HELP"`, `"↑ ROOF"`
- Placed at eye level near mid-landings and entry points
- Semi-transparent materials: green (`M_Graff1`), red (`M_Graff2`)

---

## Blender Collections

| Collection | Contents |
|---|---|
| Structure | Floors, walls, east wall arch pieces, divider sections |
| L2_Room | L2 room walls and ceiling |
| Stairs | All tread slabs, nosing strips, mid-landing slabs |
| Rails | All handrail pipes, spindles, guard rails |
| Lights | Fluorescent panel meshes, area lights, horror point lights |
| Props | Graffiti text objects |

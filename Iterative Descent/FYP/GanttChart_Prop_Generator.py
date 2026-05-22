"""
GanttChart_Prop_Generator.py
────────────────────────────
Run this inside Blender's Scripting tab (Text Editor → Run Script).
Blender 3.x / 4.x compatible.

What it builds
──────────────
A flat 3D Gantt chart prop (1.4 m × 0.75 m × 0.01 m) to mount on the
whiteboard in ClassRoom1.  It shows an INTENTIONALLY WRONG (shuffled)
schedule for P1–P4 to hint that the puzzle needs to be solved.

Correct SJF answer (ascending BT):  P4(4) → P3(6) → P2(9) → P1(12)
The prop displays them in wrong order: P1 | P3 | P4 | P2
so the player sees the unresolved state in the world.

Export target
─────────────
  …\\Iterative Descent\\Assets\\Art\\Environment\\Map\\Prop_GanttChart.fbx

Unity import settings
─────────────────────
  • Scale Factor : 1
  • Axis: Y Up, -Z Forward  (matches your other FBX exports)
  • Place as child of Prop_Whiteboard in the scene; adjust position/rotation
    in the Inspector so it sits flush on the whiteboard face.
"""

import bpy, bmesh, math

# ─────────────────────────────────────────────────────────────────────────────
# CONFIG
# ─────────────────────────────────────────────────────────────────────────────
W        = 1.40   # chart total width  (m)
H        = 0.75   # chart total height (m)
DEPTH    = 0.008  # panel backing depth (m)
LABEL_W  = 0.20   # left label column width
TIME_H   = 0.10   # bottom time row height
LINE_T   = 0.004  # grid line thickness
BAR_MAR  = 0.009  # margin inside each process row bar

ROWS     = 4      # number of process rows  (P1–P4)
TIME_MAX = 32     # total time units shown on X axis

# Burst times per process:  P1=12, P2=9, P3=6, P4=4  → SJF correct = P4,P3,P2,P1
# Display order (rows top→bottom) is SHUFFLED to show the wrong state:
# Row 0 = P1(BT=12),  Row 1 = P3(BT=6),  Row 2 = P4(BT=4),  Row 3 = P2(BT=9)
DISPLAY_ROWS = [
    ("P1", 12, (0.40, 0.65, 1.00)),   # blue
    ("P3",  6, (0.45, 0.85, 0.50)),   # green
    ("P4",  4, (1.00, 0.80, 0.30)),   # yellow
    ("P2",  9, (1.00, 0.45, 0.45)),   # red
]

OUT_PATH = (
    r"C:\Users\gaymi\Documents\GitHub\Iterative-Descent"
    r"\Iterative Descent\Assets\Art\Environment\Map\Prop_GanttChart.fbx"
)

# ─────────────────────────────────────────────────────────────────────────────
# UTILITIES
# ─────────────────────────────────────────────────────────────────────────────

def purge_scene():
    """Remove all mesh/font objects from the current scene."""
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for block in list(bpy.data.meshes) + list(bpy.data.curves):
        bpy.data.meshes.remove(block) if isinstance(block, bpy.types.Mesh) else \
        bpy.data.curves.remove(block)


def make_material(name, rgb, roughness=0.85):
    """Create or reuse a Principled-BSDF material."""
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value  = roughness
    bsdf.inputs["Metallic"].default_value   = 0.0
    bsdf.inputs["Specular"].default_value   = 0.05
    return mat


def add_box(name, x, y, z, w, h, d, mat):
    """
    Create a rectangular box.
    Origin of coordinates = bottom-left-back corner.
    x,y,z = position of that corner; w,h,d = width, height, depth.
    """
    mesh = bpy.data.meshes.new(name)
    bm   = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    # Blender cube is centred at origin; shift so corner is at (x,y,z)
    obj.location = (x + w * 0.5, y + h * 0.5, z + d * 0.5)
    obj.scale    = (w, h, d)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, scale=True)
    obj.select_set(False)

    obj.data.materials.append(mat)
    return obj


def add_text_label(text, x, y, z, size=0.045, mat=None):
    """Add a 3D text object (will be converted to mesh on export)."""
    bpy.ops.object.text_add(location=(x, y, z))
    obj = bpy.context.active_object
    obj.data.body      = text
    obj.data.size      = size
    obj.data.extrude   = 0.0015
    obj.data.align_x   = 'CENTER'
    obj.data.align_y   = 'CENTER'
    if mat:
        obj.data.materials.append(mat)
    return obj


# ─────────────────────────────────────────────────────────────────────────────
# BUILD
# ─────────────────────────────────────────────────────────────────────────────

purge_scene()

# --- Materials ----------------------------------------------------------------
m_panel    = make_material("M_GC_Panel",   (0.94, 0.93, 0.90))
m_label_bg = make_material("M_GC_LabelBg",(0.80, 0.79, 0.76))
m_time_bg  = make_material("M_GC_TimeBg", (0.80, 0.79, 0.76))
m_grid     = make_material("M_GC_Grid",   (0.55, 0.55, 0.55))
m_text     = make_material("M_GC_Text",   (0.10, 0.10, 0.10))
m_title    = make_material("M_GC_Title",  (0.08, 0.08, 0.12))

proc_mats  = [
    make_material(f"M_GC_{row[0]}", row[2])
    for row in DISPLAY_ROWS
]

# --- Derived geometry values --------------------------------------------------
grid_x = LABEL_W               # where the grid starts (x)
grid_y = TIME_H                # where the grid starts (y)
grid_w = W - LABEL_W           # grid drawing area width
grid_h = H - TIME_H            # grid drawing area height
row_h  = grid_h / ROWS
col_w  = grid_w / (TIME_MAX / 4)   # one column = 4 time units

# --- 1. Backing panel --------------------------------------------------------
add_box("GC_Panel", 0, 0, 0, W, H, DEPTH, m_panel)

# --- 2. Label column (left side) ---------------------------------------------
add_box("GC_LabelBg", 0, grid_y, DEPTH, LABEL_W, grid_h, DEPTH * 0.4, m_label_bg)

# --- 3. Time row (bottom) ----------------------------------------------------
add_box("GC_TimeBg", grid_x, 0, DEPTH, grid_w, TIME_H, DEPTH * 0.4, m_time_bg)

# --- 4. Separator lines -------------------------------------------------------
# Vertical separator (label column | grid)
add_box("GC_Sep_V",  grid_x - LINE_T * 0.5, grid_y, DEPTH, LINE_T, grid_h, DEPTH * 0.5, m_grid)
# Horizontal separator (grid | time row)
add_box("GC_Sep_H",  grid_x, grid_y - LINE_T * 0.5, DEPTH, grid_w, LINE_T, DEPTH * 0.5, m_grid)

# Outer border top
add_box("GC_Border_Top",  0, H - LINE_T, 0, W, LINE_T, DEPTH, m_grid)
# Outer border left
add_box("GC_Border_Left", 0, 0, 0, LINE_T, H, DEPTH, m_grid)
# Outer border right
add_box("GC_Border_Right", W - LINE_T, 0, 0, LINE_T, H, DEPTH, m_grid)
# Outer border bottom
add_box("GC_Border_Bot", 0, 0, 0, W, LINE_T, DEPTH, m_grid)

# --- 5. Horizontal row dividers inside the grid ------------------------------
for i in range(1, ROWS):
    y = grid_y + i * row_h - LINE_T * 0.5
    add_box(f"GC_HLine_{i}", grid_x, y, DEPTH, grid_w, LINE_T, DEPTH * 0.5, m_grid)

# --- 6. Vertical column dividers (every 4 time units) ------------------------
num_cols = int(TIME_MAX / 4)
for i in range(1, num_cols):
    x = grid_x + i * col_w - LINE_T * 0.5
    add_box(f"GC_VLine_{i}", x, grid_y, DEPTH, LINE_T, grid_h, DEPTH * 0.5, m_grid)

# --- 7. Process bars (in shuffled / wrong display order) ---------------------
total_bt_sum = sum(row[1] for row in DISPLAY_ROWS)
time_scale   = grid_w / TIME_MAX   # pixels per time unit

cursor_x = grid_x
for row_idx, (pname, bt, col) in enumerate(DISPLAY_ROWS):
    bar_w  = bt * time_scale - BAR_MAR * 2
    bar_x  = cursor_x + BAR_MAR
    bar_y  = grid_y + row_idx * row_h + BAR_MAR
    bar_h  = row_h - BAR_MAR * 2
    bar_z  = DEPTH + 0.001

    add_box(f"GC_Bar_{pname}", bar_x, bar_y, bar_z, bar_w, bar_h, DEPTH * 0.35, proc_mats[row_idx])
    cursor_x += bt * time_scale

# --- 8. Row labels (P1–P4) in the label column -------------------------------
for row_idx, (pname, bt, col) in enumerate(DISPLAY_ROWS):
    cx = LABEL_W * 0.5
    cy = grid_y + (row_idx + 0.5) * row_h
    cz = DEPTH + 0.003
    add_text_label(pname, cx, cy, cz, size=0.050, mat=m_text)

# --- 9. Time-axis labels (0, 4, 8, … TIME_MAX) -------------------------------
for i in range(num_cols + 1):
    tx = grid_x + i * col_w
    ty = TIME_H * 0.5
    tz = DEPTH + 0.003
    add_text_label(str(i * 4), tx, ty, tz, size=0.030, mat=m_text)

# --- 10. Chart title ---------------------------------------------------------
add_text_label(
    "CPU SCHEDULING",
    W * 0.5, H - 0.04, DEPTH + 0.003,
    size=0.038, mat=m_title
)
add_text_label(
    "Shortest Job First (SJF)",
    W * 0.5, H - 0.085, DEPTH + 0.003,
    size=0.028, mat=m_title
)

# ─────────────────────────────────────────────────────────────────────────────
# CONVERT TEXT → MESH, JOIN ALL, RENAME
# ─────────────────────────────────────────────────────────────────────────────

# Convert all text/font objects to meshes
bpy.ops.object.select_all(action='DESELECT')
for obj in bpy.context.scene.objects:
    if obj.type == 'FONT':
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.convert(target='MESH')
        obj.select_set(False)

# Select all mesh objects and join into one
bpy.ops.object.select_all(action='DESELECT')
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for obj in meshes:
    obj.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.join()

final = bpy.context.active_object
final.name = "Prop_GanttChart"

# Apply all transforms (important for clean Unity import)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

print(f"[GanttChart] Built '{final.name}' — {len(final.data.vertices)} verts, "
      f"{len(final.data.materials)} materials")

# ─────────────────────────────────────────────────────────────────────────────
# EXPORT FBX
# ─────────────────────────────────────────────────────────────────────────────

bpy.ops.export_scene.fbx(
    filepath             = OUT_PATH,
    use_selection        = True,
    apply_scale_options  = 'FBX_SCALE_ALL',
    axis_forward         = '-Z',
    axis_up              = 'Y',
    object_types         = {'MESH'},
    mesh_smooth_type     = 'FACE',
    use_mesh_modifiers   = True,
    bake_anim            = False,
    path_mode            = 'AUTO',
)

print(f"[GanttChart] Exported → {OUT_PATH}")

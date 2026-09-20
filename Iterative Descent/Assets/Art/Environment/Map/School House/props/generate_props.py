"""
ARBITEX Prop Generator
Run this in Blender's Scripting tab (Text Editor > Run Script).
Generates AmmoBox.fbx and HealthKit.fbx in the same folder as this script.
Tested on Blender 3.x / 4.x
"""

import bpy
import bmesh
import math
import os

# Output folder = same directory as this script
OUTPUT_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    # Remove orphan meshes/materials
    for block in bpy.data.meshes:
        bpy.data.meshes.remove(block)
    for block in bpy.data.materials:
        bpy.data.materials.remove(block)


def make_material(name, r, g, b, roughness=0.8, metallic=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def add_box(name, size_x, size_y, size_z, location=(0, 0, 0)):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    # Scale to desired dimensions
    bmesh.ops.scale(bm, vec=(size_x, size_y, size_z), verts=bm.verts)
    bm.to_mesh(mesh)
    bm.free()
    obj.location = location
    return obj


def assign_material(obj, mat):
    if len(obj.data.materials) == 0:
        obj.data.materials.append(mat)
    else:
        obj.data.materials[0] = mat


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def export_fbx(filepath, objects):
    """Export only the given objects as a single FBX."""
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        path_mode='AUTO',
    )
    print(f"Exported: {filepath}")


# ===========================================================================
# AMMO BOX  (Resident Evil style bullet box)
# ===========================================================================
# Shape: short rectangular cardboard box, ~12x8x5 cm at game scale (0.12 x 0.08 x 0.05 m)
# Features: main body, lid strip on top, two small label panels on front/back

def build_ammo_box():
    clear_scene()

    # -- Materials --
    mat_body   = make_material("Ammo_Body",   0.36, 0.28, 0.18)   # dark tan/cardboard
    mat_label  = make_material("Ammo_Label",  0.80, 0.72, 0.50)   # pale yellow label
    mat_stripe = make_material("Ammo_Stripe", 0.18, 0.22, 0.14)   # dark olive stripe
    mat_text   = make_material("Ammo_Text",   0.10, 0.10, 0.10)   # black text panel

    # -- Main box body --
    body = add_box("AmmoBox_Body", 0.12, 0.08, 0.05, location=(0, 0, 0.025))
    assign_material(body, mat_body)

    # -- Lid rim strip (top band, slightly proud) --
    lid = add_box("AmmoBox_Lid", 0.122, 0.082, 0.006, location=(0, 0, 0.052))
    assign_material(lid, mat_stripe)

    # -- Front label panel --
    label_f = add_box("AmmoBox_LabelF", 0.08, 0.002, 0.028, location=(0, -0.041, 0.026))
    assign_material(label_f, mat_label)

    # -- Back label panel --
    label_b = add_box("AmmoBox_LabelB", 0.08, 0.002, 0.028, location=(0, 0.041, 0.026))
    assign_material(label_b, mat_label)

    # -- Small text strip on front label --
    text_f = add_box("AmmoBox_TextF", 0.055, 0.003, 0.010, location=(0, -0.042, 0.026))
    assign_material(text_f, mat_text)

    # -- Bottom skid feet (two thin blocks) --
    foot_l = add_box("AmmoBox_FootL", 0.10, 0.06, 0.004, location=(-0.01, 0, 0.002))
    assign_material(foot_l, mat_stripe)
    foot_r = add_box("AmmoBox_FootR", 0.10, 0.06, 0.004, location=(0.01, 0, 0.002))
    assign_material(foot_r, mat_stripe)

    all_parts = [body, lid, label_f, label_b, text_f, foot_l, foot_r]

    # Apply all transforms
    for obj in all_parts:
        select_only(obj)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Join into single mesh
    bpy.ops.object.select_all(action='DESELECT')
    for obj in all_parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()

    joined = bpy.context.active_object
    joined.name = "AmmoBox"

    return joined


# ===========================================================================
# HEALTH KIT  (Left 4 Dead style first aid kit)
# ===========================================================================
# Shape: larger flat rectangular case, ~22x14x7 cm (0.22 x 0.14 x 0.07 m)
# Features: white body, red cross on top, metal latch on front, corner guards

def build_health_kit():
    clear_scene()

    # -- Materials --
    mat_white   = make_material("HK_White",   0.88, 0.88, 0.85)   # off-white body
    mat_red     = make_material("HK_Red",     0.82, 0.08, 0.08)   # red cross
    mat_metal   = make_material("HK_Metal",   0.55, 0.55, 0.55, roughness=0.3, metallic=0.8)
    mat_dark    = make_material("HK_Dark",    0.15, 0.15, 0.15)   # dark corner guards
    mat_handle  = make_material("HK_Handle",  0.30, 0.30, 0.30)   # grey carry handle

    W, D, H = 0.22, 0.14, 0.07

    # -- Main body --
    body = add_box("HK_Body", W, D, H, location=(0, 0, H * 0.5))
    assign_material(body, mat_white)

    # -- Red cross on top (two overlapping bars) --
    cross_h = add_box("HK_CrossH", 0.10, 0.025, 0.003, location=(0, 0, H + 0.0015))
    assign_material(cross_h, mat_red)
    cross_v = add_box("HK_CrossV", 0.025, 0.10, 0.003, location=(0, 0, H + 0.0015))
    assign_material(cross_v, mat_red)

    # -- Front latch (metal clasp) --
    latch = add_box("HK_Latch", 0.04, 0.008, 0.018, location=(0, -(D * 0.5 + 0.004), H * 0.45))
    assign_material(latch, mat_metal)

    # -- Corner guards (4 vertical edge strips) --
    guard_offsets = [
        ( W * 0.5 - 0.005,  D * 0.5 - 0.005),
        (-W * 0.5 + 0.005,  D * 0.5 - 0.005),
        ( W * 0.5 - 0.005, -D * 0.5 + 0.005),
        (-W * 0.5 + 0.005, -D * 0.5 + 0.005),
    ]
    guards = []
    for i, (gx, gy) in enumerate(guard_offsets):
        g = add_box(f"HK_Guard{i}", 0.014, 0.014, H + 0.002, location=(gx, gy, H * 0.5))
        assign_material(g, mat_dark)
        guards.append(g)

    # -- Top carry handle --
    handle = add_box("HK_Handle", 0.06, 0.012, 0.008, location=(0, 0, H + 0.022))
    assign_material(handle, mat_handle)
    # Handle arch left post
    post_l = add_box("HK_PostL", 0.008, 0.010, 0.022, location=(-0.026, 0, H + 0.011))
    assign_material(post_l, mat_handle)
    # Handle arch right post
    post_r = add_box("HK_PostR", 0.008, 0.010, 0.022, location=( 0.026, 0, H + 0.011))
    assign_material(post_r, mat_handle)

    # -- Hinge strip on back --
    hinge = add_box("HK_Hinge", W * 0.7, 0.010, 0.010, location=(0, D * 0.5 + 0.005, H - 0.005))
    assign_material(hinge, mat_metal)

    all_parts = [body, cross_h, cross_v, latch, *guards, handle, post_l, post_r, hinge]

    # Apply all transforms
    for obj in all_parts:
        select_only(obj)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Join into single mesh
    bpy.ops.object.select_all(action='DESELECT')
    for obj in all_parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()

    joined = bpy.context.active_object
    joined.name = "HealthKit"

    return joined


# ===========================================================================
# MAIN
# ===========================================================================

print("=" * 60)
print("ARBITEX Prop Generator starting...")
print(f"Output folder: {OUTPUT_DIR}")
print("=" * 60)

# -- Ammo Box --
ammo = build_ammo_box()
ammo_path = os.path.join(OUTPUT_DIR, "AmmoBox.fbx")
export_fbx(ammo_path, [ammo])

# -- Health Kit --
kit = build_health_kit()
kit_path = os.path.join(OUTPUT_DIR, "HealthKit.fbx")
export_fbx(kit_path, [kit])

print("=" * 60)
print("Done! Both FBX files saved to:")
print(f"  {ammo_path}")
print(f"  {kit_path}")
print("=" * 60)

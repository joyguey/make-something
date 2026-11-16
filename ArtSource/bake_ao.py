"""
Bakes ambient occlusion into the furniture's and characters' meshes (V3 review: "Consider using Blender to bake
ambient occlusion on your objects individually for greater visuals"), then re-exports their FBX with the result in the
vertex colours: white RGB, occlusion in alpha (the shader RoomOrchestra/LitBakedAO darkens by it).

Each object is baked on its own: furniture parts with every other part hidden, and every character part alone too.
(The eyes and mouth used to shade the body where they sit; on a low-poly body that showed as dark smudges round the
face: owner, headset 2026-11-15, "a little bit weird of a shading there".) Furniture parts are stored shrunk to their part's scale (FurnitureModels), so they are scaled back to
real size while baking, for true occlusion distances.

  Blender -b ArtSource/Furniture/Furniture.blend -P ArtSource/bake_ao.py -- furniture
  Blender -b ArtSource/Characters/Flutter.blend -P ArtSource/bake_ao.py -- character Flutter
Run after the build scripts (build_furniture.py, build_characters.py), which write meshes without AO.
"""
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
mode = args[0] if args else "furniture"


def bake(obj, distance, visible):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 64
    scene.render.bake.target = "VERTEX_COLORS"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = distance
    for o in bpy.data.objects:
        o.hide_render = o not in visible
    mesh = obj.data
    if len(mesh.materials) == 0:
        mesh.materials.append(bpy.data.materials.new("Bake"))
    for name in ("AO", "Col"):
        if name in mesh.color_attributes:
            mesh.color_attributes.remove(mesh.color_attributes[name])
    ao = mesh.color_attributes.new("AO", "FLOAT_COLOR", "CORNER")
    mesh.color_attributes.active_color = ao
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="AO", target="VERTEX_COLORS")
    ao = mesh.color_attributes["AO"]
    values = [max(0.3, min(1.0, (d.color[0] + d.color[1] + d.color[2]) / 3)) for d in ao.data]
    mesh.color_attributes.remove(ao)
    col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for i, v in enumerate(values):
        col.data[i].color = (1.0, 1.0, 1.0, v)
    mesh.color_attributes.active_color = col
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    for o in bpy.data.objects:
        o.hide_render = False
    return sum(values) / max(1, len(values))


def export(path):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        colors_type="LINEAR", prioritize_active_color=True)


meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if mode == "furniture":
    sizes = {p["mesh"]: p for p in json.load(open(os.path.join(ROOT, "ArtSource", "Furniture", "part_sizes.json")))["parts"]}
    for obj in meshes:
        key = obj.name.replace("Furniture_", "")
        part = sizes.get(obj.name) or sizes.get(key)
        s = part["scale"] if part else [1, 1, 1]
        obj.scale = (s[0], s[2], s[1])  # Unity X/Y/Z scale -> Blender X/Z/Y
        bpy.context.view_layer.update()
        mean = bake(obj, 0.12, {obj})
        obj.scale = (1, 1, 1)
        print(f"[ao] {obj.name}: mean occlusion {mean:.2f}")
    export(os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Furniture", "Furniture.fbx"))
else:
    name = args[1]
    for obj in meshes:
        family = {obj}  # alone: no part shades another (no smudges round the eyes and mouth)
        if "Outline" in obj.name:
            # an inside-out shell drawn as the dark rim: nothing to occlude, so it is plain white (no darkening)
            mesh = obj.data
            for n in ("AO", "Col"):
                if n in mesh.color_attributes:
                    mesh.color_attributes.remove(mesh.color_attributes[n])
            col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
            for d in col.data:
                d.color = (1.0, 1.0, 1.0, 1.0)
            mesh.color_attributes.active_color = col
            mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
            print(f"[ao] {obj.name}: rim, none")
            continue
        mean = bake(obj, 0.15, family)
        print(f"[ao] {obj.name}: mean occlusion {mean:.2f}")
    export(os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Characters", f"{name}.fbx"))
bpy.ops.wm.save_mainfile()
print(f"[ao] done: {mode}")

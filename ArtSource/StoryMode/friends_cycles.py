"""Shared helper: bring the REAL Flutter/Bop/Boom into a Cycles scene with their faces showing.

The characters' black outline is an inverted hull (drawn back faces only, as in Unity). Cycles has no backface
culling, so the hull would hide the face: its material here is see-through on the side facing the camera.
"""
import math
import bpy
from mathutils import Vector

CHARS = "/Users/joy/Desktop/PICO Project/ArtSource/Characters"


def hull_material(src):
    m = bpy.data.materials.new(f"{src.name if src else 'Outline'}_CyclesHull")
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dark = nt.nodes.new("ShaderNodeEmission")
    dark.inputs["Color"].default_value = tuple(src.diffuse_color) if src else (0.03, 0.02, 0.05, 1)  # the authored rim colour
    dark.inputs["Strength"].default_value = 1.0
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    # inverted hull: its faces toward the camera are back faces of the shell -> see-through; the rim beyond the body
    # shows the shell's far side, front-facing -> dark outline
    nt.links.new(geo.outputs["Backfacing"], mix.inputs["Fac"])
    nt.links.new(dark.outputs["Emission"], mix.inputs[1])
    nt.links.new(clear.outputs["BSDF"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    return m


def shaded(src):
    """The authored flat colour as a Cycles shader: a soft, slightly glossy jelly (bodies) or matte (face parts)."""
    if src is None or src.use_nodes:
        return src
    key = f"{src.name}_Cycles"
    if key in bpy.data.materials:
        return bpy.data.materials[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    c = tuple(src.diffuse_color)
    b.inputs["Base Color"].default_value = c
    body = "Body" in src.name
    b.inputs["Roughness"].default_value = 0.32 if body else 0.5
    if body:
        b.inputs["Subsurface Weight"].default_value = 0.25
        b.inputs["Subsurface Radius"].default_value = (c[0] * 0.08 + 0.01, c[1] * 0.08 + 0.01, c[2] * 0.08 + 0.01)
        b.inputs["Coat Weight"].default_value = 0.35
    if "Shine" in src.name:
        b.inputs["Emission Color"].default_value = (1, 1, 1, 1)
        b.inputs["Emission Strength"].default_value = 1.5
    return m


def friend(scene, name, loc, height, look_at=None, turn=0.0):
    """Appends the friend, sizes it to `height` (m), stands it at `loc`, its face toward `look_at` (x, y)."""
    with bpy.data.libraries.load(f"{CHARS}/{name}.blend") as (src, dst):
        dst.objects = [n for n in src.objects if not n.split("_", 1)[1].startswith("Mini") and not n.endswith("Mouth_O")]
    holder = bpy.data.objects.new(f"{name}Holder", None)
    scene.collection.objects.link(holder)
    for o in dst.objects:
        scene.collection.objects.link(o)
        if o.name.endswith("_Outline"):
            for i, slot in enumerate(o.material_slots):
                slot.material = hull_material(slot.material)
            if not o.material_slots:
                o.data.materials.append(hull_material(None))
        else:
            for slot in o.material_slots:
                slot.material = shaded(slot.material)
    bpy.context.view_layer.update()

    def bounds(objs):
        lo = Vector((1e9, 1e9, 1e9)); hi = -lo
        for o in objs:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        return lo, hi

    def mid(o):
        lo, hi = bounds([o]); return (lo + hi) / 2

    lo, hi = bounds(dst.objects)
    body = next(o for o in dst.objects if o.name.endswith("_Body"))
    eyes = [o for o in dst.objects if o.name.endswith("_EyeL") or o.name.endswith("_EyeR")]
    face = sum((mid(o) for o in eyes), Vector()) / max(1, len(eyes)) - mid(body)
    facing = math.atan2(face.y, face.x)
    foot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for o in dst.objects:
        if o.parent is None:
            o.parent = holder
            o.location -= foot
    k = height / max(hi.z - lo.z, 1e-6)
    holder.scale = (k, k, k)
    holder.location = Vector(loc)
    want = math.atan2(look_at[1] - loc[1], look_at[0] - loc[0]) if look_at else facing
    holder.rotation_euler = (0, 0, want - facing + turn)
    return holder

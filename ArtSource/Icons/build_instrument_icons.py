"""
A small 3D icon for each instrument (owner, 2026-11-11: "a small icon created depicting each instrument just in case
there are names people have never heard of before"). Original low-poly models, about 10 cm across in the game, each
showing the instrument's one telling feature: the udu's side hole, the mbira's tines, the erhu's bow, the ocarina's
finger holes, the handpan's dome and dimple, the guzheng's bridges, the balafon's bars over gourds, the slit drum's
H-shaped slit. Colours in the vertex colours, ambient occlusion baked per icon into their alpha (RoomOrchestra/LitBakedAO).

  /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Icons/build_instrument_icons.py
Writes Assets/RoomOrchestra/Art/Icons/InstrumentIcons.fbx and ArtSource/Icons/InstrumentIcons.blend.
Each icon is modelled around its own centre, about 0.1 m across, facing Blender -Y (Unity -Z, toward the player).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Icons", "InstrumentIcons.fbx")
sys.path.insert(0, os.path.join(ROOT, "ArtSource", "Meadow"))

CLAY = (0.80, 0.45, 0.28)
CLAY_DARK = (0.55, 0.28, 0.18)
WOOD = (0.66, 0.44, 0.26)
WOOD_DARK = (0.42, 0.26, 0.15)
WOOD_RED = (0.62, 0.22, 0.16)
METAL = (0.82, 0.84, 0.88)
STEEL = (0.55, 0.62, 0.70)
SKIN = (0.95, 0.90, 0.78)
BLUE = (0.36, 0.62, 0.86)
GOURD = (0.90, 0.72, 0.36)
STRING = (0.98, 0.96, 0.88)


class Icon:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.colours = {}

    def _paint(self, verts, rgb):
        for f in {f for v in verts for f in v.link_faces}:
            self.colours[f] = rgb

    def sphere(self, centre, radius, rgb, scale=(1, 1, 1), seg=(16, 10)):
        g = bmesh.ops.create_uvsphere(self.bm, u_segments=seg[0], v_segments=seg[1], radius=radius)
        for v in g["verts"]:
            v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(centre)
        self._paint(g["verts"], rgb)
        return g["verts"]

    def cylinder(self, start, end, radius, rgb, segments=10, radius2=None):
        start, end = Vector(start), Vector(end)
        axis = end - start
        g = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segments, radius1=radius,
                                  radius2=radius if radius2 is None else radius2, depth=axis.length)
        rot = axis.to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((start + end) / 2) @ rot, verts=g["verts"])
        self._paint(g["verts"], rgb)
        return g["verts"]

    def box(self, centre, size, rgb, bevel=0.0):
        g = bmesh.ops.create_cube(self.bm, size=1.0)
        for v in g["verts"]:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(centre)
        if bevel > 0:
            edges = list({e for v in g["verts"] for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=2, affect="EDGES")
        faces = {f for v in g["verts"] if v.is_valid for f in v.link_faces}
        for f in faces:
            self.colours[f] = rgb

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        bm = self.bm
        bm.faces.ensure_lookup_table()
        colours = [self.colours.get(f, (1, 1, 1)) for f in bm.faces]
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for poly in mesh.polygons:
            rgb = colours[poly.index]
            poly.use_smooth = True
            for li in poly.loop_indices:
                col.data[li].color = (rgb[0], rgb[1], rgb[2], 1.0)
        return obj


def udu():
    i = Icon("Icon_Udu")
    i.sphere((0, 0, 0), 0.045, CLAY, scale=(1, 1, 0.9), seg=(20, 12))
    i.cylinder((0, 0, 0.035), (0, 0, 0.06), 0.014, CLAY, radius2=0.018)
    i.cylinder((0, 0, 0.058), (0, 0, 0.064), 0.02, CLAY_DARK)         # the rim
    i.sphere((0, -0.043, 0.005), 0.012, CLAY_DARK, scale=(1, 0.4, 1))  # the side hole, toward the player
    return i.finish()


def mbira():
    i = Icon("Icon_Mbira")
    i.box((0, 0, 0), (0.08, 0.016, 0.1), WOOD)
    for k in range(7):
        x = -0.03 + k * 0.01
        length = 0.06 - abs(k - 3) * 0.006
        i.box((x, -0.012, 0.012 - (0.06 - length) / 2), (0.005, 0.004, length), METAL)
    i.box((0, -0.014, 0.03), (0.075, 0.006, 0.006), WOOD_DARK)  # the bridge bar
    return i.finish()


def erhu():
    i = Icon("Icon_Erhu")
    i.cylinder((0, 0, -0.05), (0, 0, -0.025), 0.018, WOOD_RED, segments=6)    # the hexagonal drum
    i.cylinder((0, -0.0125, -0.0375), (0, -0.0135, -0.0375), 0.016, SKIN, segments=6)
    i.cylinder((0, 0, -0.025), (0, 0, 0.055), 0.004, WOOD_DARK)               # the neck
    i.cylinder((-0.006, 0, 0.042), (0.006, 0, 0.042), 0.003, WOOD_DARK)        # tuning pegs
    i.cylinder((-0.006, 0, 0.032), (0.006, 0, 0.032), 0.003, WOOD_DARK)
    i.cylinder((0.002, -0.006, -0.03), (0.002, -0.006, 0.045), 0.0007, STRING, segments=4)
    i.cylinder((-0.04, -0.012, -0.045), (0.035, -0.012, -0.012), 0.0022, WOOD, segments=6)  # the bow
    return i.finish()


def ocarina():
    i = Icon("Icon_Ocarina")
    i.sphere((0, 0, 0), 0.04, BLUE, scale=(1.3, 0.75, 0.8), seg=(20, 12))
    i.cylinder((0.045, 0, 0.008), (0.07, 0, 0.012), 0.009, BLUE, radius2=0.007)  # the mouthpiece
    for x, z in [(-0.03, 0.012), (-0.015, 0.02), (0.0, 0.022), (0.015, 0.02), (-0.022, -0.006), (0.008, -0.004)]:
        i.sphere((x, -0.028, z), 0.0045, CLAY_DARK, scale=(1, 0.5, 1), seg=(8, 6))  # finger holes
    return i.finish()


def handpan():
    i = Icon("Icon_Handpan")
    i.sphere((0, 0, 0.006), 0.05, STEEL, scale=(1, 1, 0.38), seg=(24, 12))
    i.sphere((0, 0, -0.006), 0.05, STEEL, scale=(1, 1, 0.38), seg=(24, 12))
    i.sphere((0, 0, 0.022), 0.012, METAL, scale=(1, 1, 0.5), seg=(12, 8))  # the central "ding"
    for k in range(7):
        a = k / 7 * math.tau
        i.sphere((math.cos(a) * 0.031, math.sin(a) * 0.031, 0.017), 0.008, METAL, scale=(1, 1, 0.35), seg=(10, 6))
    return i.finish()


def guzheng():
    i = Icon("Icon_Guzheng")
    for k in range(9):  # a gently arched board
        x = -0.05 + k * 0.0125
        i.box((x, 0, 0.006 * math.cos((k - 4) / 4 * 1.2)), (0.0128, 0.04, 0.008), WOOD)
    for k in range(5):
        x = -0.03 + k * 0.015
        i.box((x, -0.004 + (k % 2) * 0.008, 0.014), (0.004, 0.004, 0.008), STRING)  # the bridges
    for s in range(5):
        y = -0.014 + s * 0.007
        i.cylinder((-0.052, y, 0.012), (0.052, y, 0.012), 0.0006, STRING, segments=4)
    return i.finish()


def balafon():
    i = Icon("Icon_Balafon")
    i.box((0, 0, 0.02), (0.1, 0.006, 0.004), WOOD_DARK)  # the frame rails
    i.box((0, 0.03, 0.02), (0.1, 0.006, 0.004), WOOD_DARK)
    for k in range(6):
        x = -0.042 + k * 0.0168
        length = 0.045 - k * 0.003
        i.box((x, 0.015, 0.026), (0.013, length, 0.006), WOOD_RED if k % 2 else WOOD)
        i.sphere((x, 0.015, 0.006), 0.008 - k * 0.0005, GOURD, seg=(10, 8))  # gourds under the bars
    return i.finish()


def slit_drum():
    i = Icon("Icon_SlitDrum")
    i.cylinder((-0.05, 0, 0), (0.05, 0, 0), 0.026, WOOD, segments=12)
    i.cylinder((-0.051, 0, 0), (-0.05, 0, 0), 0.022, WOOD_DARK, segments=12)
    i.cylinder((0.05, 0, 0), (0.051, 0, 0), 0.022, WOOD_DARK, segments=12)
    i.box((0, 0, 0.026), (0.06, 0.004, 0.002), WOOD_DARK)       # the H-shaped slit on top
    i.box((-0.03, 0, 0.026), (0.004, 0.02, 0.002), WOOD_DARK)
    i.box((0.03, 0, 0.026), (0.004, 0.02, 0.002), WOOD_DARK)
    i.cylinder((0.01, -0.03, 0.035), (0.03, -0.01, 0.06), 0.002, WOOD_DARK, segments=6)  # a beater
    i.sphere((0.01, -0.03, 0.035), 0.006, CLAY, seg=(8, 6))
    return i.finish()


def bake(obj):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.render.bake.target = "VERTEX_COLORS"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = 0.02
    for o in bpy.data.objects:
        o.hide_render = o is not obj
    mesh = obj.data
    mesh.materials.append(bpy.data.materials.new("Bake"))
    ao = mesh.color_attributes.new("AO", "FLOAT_COLOR", "CORNER")
    mesh.color_attributes.active_color = ao
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="AO", target="VERTEX_COLORS")
    col = mesh.color_attributes["Col"]
    ao = mesh.color_attributes["AO"]
    for k in range(len(col.data)):
        c, a = col.data[k].color, ao.data[k].color
        col.data[k].color = (c[0], c[1], c[2], max(0.35, min(1.0, (a[0] + a[1] + a[2]) / 3)))
    mesh.color_attributes.remove(ao)
    mesh.color_attributes.active_color = mesh.color_attributes["Col"]
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    for o in bpy.data.objects:
        o.hide_render = False


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    icons = [udu(), mbira(), erhu(), ocarina(), handpan(), guzheng(), balafon(), slit_drum()]
    for k, obj in enumerate(icons):
        obj.location = (k * 2.0, 0, 0)
    bpy.context.view_layer.update()
    for obj in icons:
        bake(obj)
        print(f"[icons] AO baked: {obj.name} ({len(obj.data.polygons)} faces)")
    for obj in icons:
        obj.location = (0, 0, 0)
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "InstrumentIcons.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        colors_type="LINEAR", prioritize_active_color=True)
    print(f"[icons] {len(icons)} icons -> {EXPORT}")


main()

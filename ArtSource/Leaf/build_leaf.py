"""
The leaf palette, modelled (owner, 2026-11-19: "I really love that direction", Astro Bot as a style guide): a real leaf
with a pointed tip, a rim that curls up gently, a raised midrib and five pairs of side veins, darker towards the stalk,
and a curved stalk with a flared foot. The top stays flat where the buttons and the friends stand.

  Leaf   in metres around the leaf's centre (Unity LittleEchoesSceneSetup.LeafCenter): its flat top 0.015 m above it,
         exactly where the old 3 cm leaf's top was
  Stalk  in a unit box (-0.5..0.5), as the cube it replaces: LeafStalk scales it to (width, height, width)

Colours live in the vertex colours (sRGB, RGB; alpha 1 = no occlusion): the shader RoomOrchestra/LitBakedAO multiplies
them into a white material.
  /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Leaf/build_leaf.py
Writes Assets/RoomOrchestra/Art/Leaf/LeafPad.fbx and ArtSource/Leaf/LeafPad.blend.
Blender X/Y/Z = Unity X/Z/Y.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
EXPORT = os.path.join(ROOT, "..", "Assets", "RoomOrchestra", "Art", "Leaf", "LeafPad.fbx")

LEAF_L, LEAF_W, LEAF_T = 1.02, 0.42, 0.03          # LittleEchoesSceneSetup.LeafSize (x, z, y)
RIB_Y = 1.19 - 1.17                                 # the midrib runs between the friends and the button row
HALF_LEN = LEAF_L / 2 + 0.06                         # the tip runs a little past the old ellipse
BASE, TIP, VEIN, UNDER = "#62B852", "#86D068", "#B4E596", "#3F7F35"


def rgb(code):
    code = code.lstrip("#")
    return Vector([int(code[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def outline(u):
    """Half-width (0..1) at u in [-1 (stalk end), 1 (tip)]."""
    s = (u + 1) / 2
    w = math.sin(math.pi * s ** 0.85) ** 0.62
    if s > 0.8:
        w *= 1 - 0.25 * ((s - 0.8) / 0.2) ** 2
    return w


def lift(x, y):
    """How far the top rises above flat at (x, y): the rim curls up, the middle stays flat."""
    u = max(-1.0, min(1.0, x / HALF_LEN))
    hw = max(outline(u) * LEAF_W / 2, 1e-4)
    r = min(1.0, abs(y - RIB_Y) / hw)
    return 0.022 * max(0.0, (r - 0.72) / 0.28) ** 2 + 0.03 * max(0.0, (abs(u) - 0.8) / 0.2) ** 2


def paint(face, layer, colour):
    for loop in face.loops:
        loop[layer] = (colour.x, colour.y, colour.z, 1.0)


def leaf():
    bm = bmesh.new()
    col = bm.loops.layers.float_color.new("Col")
    nu, nv = 90, 26
    top, bottom = [], []
    for i in range(nu + 1):
        u = -1 + 2 * i / nu
        x = u * HALF_LEN
        hw = outline(u) * LEAF_W / 2
        t_row, b_row = [], []
        for j in range(nv + 1):
            v = -1 + 2 * j / nv
            y = RIB_Y + v * hw
            z = LEAF_T / 2 + lift(x, y)
            t_row.append((bm.verts.new((x, y, z)), abs(v), u))
            b_row.append(bm.verts.new((x, y, z - LEAF_T * 0.75 - 0.008 * (1 - abs(v)))))
        top.append(t_row)
        bottom.append(b_row)
    cb, ct = rgb(BASE), rgb(TIP)
    for i in range(nu):
        for j in range(nv):
            q = [top[i][j], top[i + 1][j], top[i + 1][j + 1], top[i][j + 1]]
            f = bm.faces.new([p[0] for p in q])
            for loop, p in zip(f.loops, q):
                c = cb.lerp(ct, (p[2] + 1) / 2) * (1 + 0.12 * max(0.0, p[1] - 0.85) / 0.15)  # a lighter rim
                loop[col] = (min(1, c.x), min(1, c.y), min(1, c.z), 1.0)
            paint(bm.faces.new([bottom[i][j + 1], bottom[i + 1][j + 1], bottom[i + 1][j], bottom[i][j]]), col, rgb(UNDER))
    side = cb * 0.8
    for i in range(nu):  # the rim, both long edges
        paint(bm.faces.new([top[i][0][0], bottom[i][0], bottom[i + 1][0], top[i + 1][0][0]]), col, side)
        paint(bm.faces.new([top[i][nv][0], top[i + 1][nv][0], bottom[i + 1][nv], bottom[i][nv]]), col, side)
    for j in range(nv):  # and the two ends
        paint(bm.faces.new([top[0][j][0], top[0][j + 1][0], bottom[0][j + 1], bottom[0][j]]), col, side)
        paint(bm.faces.new([top[nu][j][0], bottom[nu][j], bottom[nu][j + 1], top[nu][j + 1][0]]), col, side)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)

    def strip(points, w0, w1, height):
        """A raised vein lying on the top: a low rounded ridge, tapering."""
        prev = None
        for k, (x, y) in enumerate(points):
            t = k / (len(points) - 1)
            w = w0 + (w1 - w0) * t
            if k < len(points) - 1:
                d = Vector((points[k + 1][0] - x, points[k + 1][1] - y, 0)).normalized()
            s = Vector((-d.y, d.x, 0)) * (w / 2)
            c = Vector((x, y, LEAF_T / 2 + lift(x, y)))
            ring = [bm.verts.new(c - s - Vector((0, 0, 0.001))), bm.verts.new(c - s * 0.5 + Vector((0, 0, height * 0.8))),
                    bm.verts.new(c + Vector((0, 0, height))), bm.verts.new(c + s * 0.5 + Vector((0, 0, height * 0.8))),
                    bm.verts.new(c + s - Vector((0, 0, 0.001)))]
            if prev:
                for n in range(4):
                    paint(bm.faces.new((prev[n], prev[n + 1], ring[n + 1], ring[n])), col, rgb(VEIN))
            prev = ring

    tip = HALF_LEN * 0.98
    strip([(-tip + 2 * tip * k / 60, RIB_Y) for k in range(61)], 0.017, 0.004, 0.003)  # the midrib
    for u0 in (-0.66, -0.38, -0.1, 0.18, 0.46):
        for sgn in (-1, 1):
            pts = []
            for k in range(25):
                t = 0.02 + 0.8 * k / 24
                u = u0 + 0.34 * t ** 1.25
                pts.append((u * HALF_LEN, RIB_Y + sgn * t * outline(u) * LEAF_W / 2))
            strip(pts, 0.009, 0.003, 0.0018)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish("Leaf", bm, smooth=True)


def stalk():
    """A tapered stalk in a unit box: leans and bends a little on its way up, flares at its foot and into the leaf."""
    bm = bmesh.new()
    col = bm.loops.layers.float_color.new("Col")
    rings, sides = 24, 14
    prev = None
    first = last = None
    for i in range(rings + 1):
        t = i / rings
        p = Vector((0.16 * math.sin(math.pi * t) - 0.05 * t, -0.08 * math.sin(math.pi * t * 0.9), t - 0.5))
        r = 0.2 * (1 - 0.3 * t) + 0.18 * max(0.0, 0.12 - t) / 0.12 + 0.1 * max(0.0, t - 0.88) / 0.12
        ring = [bm.verts.new(p + Vector((math.cos(2 * math.pi * k / sides) * r, math.sin(2 * math.pi * k / sides) * r, 0))) for k in range(sides)]
        if prev:
            for k in range(sides):
                f = bm.faces.new((prev[k], prev[(k + 1) % sides], ring[(k + 1) % sides], ring[k]))
                paint(f, col, rgb("#4A8F3A").lerp(rgb("#5FAA4A"), t))
        else:
            first = ring
        prev = ring
        last = ring
    paint(bm.faces.new(list(reversed(first))), col, rgb("#3F7F35"))
    paint(bm.faces.new(last), col, rgb("#5FAA4A"))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish("Stalk", bm, smooth=True)


def finish(name, bm, smooth):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = smooth
    mesh.color_attributes.active_color = mesh.color_attributes["Col"]
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    print(f"[leaf] {name}: {len(mesh.polygons)} faces")
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    leaf()
    stalk()
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "LeafPad.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        colors_type="LINEAR", prioritize_active_color=True)
    print(f"[leaf] -> {os.path.abspath(EXPORT)}")


main()

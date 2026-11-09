"""
More detailed furniture for Little Echoes (Version 1 feedback: "more detailed 3D models"). Original low-poly models,
built at each part's real size (ArtSource/Furniture/part_sizes.json, written by FurnitureModels.DumpPartSizes):

  table   a top of five bevelled planks with grooves between them and an apron underneath; tapered square legs with feet
  chair   a seat board with a soft cushion; a back of two stiles, a curved top rail, a bottom rail and three slats;
          tapered round legs
  lamp    a stepped, bevelled base; a slim pole with two collars (the drum shade is built in Unity: RoundedShapes)

Each part is modelled in metres around its pivot (the primitive's centre), then divided by the part's scale so the mesh
lives in the part's own local space: it replaces the cube or cylinder exactly in Unity (FurnitureModels.Apply).
Blender X/Y/Z = Unity X/Z/Y; every model is symmetric in X and Y, so the axis handedness doesn't matter.

  /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Furniture/build_furniture.py
Writes Assets/RoomOrchestra/Art/Furniture/Furniture.fbx and ArtSource/Furniture/Furniture.blend.
"""
import json
import math
import os

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Furniture", "Furniture.fbx")
SIZES = {p["mesh"]: p for p in json.load(open(os.path.join(HERE, "part_sizes.json")))["parts"]}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def box(bm, cx, cy, cz, sx, sy, sz, bevel=0.0, segments=2):
    """An axis-aligned box (centre, size in metres), optionally bevelled, added to bmesh bm."""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    for v in tmp.verts:
        v.co.x = cx + v.co.x * sx
        v.co.y = cy + v.co.y * sy
        v.co.z = cz + v.co.z * sz
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=min(bevel, min(sx, sy, sz) * 0.45), segments=segments,
                        profile=0.5, affect="EDGES")
    merge(bm, tmp)


def frustum(bm, cz0, cz1, w0, w1, sides=4, round_=False, bevel=0.0):
    """A tapered post from height cz0 (width w0) to cz1 (width w1): square (sides=4) or round."""
    tmp = bmesh.new()
    ring0, ring1 = [], []
    for i in range(sides):
        a = 2 * math.pi * (i + 0.5) / sides if not round_ else 2 * math.pi * i / sides
        r0 = w0 / 2 * (math.sqrt(2) if sides == 4 and not round_ else 1)
        r1 = w1 / 2 * (math.sqrt(2) if sides == 4 and not round_ else 1)
        ring0.append(tmp.verts.new((math.cos(a) * r0, math.sin(a) * r0, cz0)))
        ring1.append(tmp.verts.new((math.cos(a) * r1, math.sin(a) * r1, cz1)))
    for i in range(sides):
        j = (i + 1) % sides
        tmp.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
    tmp.faces.new(list(reversed(ring0)))
    tmp.faces.new(ring1)
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces)
    if bevel > 0:
        bmesh.ops.bevel(tmp, geom=list(tmp.edges), offset=bevel, segments=2, profile=0.5, affect="EDGES")
    merge(bm, tmp)


def disc(bm, cz, h, d, bevel=0.0, sides=40):
    """A round disc (a cylinder) centred at height cz."""
    tmp = bmesh.new()
    bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=sides, radius1=d / 2, radius2=d / 2, depth=h)
    for v in tmp.verts:
        v.co.z += cz
    if bevel > 0:
        rims = [e for e in tmp.edges if len(e.link_faces) == 2 and abs(e.link_faces[0].normal.z - e.link_faces[1].normal.z) > 0.5]
        bmesh.ops.bevel(tmp, geom=rims, offset=bevel, segments=3, profile=0.5, affect="EDGES")
    merge(bm, tmp)


def merge(bm, tmp):
    tmp.verts.ensure_lookup_table()
    mapping = {v: bm.verts.new(v.co) for v in tmp.verts}
    for f in tmp.faces:
        try:
            bm.faces.new([mapping[v] for v in f.verts])
        except ValueError:
            pass
    tmp.free()


def finish(name, bm):
    """Into the part's local space (divide by its scale: Unity X, Y(up), Z = Blender X, Z, Y) and into the scene."""
    s = SIZES[name]["scale"]
    for v in bm.verts:
        v.co.x /= s[0]
        v.co.y /= s[2]
        v.co.z /= s[1]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = False
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    print(f"[furniture] {name}: {len(mesh.polygons)} faces")


def size(name):
    w, h, d = SIZES[name]["size"]  # Unity x, y (up), z
    return w, d, h  # Blender x, y, z


def table_top():
    w, d, h = size("Furniture_TableTop")
    bm = bmesh.new()
    planks, groove = 5, 0.004
    pw = (w - groove * (planks - 1)) / planks
    for i in range(planks):  # planks run front to back, grooves between them
        cx = -w / 2 + pw / 2 + i * (pw + groove)
        box(bm, cx, 0, 0, pw, d, h, bevel=0.008)
    # An apron (skirt) under the top, set in from the edges; it hangs below the top's own box.
    apron_h, inset, t = 0.075, 0.06, 0.022
    z = -h / 2 - apron_h / 2
    box(bm, 0, d / 2 - inset, z, w - 2 * inset, t, apron_h, bevel=0.004)
    box(bm, 0, -(d / 2 - inset), z, w - 2 * inset, t, apron_h, bevel=0.004)
    box(bm, w / 2 - inset, 0, z, t, d - 2 * inset, apron_h, bevel=0.004)
    box(bm, -(w / 2 - inset), 0, z, t, d - 2 * inset, apron_h, bevel=0.004)
    finish("Furniture_TableTop", bm)


def table_leg():
    w, d, h = size("Furniture_TableLeg")
    bm = bmesh.new()
    foot = 0.025
    frustum(bm, -h / 2 + foot, h / 2, w * 0.68, w, sides=4, bevel=0.006)  # tapers toward the floor
    box(bm, 0, 0, -h / 2 + foot / 2, w * 0.74, w * 0.74, foot, bevel=0.006)  # a small foot
    finish("Furniture_TableLeg", bm)


def chair_seat():
    w, d, h = size("Furniture_ChairSeat")
    bm = bmesh.new()
    board = h * 0.55
    box(bm, 0, 0, -h / 2 + board / 2, w, d, board, bevel=0.008)
    cushion = h - board + 0.02  # a little proud of the seat's box: soft
    box(bm, 0, 0.01, -h / 2 + board + cushion / 2 - 0.005, w - 0.05, d - 0.07, cushion, bevel=0.018, segments=4)
    finish("Furniture_ChairSeat", bm)


def chair_back():
    w, d, h = size("Furniture_ChairBack")
    bm = bmesh.new()
    stile = 0.05
    for sx in (-1, 1):  # the two uprights
        box(bm, sx * (w / 2 - stile / 2), 0, 0, stile, d, h, bevel=0.008)
    top = 0.11
    # A curved top rail: three boxes stepping up in the middle.
    box(bm, 0, 0, h / 2 - top / 2, w - 2 * stile, d * 0.9, top, bevel=0.012)
    box(bm, 0, 0, h / 2 - top / 2 + 0.012, w * 0.45, d * 0.9, top, bevel=0.014)
    bottom = 0.05
    box(bm, 0, 0, -h / 2 + bottom / 2 + 0.04, w - 2 * stile, d * 0.8, bottom, bevel=0.006)
    span_lo, span_hi = -h / 2 + 0.04 + bottom, h / 2 - top
    for i in range(3):  # three slats
        cx = (i - 1) * (w - 2 * stile) / 3.2
        box(bm, cx, 0, (span_lo + span_hi) / 2, 0.055, d * 0.6, span_hi - span_lo, bevel=0.006)
    finish("Furniture_ChairBack", bm)


def chair_leg():
    w, d, h = size("Furniture_ChairLeg")
    bm = bmesh.new()
    frustum(bm, -h / 2, h / 2, w * 0.7, w, sides=12, round_=True)
    finish("Furniture_ChairLeg", bm)


def lamp_base():
    w, d, h = size("Furniture_LampBase")
    bm = bmesh.new()
    disc(bm, -h / 2 + h * 0.35, h * 0.7, w, bevel=0.012)            # the wide, heavy plate
    disc(bm, -h / 2 + h * 0.7 + h * 0.25, h * 0.5, w * 0.42, bevel=0.008)  # a raised step under the pole
    finish("Furniture_LampBase", bm)


def lamp_pole():
    w, d, h = size("Furniture_LampPole")
    bm = bmesh.new()
    pole = w * 0.62
    disc(bm, 0, h, pole, sides=20)
    for z in (-h / 2 + 0.06, h / 2 - 0.05):  # collars near each end
        disc(bm, z, 0.035, w, bevel=0.006, sides=24)
    finish("Furniture_LampPole", bm)


def main():
    reset()
    table_top()
    table_leg()
    chair_seat()
    chair_back()
    chair_leg()
    lamp_base()
    lamp_pole()
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Furniture.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    print(f"[furniture] -> {EXPORT}")


main()

"""
Little Echoes characters (Flutter, Bop, Boom) built procedurally in Blender from the owner-approved
reference docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png. No downloaded assets.

Each character is a set of separate low-poly meshes so Unity can colour them with project
materials and move face parts for expressions (no blend shapes, no runtime objects):
  <Name>_Body       soft blob, the silhouette (Flutter tall droplet, Bop round, Boom wide dumpling)
  <Name>_Outline    inverted hull a little larger than the body: the reference's darker rim
  <Name>_EyeL/R     dark oval eyes
  <Name>_Smile      curved smile
  <Name>_Mouth_O    small round "oh" mouth for surprise (Unity parks it inside the body until used)
  <Name>_Highlight  soft shine, upper left
  <Name>_Motif      Flutter sparkle / Bop dot / Boom ripple, floating at the upper left (shape ID beyond colour)

Units: a round Bop is 1.0 wide; Unity scales the whole character by the orb diameter.
Axes: modelled with the face toward Blender -Y, Z up; exported so the face points to Unity -Z (toward the player), Y up.

Run headless:
  Blender --background --factory-startup --python ArtSource/Characters/build_characters.py -- Flutter [Bop Boom]
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT_DIR = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Characters")

# Proportions follow OrbCatalog (width, height); colours are only for the Blender preview.
CHARACTERS = {
    "Flutter": dict(width=0.85, height=1.25, top_taper=0.10, lean=-0.035, bottom_flat=0.38, motif="sparkle",
                    body=(0.60, 0.89, 0.89), face=(0.13, 0.27, 0.28), rim=(0.29, 0.60, 0.64)),
    "Bop": dict(width=1.0, height=1.0, top_taper=0.0, lean=0.0, bottom_flat=0.18, motif="dot",
                body=(0.77, 0.60, 0.94), face=(0.29, 0.18, 0.42), rim=(0.53, 0.38, 0.80)),
    "Boom": dict(width=1.3, height=1.15, top_taper=0.0, lean=0.0, bottom_flat=0.5, motif="ripple",
                 body=(0.91, 0.72, 0.42), face=(0.45, 0.28, 0.15), rim=(0.76, 0.53, 0.27)),
}

OUTLINE = 0.035      # rim thickness (Bop-width units)
FACE_EMBED = 0.012   # face parts sink this far into the body so no gap shows at an angle


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, rgb):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*rgb, 1.0)
    return mat


def new_object(name, bm, mat, smooth=True):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def blob(spec, inflate=0.0):
    """UV sphere reshaped into the character's silhouette; inflate pushes it out along the normals."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=18, radius=0.5)
    w, h = spec["width"], spec["height"]
    for v in bm.verts:
        x, y, z = v.co
        t = z / 0.5  # -1 bottom .. 1 top
        # Narrower toward the top (Flutter's droplet), slight lean for a hand-made feel.
        radial = 1.0 - spec["top_taper"] * max(0.0, t) ** 1.5
        x, y = x * radial, y * radial
        # Flatter underside: the lower half is squashed so the character sits like a soft dumpling.
        if t < 0:
            z = z * (1.0 - spec["bottom_flat"] * (-t))
        v.co = Vector((x * w + spec["lean"] * t * w, y * w, z * h))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if inflate:
        for v in bm.verts:
            v.co += v.normal * inflate
    # Centre the bounds on the origin, as the existing ball/token code expects of a sphere.
    zs = [v.co.z for v in bm.verts]
    mid = (max(zs) + min(zs)) / 2
    for v in bm.verts:
        v.co.z -= mid
    return bm


def outline(spec, mat, name):
    bm = blob(spec, inflate=OUTLINE)
    # Inverted hull: flip the faces so only the back shows, drawn as a rim around the body.
    bmesh.ops.reverse_faces(bm, faces=bm.faces)
    return new_object(name, bm, mat)


def surface_point(tree, x, z):
    """Front surface point (facing -Y) of the body at (x, z), and its normal."""
    hit = tree.ray_cast(Vector((x, -5.0, z)), Vector((0.0, 1.0, 0.0)))
    if hit[0] is None:
        raise RuntimeError(f"no body surface at x={x:.3f} z={z:.3f}")
    return hit[0], hit[1]


def oriented_ellipsoid(name, mat, tree, x, z, size, segments=(12, 8)):
    """Ellipsoid lying on the body surface at (x, z): size = (width, thickness, height)."""
    point, normal = surface_point(tree, x, z)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments[0], v_segments=segments[1], radius=0.5)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    # Tilt so the flat side follows the surface (rotate local -Y onto the outward normal).
    rot = Vector((0.0, -1.0, 0.0)).rotation_difference(normal)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=Vector(), matrix=rot.to_matrix())
    bmesh.ops.translate(bm, verts=bm.verts, vec=point - normal * FACE_EMBED)
    return new_object(name, bm, mat)


def tube_along(name, mat, points, radius, sides=8):
    """Closed-ended tube through points (a stroke such as the smile)."""
    bm = bmesh.new()
    rings = []
    for i, p in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        tangent = (b - a).normalized()
        side = tangent.cross(Vector((0.0, -1.0, 0.0))).normalized()
        up = side.cross(tangent).normalized()
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(p + (side * math.cos(ang) + up * math.sin(ang)) * radius))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_object(name, bm, mat)


def smile(name, mat, tree, cx, cz, half_width, depth, radius):
    points = []
    for i in range(9):
        u = -1.0 + 2.0 * i / 8
        x = cx + u * half_width
        z = cz - depth * (1.0 - u * u)  # U-shaped smile
        point, normal = surface_point(tree, x, z)
        points.append(point - normal * (FACE_EMBED * 0.5))
    return tube_along(name, mat, points, radius)


def mouth_o(name, mat, tree, cx, cz, r, thickness):
    points = []
    for i in range(17):
        ang = 2 * math.pi * i / 16
        point, normal = surface_point(tree, cx + math.cos(ang) * r, cz + math.sin(ang) * r * 1.2)
        points.append(point - normal * (FACE_EMBED * 0.5))
    return tube_along(name, mat, points, thickness)


def sparkle(name, mat, centre, size):
    """Four-pointed star, a thin prism facing the player."""
    bm = bmesh.new()
    outer, inner = size, size * 0.32
    front, back = [], []
    for k in range(8):
        ang = math.pi / 2 + k * math.pi / 4
        r = outer if k % 2 == 0 else inner
        p = Vector((math.cos(ang) * r, 0.0, math.sin(ang) * r))
        front.append(bm.verts.new(centre + p + Vector((0, -size * 0.12, 0))))
        back.append(bm.verts.new(centre + p + Vector((0, size * 0.12, 0))))
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    for k in range(8):
        bm.faces.new((front[k], back[k], back[(k + 1) % 8], front[(k + 1) % 8]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return new_object(name, bm, mat, smooth=False)


def dot(name, mat, centre, size):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=size)
    bmesh.ops.translate(bm, verts=bm.verts, vec=centre)
    return new_object(name, bm, mat)


def ripple(name, mat, centre, size):
    """Three stacked arcs (upward bows), like sound spreading out."""
    objs = []
    for i in range(3):
        r = size * (1.0 - 0.28 * i)
        pts = [centre + Vector((math.cos(a) * r, 0.0, math.sin(a) * r * 0.55 - i * size * 0.32))
               for a in [math.pi * (0.15 + 0.7 * j / 8) for j in range(9)]]
        objs.append(tube_along(f"{name}{i}", mat, pts, size * 0.09))
    # Join the arcs into one mesh object.
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    objs[0].name = name
    objs[0].data.name = name
    return objs[0]


def build(name):
    spec = CHARACTERS[name]
    m_body = material(f"{name}_BodyMat", spec["body"])
    m_rim = material(f"{name}_RimMat", spec["rim"])
    m_face = material(f"{name}_FaceMat", spec["face"])
    m_shine = material("ShineMat", (0.97, 0.97, 0.97))
    m_motif = material(f"{name}_MotifMat", (0.94, 0.80, 0.38))

    body_bm = blob(spec)
    tree = BVHTree.FromBMesh(body_bm)
    body = new_object(f"{name}_Body", body_bm, m_body)
    outline(spec, m_rim, f"{name}_Outline")

    w, h = spec["width"], spec["height"]
    # Face proportions read from the reference: eyes a little above centre, smile just below.
    eye_x, eye_z = 0.17 * min(w, 1.0), 0.06 * h
    eye_size = (0.11, 0.06, 0.17)
    oriented_ellipsoid(f"{name}_EyeL", m_face, tree, -eye_x, eye_z, eye_size)
    oriented_ellipsoid(f"{name}_EyeR", m_face, tree, eye_x, eye_z, eye_size)
    smile(f"{name}_Smile", m_face, tree, 0.0, -0.12 * h, 0.12 * min(w, 1.0), 0.06, 0.028)
    mouth_o(f"{name}_Mouth_O", m_face, tree, 0.0, -0.15 * h, 0.045, 0.02)
    oriented_ellipsoid(f"{name}_Highlight", m_shine, tree, -0.27 * w, 0.27 * h, (0.17, 0.04, 0.15))

    motif_centre = Vector((-0.55 * w, -0.05, 0.38 * h))
    if spec["motif"] == "sparkle":
        sparkle(f"{name}_Motif", m_motif, motif_centre, 0.2)
    elif spec["motif"] == "dot":
        dot(f"{name}_Motif", m_motif, motif_centre, 0.08)
    else:
        ripple(f"{name}_Motif", m_motif, motif_centre, 0.18)
    return body


def export(name):
    os.makedirs(EXPORT_DIR, exist_ok=True)
    blend_path = os.path.join(HERE, f"{name}.blend")
    fbx_path = os.path.join(EXPORT_DIR, f"{name}.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    counts = {o.name: len(o.data.polygons) for o in bpy.data.objects if o.type == "MESH"}
    print(f"[characters] {name}: {sum(counts.values())} faces -> {fbx_path}")
    for k, v in sorted(counts.items()):
        print(f"[characters]   {k}: {v}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["Flutter"]
    for name in argv:
        reset_scene()
        build(name)
        export(name)


if __name__ == "__main__":
    main()

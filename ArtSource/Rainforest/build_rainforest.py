"""
Magical rainforest set dressing for Little Echoes: curly vines with chunky leaves and flowers, a
butterfly with flapping wings, and a four-point sparkle. Original procedural shapes in a bright,
cute, thick-outlined 2D-game look (inspired by the feel of games like MapleStory; nothing copied,
no downloaded assets).

Every part is a separate mesh so Unity colours it with project materials:
  Vine_<Name>_Stem / _Leaves / _LeafRims / _Flowers / _FlowerHearts
      a vine shaped to its furniture (built in place around the local origin):
      Lamp, Stand (ball stand post), TableL, TableR (front table legs) spiral up a pole; Chair drapes
      along the chair back's top edge.
  Butterfly_Body, Butterfly_WingL/R, Butterfly_WingRimL/R, Butterfly_SpotsL/R
      wings pivot on the body axis (Unity flaps them by rotation)
  Sparkle, Sparkle_Rim
      chunky four-point star with a soft rim

Axes as for the characters: Blender Z up, -Y toward the player; exported so -Y -> Unity -Z, Z -> Unity Y.
Run headless:
  Blender --background --factory-startup --python ArtSource/Rainforest/build_rainforest.py
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Rainforest", "Rainforest.fbx")

RIM = 0.004  # outline thickness (m) on leaves, wings and sparkles

# Vines that climb a pole: (name, pole radius, bottom, top, turns, leaves per turn, flowers)
SPIRALS = [
    ("Lamp", 0.045, 0.08, 1.04, 3.0, 6, 3),
    ("Stand", 0.07, 0.05, 0.92, 2.5, 6, 2),
    ("TableL", 0.055, 0.02, 0.72, 1.75, 6, 1),
    ("TableR", 0.055, 0.02, 0.72, 1.75, 6, 1),
]
CHAIR_DRAPE = dict(width=0.56, sag=0.05, tails=0.32)  # along the chair back's top edge


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def obj_from(bm, name, smooth=True):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def tube(bm, points, radius, sides=6, taper_end=0.5):
    """Adds a closed tube through points to bm (radius tapers toward the end, like a vine tip)."""
    rings = []
    n = len(points)
    for i, p in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, n - 1)]
        t = (b - a).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        side = t.cross(ref).normalized()
        up = side.cross(t).normalized()
        r = radius * (1.0 - (1.0 - taper_end) * i / max(n - 1, 1))
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / sides) + up * math.sin(2 * math.pi * k / sides)) * r)
                      for k in range(sides)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(sides):
            bm.faces.new((r0[k], r0[(k + 1) % sides], r1[(k + 1) % sides], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


def leaf_shape(length, width):
    """Chunky teardrop leaf outline in its own plane (x across, y along), base at the origin."""
    pts = []
    for i in range(14):
        u = i / 13.0                                   # 0 base .. 1 tip
        half = width * 0.5 * math.sin(math.pi * u) ** 0.8 * (1.0 - 0.35 * u)
        pts.append((u * length, half))
    right = [Vector((h, y, 0)) for y, h in pts]
    left = [Vector((-h, y, 0)) for y, h in reversed(pts[1:-1])]
    return right + left


def add_flat(bm, outline, thickness, matrix, cup=0.0, inflate=0.0):
    """Extruded flat shape (leaf, wing) placed by matrix; cup curls the sides up a little."""
    centre = sum(outline, Vector()) / len(outline)
    front, back = [], []
    for p in outline:
        q = p + (p - centre).normalized() * inflate if inflate else p.copy()
        q.z += cup * (q.x ** 2) * 40.0
        front.append(bm.verts.new(matrix @ (q + Vector((0, 0, thickness * 0.5 + inflate)))))
        back.append(bm.verts.new(matrix @ (q - Vector((0, 0, thickness * 0.5 + inflate)))))
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(outline)
    for i in range(n):
        bm.faces.new((back[i], back[(i + 1) % n], front[(i + 1) % n], front[i]))


def flower(bm_petals, bm_heart, at, normal, size):
    """Five round petals and a yellow heart, facing along normal."""
    rot = Vector((0, 0, 1)).rotation_difference(normal).to_matrix().to_4x4()
    for k in range(5):
        a = 2 * math.pi * k / 5
        petal = bmesh.ops.create_uvsphere(bm_petals, u_segments=8, v_segments=5, radius=1.0)
        m = Matrix.Translation(at) @ rot @ Matrix.Translation(Vector((math.cos(a), math.sin(a), 0)) * size * 0.55) \
            @ Matrix.Diagonal((size * 0.42, size * 0.42, size * 0.15, 1))
        bmesh.ops.transform(bm_petals, matrix=m, verts=petal["verts"])
    heart = bmesh.ops.create_uvsphere(bm_heart, u_segments=8, v_segments=5, radius=1.0)
    m = Matrix.Translation(at + normal * size * 0.08) @ rot @ Matrix.Diagonal((size * 0.3, size * 0.3, size * 0.2, 1))
    bmesh.ops.transform(bm_heart, matrix=m, verts=heart["verts"])


def leaf_matrix(base, along, facing):
    """Places a leaf with its base at `base`, pointing along `along`, its face toward `facing`."""
    y = along.normalized()
    z = (facing - y * facing.dot(y)).normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = base
    return m


def build_vine(name, path, side_of, leaves_every, flower_at, stem_radius=0.009):
    """Stem along path; leaves alternate left/right; flowers at given path fractions."""
    stem, leaves, rims, petals, hearts = (bmesh.new() for _ in range(5))
    tube(stem, path, stem_radius)
    n = len(path)
    for i in range(2, n - 1, leaves_every):
        p, q = path[i], path[i + 1]
        tangent = (q - p).normalized()
        out = side_of(p)                               # away from the furniture, toward the room
        flip = 1 if (i // leaves_every) % 2 == 0 else -1
        sideways = tangent.cross(out).normalized() * flip
        along = (sideways * 0.8 + out * 0.6 + Vector((0, 0, 0.35))).normalized()
        size = 0.055 + 0.015 * ((i * 7) % 3) / 2.0
        shape = leaf_shape(size, size * 0.62)
        m = leaf_matrix(p + out * stem_radius, along, out + Vector((0, -0.4, 0.2)))
        add_flat(leaves, shape, 0.004, m, cup=0.0025)
        add_flat(rims, shape, 0.004, m, cup=0.0025, inflate=RIM)
    for f in flower_at:
        i = min(int(f * (n - 1)), n - 2)
        flower(petals, hearts, path[i] + side_of(path[i]) * 0.02, side_of(path[i]) + Vector((0, -0.3, 0.2)), 0.045)
    bmesh.ops.reverse_faces(rims, faces=rims.faces)    # inverted hull: only the rim shows
    obj_from(stem, f"Vine_{name}_Stem")
    obj_from(leaves, f"Vine_{name}_Leaves", smooth=False)
    obj_from(rims, f"Vine_{name}_LeafRims", smooth=False)
    obj_from(petals, f"Vine_{name}_Flowers")
    obj_from(hearts, f"Vine_{name}_FlowerHearts")


def spiral_vines():
    for name, radius, bottom, top, turns, per_turn, flowers in SPIRALS:
        steps = int(turns * 24)
        r = radius + 0.012
        # Start on the player's side (-Y) so the first leaves face the room.
        path = [Vector((r * math.sin(2 * math.pi * turns * k / steps), -r * math.cos(2 * math.pi * turns * k / steps),
                        bottom + (top - bottom) * k / steps)) for k in range(steps + 1)]
        # A curly tip that unwinds outward at the top, the cartoon-vine signature.
        tip = path[-1]
        for k in range(1, 9):
            a = k * 0.7
            path.append(tip + Vector((0.025 * math.sin(a) * (1 + k * 0.15), -0.025 * (1 - math.cos(a)), 0.012 * k)))
        side = lambda p: Vector((p.x, p.y, 0)).normalized() if Vector((p.x, p.y, 0)).length > 1e-4 else Vector((0, -1, 0))
        build_vine(name, path, side, max(2, int(24 / per_turn)),
                   [0.25 + 0.6 * j / max(flowers - 1, 1) for j in range(flowers)] if flowers > 1 else [0.55])


def chair_drape():
    w, sag, tails = CHAIR_DRAPE["width"], CHAIR_DRAPE["sag"], CHAIR_DRAPE["tails"]
    path = []
    # Down the left side, across the top with a gentle sag, down the right side, curling at both tips.
    for k in range(10):
        path.append(Vector((-w / 2 - 0.015, -0.03, -tails * (1 - k / 9.0))))
    for k in range(1, 24):
        u = k / 24.0
        path.append(Vector((-w / 2 + w * u, -0.03 - 0.01 * math.sin(math.pi * u), 0.02 - sag * 0.2 * math.sin(math.pi * u))))
    for k in range(10):
        path.append(Vector((w / 2 + 0.015, -0.03, -tails * k / 9.0)))
    build_vine("Chair", path, lambda p: Vector((0, -1, 0.3)).normalized(), 3, [0.12, 0.5, 0.88])


def butterfly():
    body = bmesh.new()
    seg = bmesh.ops.create_uvsphere(body, u_segments=10, v_segments=6, radius=1.0)
    bmesh.ops.transform(body, matrix=Matrix.Diagonal((0.008, 0.032, 0.008, 1)), verts=seg["verts"])
    head = bmesh.ops.create_uvsphere(body, u_segments=10, v_segments=6, radius=0.011)
    bmesh.ops.translate(body, vec=Vector((0, 0.035, 0.002)), verts=head["verts"])
    for sx in (-1, 1):  # curly antennae
        pts = [Vector((sx * 0.004 * k, 0.04 + 0.004 * k, 0.004 * k + 0.0015 * k * k)) for k in range(6)]
        tube(body, pts, 0.0018, sides=4, taper_end=1.0)
    obj_from(body, "Butterfly_Body")

    def wing_outline(sx):
        """One simple closed outline (no doubling back): a big rounded upper lobe and a smaller lower
        lobe, pinched at the waist where they meet, inner edge along the body."""
        pts = []
        for i in range(28):
            a = 2 * math.pi * i / 28                         # 0 = straight out from the body
            upper = math.sin(a) >= 0
            rx, ry = (0.036, 0.046) if upper else (0.028, 0.03)
            pinch = 1.0 - 0.28 * math.exp(-((math.cos(a) - 1.0) ** 2) * 6.0)   # notch at the outer waist
            x = 0.032 + math.cos(a) * rx * pinch
            y = math.sin(a) * ry + (0.004 if upper else -0.002)
            pts.append(Vector((sx * max(x, 0.002), y, 0)))
        return pts if sx > 0 else list(reversed(pts))       # keep the same winding on both sides

    for sx, side in ((-1, "L"), (1, "R")):
        wing, rim = bmesh.new(), bmesh.new()
        add_flat(wing, wing_outline(sx), 0.002, Matrix.Identity(4))
        add_flat(rim, wing_outline(sx), 0.002, Matrix.Identity(4), inflate=RIM * 0.8)
        bmesh.ops.reverse_faces(rim, faces=rim.faces)
        obj_from(wing, f"Butterfly_Wing{side}", smooth=False)
        obj_from(rim, f"Butterfly_WingRim{side}", smooth=False)

        spots = bmesh.new()  # white polka dots, part of this wing so they flap with it
        for face in (1, -1):  # both faces of the wing: it is seen from above and below as it flaps
            for (x, y, r) in ((0.034, 0.026, 0.009), (0.05, 0.006, 0.007), (0.03, -0.018, 0.007)):
                s = bmesh.ops.create_uvsphere(spots, u_segments=8, v_segments=4, radius=1.0)
                bmesh.ops.transform(spots, matrix=Matrix.Translation(Vector((sx * x, y, face * 0.0015))) @ Matrix.Diagonal((r, r, 0.0012, 1)), verts=s["verts"])
        obj_from(spots, f"Butterfly_Spots{side}")


def sparkle():
    def star(inflate):
        pts = []
        for k in range(8):
            a = math.pi / 2 + k * math.pi / 4
            r = 0.03 if k % 2 == 0 else 0.009
            pts.append(Vector((math.cos(a) * r, math.sin(a) * r, 0)))
        bm = bmesh.new()
        add_flat(bm, pts, 0.008, Matrix.Rotation(math.radians(90), 4, "X"), inflate=inflate)  # stand it up to face -Y
        return bm
    obj_from(star(0.0), "Sparkle", smooth=False)
    rim = star(RIM * 0.6)
    bmesh.ops.reverse_faces(rim, faces=rim.faces)
    obj_from(rim, "Sparkle_Rim", smooth=False)


def main():
    reset()
    spiral_vines()
    chair_drape()
    butterfly()
    sparkle()
    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Rainforest.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    total = 0
    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        if o.type == "MESH":
            total += len(o.data.polygons)
            print(f"[rainforest]   {o.name}: {len(o.data.polygons)}")
    print(f"[rainforest] {total} faces -> {EXPORT}")


if __name__ == "__main__":
    main()

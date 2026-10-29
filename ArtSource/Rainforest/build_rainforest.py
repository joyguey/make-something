"""
Magical rainforest set dressing for Little Echoes: curly vines with chunky leaves and flowers, a
butterfly with flapping wings, and a four-point sparkle. Original procedural shapes in a bright,
cute, thick-outlined 2D-game look (inspired by the feel of games like MapleStory; nothing copied,
no downloaded assets).

Every part is a separate mesh so Unity colours it with project materials:
  Vine_<Name>_Seg<k>_Stem / _Leaves / _LeafRims (k = 0..7, bottom to tip), Vine_<Name>_Flowers / _FlowerHearts
      a vine shaped to its furniture, in pieces that creep out one by one as music plays. Each piece's
      origin is inside the furniture, so Unity hides it by shrinking it there. Lamp, TableL, TableR
      (front table legs) spiral up a pole; Chair climbs a back leg and runs along the top of the back.
      (The ball-stand vine was retired 2026-10-28: the stand is a floating cloud now.)
  Toadstool_Cap / _CapRim / _Spots / _Stem / _StemRim / _Face
      a cute spotted toadstool with a sleepy face that grows from a bud and sways to the beat
  Flower_Stem / _Leaves / _LeafRims / _Petals / _PetalRims / _Heart / _Bud
      a little floor flower (about 20 cm) that pops up around the room as the song grows; Unity
      colours the petals per flower. Origin at the foot of the stem. It grows as a closed green bud,
      then the petals bloom open around the heart (Unity scales them about the heart's centre).
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
    ("TableL", 0.055, 0.02, 0.72, 1.75, 6, 1),
    ("TableR", 0.055, 0.02, 0.72, 1.75, 6, 1),
]


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


SEGMENTS = 8  # each vine creeps out piece by piece as its instrument plays


def obj_at(bm, name, pivot, smooth=True):
    """Object whose origin is `pivot` (mesh moved to stay where it was): Unity hides a piece by
    shrinking it toward this point, which sits inside the furniture."""
    bmesh.ops.translate(bm, vec=-pivot, verts=bm.verts)
    obj = obj_from(bm, name, smooth)
    obj.location = pivot
    return obj


def build_vine(name, path, side_of, core_of, leaves_every, flower_at, stem_radius=0.009):
    """Vine in SEGMENTS pieces (stem, leaves, leaf rims each) plus one flower group. Leaves alternate
    sides; core_of(point) gives the hiding point inside the furniture nearest that point."""
    n = len(path)
    bounds = [round(k * (n - 1) / SEGMENTS) for k in range(SEGMENTS + 1)]
    for k in range(SEGMENTS):
        lo, hi = bounds[k], bounds[k + 1]
        stem, leaves, rims = bmesh.new(), bmesh.new(), bmesh.new()
        tube(stem, path[lo:hi + 1], stem_radius, taper_end=1.0 if k < SEGMENTS - 1 else 0.5)
        for i in range(max(lo, 2), min(hi, n - 1)):
            if (i - 2) % leaves_every:
                continue
            p, q = path[i], path[i + 1]
            tangent = (q - p).normalized()
            out = side_of(p)                           # away from the furniture, toward the room
            flip = 1 if ((i - 2) // leaves_every) % 2 == 0 else -1
            sideways = tangent.cross(out).normalized() * flip
            along = (sideways * 0.8 + out * 0.6 + Vector((0, 0, 0.35))).normalized()
            size = 0.055 + 0.015 * ((i * 7) % 3) / 2.0
            shape = leaf_shape(size, size * 0.62)
            m = leaf_matrix(p + out * stem_radius, along, out + Vector((0, -0.4, 0.2)))
            add_flat(leaves, shape, 0.004, m, cup=0.0025)
            add_flat(rims, shape, 0.004, m, cup=0.0025, inflate=RIM)
        bmesh.ops.reverse_faces(rims, faces=rims.faces)  # inverted hull: only the rim shows
        pivot = core_of(path[(lo + hi) // 2])
        obj_at(stem, f"Vine_{name}_Seg{k}_Stem", pivot.copy())
        obj_at(leaves, f"Vine_{name}_Seg{k}_Leaves", pivot.copy(), smooth=False)
        obj_at(rims, f"Vine_{name}_Seg{k}_LeafRims", pivot.copy(), smooth=False)
    petals, hearts = bmesh.new(), bmesh.new()
    for f in flower_at:
        i = min(int(f * (n - 1)), n - 2)
        flower(petals, hearts, path[i] + side_of(path[i]) * 0.02, side_of(path[i]) + Vector((0, -0.3, 0.2)), 0.045)
    pivot = core_of(path[n // 2])
    obj_at(petals, f"Vine_{name}_Flowers", pivot.copy())
    obj_at(hearts, f"Vine_{name}_FlowerHearts", pivot.copy())


def spiral_path(radius, bottom, top, turns, axis=None):
    axis = axis if axis is not None else Vector((0, 0, 0))
    steps = int(turns * 24)
    r = radius + 0.012
    # Start on the player's side (-Y) so the first leaves face the room.
    return [axis + Vector((r * math.sin(2 * math.pi * turns * k / steps), -r * math.cos(2 * math.pi * turns * k / steps),
                           bottom + (top - bottom) * k / steps)) for k in range(steps + 1)]


def curl(path):
    """A curly tip that unwinds outward: the cartoon-vine signature."""
    tip = path[-1]
    for k in range(1, 9):
        a = k * 0.7
        path.append(tip + Vector((0.025 * math.sin(a) * (1 + k * 0.15), -0.025 * (1 - math.cos(a)), 0.012 * k)))
    return path


def radial(axis):
    def side(p):
        d = Vector((p.x - axis.x, p.y - axis.y, 0))
        return d.normalized() if d.length > 1e-4 else Vector((0, -1, 0))
    return side


def spiral_vines():
    for name, radius, bottom, top, turns, per_turn, flowers in SPIRALS:
        path = curl(spiral_path(radius, bottom, top, turns))
        core = lambda p, b=bottom, t=top: Vector((0, 0, min(max(p.z, b), t)))  # on the pole axis
        build_vine(name, path, radial(Vector((0, 0, 0))), core, max(2, int(24 / per_turn)),
                   [0.25 + 0.6 * j / max(flowers - 1, 1) for j in range(flowers)] if flowers > 1 else [0.55])


def chair_vine():
    """Climbs the chair's back-left leg and the back's left edge, then runs along the top of the back
    (local origin = the chair's centre line at floor level, on the back's mid-plane)."""
    axis = Vector((-0.22, 0, 0))
    path = spiral_path(0.03, 0.02, 1.0, 4.0, axis)
    top = path[-1]
    for k in range(1, 8):  # up and over the corner
        path.append(top.lerp(Vector((-0.2, -0.04, 1.1)), k / 7.0))
    for k in range(1, 22):  # along the top edge with a gentle sag
        u = k / 21.0
        path.append(Vector((-0.2 + 0.46 * u, -0.04 - 0.008 * math.sin(math.pi * u), 1.1 - 0.02 * math.sin(math.pi * u))))
    path = curl(path)
    spiral_side = radial(axis)
    side = lambda p: spiral_side(p) if p.z < 1.0 else Vector((0, -1, 0.3)).normalized()
    # Hide inside the leg (below the seat) or the back slab (above it): the slab top is at 1.08.
    core = lambda p: Vector((axis.x, 0, max(p.z, 0.02))) if p.z < 1.0 else Vector((min(max(p.x, -0.24), 0.24), 0, 1.02))
    build_vine("Chair", path, side, core, 3, [0.3, 0.62, 0.9])


def toadstool():
    """A cute toadstool: domed cap with white spots, chunky stem with a sleepy face, all outlined.
    Origin at the foot of the stem; about 25 cm tall."""
    cap, rim_cap, spots = bmesh.new(), bmesh.new(), bmesh.new()

    def dome(bm, inflate):
        g = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=1.0)
        for v in g["verts"]:
            x, y, z = v.co
            z = z * 0.62 if z > 0 else z * 0.18           # flat underside, round top
            v.co = Vector((x * (0.13 + inflate), y * (0.13 + inflate), 0.16 + z * (0.13 + inflate)))

    dome(cap, 0.0)
    dome(rim_cap, RIM * 1.5)
    bmesh.ops.reverse_faces(rim_cap, faces=rim_cap.faces)
    for a, el, r in ((0.3, 0.55, 0.028), (2.0, 0.35, 0.022), (3.6, 0.6, 0.025), (5.0, 0.3, 0.02),
                     (1.2, 0.95, 0.024), (4.3, 0.9, 0.02), (2.9, 0.85, 0.018)):
        n = Vector((math.cos(a) * math.cos(el), math.sin(a) * math.cos(el), math.sin(el)))
        at = Vector((n.x * 0.131, n.y * 0.131, 0.16 + n.z * 0.131 * 0.62))
        g = bmesh.ops.create_uvsphere(spots, u_segments=8, v_segments=5, radius=1.0)
        rot = Vector((0, 0, 1)).rotation_difference(Vector((n.x, n.y, n.z * 1.6)).normalized()).to_matrix().to_4x4()
        bmesh.ops.transform(spots, matrix=Matrix.Translation(at) @ rot @ Matrix.Diagonal((r, r, 0.004, 1)), verts=g["verts"])

    stem, rim_stem, face = bmesh.new(), bmesh.new(), bmesh.new()

    def column(bm, inflate):
        g = bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=0.055 + inflate, radius2=0.045 + inflate, depth=0.17)
        for v in g["verts"]:
            v.co.z += 0.085
            bulge = 1.0 + 0.18 * math.sin(math.pi * min(max(v.co.z / 0.17, 0), 1))
            v.co.x *= bulge
            v.co.y *= bulge

    column(stem, 0.0)
    column(rim_stem, RIM * 1.5)
    bmesh.ops.reverse_faces(rim_stem, faces=rim_stem.faces)
    for sx in (-1, 1):  # sleepy dot eyes on the stem, facing the player (-Y)
        g = bmesh.ops.create_uvsphere(face, u_segments=8, v_segments=5, radius=1.0)
        bmesh.ops.transform(face, matrix=Matrix.Translation(Vector((sx * 0.02, -0.06, 0.09))) @ Matrix.Diagonal((0.008, 0.004, 0.011, 1)), verts=g["verts"])
    smile_pts = [Vector((0.012 * math.sin(a), -0.062, 0.068 - 0.006 * math.cos(a))) for a in [math.pi * (-0.5 + j / 6) for j in range(7)]]
    tube(face, smile_pts, 0.0025, sides=4, taper_end=1.0)
    obj_from(cap, "Toadstool_Cap")
    obj_from(rim_cap, "Toadstool_CapRim")
    obj_from(spots, "Toadstool_Spots")
    obj_from(stem, "Toadstool_Stem")
    obj_from(rim_stem, "Toadstool_StemRim")
    obj_from(face, "Toadstool_Face")


def floor_flower():
    """A little flower for the floor: a gently curved stem, two chunky leaves, five outlined flat petals
    and a round heart, its face tipped toward the player (-Y). Origin at the foot; about 20 cm tall.
    Kept light (about 350 triangles) because twenty of them sit round the room."""
    stem, leaves, leaf_rims = bmesh.new(), bmesh.new(), bmesh.new()
    path = [Vector((0.012 * math.sin(math.pi * u), 0.0, 0.17 * u)) for u in [j / 3 for j in range(4)]]
    tube(stem, path, 0.007, sides=5, taper_end=0.7)

    def blade(length, width, points):
        # A plump leaf/petal outline with few points: base at the origin, tip along +y.
        return [Vector((width * 0.5 * math.sin(2 * math.pi * k / points),
                        length * 0.5 * (1 - math.cos(2 * math.pi * k / points)), 0)) for k in range(points)]

    for side, height in ((-1, 0.05), (1, 0.085)):
        m = leaf_matrix(Vector((0.0, 0.0, height)), Vector((side * 0.9, -0.25, 0.55)), Vector((0, -0.4, 1)))
        add_flat(leaves, blade(0.06, 0.032, 6), 0.005, m)
        add_flat(leaf_rims, blade(0.06, 0.032, 6), 0.005, m, inflate=RIM)
    bmesh.ops.reverse_faces(leaf_rims, faces=leaf_rims.faces)

    petals, petal_rims, heart = bmesh.new(), bmesh.new(), bmesh.new()
    at = path[-1] + Vector((0, -0.005, 0.005))
    normal = Vector((0, -0.55, 1)).normalized()
    face = Vector((0, 0, 1)).rotation_difference(normal).to_matrix().to_4x4()
    for k in range(5):
        spin = Matrix.Rotation(2 * math.pi * k / 5, 4, "Z") @ Matrix.Translation(Vector((0, 0.008, 0)))
        m = Matrix.Translation(at) @ face @ spin
        add_flat(petals, blade(0.034, 0.026, 6), 0.006, m)
        add_flat(petal_rims, blade(0.034, 0.026, 6), 0.006, m, inflate=RIM)
    bmesh.ops.reverse_faces(petal_rims, faces=petal_rims.faces)
    g = bmesh.ops.create_uvsphere(heart, u_segments=6, v_segments=4, radius=1.0)
    bmesh.ops.transform(heart, matrix=Matrix.Translation(at + normal * 0.004) @ face @ Matrix.Diagonal((0.013, 0.013, 0.008, 1)), verts=g["verts"])
    # The closed bud the flower grows as: a green teardrop over the heart, pointing out along the face.
    bud = bmesh.new()
    g = bmesh.ops.create_uvsphere(bud, u_segments=6, v_segments=5, radius=1.0)
    for v in g["verts"]:
        if v.co.z > 0:
            v.co.x *= 1.0 - 0.55 * v.co.z  # taper to a point
            v.co.y *= 1.0 - 0.55 * v.co.z
    bmesh.ops.transform(bud, matrix=Matrix.Translation(at + normal * 0.008) @ face @ Matrix.Diagonal((0.011, 0.011, 0.019, 1)), verts=g["verts"])
    obj_from(stem, "Flower_Stem")
    obj_from(leaves, "Flower_Leaves", smooth=False)
    obj_from(leaf_rims, "Flower_LeafRims", smooth=False)
    obj_from(petals, "Flower_Petals", smooth=False)
    obj_from(petal_rims, "Flower_PetalRims", smooth=False)
    obj_from(heart, "Flower_Heart")
    obj_from(bud, "Flower_Bud")


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
    chair_vine()
    butterfly()
    sparkle()
    toadstool()
    floor_flower()
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

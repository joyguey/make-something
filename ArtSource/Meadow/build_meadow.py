"""
The meadow the lesson takes place in (V3 review: "the room environment is just a box, what about a little meadow with
your bouncy happy characters"), and the title's wooden sign. Original low-poly pieces; Unity lays them out
(LittleEchoesSceneSetup.CreateMeadow), so each piece is modelled once around its own origin, base on the ground.

Each piece's colours are painted into the colour attribute "Col" (RGB), and its ambient occlusion is baked on its own
(V3 review: "use Blender to bake ambient occlusion on your objects individually"): every other piece hidden from the
render, Cycles bakes AO into the vertex colours, and the result goes into Col's alpha. The shader
RoomOrchestra/LitBakedAO multiplies them in. The sky and the ground tiles are not baked (nothing occludes them).

  /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Meadow/build_meadow.py
Writes Assets/RoomOrchestra/Art/Meadow/Meadow.fbx and ArtSource/Meadow/Meadow.blend.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Meadow", "Meadow.fbx")
rng = random.Random(2026_11_11)

# A soft, sunny palette in the game's own colours (the leaf green, the characters' turquoise, lilac and butter yellow).
GRASS = [(0.45, 0.74, 0.38), (0.48, 0.77, 0.40), (0.43, 0.71, 0.37), (0.50, 0.78, 0.42)]  # close greens: soft patches, no checkerboard
HILL = [(0.36, 0.64, 0.38), (0.44, 0.71, 0.42), (0.31, 0.58, 0.36)]
LEAVES = [(0.30, 0.62, 0.32), (0.38, 0.70, 0.36), (0.26, 0.55, 0.30)]
BLOSSOM = [(1.00, 0.72, 0.84), (0.98, 0.80, 0.90)]
BARK = (0.50, 0.34, 0.22)
ROCK = [(0.70, 0.70, 0.72), (0.62, 0.63, 0.66)]
PETALS = [(1.0, 0.86, 0.36), (0.76, 0.62, 1.0), (0.40, 0.86, 0.92), (1.0, 0.62, 0.72), (1.0, 1.0, 1.0)]
CENTRE = (1.0, 0.80, 0.25)
STEM = (0.30, 0.58, 0.28)
CLOUD = (0.97, 0.98, 1.0)
WOOD = (0.62, 0.43, 0.27)
WOOD_DARK = (0.48, 0.32, 0.20)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ---- building blocks: bmesh pieces with a colour per face ----
class Piece:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.colours = {}  # face index -> rgb

    def paint(self, faces, rgb):
        for f in faces:
            self.colours[f] = rgb

    def add(self, geom_faces, rgb):
        faces = [f for f in geom_faces if isinstance(f, bmesh.types.BMFace)]
        for f in faces:
            f.material_index = 0
        self.bm.faces.ensure_lookup_table()
        for f in faces:
            self.colours[f] = rgb
        return faces


def jitter(rgb, amount=0.04):
    return tuple(max(0.0, min(1.0, c + rng.uniform(-amount, amount))) for c in rgb)


def ico(piece, centre, radius, rgb, subdiv=1, squash=(1, 1, 1), wobble=0.12):
    geom = bmesh.ops.create_icosphere(piece.bm, subdivisions=subdiv, radius=radius)
    verts = geom["verts"]
    for v in verts:
        n = noise.noise(v.co * 3.0 + Vector((centre[0], centre[1], centre[2])))
        v.co *= 1.0 + wobble * n
        v.co.x *= squash[0]
        v.co.y *= squash[1]
        v.co.z *= squash[2]
        v.co += Vector(centre)
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        piece.colours[f] = jitter(rgb, 0.03)
    return faces


def cone(piece, base, radius1, radius2, depth, rgb, segments=8):
    geom = bmesh.ops.create_cone(piece.bm, cap_ends=True, segments=segments, radius1=radius1, radius2=radius2, depth=depth)
    verts = geom["verts"]
    for v in verts:
        v.co += Vector(base) + Vector((0, 0, depth / 2))
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        piece.colours[f] = jitter(rgb, 0.02)
    return faces


def box(piece, centre, size, rgb):
    geom = bmesh.ops.create_cube(piece.bm, size=1.0)
    for v in geom["verts"]:
        v.co.x = centre[0] + v.co.x * size[0]
        v.co.y = centre[1] + v.co.y * size[1]
        v.co.z = centre[2] + v.co.z * size[2]
    faces = list({f for v in geom["verts"] for f in v.link_faces})
    for f in faces:
        piece.colours[f] = rgb
    return faces


def finish(piece):
    """The piece as a Blender object, its face colours painted into the corner colour attribute "Col"."""
    mesh = bpy.data.meshes.new(piece.name)
    bm = piece.bm
    bm.faces.ensure_lookup_table()
    colours = [piece.colours.get(f, (1, 1, 1)) for f in bm.faces]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(piece.name, mesh)
    bpy.context.collection.objects.link(obj)
    col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for poly in mesh.polygons:
        rgb = colours[poly.index] if poly.index < len(colours) else (1, 1, 1)
        for li in poly.loop_indices:
            col.data[li].color = (rgb[0], rgb[1], rgb[2], 1.0)
    for poly in mesh.polygons:
        poly.use_smooth = False
    return obj


# ---- the pieces ----
def tree_round(name, height=3.2):
    p = Piece(name)
    cone(p, (0, 0, 0), 0.16, 0.10, height * 0.55, BARK, segments=7)
    top = height * 0.55
    ico(p, (0, 0, top + 0.55), 0.95, rng.choice(LEAVES), subdiv=2, squash=(1, 1, 0.85))
    ico(p, (0.55, 0.15, top + 0.25), 0.62, rng.choice(LEAVES), subdiv=1)
    ico(p, (-0.5, -0.2, top + 0.3), 0.58, rng.choice(LEAVES), subdiv=1)
    ico(p, (0.05, 0.45, top + 1.15), 0.55, rng.choice(LEAVES), subdiv=1)
    return finish(p)


def tree_pine(name, height=3.8):
    p = Piece(name)
    cone(p, (0, 0, 0), 0.13, 0.09, height * 0.3, BARK, segments=6)
    for i, (r, z, d) in enumerate([(1.05, 0.30, 1.4), (0.85, 0.55, 1.2), (0.62, 0.78, 1.0), (0.38, 0.95, 0.8)]):
        cone(p, (0, 0, height * z - d * 0.35), r, 0.02, d, LEAVES[(i + 1) % len(LEAVES)], segments=8)
    return finish(p)


def tree_blossom(name, height=2.8):
    p = Piece(name)
    cone(p, (0, 0, 0), 0.14, 0.09, height * 0.5, BARK, segments=7)
    top = height * 0.5
    for (x, y, z, r) in [(0, 0, 0.5, 0.85), (0.6, 0.2, 0.3, 0.55), (-0.55, -0.15, 0.35, 0.55), (0.1, -0.5, 0.75, 0.5), (-0.1, 0.45, 0.85, 0.5)]:
        ico(p, (x, y, top + z), r, rng.choice(BLOSSOM), subdiv=1)
    return finish(p)


def bush(name, size=0.7):
    p = Piece(name)
    for i in range(rng.randint(3, 4)):
        a = rng.uniform(0, math.tau)
        d = rng.uniform(0.0, size * 0.5)
        ico(p, (math.cos(a) * d, math.sin(a) * d, size * 0.45), size * rng.uniform(0.45, 0.6), rng.choice(LEAVES), subdiv=1, squash=(1, 1, 0.8))
    # a few berries or blossoms on top
    for i in range(5):
        a = rng.uniform(0, math.tau)
        ico(p, (math.cos(a) * size * 0.45, math.sin(a) * size * 0.45, size * rng.uniform(0.5, 0.8)), 0.05, rng.choice(PETALS), subdiv=0, wobble=0)
    return finish(p)


def rock(name, size=0.5):
    p = Piece(name)
    ico(p, (0, 0, size * 0.25), size, rng.choice(ROCK), subdiv=1, squash=(1.2, 0.9, 0.55), wobble=0.3)
    return finish(p)


def hill(name, radius=4.0, height=2.0):
    p = Piece(name)
    geom = bmesh.ops.create_uvsphere(p.bm, u_segments=20, v_segments=10, radius=1.0)
    verts = geom["verts"]
    for v in verts:
        z = v.co.z
        if z < 0:
            v.co.z = 0
        n = noise.noise(v.co * 2.0 + Vector((radius, 0, 0)))
        v.co.x *= radius * (1 + 0.08 * n)
        v.co.y *= radius * 0.75 * (1 + 0.08 * n)
        v.co.z = max(0.0, v.co.z) * height * (1 + 0.1 * n)
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        c = f.calc_center_median()
        shade = HILL[0] if c.z > height * 0.6 else (HILL[1] if c.z > height * 0.25 else HILL[2])
        p.colours[f] = jitter(shade, 0.025)
    bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts, dist=1e-4)
    return finish(p)


def flower(p, at, rgb, height):
    cone(p, at, 0.012, 0.008, height, STEM, segments=4)
    head = (at[0], at[1], at[2] + height)
    for k in range(5):
        a = k / 5 * math.tau
        ico(p, (head[0] + math.cos(a) * 0.045, head[1] + math.sin(a) * 0.045, head[2]), 0.035, rgb, subdiv=0, squash=(1, 1, 0.4), wobble=0)
    ico(p, head, 0.028, CENTRE, subdiv=0, wobble=0)


def flower_patch(name, count=9, spread=0.5):
    p = Piece(name)
    colour = rng.choice(PETALS)
    for i in range(count):
        a = rng.uniform(0, math.tau)
        d = math.sqrt(rng.random()) * spread
        flower(p, (math.cos(a) * d, math.sin(a) * d, 0), colour if rng.random() < 0.7 else rng.choice(PETALS), rng.uniform(0.18, 0.32))
    return finish(p)


def grass_tuft(name):
    p = Piece(name)
    for i in range(7):
        a = rng.uniform(0, math.tau)
        d = rng.uniform(0, 0.08)
        h = rng.uniform(0.15, 0.3)
        lean = Vector((math.cos(a) * 0.06, math.sin(a) * 0.06, 0))
        base = Vector((math.cos(a) * d, math.sin(a) * d, 0))
        geom = bmesh.ops.create_cone(p.bm, cap_ends=False, segments=3, radius1=0.025, radius2=0.0, depth=h)
        for v in geom["verts"]:
            t = (v.co.z + h / 2) / h
            v.co += base + Vector((0, 0, h / 2)) + lean * t * t
        for f in {f for v in geom["verts"] for f in v.link_faces}:
            p.colours[f] = jitter(rng.choice(GRASS), 0.05)
    return finish(p)


def cloud(name, size=1.4):
    p = Piece(name)
    for (x, z, r) in [(0, 0.1, 0.62), (0.7, 0, 0.5), (-0.7, 0, 0.48), (0.3, 0.35, 0.45), (-0.3, 0.3, 0.42), (1.2, -0.1, 0.33), (-1.15, -0.1, 0.32)]:
        ico(p, (x * size, rng.uniform(-0.1, 0.1), z * size), r * size, CLOUD, subdiv=2, squash=(1, 0.85, 0.75), wobble=0.06)
    return finish(p)


def ground_tile(name, size=2.0, seed=0):
    """A square of meadow (size m), its grass in soft patches; edges level so tiles meet."""
    p = Piece(name)
    geom = bmesh.ops.create_grid(p.bm, x_segments=8, y_segments=8, size=size / 2)
    for f in {f for v in geom["verts"] for f in v.link_faces}:
        c = f.calc_center_median()
        n = noise.noise(Vector((c.x * 0.9 + seed * 7.1, c.y * 0.9 + seed * 3.3, 0.5)))
        p.colours[f] = GRASS[int((n + 1) * 2) % len(GRASS)]
    return finish(p)


def sky_wedge(name, radius=40.0, wedges=8):
    """One of the sky's wedges: a dome slice, inside facing, blue overhead to pale at the horizon."""
    p = Piece(name)
    rings, steps = 8, 4
    verts = []
    for r in range(rings + 1):
        el = r / rings * (math.pi / 2)
        row = []
        for s in range(steps + 1):
            az = s / steps * (math.tau / wedges)
            row.append(p.bm.verts.new((radius * math.cos(el) * math.cos(az), radius * math.cos(el) * math.sin(az), radius * math.sin(el) - 2.0)))
        verts.append(row)
    for r in range(rings):
        for s in range(steps):
            f = p.bm.faces.new((verts[r][s], verts[r][s + 1], verts[r + 1][s + 1], verts[r + 1][s]))
            t = (r + 0.5) / rings
            horizon, zenith = (0.86, 0.94, 0.99), (0.45, 0.70, 0.95)
            p.colours[f] = tuple(horizon[i] + (zenith[i] - horizon[i]) * t ** 0.8 for i in range(3))
    obj = finish(p)
    obj.data.flip_normals()  # seen from inside
    return obj


def sign(name):
    """The title's wooden sign frame: two posts and a top rail, around the title card (1.0 x 0.6 m)."""
    p = Piece(name)
    for x in (-0.58, 0.58):
        box(p, (x, 0, 0.75), (0.07, 0.07, 1.5), WOOD)
        box(p, (x, 0, 1.52), (0.1, 0.1, 0.06), WOOD_DARK)
    box(p, (0, 0, 1.5), (1.26, 0.08, 0.07), WOOD)
    box(p, (0, 0, 0.82), (1.18, 0.05, 0.05), WOOD_DARK)
    # a little vine with leaves and flowers along the top rail
    for i in range(9):
        x = -0.55 + i * 0.1375
        ico(p, (x, -0.05, 1.55 + 0.03 * math.sin(i)), 0.05, rng.choice(LEAVES), subdiv=0, squash=(1.2, 0.5, 0.8), wobble=0)
        if i % 2 == 0:
            ico(p, (x + 0.04, -0.07, 1.6), 0.03, rng.choice(PETALS), subdiv=0, wobble=0)
    return finish(p)


# ---- ambient occlusion, baked per object ----
def bake_ao(obj, distance, floor=0.25):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.render.bake.target = "VERTEX_COLORS"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = distance
    for o in bpy.data.objects:
        o.hide_render = o is not obj  # each piece on its own
    mesh = obj.data
    if obj.data.materials.__len__() == 0:
        mesh.materials.append(bpy.data.materials.new("Bake"))
    ao = mesh.color_attributes.new("AO", "FLOAT_COLOR", "CORNER")
    mesh.color_attributes.active_color = ao
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type="AO", target="VERTEX_COLORS")
    col = mesh.color_attributes["Col"]
    ao = mesh.color_attributes["AO"]
    for i in range(len(col.data)):
        c = col.data[i].color
        a = ao.data[i].color
        col.data[i].color = (c[0], c[1], c[2], max(floor, min(1.0, (a[0] + a[1] + a[2]) / 3)))
    mesh.color_attributes.remove(ao)
    mesh.color_attributes.active_color = mesh.color_attributes["Col"]
    mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    for o in bpy.data.objects:
        o.hide_render = False


def main():
    reset()
    built = []  # (object, ao distance or None)
    for i in range(2):
        built.append((tree_round(f"Meadow_TreeRound{i}", height=rng.uniform(3.0, 3.6)), 1.2))
        built.append((tree_pine(f"Meadow_TreePine{i}", height=rng.uniform(3.4, 4.2)), 1.0))
    built.append((tree_blossom("Meadow_TreeBlossom0"), 1.0))
    for i in range(3):
        built.append((bush(f"Meadow_Bush{i}", size=rng.uniform(0.55, 0.8)), 0.6))
    for i in range(2):
        built.append((rock(f"Meadow_Rock{i}", size=rng.uniform(0.35, 0.6)), 0.5))
    for i in range(3):
        built.append((hill(f"Meadow_Hill{i}", radius=rng.uniform(4.5, 7.0), height=rng.uniform(1.6, 3.2)), 2.0))
    for i in range(3):
        built.append((flower_patch(f"Meadow_Flowers{i}"), 0.25))
    built.append((grass_tuft("Meadow_Grass0"), 0.15))
    built.append((grass_tuft("Meadow_Grass1"), 0.15))
    for i in range(2):
        built.append((cloud(f"Meadow_Cloud{i}", size=rng.uniform(1.2, 1.7)), 0.8))
    for i in range(3):
        built.append((ground_tile(f"Meadow_Ground{i}", seed=i), None))
    built.append((sky_wedge("Meadow_SkyWedge"), None))
    built.append((sign("Meadow_TitleSign"), 0.3))

    # each piece apart from the others while baking, then home at the origin
    for k, (obj, distance) in enumerate(built):
        obj.location = (k * 60.0, 0, 0) if obj.name != "Meadow_SkyWedge" else (0, 0, -500)
    bpy.context.view_layer.update()
    for obj, distance in built:
        if distance is not None:
            bake_ao(obj, distance, floor=0.82 if "Cloud" in obj.name else 0.25)  # clouds stay bright and soft
            print(f"[meadow] AO baked: {obj.name}")
    for obj, _ in built:
        obj.location = (0, 0, 0)

    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Meadow.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        colors_type="LINEAR", prioritize_active_color=True)
    total = 0
    for obj, _ in built:
        total += len(obj.data.polygons)
        print(f"[meadow]   {obj.name}: {len(obj.data.polygons)} faces")
    print(f"[meadow] {len(built)} pieces, {total} faces -> {EXPORT}")


main()

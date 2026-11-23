"""
The meadow the lesson takes place in (V3 review: "the room environment is just a box, what about a little meadow with
your bouncy happy characters"), and the title's wooden sign. Original low-poly pieces; Unity lays them out
(LittleEchoesSceneSetup.CreateMeadow), so each piece is modelled once around its own origin, base on the ground.

Each piece's colours are painted into the colour attribute "Col" (RGB), and its ambient occlusion is baked on its own
(V3 review: "use Blender to bake ambient occlusion on your objects individually"): every other piece hidden from the
render, Cycles bakes AO into the vertex colours, and the result goes into Col's alpha. The shader
RoomOrchestra/LitBakedAO multiplies them in. The sky and the ground tiles are not baked (nothing occludes them).

Two times of day (owner, Fable refinement 2026-11-11: "A for morning and C for night time depending on the headset's
time"): the same pieces in a golden-hour palette (Meadow.fbx) and a dusk palette (Meadow_Dusk.fbx). The random choices
never depend on the palette, so both exports are the same shapes; Unity swaps the meshes by the headset's clock.

  /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Meadow/build_meadow.py              (day)
  MEADOW_PALETTE=dusk /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Meadow/build_meadow.py
Writes Assets/RoomOrchestra/Art/Meadow/Meadow.fbx (or Meadow_Dusk.fbx) and, for the day, ArtSource/Meadow/Meadow.blend.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
rng = random.Random(2026_11_11)

PALETTE = os.environ.get("MEADOW_PALETTE", "day")
assert PALETTE in ("day", "dusk"), PALETTE
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Meadow", "Meadow.fbx" if PALETTE == "day" else "Meadow_Dusk.fbx")
# The look (V4 review: "more polygons", "smooth shading on the ground", "nothing boxy"):
#   faceted  the first low-poly look: flat faces, one colour a face
#   smooth   the same shapes with more polygons, smooth shaded, colours blended from point to point
#   rich     smooth, plus shaped detail: clumpy canopies, flared trunks with branches, scalloped pines, chiselled
#            mossy rocks, curved grass blades, a floor that rolls away beyond the meadow
#   lush     rich, with canopies and blossom clouds covered in overlapping leaf and petal scales, many-petalled
#            daisies and clearer, more saturated colours (owner, 2026-11-19: a lush, polished storybook look; general
#            ideas only, no other game's shapes or characters)
STYLE = os.environ.get("MEADOW_STYLE", "lush")  # the game's look (owner, 2026-11-19)
assert STYLE in ("faceted", "smooth", "rich", "lush"), STYLE
SMOOTH, RICH, LUSH = STYLE != "faceted", STYLE in ("rich", "lush"), STYLE == "lush"
TRIAL = os.environ.get("MEADOW_EXPORT")  # a look trial writes somewhere else and leaves the .blend alone
if TRIAL:
    EXPORT = TRIAL


def hexes(*codes):
    return [tuple(round(int(c[i:i + 2], 16) / 255, 3) for i in (1, 3, 5)) for c in codes]


# Grown-up colours (the teen/adult refinement): less saturated than the first sunny palette, so the characters' own
# colours carry. Every list keeps the same length in both palettes, so the seeded random choices (and so the shapes)
# are identical.
if PALETTE == "day":  # golden hour: warm, soft greens under a peach-to-blue sky
    GRASS = hexes("#7FA55A", "#86AD5F", "#789E55", "#82A85C")
    HILL = hexes("#5F8A5A", "#6F9A62", "#55805A")
    LEAVES = hexes("#4C7A48", "#5A8A52", "#406A42")
    BLOSSOM = hexes("#D9A9A0", "#E0B5AC")
    BLOSSOM_RICH = hexes("#F4CDD6", "#EBB0C1", "#E097B0", "#F7DCE2")  # cherry: pale to deep pink
    FLORET = hexes("#FBEFF1")[0]
    PETALS = hexes("#E6C45A", "#B49AD6", "#7FB8BE", "#D98A8A", "#F0ECE0")
    CLOUD = hexes("#F2EBE0")[0]
    SKY_HORIZON, SKY_ZENITH = hexes("#F2D9B8", "#6E93C4")
    GROUND_LO, GROUND_HI = hexes("#789E55", "#8AB262")
else:  # dusk: deep teal grass under a coral-to-indigo evening sky
    GRASS = hexes("#4F7A58", "#557F5E", "#4A7454", "#527C5B")
    HILL = hexes("#3C5F52", "#44695A", "#36574A")
    LEAVES = hexes("#35584A", "#3D6252", "#2F4F42")
    BLOSSOM = hexes("#C98FA8", "#D49DB4")
    BLOSSOM_RICH = hexes("#D8A2BC", "#C98AAA", "#B9779B", "#E3B6CA")
    FLORET = hexes("#EED8E4")[0]
    PETALS = hexes("#E6B422", "#B49AD6", "#7FB8BE", "#E6B422", "#F0ECE0")
    CLOUD = hexes("#D8CFE0")[0]
    SKY_HORIZON, SKY_ZENITH = hexes("#E09A7A", "#2B3A6B")
    GROUND_LO, GROUND_HI = hexes("#4A7454", "#5C8664")
if LUSH and PALETTE == "day":  # clearer and more saturated: bright leaves under a clean blue sky
    GRASS = hexes("#6AB040", "#74BA46", "#62A63B", "#6FB543")
    HILL = hexes("#4E9A3E", "#5EAA48", "#438A38")
    LEAVES = hexes("#3E9433", "#4BA63A", "#33802C")
    BLOSSOM_RICH = hexes("#F59BB8", "#EE7FA3", "#E0658F", "#F9BDD0")
    FLORET = hexes("#FFF4F7")[0]
    SKY_HORIZON, SKY_ZENITH = hexes("#DDF0F7", "#3F87D8")
    CLOUD = hexes("#FFFDF8")[0]
    GROUND_LO, GROUND_HI = hexes("#62A63B", "#7CC24C")
elif LUSH:
    LEAVES = hexes("#2F6A4A", "#387856", "#285C40")
    BLOSSOM_RICH = hexes("#D98AB0", "#CB73A0", "#B95F8E", "#E6A8C6")
DAISY = hexes("#FFFFFF", "#F6B8D2", "#BFD8FF", "#FFF0A8", "#FFFFFF") if PALETTE == "day" else hexes("#E8E4F0", "#D8A2C4", "#9FB8E6", "#E8D890", "#E8E4F0")
DAISY_EYE = hexes("#F2B620", "#4A7FD6", "#E86A9A")
BARK = (0.50, 0.34, 0.22)
ROCK = [(0.70, 0.70, 0.72), (0.62, 0.63, 0.66)]
CENTRE = (1.0, 0.80, 0.25)
STEM = (0.30, 0.58, 0.28)
WOOD = (0.62, 0.43, 0.27)
WOOD_DARK = (0.48, 0.32, 0.20)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ---- building blocks: bmesh pieces with a colour per face ----
class Piece:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.colours = {}  # face -> rgb
        self.tone = {}     # vert -> brightness (smooth looks: lighter where a shape faces the sky)
        self.sharp = None  # smooth looks: edges sharper than this angle (degrees) stay crisp; None: all smooth
        self.fixed = set()  # faces wound by hand (leaf scales, petals): never re-wound
        self.normals = {}   # vert -> normal to shade with (leaf scales: the clump's, so it lights like one soft mass)

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


def ico(piece, centre, radius, rgb, subdiv=1, squash=(1, 1, 1), wobble=0.12, clumps=0.0, lit=(0.80, 1.12)):
    """A wobbly ball. Smooth looks: one more subdivision, and lighter toward the sky (lit: brightness underneath and on
    top). clumps (rich look): leafy bumps, as a share of the radius."""
    if SMOOTH and subdiv > 0:
        subdiv += 1
    geom = bmesh.ops.create_icosphere(piece.bm, subdivisions=subdiv, radius=radius)
    verts = geom["verts"]
    for v in verts:
        n = noise.noise(v.co * 3.0 + Vector((centre[0], centre[1], centre[2])))
        up = v.co.normalized().z
        piece.tone[v] = lit[0] + (lit[1] - lit[0]) * (0.5 + 0.5 * up)
        v.co *= 1.0 + wobble * n
        if RICH and clumps > 0.0:
            near = noise.voronoi((v.co + Vector(centre)) * (2.6 / radius))[0][0]
            v.co *= 1.0 + clumps * (0.45 - min(near, 0.9))
            piece.tone[v] *= 1.0 - 0.22 * max(0.0, min(1.0, near * 1.6 - 0.25))  # the creases between clumps are darker
        v.co.x *= squash[0]
        v.co.y *= squash[1]
        v.co.z *= squash[2]
        v.co += Vector(centre)
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        piece.colours[f] = jitter(rgb, 0.03)
    return faces


def cone(piece, base, radius1, radius2, depth, rgb, segments=8):
    if SMOOTH:
        segments = max(segments, 6) * 2
    geom = bmesh.ops.create_cone(piece.bm, cap_ends=True, segments=segments, radius1=radius1, radius2=radius2, depth=depth)
    verts = geom["verts"]
    for v in verts:
        v.co += Vector(base) + Vector((0, 0, depth / 2))
    faces = list({f for v in verts for f in v.link_faces})
    for f in faces:
        piece.colours[f] = jitter(rgb, 0.02)
    return faces


def box(piece, centre, size, rgb):
    before = set(piece.bm.faces)
    geom = bmesh.ops.create_cube(piece.bm, size=1.0)
    for v in geom["verts"]:
        v.co.x = centre[0] + v.co.x * size[0]
        v.co.y = centre[1] + v.co.y * size[1]
        v.co.z = centre[2] + v.co.z * size[2]
    faces = list({f for v in geom["verts"] for f in v.link_faces})
    if SMOOTH:  # no knife edges: every edge of the block rounded (the wooden sign)
        edges = list({e for f in faces for e in f.edges})
        bmesh.ops.bevel(piece.bm, geom=edges, offset=min(size) * 0.16, segments=3, profile=0.5, affect="EDGES")
        faces = [f for f in piece.bm.faces if f not in before]  # the block's faces, rounded ones included
    for f in faces:
        piece.colours[f] = rgb
    return faces


def finish(piece):
    """The piece as a Blender object, its face colours painted into the corner colour attribute "Col"."""
    mesh = bpy.data.meshes.new(piece.name)
    bm = piece.bm
    bm.faces.ensure_lookup_table()
    colours = [piece.colours.get(f, (1, 1, 1)) for f in bm.faces]
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in piece.fixed])
    bm.normal_update()
    bm.verts.ensure_lookup_table()
    shading = [piece.normals.get(v, v.normal) for v in bm.verts] if piece.normals else None
    blended = []
    if SMOOTH:
        bm.verts.ensure_lookup_table()
        for v in bm.verts:
            near = [piece.colours.get(f, (1, 1, 1)) for f in v.link_faces] or [(1, 1, 1)]
            k = piece.tone.get(v, 1.0)
            blended.append(tuple(max(0.0, min(1.0, sum(c[i] for c in near) / len(near) * k)) for i in range(3)))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(piece.name, mesh)
    bpy.context.collection.objects.link(obj)
    col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    if not SMOOTH:
        for poly in mesh.polygons:
            rgb = colours[poly.index] if poly.index < len(colours) else (1, 1, 1)
            for li in poly.loop_indices:
                col.data[li].color = (rgb[0], rgb[1], rgb[2], 1.0)
        for poly in mesh.polygons:
            poly.use_smooth = False
        return obj
    # smooth looks: each point takes the blend of the faces round it (no hard-edged colour patches), lighter where
    # the shape faces the sky
    for poly in mesh.polygons:
        poly.use_smooth = True
    for li, loop in enumerate(mesh.loops):
        rgb = blended[loop.vertex_index]
        col.data[li].color = (rgb[0], rgb[1], rgb[2], 1.0)
    if piece.sharp is not None:
        mesh.set_sharp_from_angle(angle=math.radians(piece.sharp))
    if shading:
        mesh.normals_split_custom_set_from_vertices([Vector(n).normalized() for n in shading])
    return obj


# ---- the pieces ----
def trunk(p, radius, height, segments=7):
    """A tree's trunk. Rich look: it flares into the ground and two short boughs reach up into the leaves."""
    cone(p, (0, 0, 0), radius, radius * 0.62, height, BARK, segments=segments)
    if not RICH:
        return
    cone(p, (0, 0, 0), radius * 1.9, radius * 0.98, height * 0.16, BARK, segments=segments)
    for (ax, ay) in ((0.55, 0.2), (-0.5, -0.3)):
        geom = bmesh.ops.create_cone(p.bm, cap_ends=True, segments=10, radius1=radius * 0.5, radius2=radius * 0.25, depth=height * 0.5)
        for v in geom["verts"]:
            t = v.co.z / (height * 0.5) + 0.5
            v.co = Vector((ax * t * height * 0.42, ay * t * height * 0.42, height * 0.62 + t * height * 0.42)) + Vector((v.co.x, v.co.y, 0))
        for f in {f for v in geom["verts"] for f in v.link_faces}:
            p.colours[f] = jitter(BARK, 0.02)


def shingles(p, centre, radius, squash, colours, leaf, dice, round_tip=False, lit=(0.6, 1.16)):
    """Lush look: a clump covered in overlapping scales (leaves, or petals with round_tip), each a folded kite whose
    tip points down and out like a roof tile, dark at its root and light at its tip, over a dark core that fills the
    gaps. The scales shade with the clump's own normal, so the clump lights as one soft mass and the scales give it
    its texture. Seeded by 'dice' only."""
    c = Vector(centre)
    sx, sy, sz = squash
    core = ico(p, centre, radius * 0.9, mix(colours[0], (0.0, 0.0, 0.0), 0.3), subdiv=1, squash=squash, wobble=0.04, lit=(0.62, 0.9))
    for v in {v for f in core for v in f.verts}:
        p.normals[v] = Vector((0, 0, -1))  # the inside of the clump is in shade: no shine shows between the scales
    mean = radius * (sx * sy * sz) ** (1 / 3)
    count = int(4 * math.pi * mean * mean / (leaf * leaf * 0.26))  # each scale overlaps its neighbours
    for i in range(count):
        z = 1 - 2 * (i + 0.5) / count
        if z < -0.8:
            continue  # the very bottom is never seen: the core covers it
        rr = math.sqrt(max(0.0, 1 - z * z))
        th = i * 2.399963 + dice.uniform(-0.15, 0.15)
        d = Vector((rr * math.cos(th), rr * math.sin(th), z))
        pos = c + Vector((d.x * radius * sx, d.y * radius * sy, d.z * radius * sz)) * dice.uniform(0.97, 1.05)
        n = Vector((d.x / sx, d.y / sy, d.z / sz)).normalized()
        down = Vector((0, 0, -1)) - n * (-n.z)
        if down.length < 0.15:  # on top: lie the scale any way round
            a = dice.uniform(0, math.tau)
            down = Vector((math.cos(a), math.sin(a), 0)) - n * (n.x * math.cos(a) + n.y * math.sin(a))
        down.normalize()
        side = n.cross(down).normalized()
        L = leaf * dice.uniform(0.85, 1.2)
        W = L * (0.95 if round_tip else 0.82)
        lift = 0.08 if round_tip else 0.1
        ring = [p.bm.verts.new(pos + down * L * dd + side * W * ss + n * L * nn) for dd, ss, nn in (
            (-0.35, 0.0, -0.12), (-0.08, 0.42, 0.03), (0.33, 0.45, 0.06), (0.65, 0.0, lift), (0.33, -0.45, 0.06), (-0.08, -0.42, 0.03))]
        mid = p.bm.verts.new(pos + down * L * 0.15 + n * L * 0.12)  # the fold along the middle
        base, tip = ring[0], ring[3]
        up = lit[0] + (lit[1] - lit[0]) * (0.5 + 0.5 * n.z)
        kn = (tip.co - base.co).cross(ring[1].co - ring[5].co).normalized()
        if kn.dot(n) < 0:
            kn = -kn
        shade_n = (n * 0.55 + kn * 0.45).normalized()  # mostly the clump's, a little the scale's own: the light
        for v, t in zip(ring + [mid], (0.76, 0.9, 1.0, 1.1, 1.0, 0.9, 1.03)):  # breaks into glints, not one shiny spot
            p.tone[v] = t * up * dice.uniform(0.97, 1.03)
            p.normals[v] = shade_n
        rgb = jitter_with(dice, colours[0] if dice.random() < 0.55 else dice.choice(colours), 0.02)
        for k in range(6):  # an oval leaf, fanned round its fold
            tri = (ring[k], ring[(k + 1) % 6], mid)
            g = (tri[1].co - tri[0].co).cross(tri[2].co - tri[0].co)
            f = p.bm.faces.new(tri if g.dot(n) > 0 else tri[::-1])
            p.colours[f] = rgb
            p.fixed.add(f)


def jitter_with(dice, rgb, amount):
    return tuple(max(0.0, min(1.0, c + dice.uniform(-amount, amount))) for c in rgb)


def tree_round(name, height=3.2):
    p = Piece(name)
    trunk(p, 0.16, height * 0.55)
    top = height * 0.55
    if LUSH:
        dice = random.Random(sum(map(ord, name)) + 41)  # the same in both palettes
        for (x, y, z, r) in ((0, 0, 0.55, 0.95), (0.55, 0.15, 0.25, 0.62), (-0.5, -0.2, 0.3, 0.58), (0.05, 0.45, 1.15, 0.55)):
            rng.choice(LEAVES)
            shingles(p, (x, y, top + z), r, (1, 1, 0.85), LEAVES, 0.2, dice)
        return finish(p)
    ico(p, (0, 0, top + 0.55), 0.95, rng.choice(LEAVES), subdiv=2, squash=(1, 1, 0.85), clumps=0.2)
    ico(p, (0.55, 0.15, top + 0.25), 0.62, rng.choice(LEAVES), subdiv=1, clumps=0.2)
    ico(p, (-0.5, -0.2, top + 0.3), 0.58, rng.choice(LEAVES), subdiv=1, clumps=0.2)
    ico(p, (0.05, 0.45, top + 1.15), 0.55, rng.choice(LEAVES), subdiv=1, clumps=0.2)
    return finish(p)


def tree_pine(name, height=3.8):
    p = Piece(name)
    trunk(p, 0.13, height * 0.3, segments=6)
    p.sharp = 50  # each tier's hem stays crisp
    for i, (r, z, d) in enumerate([(1.05, 0.30, 1.4), (0.85, 0.55, 1.2), (0.62, 0.78, 1.0), (0.38, 0.95, 0.8)]):
        faces = cone(p, (0, 0, height * z - d * 0.35), r, 0.02, d, LEAVES[(i + 1) % len(LEAVES)], segments=8)
        low = height * z - d * 0.35
        hem = sorted({v for f in faces for v in f.verts if abs(v.co.z - low) < 1e-4 and v.co.xy.length > r * 0.5},
                     key=lambda v: math.atan2(v.co.y, v.co.x))
        for v in {v for f in faces for v in f.verts}:
            p.tone[v] = 0.78 + 0.34 * max(0.0, min(1.0, (v.co.z - low) / d))  # darker under each skirt, light at its tip
        if RICH:  # a scalloped hem: the boughs droop in pairs
            for k, v in enumerate(hem):
                if k % 2 == 0:
                    v.co.z -= d * 0.11
                    v.co.xy *= 1.07
                else:
                    v.co.xy *= 0.9
    return finish(p)


def tube(p, path, radii, rgb, sides=8):
    """A branch: rings of 'sides' points swept along 'path' (Vectors), radius radii[k] at path[k], capped at the tip."""
    rings = []
    for k, at in enumerate(path):
        ahead = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
        side = ahead.cross(Vector((0.3, 0.7, 0.1)) if abs(ahead.z) > 0.9 else Vector((0, 0, 1))).normalized()
        up = side.cross(ahead).normalized()
        ring = []
        for s in range(sides):
            a = math.tau * s / sides
            v = p.bm.verts.new(at + (side * math.cos(a) + up * math.sin(a)) * radii[k])
            p.tone[v] = 0.82 + 0.1 * math.sin(a)
            ring.append(v)
        rings.append(ring)
    for r in range(len(rings) - 1):
        for s in range(sides):
            f = p.bm.faces.new((rings[r][s], rings[r][(s + 1) % sides], rings[r + 1][(s + 1) % sides], rings[r + 1][s]))
            p.colours[f] = jitter(rgb, 0.015)
    tip = p.bm.verts.new(path[-1] + (path[-1] - path[-2]).normalized() * radii[-1])
    for s in range(sides):
        f = p.bm.faces.new((rings[-1][s], rings[-1][(s + 1) % sides], tip))
        p.colours[f] = rgb


def floret(p, at, normal, size, rgb, dice):
    """A tiny five-petal blossom lying on the canopy, facing out: the detail that says 'cherry' up close."""
    n = normal.normalized()
    side = n.cross(Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))).normalized()
    up = side.cross(n)
    turn = dice.uniform(0, math.tau)
    middle = p.bm.verts.new(at + n * 0.004)
    p.tone[middle] = 1.0
    rim = []
    for k in range(10):  # petal tips and the notches between them
        a = turn + math.tau * k / 10
        r = size if k % 2 == 0 else size * 0.45
        v = p.bm.verts.new(at + (side * math.cos(a) + up * math.sin(a)) * r)
        p.tone[v] = 1.05
        rim.append(v)
    for k in range(10):
        f = p.bm.faces.new((middle, rim[k], rim[(k + 1) % 10]))
        p.colours[f] = rgb


def tree_blossom_rich(p, height):
    """A cherry in bloom, modelled rather than balled (owner, 2026-11-17: "the pink blossom trees look like blobs
    still"): a leaning trunk that forks into three limbs and their twigs, each twig ending in a spray of small petal
    puffs in four pinks, the bark showing through the gaps, little five-petal florets on the outside and a ring of
    fallen petals on the grass. Its own seeded random, so the rest of the meadow keeps its shapes."""
    dice = random.Random(2611)
    bark = mix(BARK, (0.30, 0.19, 0.20), 0.55)  # cherry bark: darker, a little plum
    fork = Vector((0.16, 0.05, height * 0.34))  # a short trunk with a lean and a kink
    tube(p, [Vector((0, 0, -0.05)), Vector((-0.02, 0, height * 0.1)), Vector((0.07, 0.01, height * 0.22)), fork,
             fork + Vector((0.03, 0, 0.22))],  # on past the fork, so the limbs grow out of wood, not off a cut end
         [0.19, 0.14, 0.125, 0.115, 0.07], bark, sides=10)
    tips = []
    for k, turn in enumerate((10, 130, 250)):
        a = math.radians(turn + dice.uniform(-20, 20))
        out = Vector((math.cos(a), math.sin(a), 0))
        reach = dice.uniform(0.85, 1.1)
        rise = height * dice.uniform(0.3, 0.42)
        # a limb that leaves steeply and bends out: four points on a curve, not a straight stick
        pts = [fork + out * reach * t + Vector((0, 0, rise * math.sin(t * math.pi * 0.62) / math.sin(math.pi * 0.62)))
               for t in (0.0, 0.3, 0.62, 1.0)]
        tube(p, pts, [0.095, 0.075, 0.058, 0.04], bark)
        tips.append(pts[-1])
        for side, at in ((-1, 1), (1, 2)):  # two twigs off each limb, from different points
            b = a + side * math.radians(dice.uniform(40, 65))
            o2 = Vector((math.cos(b), math.sin(b), 0))
            twig = pts[at] + o2 * dice.uniform(0.38, 0.55) + Vector((0, 0, dice.uniform(0.15, 0.32)))
            tube(p, [pts[at], (pts[at] + twig) / 2 + Vector((0, 0, 0.06)), twig], [0.045, 0.032, 0.02], bark, sides=6)
            tips.append(twig)
    tips.append(fork + Vector((0.05, 0, height * 0.55)))  # a crown cloud over the fork
    if LUSH:  # each tip in a round cloud of overlapping petals
        for tip in tips:
            size = dice.uniform(0.4, 0.52)
            shingles(p, tuple(tip + Vector((0, 0, size * 0.3))), size, (1.1, 1.1, 0.8), BLOSSOM_RICH, 0.125, dice, round_tip=True, lit=(0.66, 1.12))
        tips = []
    for tip in tips:  # a broad, flat-bottomed cloud of blossom over each tip: a core and a crust of small bumps
        size = dice.uniform(0.42, 0.55)
        centre = tip + Vector((0, 0, size * 0.25))
        tint = dice.choice(BLOSSOM_RICH)
        ico(p, tuple(centre), size * 0.8, tint, subdiv=1, squash=(1.15, 1.15, 0.6), wobble=0.08, lit=(0.66, 1.0))
        for _ in range(11):
            a = dice.uniform(0, math.tau)
            up = dice.uniform(-0.15, 0.95)
            on = Vector((math.cos(a) * math.sqrt(1 - up * up) * 1.05, math.sin(a) * math.sqrt(1 - up * up) * 1.05, up * 0.58)) * size * 0.85
            r = size * dice.uniform(0.26, 0.4)
            shade = tint if dice.random() < 0.6 else dice.choice(BLOSSOM_RICH)
            faces = ico(p, tuple(centre + on), r, shade, subdiv=1, squash=(1, 1, 0.72), wobble=0.12, lit=(0.7, 1.14))
            if dice.random() < 0.7:
                outward = (on + Vector((0, 0, 0.3))).normalized()
                f = max(faces, key=lambda f: (f.calc_center_median() - (centre + on)).normalized().dot(outward))
                floret(p, f.calc_center_median(), f.normal if f.normal.length > 0 else outward, r * 0.38, FLORET, dice)
    for _ in range(36):  # fallen petals in a ring on the grass under the crown
        a = dice.uniform(0, math.tau)
        d = math.sqrt(dice.uniform(0.09, 1.0)) * 1.25
        at = Vector((math.cos(a) * d, math.sin(a) * d, 0.012))
        floret(p, at, Vector((0, 0, 1)), dice.uniform(0.03, 0.05), dice.choice(BLOSSOM_RICH), dice)
    p.bm.normal_update()


def tree_blossom(name, height=2.8):
    p = Piece(name)
    if RICH:
        for _ in range(5):
            rng.choice(BLOSSOM)  # the same draws as the other looks, so the meadow's other pieces keep their shapes
        tree_blossom_rich(p, height)
        return finish(p)
    trunk(p, 0.14, height * 0.5)
    top = height * 0.5
    for (x, y, z, r) in [(0, 0, 0.5, 0.85), (0.6, 0.2, 0.3, 0.55), (-0.55, -0.15, 0.35, 0.55), (0.1, -0.5, 0.75, 0.5), (-0.1, 0.45, 0.85, 0.5)]:
        ico(p, (x, y, top + z), r, rng.choice(BLOSSOM), subdiv=1, clumps=0.16, lit=(0.86, 1.08))
    return finish(p)


def bush(name, size=0.7):
    p = Piece(name)
    dice = random.Random(sum(map(ord, name)) + 7)
    for i in range(rng.randint(3, 4)):
        a = rng.uniform(0, math.tau)
        d = rng.uniform(0.0, size * 0.5)
        if LUSH:
            shingles(p, (math.cos(a) * d, math.sin(a) * d, size * 0.45), size * rng.uniform(0.45, 0.6), (1, 1, 0.8), LEAVES, 0.12, dice)
            continue
        ico(p, (math.cos(a) * d, math.sin(a) * d, size * 0.45), size * rng.uniform(0.45, 0.6), rng.choice(LEAVES), subdiv=1, squash=(1, 1, 0.8), clumps=0.18)
    # a few berries or blossoms on top
    for i in range(5):
        a = rng.uniform(0, math.tau)
        ico(p, (math.cos(a) * size * 0.45, math.sin(a) * size * 0.45, size * rng.uniform(0.5, 0.8)), 0.05, rng.choice(PETALS), subdiv=0, wobble=0)
    return finish(p)


def rock(name, size=0.5):
    p = Piece(name)
    faces = ico(p, (0, 0, size * 0.25), size, rng.choice(ROCK), subdiv=1, squash=(1.2, 0.9, 0.55), wobble=0.3, lit=(0.86, 1.06))
    if RICH:  # chiselled: a few flat cuts with crisp edges, and moss where it faces the sky
        p.sharp = 28
        verts = {v for f in faces for v in f.verts}
        middle = Vector((0, 0, size * 0.25))
        for (dx, dy, dz, reach) in ((0.8, 0.3, 0.5, 0.62), (-0.6, 0.6, 0.45, 0.6), (0.1, -0.85, 0.5, 0.56), (-0.2, 0.1, 1.0, 0.44)):
            n = Vector((dx, dy, dz)).normalized()
            for v in verts:
                over = (v.co - middle).dot(n) - reach * size
                if over > 0:
                    v.co -= n * over
        p.bm.normal_update()
        moss = mix(GRASS[0], ROCK[1], 0.3)
        for f in faces:
            if f.normal.z > 0.8 and f.calc_center_median().z > size * 0.45:
                p.colours[f] = moss
    return finish(p)


HILLS = os.environ.get("MEADOW_HILLS", "cliffs")  # lush look: "rolling" grass hills or "cliffs" (grass-topped rock)
assert HILLS in ("rolling", "cliffs"), HILLS


def hill_rolling(p, radius, height, seed):
    """A long, low hill of two or three swells whose foot runs out flat into the meadow (no edge where it meets the
    grass), the colour of the meadow at its foot and lighter up its crests (owner, 2026-11-19: "the green mounds are
    so strange")."""
    dice = random.Random(seed)
    swells = [(dice.uniform(-0.45, -0.25), dice.uniform(-0.1, 0.1), 1.0), (dice.uniform(0.05, 0.2), dice.uniform(-0.15, 0.15), dice.uniform(0.6, 0.8)),
              (dice.uniform(0.4, 0.55), dice.uniform(-0.1, 0.1), dice.uniform(0.4, 0.6))]
    def h(x, y):
        z = 0.0
        for (cx, cy, k) in swells:
            dx, dy = x / radius - cx, (y / radius - cy) / 0.55
            z += k * math.exp(-(dx * dx + dy * dy) / (2 * 0.2 ** 2))
        edge = min(1.0, math.hypot(x / radius, y / (radius * 0.6)))
        return height * z * (1 - edge ** 6)
    rings, spokes = 18, 56
    grid = []
    for i in range(rings + 1):
        t = i / rings
        row = []
        for j in range(spokes):
            a = math.tau * j / spokes
            x, y = math.cos(a) * radius * t, math.sin(a) * radius * 0.6 * t
            z = h(x, y) * (1 + 0.03 * noise.noise(Vector((x, y, 0)) * 0.4))
            v = p.bm.verts.new((x, y, z))
            crest = max(0.0, min(1.0, z / height))
            p.tone[v] = 0.9 + 0.22 * crest
            row.append(v)
        grid.append(row)
    centre = p.bm.verts.new((0, 0, h(0, 0)))
    for j in range(spokes):
        f = p.bm.faces.new((centre, grid[1][j], grid[1][(j + 1) % spokes]))
        p.colours[f] = HILL[1]
    for i in range(1, rings):
        for j in range(spokes):
            f = p.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][(j + 1) % spokes], grid[i][(j + 1) % spokes]))
            z = f.calc_center_median().z / height
            p.colours[f] = mix(GROUND_LO, HILL[1] if z > 0.45 else HILL[0], min(1.0, z * 2.5))  # the meadow's colour at the foot


def hill_cliff(p, radius, height, seed):
    """A grass-topped rock: steep walls of stacked rock bands, each a little in from the one below, under a grassy top
    that hangs a lip over the edge, with a few trails of ivy down the face."""
    dice = random.Random(seed)
    n = 28
    shape = [radius * (0.55 + 0.45 * (0.5 + 0.5 * noise.noise(Vector((math.cos(math.tau * k / n), math.sin(math.tau * k / n), seed * 0.1)) * 1.6)))
             for k in range(n)]
    def ring(scale, z, sy=0.7):
        return [p.bm.verts.new((math.cos(math.tau * k / n) * shape[k] * scale * (1 + 0.04 * noise.noise(Vector((k * 0.7, z * 1.3, seed)))),
                                math.sin(math.tau * k / n) * shape[k] * scale * sy * (1 + 0.04 * noise.noise(Vector((k * 0.7, z * 1.3, seed + 5)))), z)) for k in range(n)]
    bands = 3
    rock = [hexes("#9C8C7A")[0], hexes("#86776A")[0]] if PALETTE == "day" else [hexes("#6E6478")[0], hexes("#5E566A")[0]]  # warm sandstone
    top = height
    prev = ring(1.0, -0.2)
    for b in range(bands):
        z0, z1 = top * b / bands, top * (b + 1) / bands
        scale = 1.0 - 0.06 * b + dice.uniform(-0.015, 0.015)
        lo, hi = ring(scale + 0.03, z0), ring(scale, z1 - 0.06)
        shelf = ring(scale - 0.02, z1)
        for k in range(n):
            m = (k + 1) % n
            for (a, c), tone in (((prev, lo), 0.8), ((lo, hi), 0.95), ((hi, shelf), 0.88)):
                f = p.bm.faces.new((a[k], a[m], c[m], c[k]))
                p.colours[f] = jitter(rock[b % 2], 0.02)
            for v in hi:
                p.tone[v] = 1.0
            for v in lo:
                p.tone[v] = 0.84
        prev = shelf
    # the grassy top: a lip that hangs over the edge, then a gentle dome
    lip_under = ring(1.0 - 0.06 * bands + 0.02, top - 0.05)
    lip = ring(1.0 - 0.06 * bands + 0.07, top + 0.12)
    crown = ring(0.7, top + 0.35)
    for k in range(n):
        m = (k + 1) % n
        for (a, c), col in (((prev, lip_under), GRASS[2]), ((lip_under, lip), GRASS[1]), ((lip, crown), GRASS[0])):
            f = p.bm.faces.new((a[k], a[m], c[m], c[k]))
            p.colours[f] = jitter(col, 0.02)
    peak = p.bm.verts.new((0, 0, top + 0.5))
    for k in range(n):
        f = p.bm.faces.new((crown[k], crown[(k + 1) % n], peak))
        p.colours[f] = GRASS[3]
    for v in lip_under:
        p.tone[v] = 0.8
    for v in crown + [peak, *lip]:
        p.tone[v] = 1.1
    for _ in range(5):  # ivy trailing down from the lip
        k = dice.randrange(n)
        a = math.tau * k / n
        out = Vector((math.cos(a), math.sin(a) * 0.7, 0))
        r = shape[k] * (1.0 - 0.06 * bands + 0.08)
        length = dice.uniform(0.35, 0.7) * top
        w = 0.12
        side = Vector((-out.y, out.x, 0)).normalized()
        pts = [Vector((math.cos(a) * r, math.sin(a) * r * 0.7, top + 0.1 - length * t)) + out * (0.04 - 0.03 * t) for t in (0, 0.5, 1)]
        vs = [(p.bm.verts.new(q + side * w * (1 - 0.6 * t)), p.bm.verts.new(q - side * w * (1 - 0.6 * t))) for q, t in zip(pts, (0, 0.5, 1))]
        for i in range(2):
            f = p.bm.faces.new((vs[i][0], vs[i][1], vs[i + 1][1], vs[i + 1][0]))
            p.colours[f] = LEAVES[1]


def hill(name, radius=4.0, height=2.0):
    p = Piece(name)
    if LUSH:
        seed = sum(map(ord, name)) * 13
        if HILLS == "rolling":
            hill_rolling(p, radius * 1.5, height * 0.9, seed)
        else:
            p.sharp = 35
            hill_cliff(p, radius * 1.05, height * 1.1, seed)
        bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts, dist=1e-4)
        return finish(p)
    geom = bmesh.ops.create_uvsphere(p.bm, u_segments=40 if SMOOTH else 20, v_segments=20 if SMOOTH else 10, radius=1.0)
    verts = geom["verts"]
    for v in verts:
        p.tone[v] = 0.86 + 0.24 * max(0.0, v.co.z)  # lighter toward the crest
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


def daisy(p, at, height, dice):
    """Lush look: a daisy, a dozen long petals round a domed eye, tipped up a little toward the sky."""
    K = 1.7  # big enough to read across the clearing
    cone(p, at, 0.01, 0.007, height, STEM, segments=5)
    head = Vector((at[0], at[1], at[2] + height))
    petal, eye = dice.choice(DAISY), dice.choice(DAISY_EYE)
    tilt = Vector((dice.uniform(-0.25, 0.25), dice.uniform(-0.25, 0.25), 1)).normalized()
    side = tilt.cross(Vector((1, 0, 0))).normalized()
    fwd = side.cross(tilt)
    turn = dice.uniform(0, math.tau)
    for k in range(12):
        a = turn + math.tau * k / 12
        out = side * math.cos(a) + fwd * math.sin(a)
        across = tilt.cross(out)
        root = head + out * 0.012 * K
        verts = [p.bm.verts.new(root), p.bm.verts.new(root + out * 0.03 * K + across * 0.009 * K + tilt * 0.006 * K),
                 p.bm.verts.new(root + out * 0.058 * K - tilt * 0.004 * K), p.bm.verts.new(root + out * 0.03 * K - across * 0.009 * K + tilt * 0.006 * K)]
        for v, t in zip(verts, (0.9, 1.05, 1.12, 1.05)):
            p.tone[v] = t
            p.normals[v] = tilt
        for tri in ((verts[0], verts[1], verts[2]), (verts[0], verts[2], verts[3])):
            g = (tri[1].co - tri[0].co).cross(tri[2].co - tri[0].co)
            f = p.bm.faces.new(tri if g.dot(tilt) > 0 else tri[::-1])
            p.colours[f] = petal
            p.fixed.add(f)
    ico(p, tuple(head + tilt * 0.006 * K), 0.02 * K, eye, subdiv=1, squash=(1, 1, 0.55), wobble=0, lit=(0.85, 1.1))


def flower(p, at, rgb, height):
    if LUSH:
        daisy(p, at, height, random.Random(int(at[0] * 1e4) * 31 + int(at[1] * 1e4)))
        return
    cone(p, at, 0.012, 0.008, height, STEM, segments=3 if SMOOTH else 4)
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


def grass_blades(name):
    """Rich look: a tuft of curved blades, dark at the root and light at the tip, each bending its own way."""
    p = Piece(name)
    p.sharp = 40
    for i in range(11):
        a = rng.uniform(0, math.tau)
        d = rng.uniform(0, 0.09)
        h = rng.uniform(0.16, 0.36)
        out = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-out.y, out.x, 0))
        base = out * d
        colour = jitter(rng.choice(GRASS), 0.04)
        rings = []
        for k in range(4):  # root, two bends, then the tip
            t = k / 4
            centre = base + out * (0.11 * t * t) + Vector((0, 0, h * t))
            w = 0.022 * (1 - t * 0.75)
            ring = [p.bm.verts.new(centre + side * w), p.bm.verts.new(centre - side * w), p.bm.verts.new(centre + out * w * 0.8)]
            for v in ring:
                p.tone[v] = 0.72 + 0.5 * t
            rings.append(ring)
        tip = p.bm.verts.new(base + out * 0.11 + Vector((0, 0, h)))
        p.tone[tip] = 1.24
        for k in range(3):
            for j in range(3):
                f = p.bm.faces.new((rings[k][j], rings[k][(j + 1) % 3], rings[k + 1][(j + 1) % 3], rings[k + 1][j]))
                p.colours[f] = colour
        for j in range(3):
            f = p.bm.faces.new((rings[3][j], rings[3][(j + 1) % 3], tip))
            p.colours[f] = colour
    return finish(p)


def grass_tuft(name):
    if RICH:
        return grass_blades(name)
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


CLOUD_SHADE = hexes("#D3DCEA")[0] if PALETTE == "day" else hexes("#9E92B8")[0]  # a cumulus's cool shaded underside


def cloud(name, size=1.4):
    """A cumulus: round puffs, biggest in the middle, with puffs in front, behind and underneath, so it is round all
    the way around from every angle, below included (owner, Fable refinement 2026-11-11: "I like the cumulus but don't
    make it flat at the bottom, make it round all the way around at all angles").
    Lush look (owner, 2026-11-19: "I need them to look like a natural cloud"): billows heaped on billows, smaller
    toward the top, and lit softly in the colours themselves (drawn unlit, so no hard line between light and shade):
    bright on top, cool and grey underneath, a little darker where billows tuck into each other."""
    p = Piece(name)
    puffs = [  # (x along, y across, z up, radius) in units of size
        (0.0, 0.0, 0.14, 0.62), (0.62, 0.10, 0.02, 0.48), (-0.60, -0.08, 0.02, 0.46),
        (1.05, 0.0, -0.05, 0.32), (-1.02, 0.04, -0.05, 0.30),
        (0.20, -0.32, 0.04, 0.42), (-0.16, 0.34, 0.0, 0.40), (0.05, 0.0, -0.20, 0.46),
    ]
    if not LUSH:
        for (x, y, z, r) in puffs:
            ico(p, (x * size, y * size, z * size), r * size, CLOUD, subdiv=2, squash=(1.05, 1.0, 0.92), wobble=0.02, lit=(0.93, 1.03))
        return finish(p)
    dice = random.Random(sum(map(ord, name)) + 3)
    puffs += [(0.25, 0.05, 0.62, 0.36), (-0.3, -0.05, 0.55, 0.32), (0.0, 0.1, 0.88, 0.24), (0.7, -0.1, 0.42, 0.26),
              (-0.72, 0.1, 0.38, 0.24), (0.45, 0.25, 0.3, 0.28), (-0.4, -0.3, 0.32, 0.27)]
    centres = [Vector((x, y, z)) * size for (x, y, z, r) in puffs]
    radii = [r * size for (*_, r) in puffs]
    low, high = min(c.z - r for c, r in zip(centres, radii)), max(c.z + r for c, r in zip(centres, radii))
    for k, (c, r) in enumerate(zip(centres, radii)):
        geom = bmesh.ops.create_icosphere(p.bm, subdivisions=3, radius=r)
        verts = geom["verts"]
        for v in verts:
            n = v.co.normalized()
            v.co *= 1.0 + 0.04 * noise.noise(v.co * (3.0 / r) + Vector((k, 0, 0)))  # a soft, lumpy edge
            v.co.z *= 0.92
            v.co += c
            height = (v.co.z - low) / (high - low)
            light = 0.78 + 0.16 * (0.5 + 0.5 * n.z) + 0.1 * height
            tucked = sum(max(0.0, 1 - (v.co - o).length / (ro * 1.05)) for j, (o, ro) in enumerate(zip(centres, radii)) if j != k)
            p.tone[v] = light * (1 - 0.12 * min(1.0, tucked * 2))
        for f in {f for v in verts for f in v.link_faces}:
            z = (f.calc_center_median().z - low) / (high - low)
            p.colours[f] = mix(CLOUD_SHADE, CLOUD, min(1.0, 0.25 + z * 1.2))
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


def ground_height(x, y):
    """The floor is level everywhere (a rolling rise beyond the meadow was tried for the rich look: it showed a dark
    sliver at the horizon, so it is left out)."""
    return 0.0


def ground_colour(x, y):
    """The grass at a point: soft patches from one noise over the whole meadow; the rich look adds sunlit drifts and
    darker clover patches."""
    n = noise.noise(Vector((x * 0.28, y * 0.28, 0.37))) * 0.6 + noise.noise(Vector((x * 0.9, y * 0.9, 1.7))) * 0.25
    t = max(0.0, min(1.0, 0.5 + n))
    rgb = tuple(GROUND_LO[q] + (GROUND_HI[q] - GROUND_LO[q]) * t for q in range(3))
    if RICH:
        drift = max(0.0, noise.noise(Vector((x * 0.17 + 9.0, y * 0.5 - 3.0, 2.2))) - 0.15)  # long light streaks, as wind lays the grass
        clover = max(0.0, noise.noise(Vector((x * 1.6, y * 1.6, 7.7))) - 0.35)
        k = 1.0 + 0.22 * drift - 0.3 * clover
        rgb = tuple(max(0.0, min(1.0, c * k)) for c in rgb)
    return rgb


def ground_sectors(radius=12.0, rings=(0.0, 3.0, 6.0, 9.0, 12.0, 20.0, 31.0)):
    """The meadow floor around the stage (its centre at the origin): ring sectors that meet exactly, coloured by one
    noise over the whole meadow (soft patches that flow across pieces, no checkerboard). Built in place: Unity moves
    each piece's origin to its middle, so the floor still comes apart piece by piece. The two outer rings run on under
    the sky's lower edge (V4 review: "a slight gap between the skybox and the ground" let the real room show: the
    floor stopped at 12 m, the sky's edge is 28 m out and 1.4 m down)."""
    pieces = []
    for r in range(len(rings) - 1):
        r0, r1 = rings[r], rings[r + 1]
        sectors = 4 if r == 0 else 8
        for k in range(sectors):
            p = Piece(f"Meadow_GroundS{r}_{k}")
            a0, a1 = k / sectors * math.tau, (k + 1) / sectors * math.tau
            rad_steps, ang_steps = (6, 128 // sectors) if SMOOTH else (4, 64 // sectors)  # every ring cut into the same steps round: edges meet with no gaps
            grid = []
            for i in range(rad_steps + 1):
                rr = r0 + (r1 - r0) * i / rad_steps
                row = []
                for j in range(ang_steps + 1):
                    aa = a0 + (a1 - a0) * j / ang_steps
                    x, y = rr * math.cos(aa), rr * math.sin(aa)
                    row.append(p.bm.verts.new((x, y, ground_height(x, y))))
                grid.append(row)
            for i in range(rad_steps):
                for j in range(ang_steps):
                    if i == 0 and r0 == 0.0:
                        f = p.bm.faces.new((grid[0][0], grid[1][j], grid[1][j + 1]))
                    else:
                        f = p.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
                    c = f.calc_center_median()
                    p.colours[f] = ground_colour(c.x, c.y)
            bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts, dist=1e-5)
            obj = finish(p)
            if SMOOTH:  # each point its own colour: the grass shades from patch to patch with no facets, and across pieces
                col = obj.data.color_attributes["Col"]
                for loop in obj.data.loops:
                    co = obj.data.vertices[loop.vertex_index].co
                    rgb = ground_colour(co.x, co.y)
                    col.data[loop.index].color = (rgb[0], rgb[1], rgb[2], 1.0)
            pieces.append(obj)
    return pieces


def sky_wedge(name, radius=40.0, wedges=8):
    """One of the sky's wedges: a dome slice, inside facing, blue overhead to pale at the horizon."""
    p = Piece(name)
    rings, steps = 12, 4
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
            horizon, zenith = SKY_HORIZON, SKY_ZENITH
            p.colours[f] = tuple(horizon[i] + (zenith[i] - horizon[i]) * t ** 0.8 for i in range(3))
    obj = finish(p)
    # one smooth gradient: each corner takes the sky's colour at its own height (the per-face colours showed as bands)
    col = obj.data.color_attributes["Col"]
    for loop in obj.data.loops:
        z = obj.data.vertices[loop.vertex_index].co.z
        el = math.asin(max(-1.0, min(1.0, (z + 2.0) / radius)))
        rgb = sky_colour(el)
        col.data[loop.index].color = (rgb[0], rgb[1], rgb[2], 1.0)
    obj.data.flip_normals()  # seen from inside
    return obj


def sky_colour(elevation):
    """The sky's colour at an elevation (radians): the horizon colour rising to the zenith colour."""
    t = max(0.0, min(1.0, elevation / (math.pi / 2)))
    return tuple(SKY_HORIZON[i] + (SKY_ZENITH[i] - SKY_HORIZON[i]) * t ** 0.8 for i in range(3))


def sky_disc(p, direction, distance, radius, rgb, segments=28):
    """A flat disc on the sky facing the middle of the meadow, at 'distance' along 'direction' (a unit vector)."""
    d = Vector(direction).normalized()
    side = d.cross(Vector((0, 0, 1))).normalized()
    up = side.cross(d).normalized()
    centre = d * distance - Vector((0, 0, 2.0))
    middle = p.bm.verts.new(centre)
    rim = [p.bm.verts.new(centre + (side * math.cos(a) + up * math.sin(a)) * radius)
           for a in (k * math.tau / segments for k in range(segments))]
    for k in range(segments):
        f = p.bm.faces.new((middle, rim[k], rim[(k + 1) % segments]))
        p.colours[f] = rgb


def face_inward(obj):
    """Turns every face toward the middle of the meadow: the sky is seen from inside."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    outward = [f for f in bm.faces if f.normal.dot(f.calc_center_median() + Vector((0, 0, 2.0))) > 0]
    bmesh.ops.reverse_faces(bm, faces=outward)
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


SUN_DIRECTION = (0.0, 1.0, 1.19)  # 50 degrees up (the scene light's height); Unity turns the piece toward the light


def sun_or_moon(name):
    """By day a low-poly sun with two soft rings of glow; at dusk a crescent moon. Unity turns it to where the scene's
    light comes from, so the light on every shape agrees with it."""
    p = Piece(name)
    el = math.atan2(SUN_DIRECTION[2], SUN_DIRECTION[1])
    sky = sky_colour(el)
    if PALETTE == "day":
        sun = hexes("#FFF3D0")[0]
        sky_disc(p, SUN_DIRECTION, 37.6, 5.2, mix(sky, sun, 0.22))
        sky_disc(p, SUN_DIRECTION, 37.3, 3.6, mix(sky, sun, 0.5))
        sky_disc(p, SUN_DIRECTION, 37.0, 2.3, sun)
    else:
        moon = hexes("#F4ECDA")[0]
        sky_disc(p, SUN_DIRECTION, 37.6, 4.6, mix(sky, moon, 0.12))
        sky_disc(p, SUN_DIRECTION, 37.3, 2.0, moon)
        d = Vector(SUN_DIRECTION).normalized()
        side = d.cross(Vector((0, 0, 1))).normalized()
        sky_disc(p, tuple(d * 37.0 + side * 0.85 + Vector((0, 0, 0.45))), 37.0, 1.75, mix(sky, moon, 0.12))  # the crescent's shade
    return face_inward(finish(p))


def stars(name, count=90):
    """Small four-point stars over the whole sky (dusk). By day the piece is one tiny speck under the horizon, so both
    palettes have the same pieces. Its own seeded random: the meadow's choices never depend on it."""
    p = Piece(name)
    dice = random.Random(77)
    if PALETTE == "day":
        sky_disc(p, (0.0, 1.0, -0.2), 37.0, 0.01, SKY_HORIZON, segments=3)
        return face_inward(finish(p))
    for _ in range(count):
        az = dice.uniform(0.0, math.tau)
        el = math.asin(dice.uniform(math.sin(math.radians(14)), math.sin(math.radians(86))))  # even over the dome
        d = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
        size = dice.choice((0.12, 0.12, 0.16, 0.16, 0.2, 0.28))  # 3 to 8 pixels across in the headset
        bright = dice.uniform(0.55, 1.0)
        rgb = mix(sky_colour(el), hexes("#FBF4E2")[0], bright)
        side = d.cross(Vector((0, 0, 1))).normalized()
        up = side.cross(d).normalized()
        centre = d * 37.0 - Vector((0, 0, 2.0))
        middle = p.bm.verts.new(centre)
        points = []
        for k in range(8):  # a four-point star: long points up, down and to the sides, short ones between
            a = k * math.tau / 8
            r = size if k % 2 == 0 else size * 0.32
            points.append(p.bm.verts.new(centre + (side * math.cos(a) + up * math.sin(a)) * r))
        for k in range(8):
            f = p.bm.faces.new((middle, points[k], points[(k + 1) % 8]))
            p.colours[f] = rgb
    return face_inward(finish(p))


def sign(name):
    """The title's wooden sign frame: two posts, a top rail the friends hop on and a lower rail, around the title card."""
    p = Piece(name)
    for x in (-0.58, 0.58):
        box(p, (x, 0, 0.75), (0.07, 0.07, 1.5), WOOD)
        box(p, (x, 0, 1.52), (0.1, 0.1, 0.06), WOOD_DARK)
    box(p, (0, 0, 1.5), (1.26, 0.08, 0.07), WOOD)
    box(p, (0, 0, 0.82), (1.18, 0.05, 0.05), WOOD_DARK)
    # a bare top rail: the three friends hop on it (owner, headset 2026-11-12: the vine read as artifacts)
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
    for g in ground_sectors():
        built.append((g, None))
    built.append((sky_wedge("Meadow_SkyWedge"), None))
    built.append((sun_or_moon("Meadow_Sun"), None))
    built.append((stars("Meadow_Stars"), None))
    built.append((sign("Meadow_TitleSign"), 0.3))

    # each piece apart from the others while baking, then home at the origin
    for k, (obj, distance) in enumerate(built):
        if distance is not None:  # only the baked pieces move apart (the floor and sky are built in place)
            obj.location = (k * 60.0, 0, 0)
    bpy.context.view_layer.update()
    for obj, distance in built:
        if distance is not None:
            bake_ao(obj, distance, floor=0.82 if "Cloud" in obj.name else 0.25)  # clouds stay bright and soft
            print(f"[meadow] AO baked: {obj.name}")
    for obj, distance in built:
        if distance is not None:
            obj.location = (0, 0, 0)

    os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
    if PALETTE == "day" and not TRIAL:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Meadow.blend"))
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=EXPORT, use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="OFF" if SMOOTH else "FACE", add_leaf_bones=False, bake_anim=False,
        colors_type="LINEAR", prioritize_active_color=True)
    total = 0
    for obj, _ in built:
        total += len(obj.data.polygons)
        print(f"[meadow]   {obj.name}: {len(obj.data.polygons)} faces")
    print(f"[meadow] {len(built)} pieces, {total} faces -> {EXPORT}")


main()

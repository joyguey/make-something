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
    PETALS = hexes("#E6C45A", "#B49AD6", "#7FB8BE", "#D98A8A", "#F0ECE0")
    CLOUD = hexes("#F2EBE0")[0]
    SKY_HORIZON, SKY_ZENITH = hexes("#F2D9B8", "#6E93C4")
    GROUND_LO, GROUND_HI = hexes("#789E55", "#8AB262")
else:  # dusk: deep teal grass under a coral-to-indigo evening sky
    GRASS = hexes("#4F7A58", "#557F5E", "#4A7454", "#527C5B")
    HILL = hexes("#3C5F52", "#44695A", "#36574A")
    LEAVES = hexes("#35584A", "#3D6252", "#2F4F42")
    BLOSSOM = hexes("#C98FA8", "#D49DB4")
    PETALS = hexes("#E6B422", "#B49AD6", "#7FB8BE", "#E6B422", "#F0ECE0")
    CLOUD = hexes("#D8CFE0")[0]
    SKY_HORIZON, SKY_ZENITH = hexes("#E09A7A", "#2B3A6B")
    GROUND_LO, GROUND_HI = hexes("#4A7454", "#5C8664")
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
    """A cumulus: round puffs, biggest in the middle, with puffs in front, behind and underneath, so it is round all
    the way around from every angle, below included (owner, Fable refinement 2026-11-11: "I like the cumulus but don't
    make it flat at the bottom, make it round all the way around at all angles")."""
    p = Piece(name)
    puffs = [  # (x along, y across, z up, radius) in units of size
        (0.0, 0.0, 0.14, 0.62), (0.62, 0.10, 0.02, 0.48), (-0.60, -0.08, 0.02, 0.46),
        (1.05, 0.0, -0.05, 0.32), (-1.02, 0.04, -0.05, 0.30),
        (0.20, -0.32, 0.04, 0.42), (-0.16, 0.34, 0.0, 0.40), (0.05, 0.0, -0.20, 0.46),
    ]
    for (x, y, z, r) in puffs:
        ico(p, (x * size, y * size, z * size), r * size, CLOUD, subdiv=2, squash=(1.05, 1.0, 0.92), wobble=0.02)
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


def ground_sectors(radius=12.0, rings=(0.0, 3.0, 6.0, 9.0, 12.0)):
    """The meadow floor around the stage (its centre at the origin): ring sectors that meet exactly, coloured by one
    noise over the whole meadow (soft patches that flow across pieces, no checkerboard). Built in place: Unity moves
    each piece's origin to its middle, so the floor still comes apart piece by piece."""
    pieces = []
    for r in range(len(rings) - 1):
        r0, r1 = rings[r], rings[r + 1]
        sectors = 4 if r == 0 else 8
        for k in range(sectors):
            p = Piece(f"Meadow_GroundS{r}_{k}")
            a0, a1 = k / sectors * math.tau, (k + 1) / sectors * math.tau
            rad_steps, ang_steps = 4, 64 // sectors  # every ring cut into the same 64 steps round: edges meet with no gaps
            grid = []
            for i in range(rad_steps + 1):
                rr = r0 + (r1 - r0) * i / rad_steps
                row = []
                for j in range(ang_steps + 1):
                    aa = a0 + (a1 - a0) * j / ang_steps
                    row.append(p.bm.verts.new((rr * math.cos(aa), rr * math.sin(aa), 0.0)))
                grid.append(row)
            for i in range(rad_steps):
                for j in range(ang_steps):
                    if i == 0 and r0 == 0.0:
                        f = p.bm.faces.new((grid[0][0], grid[1][j], grid[1][j + 1]))
                    else:
                        f = p.bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
                    c = f.calc_center_median()
                    n = noise.noise(Vector((c.x * 0.28, c.y * 0.28, 0.37))) * 0.6 + noise.noise(Vector((c.x * 0.9, c.y * 0.9, 1.7))) * 0.25
                    t = max(0.0, min(1.0, 0.5 + n))
                    lo, hi = GROUND_LO, GROUND_HI
                    p.colours[f] = tuple(lo[q] + (hi[q] - lo[q]) * t for q in range(3))
            bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts, dist=1e-5)
            pieces.append(finish(p))
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
    if PALETTE == "day":
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

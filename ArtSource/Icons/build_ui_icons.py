"""
The leaf and menu buttons' icons, modelled instead of assembled from boxes (V4 review: "touch up the icon models'
quality: bevel the pause button's edges, remove the sharp 90-degree turn in the undo button"). Original shapes.

Each icon is built in the button's own space as Unity sees it (x right, y up, the player on -z; metres), at the place
the box-made icon stood, so it drops in on the button at local origin, identity rotation, scale 1. Pause and Play are
the exception: they sit under their own holders, so they are modelled around the origin.

Colour: the vertex colour "Col" is white (the button's mark material tints it), with ambient occlusion baked per icon
into its alpha (RoomOrchestra/LitBakedAO). Parts that use another material are their own objects
(UiIcon_<Key>_<Part>). Normals are written per vertex (rounded rims, flat faces), so the icons read crisp, not boxy.

Two styles for the owner to choose between:
  UI_ICON_STYLE=A  "soft bevel": today's silhouettes with every edge rounded
  UI_ICON_STYLE=B  "chunky pill": rounder and thicker, toy-like (capsule bars, fat arrows), 20% deeper
  UI_ICON_STYLE=NOW  the box-made icons as they are today (Undo, Pause, Clear, Menu), for an honest comparison only

  UI_ICON_STYLE=A /Applications/Blender.app/Contents/MacOS/Blender -b -P ArtSource/Icons/build_ui_icons.py
Writes Assets/RoomOrchestra/Art/Icons/UiIcons_<style>.fbx and ArtSource/Icons/UiIcons_<style>.blend.
UI_ICON_PREVIEW=<png> also renders a contact sheet (Blender's renderer, not the game's).
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector
from mathutils.geometry import tessellate_polygon

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
STYLE = os.environ.get("UI_ICON_STYLE", "A")
assert STYLE in ("A", "B", "NOW"), STYLE
PREVIEW = os.environ.get("UI_ICON_PREVIEW", "")
EXPORT = os.path.join(ROOT, "Assets", "RoomOrchestra", "Art", "Icons", "UiIcons.fbx" if STYLE == "A" else f"UiIcons_{STYLE}.fbx")  # A: the owner's choice (2026-11-17), in the game
B = STYLE == "B"
DEPTH = 0.012 if B else 0.010  # front to back (m)
FAT = 1.25 if B else 1.0       # stroke widths


# ---- 2D outlines (x, y), counter-clockwise ----
def area(poly):
    return 0.5 * sum(poly[i][0] * poly[(i + 1) % len(poly)][1] - poly[(i + 1) % len(poly)][0] * poly[i][1] for i in range(len(poly)))


def ccw(poly):
    return poly if area(poly) > 0 else poly[::-1]


def arc(centre, radius, a0, a1, n):
    """Points on an arc from a0 to a1 (degrees), n steps."""
    return [(centre[0] + radius * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             centre[1] + radius * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def rect(centre, size, angle=0.0):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    hx, hy = size[0] / 2, size[1] / 2
    return [(centre[0] + x * c - y * s, centre[1] + x * s + y * c) for x, y in ((-hx, -hy), (hx, -hy), (hx, hy), (-hx, hy))]


def rounded(poly, radius, seg=3, min_turn=25.0):
    """The outline with its real corners (a turn over min_turn degrees) rounded off; gentle bends are left alone."""
    poly = ccw(poly)
    n = len(poly)
    out = []
    for i in range(n):
        p, a, b = Vector(poly[i]), Vector(poly[i - 1]), Vector(poly[(i + 1) % n])
        u, v = (a - p), (b - p)
        if u.length < 1e-9 or v.length < 1e-9:
            continue
        ang = u.angle(v)  # interior angle between the two edges
        if math.degrees(math.pi - ang) < min_turn or radius <= 0:
            out.append(tuple(p))
            continue
        t = min(radius / math.tan(ang / 2), u.length * 0.45, v.length * 0.45)
        r = t * math.tan(ang / 2)
        ta, tb = p + u.normalized() * t, p + v.normalized() * t
        bis = (u.normalized() + v.normalized()).normalized()
        centre = p + bis * (r / math.sin(ang / 2))
        a0 = math.atan2(ta.y - centre.y, ta.x - centre.x)
        a1 = math.atan2(tb.y - centre.y, tb.x - centre.x)
        d = a1 - a0
        while d > math.pi:
            d -= math.tau
        while d < -math.pi:
            d += math.tau
        for k in range(seg + 1):
            aa = a0 + d * k / seg
            out.append((centre.x + r * math.cos(aa), centre.y + r * math.sin(aa)))
    return out


def stroke(points, width, round_caps=None, cap_seg=4):
    """A line of the given width along 'points'. Round ends in the pill style, squared (then softened) otherwise."""
    if round_caps is None:
        round_caps = B
    pts = [Vector(p) for p in points]
    h = width / 2
    left, right = [], []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        nrm = Vector((-t.y, t.x))
        left.append(p + nrm * h)
        right.append(p - nrm * h)
    out = [tuple(q) for q in right]
    t_end = (pts[-1] - pts[-2]).normalized()
    if round_caps:
        a = math.atan2(-t_end.x, t_end.y)  # from the right side, round the tip, to the left
        out += [(pts[-1].x + h * math.cos(a + math.pi * k / cap_seg), pts[-1].y + h * math.sin(a + math.pi * k / cap_seg)) for k in range(1, cap_seg)]
    out += [tuple(q) for q in reversed(left)]
    t_start = (pts[1] - pts[0]).normalized()
    if round_caps:
        a = math.atan2(t_start.x, -t_start.y)  # from the left side, round the tail, to the right
        out += [(pts[0].x + h * math.cos(a + math.pi * k / cap_seg), pts[0].y + h * math.sin(a + math.pi * k / cap_seg)) for k in range(1, cap_seg)]
    out = ccw(out)
    return out if round_caps else rounded(out, width * 0.22, seg=2, min_turn=60.0)


def bar(centre, size, angle=0.0):
    """A straight bar: a capsule in the pill style, a softened rectangle otherwise."""
    if B:
        long_x = size[0] >= size[1]
        length, width = (size[0], size[1]) if long_x else (size[1], size[0])
        width *= FAT
        half = max(1e-4, length / 2 - width / 2)
        a = math.radians(angle + (0 if long_x else 90))
        d = Vector((math.cos(a), math.sin(a))) * half
        c = Vector(centre)
        return stroke([tuple(c - d), tuple(c + d)], width, round_caps=True)
    return rounded(rect(centre, size, angle), min(size) * 0.28, seg=3)


def thin(width, depth=None):
    """The rim rounding for a thin stroke: never more than 30% of its width, so its face keeps some flat."""
    depth = DEPTH if depth is None else depth
    return min(depth * (0.42 if B else 0.28), width * 0.3)


def curve(p0, p1, p2, n=6):
    """A smooth bend from p0 to p2, pulled toward p1."""
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (k / n for k in range(n + 1))]


# ---- meshes: positions, normals and faces in Unity's button space ----
class Icon:
    def __init__(self, name):
        self.name = name
        self.verts, self.normals, self.faces = [], [], []

    def prism(self, outline, depth=None, z=0.0, rim=None, rim_seg=2):
        """The outline pushed through 'depth' along z (centred on z), its front and back rims rounded by 'rim'."""
        depth = DEPTH if depth is None else depth
        poly = ccw(outline)
        n = len(poly)
        if rim is None:
            rim = depth * (0.42 if B else 0.28)
        rim = min(rim, depth * 0.49)
        # the outward direction at each outline point (mitred)
        outs = []
        for i in range(n):
            p, a, b = Vector(poly[i]), Vector(poly[i - 1]), Vector(poly[(i + 1) % n])
            e0, e1 = (p - a).normalized(), (b - p).normalized()
            n0, n1 = Vector((e0.y, -e0.x)), Vector((e1.y, -e1.x))
            m = (n0 + n1)
            m = m.normalized() if m.length > 1e-6 else n0
            outs.append((m, min(2.0, 1.0 / max(0.5, m.dot(n0)))))
        rings = []  # front cap edge ... front wall, back wall ... back cap edge
        layers = [(rim * (1 - math.cos(th)), -depth / 2 + rim * (1 - math.sin(th)), math.cos(th), -math.sin(th))
                  for th in (math.radians(90 - 90 * j / rim_seg) for j in range(rim_seg + 1))]
        layers += [(inset, -zz, co, -nz) for (inset, zz, co, nz) in reversed(layers)]
        for inset, zz, co, nz in layers:
            ring = []
            for i in range(n):
                m, scale = outs[i]
                p = Vector(poly[i]) - m * inset * scale
                ring.append(len(self.verts))
                self.verts.append((p.x, p.y, z + zz))
                self.normals.append((m.x * co, m.y * co, nz))
            rings.append(ring)
        for r in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                self.faces.append((rings[r][i], rings[r][j], rings[r + 1][j], rings[r + 1][i]))
        for ring, zz in ((rings[0], -1.0), (rings[-1], 1.0)):  # flat caps, their own vertices (flat normals)
            base = len(self.verts)
            for i in ring:
                self.verts.append(self.verts[i])
                self.normals.append((0.0, 0.0, zz))
            for tri in tessellate_polygon([[Vector(self.verts[i]) for i in ring]]):
                self.faces.append(tuple(base + t for t in tri))
        return self

    def ball(self, centre, size, seg=(32, 16), tilt=0.0):
        """An ellipsoid 'size' across (x, y, z), turned 'tilt' degrees in the icon's plane."""
        c, s = math.cos(math.radians(tilt)), math.sin(math.radians(tilt))
        base = len(self.verts)
        u_n, v_n = seg
        for v in range(v_n + 1):
            phi = math.pi * v / v_n
            for u in range(u_n):
                th = math.tau * u / u_n
                d = (math.sin(phi) * math.cos(th), math.cos(phi), math.sin(phi) * math.sin(th))
                p = (d[0] * size[0] / 2, d[1] * size[1] / 2, d[2] * size[2] / 2)
                nn = Vector((d[0] / size[0], d[1] / size[1], d[2] / size[2])).normalized()
                self.verts.append((centre[0] + p[0] * c - p[1] * s, centre[1] + p[0] * s + p[1] * c, centre[2] + p[2]))
                self.normals.append((nn.x * c - nn.y * s, nn.x * s + nn.y * c, nn.z))
        for v in range(v_n):
            for u in range(u_n):
                a, b2 = base + v * u_n + u, base + v * u_n + (u + 1) % u_n
                c2, d2 = a + u_n, b2 + u_n
                if v == 0:
                    self.faces.append((a, d2, c2))
                elif v == v_n - 1:
                    self.faces.append((a, b2, c2))
                else:
                    self.faces.append((a, b2, d2, c2))
        return self

    def torus(self, centre, ring_radius, tube, depth_scale=1.0, seg=(48, 16)):
        """A ring facing the player (in the x-y plane)."""
        base = len(self.verts)
        u_n, v_n = seg
        for u in range(u_n):
            th = math.tau * u / u_n
            for v in range(v_n):
                ph = math.tau * v / v_n
                r = ring_radius + tube * math.cos(ph)
                self.verts.append((centre[0] + r * math.cos(th), centre[1] + r * math.sin(th), centre[2] + tube * depth_scale * math.sin(ph)))
                nn = Vector((math.cos(ph) * math.cos(th), math.cos(ph) * math.sin(th), math.sin(ph) / depth_scale)).normalized()
                self.normals.append(tuple(nn))
        for u in range(u_n):
            for v in range(v_n):
                a = base + u * v_n + v
                b2 = base + u * v_n + (v + 1) % v_n
                c2 = base + ((u + 1) % u_n) * v_n + (v + 1) % v_n
                d2 = base + ((u + 1) % u_n) * v_n + v
                self.faces.append((a, b2, c2, d2))
        return self

    def turned_y(self, start, degrees, pivot):
        """Turns everything added since vertex 'start' about the vertical through 'pivot' (Unity's Euler y)."""
        c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
        for i in range(start, len(self.verts)):
            x, y, z = self.verts[i]
            x, z = x - pivot[0], z - pivot[2]
            self.verts[i] = (pivot[0] + x * c + z * s, y, pivot[2] - x * s + z * c)
            nx, ny, nz = self.normals[i]
            self.normals[i] = (nx * c + nz * s, ny, -nx * s + nz * c)
        return self

    def finish(self):
        """A Blender object: Unity (x, y, z) is Blender (x, z, y) with the FBX settings below."""
        mesh = bpy.data.meshes.new(self.name)
        pos = [Vector((x, z, y)) for (x, y, z) in self.verts]
        nrm = [Vector((x, z, y)) for (x, y, z) in self.normals]
        faces = []
        for f in self.faces:  # wind every face to agree with its vertices' normals (outward)
            g = sum(((pos[f[k]] - pos[f[0]]).cross(pos[f[k + 1]] - pos[f[0]]) for k in range(1, len(f) - 1)), Vector())
            if g.length < 1e-14:
                continue
            faces.append(f if g.dot(sum((nrm[i] for i in f), Vector())) > 0 else f[::-1])
        mesh.from_pydata([tuple(p) for p in pos], [], faces)
        for poly in mesh.polygons:
            poly.use_smooth = True
        mesh.normals_split_custom_set_from_vertices([Vector((x, z, y)).normalized() for (x, y, z) in self.normals])
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        col = mesh.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for d in col.data:
            d.color = (1.0, 1.0, 1.0, 1.0)
        return obj


def triangle(points, radius):
    return rounded(points, radius, seg=4)


def arrow_head(tip, direction_deg, length, width):
    """A triangle whose point is at 'tip', pointing along direction_deg."""
    d = Vector((math.cos(math.radians(direction_deg)), math.sin(math.radians(direction_deg))))
    side = Vector((-d.y, d.x))
    back = Vector(tip) - d * length
    return triangle([tuple(Vector(tip)), tuple(back + side * width / 2), tuple(back - side * width / 2)], length * (0.22 if B else 0.16))


# ---- the icons ----
def undo():
    """A curved arrow: out to the right, round and back to the left, ending in an arrowhead (no square corner)."""
    i = Icon("UiIcon_Undo")
    w = 0.009 * FAT
    c, r = (0.012, 0.0335), 0.0125
    path = arc(c, r, 120, -90, 10)               # over the top, down the right side, to the bottom
    path += [(0.002, c[1] - r), (-0.010, c[1] - r)]  # then straight back to the left
    i.prism(stroke(path, w))
    i.prism(arrow_head((-0.031, c[1] - r), 180, 0.019, 0.026 if B else 0.023))
    return [i.finish()]


def pause():
    i = Icon("UiIcon_Pause")
    for x in (-0.012, 0.012):
        i.prism(bar((x, 0), (0.012, 0.028)) if not B else stroke([(x, -0.0075), (x, 0.0075)], 0.013, round_caps=True), depth=0.012 * (1.2 if B else 1.0))
    return [i.finish()]


def play():
    i = Icon("UiIcon_Play")
    k = 1.3 if B else 1.1  # the rounding eats the corners: drawn larger so it fills the same 26 x 28 mm
    i.prism(triangle([(-0.013 * k + 0.001, -0.014 * k), (0.013 * k + 0.001, 0.0), (-0.013 * k + 0.001, 0.014 * k)], 0.0055 if B else 0.0028), depth=0.012 * (1.2 if B else 1.0))
    return [i.finish()]


def clear():
    i = Icon("UiIcon_Clear")
    h, w = 0.0225, (0.012 if B else 0.010) / 2  # one outline (a plus sign turned 45 degrees), so nothing overlaps
    plus = [(w, -h), (w, -w), (h, -w), (h, w), (w, w), (w, h), (-w, h), (-w, w), (-h, w), (-h, -w), (-w, -w), (-w, -h)]
    c = math.sqrt(0.5)
    i.prism(rounded([(x * c - y * c, 0.03 + x * c + y * c) for x, y in plus], w * (0.95 if B else 0.3), seg=4 if B else 3))
    return [i.finish()]


def menu(name="UiIcon_Menu"):
    i = Icon(name)
    for k in range(3):
        i.prism(bar((0, 0.02 + 0.014 * k), (0.04, 0.007 if not B else 0.0068)), rim=thin(0.007))
    return [i.finish()]


def close():
    """Close the menu tray: a soft chevron pointing down (the tray folds away), not Menu's three bars."""
    i = Icon("UiIcon_Close")
    i.prism(stroke([(-0.017, 0.044), (0, 0.026), (0.017, 0.044)], 0.0085 * FAT))
    return [i.finish()]


def recenter():
    i = Icon("UiIcon_Recenter")
    tube = 0.0042 if B else 0.0032
    i.torus((0, 0.035, 0), 0.018 - tube, tube, depth_scale=1.25)
    i.ball((0, 0.035, 0), (0.012 if B else 0.010,) * 2 + (0.009,))
    return [i.finish()]


def height():
    i = Icon("UiIcon_Height")
    w = 0.008 if not B else 0.0085
    if B:
        i.prism(stroke([(0, 0.024), (0, 0.046)], w, round_caps=True))
        i.prism(arrow_head((0, 0.062), 90, 0.018, 0.032))
        i.prism(arrow_head((0, 0.008), -90, 0.018, 0.032))
    else:
        i.prism(bar((0, 0.035), (0.008, 0.04)), depth=0.008)
        for y, up in ((0.052, 1), (0.018, -1)):  # chevrons, as now, softened
            i.prism(stroke([(-0.0125, y - up * 0.0055), (0, y + up * 0.0045), (0.0125, y - up * 0.0055)], 0.006), depth=0.008, rim=thin(0.006, 0.008))
    return [i.finish()]


def view():
    """An eye: a pale lens (its own material) with a dark pupil."""
    white = Icon("UiIcon_View_White")
    if B:
        white.ball((0, 0.032, 0), (0.052, 0.030, 0.013))
    else:  # an almond: two shallow arcs meeting in soft points
        bottom = arc((0, 0.032 + 0.0165), 0.0295, 212, 328, 12)[1:-1]
        top = arc((0, 0.032 - 0.0165), 0.0295, 32, 148, 12)[1:-1]
        white.prism(rounded([(-0.0255, 0.032)] + bottom + [(0.0255, 0.032)] + top, 0.003, seg=3), depth=0.010)
    pupil = Icon("UiIcon_View")
    pupil.ball((0, 0.032, -0.0055), (0.018, 0.018, 0.007) if not B else (0.020, 0.020, 0.008))
    return [pupil.finish(), white.finish()]


def sounds():
    """A speaker and two sound waves (arcs now, not bars)."""
    i = Icon("UiIcon_Sounds")
    cy = 0.03
    body = [(-0.032, cy - 0.007), (-0.024, cy - 0.007), (-0.011, cy - 0.016), (-0.011, cy + 0.016), (-0.024, cy + 0.007), (-0.032, cy + 0.007)]
    i.prism(rounded(body, 0.004 if B else 0.002, seg=3))
    for r, half in ((0.0165, 38), (0.0295, 40)):
        i.prism(stroke(arc((-0.011, cy), r, -half, half, 6), 0.005 * FAT), depth=DEPTH * 0.8, rim=thin(0.005 * FAT, DEPTH * 0.8))
    return [i.finish()]


def lesson():
    """A music note: a tilted round head, a stem and a curved flag."""
    i = Icon("UiIcon_Lesson")
    i.ball((-0.008, 0.026, 0), (0.026, 0.018, 0.012) if not B else (0.028, 0.021, 0.014), tilt=20)
    w = 0.005 * FAT
    d = 0.008 * (1.2 if B else 1)
    i.prism(stroke([(0.004, 0.026), (0.004, 0.064)], w), depth=d, rim=thin(w, d))
    flag = curve((0.005, 0.0625), (0.018, 0.060), (0.0185, 0.044))
    i.prism(stroke(flag, w * 1.1), depth=d, rim=thin(w, d))
    return [i.finish()]


def places():
    """A map pin."""
    i = Icon("UiIcon_Places")
    r = 0.012 if B else 0.011
    cy = 0.05
    tip = (0, 0.014)
    a = math.degrees(math.acos(r / (cy - tip[1])))  # where the sides leave the round head
    outline = arc((0, cy), r, -90 + a, 270 - a, 16) + [tip]
    i.prism(rounded(outline, 0.0035 if B else 0.003, seg=4, min_turn=40))
    i.ball((0, cy, -DEPTH / 2), (r * 0.8, r * 0.8, 0.004))  # the pin's dot, proud of the face
    return [i.finish()]


def real_hides():
    """A ball half behind a wall (the wall takes the slab's material)."""
    ball = Icon("UiIcon_RealHides")
    ball.ball((0.008, 0.04, 0.01), (0.026, 0.026, 0.026))
    wall = Icon("UiIcon_RealHides_Wall")
    wall.prism(rounded(rect((-0.006, 0.035), (0.026, 0.04)), 0.006 if B else 0.003, seg=3), depth=0.006 * (1.2 if B else 1), z=-0.004)
    return [ball.finish(), wall.finish()]


def start_over():
    """A circular arrow."""
    i = Icon("UiIcon_StartOver")
    c, r = (-0.002, 0.045), 0.0125
    w = 0.0055 * FAT
    d = 0.008 * (1.2 if B else 1)
    i.prism(stroke(arc(c, r, 30, -250, 14), w), depth=d, rim=thin(w, d))  # clockwise, a gap at the top right
    end = Vector((c[0] + r * math.cos(math.radians(110)), c[1] + r * math.sin(math.radians(110))))
    along = Vector((math.sin(math.radians(110)), -math.cos(math.radians(110))))  # clockwise at the end
    i.prism(arrow_head(tuple(end + along * 0.0095), math.degrees(math.atan2(along.y, along.x)), 0.0145, 0.022 if B else 0.017), depth=d)
    return [i.finish()]


def quit_door():
    """A door frame with a door swung open toward the player (the door takes the slab's material) and a knob."""
    frame = Icon("UiIcon_Quit")
    frame.prism(rounded(rect((0, 0.04), (0.03, 0.046)), 0.005 if B else 0.0025, seg=3), depth=0.004 * (1.5 if B else 1), z=0.004)
    start = len(frame.verts)
    frame.ball((0.003, 0.04, -0.012), (0.006 * FAT,) * 3)
    door = Icon("UiIcon_Quit_Door")
    door.prism(rounded(rect((0, 0.04), (0.024, 0.04)), 0.004 if B else 0.002, seg=3), depth=0.006, z=0.0)
    door.turned_y(0, 28, (0, 0, 0))
    for k in range(len(door.verts)):
        x, y, z = door.verts[k]
        door.verts[k] = (x - 0.004, y, z - 0.004)
    return [frame.finish(), door.finish()]


def call():
    """Come here: a small leaf lying on the slab with a vein (the vein takes the vein material)."""
    leaf = Icon("UiIcon_Call")
    pts = []
    for k in range(13):  # a pointed leaf seen from above, drawn in x-z then laid flat
        t = k / 12
        x = -0.03 + 0.06 * t
        pts.append((x, 0.016 * math.sin(math.pi * t) ** 0.8))
    outline = pts + [(x, -y) for (x, y) in reversed(pts[1:-1])]
    flat = Icon("tmp")
    flat.prism(rounded(outline, 0.002, seg=2, min_turn=50), depth=0.010 if B else 0.008, rim=0.0035)
    for (x, y, z), (nx, ny, nz) in zip(flat.verts, flat.normals):  # x-y outline, z depth  ->  x-z outline, y up
        leaf.verts.append((x, 0.03 - z, y))
        leaf.normals.append((nx, -nz, ny))
    leaf.faces = flat.faces
    leaf.turned_y(0, 30, (0, 0, 0))
    vein = Icon("UiIcon_Call_Vein")
    flat = Icon("tmp")
    flat.prism(stroke([(-0.022, 0), (0.022, 0)], 0.003, round_caps=True, cap_seg=3), depth=0.002, rim=0.0008, rim_seg=1)
    top = 0.03 + (0.010 if B else 0.008) / 2
    for (x, y, z), (nx, ny, nz) in zip(flat.verts, flat.normals):
        vein.verts.append((x, top - z, y))
        vein.normals.append((nx, -nz, ny))
    vein.faces = flat.faces
    vein.turned_y(0, 30, (0, 0, 0))
    return [leaf.finish(), vein.finish()]


# ---- today's box-made icons, for the comparison sheet only ----
def together():
    """Play together (owner, 2026-11-22): two people side by side, a round head over rounded shoulders each, the one
    on the right a little smaller and set back. Generic figures, not the characters."""
    i = Icon("UiIcon_Together")
    for x, s, z in ((-0.0115, 1.0, -0.001), (0.0135, 0.86, 0.002)):
        i.ball((x, 0.012 + 0.0255 * s + 0.001, z), (0.019 * s, 0.019 * s, 0.012 * s))  # the head just clears the shoulders
        shoulders = arc((x, 0.012), 0.016 * s, 0, 180, 12)  # a half disc; the outline closes along its flat base
        i.prism(rounded(shoulders, 0.003, seg=3), depth=DEPTH * s, z=z)
    return [i.finish()]


def box_icon(name, boxes):
    i = Icon(name)
    for centre, size, angle in boxes:
        i.prism(rect((centre[0], centre[1]), (size[0], size[1]), angle), depth=size[2], z=centre[2], rim=1e-5, rim_seg=1)
    return [i.finish()]


def now_icons():
    return [
        ("Undo", box_icon("UiIcon_Undo", [((0.006, 0.025, 0), (0.046, 0.01, 0.01), 0), ((0.029, 0.037, 0), (0.01, 0.024, 0.01), 0), ((-0.019, 0.025, 0), (0.018, 0.018, 0.01), 45)])),
        ("Pause", box_icon("UiIcon_Pause", [((-0.012, 0, 0), (0.012, 0.028, 0.012), 0), ((0.012, 0, 0), (0.012, 0.028, 0.012), 0)])),
        ("Clear", box_icon("UiIcon_Clear", [((0, 0.03, 0), (0.045, 0.01, 0.01), 45), ((0, 0.03, 0), (0.045, 0.01, 0.01), -45)])),
        ("Menu", box_icon("UiIcon_Menu", [((0, 0.02 + 0.014 * k, 0), (0.04, 0.007, 0.01), 0) for k in range(3)])),
    ]


def all_icons():
    if STYLE == "NOW":
        return now_icons()
    return [("Undo", undo()), ("Pause", pause()), ("Play", play()), ("Clear", clear()), ("Menu", menu()),
            ("Close", close()), ("Recenter", recenter()), ("Height", height()), ("View", view()),
            ("Sounds", sounds()), ("Lesson", lesson()), ("Places", places()), ("RealHides", real_hides()),
            ("StartOver", start_over()), ("Quit", quit_door()), ("Call", call()), ("Together", together())]


# ---- one continuous shape per part (owner, 2026-11-17: "remove the weird black artifacts and make them a continuous
# shape through blender first") ----
# The parts of an icon were overlapping solids (the note's head, stem and flag; the speaker and its waves) with flat caps
# cut into long thin triangles: the baked shade at a joint smeared along those triangles as black streaks. A voxel
# remesh melts the parts into one closed surface with filleted joints and an even mesh; a gentle decimate keeps it
# light, and area-weighted normals keep the big faces flat and the rims round.
FUSE_TRIS = 3000  # per object, at most


def fuse(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
    dims = sorted(obj.dimensions)
    rem = obj.modifiers.new("Fuse", "REMESH")
    rem.mode = "VOXEL"
    rem.voxel_size = max(0.00018, min(0.00032, dims[0] / 7))  # at least 7 voxels through the thinnest part
    rem.adaptivity = 0.0
    bpy.ops.object.modifier_apply(modifier=rem.name)
    for _ in range(2):  # take off the voxel stair-steps without losing the shape
        sm = obj.modifiers.new("Relax", "CORRECTIVE_SMOOTH")
        sm.factor, sm.iterations, sm.smooth_type = 0.5, 4, "SIMPLE"
        sm.use_only_smooth = True
        bpy.ops.object.modifier_apply(modifier=sm.name)
    if tris(obj) > FUSE_TRIS:
        dec = obj.modifiers.new("Light", "DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = FUSE_TRIS / tris(obj)
        dec.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.shade_smooth()
    wn = obj.modifiers.new("Normals", "WEIGHTED_NORMAL")
    wn.mode, wn.weight, wn.keep_sharp = "FACE_AREA", 50, False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    if "Col" not in obj.data.color_attributes:
        col = obj.data.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
        for d in col.data:
            d.color = (1.0, 1.0, 1.0, 1.0)


# ---- ambient occlusion per icon (its parts together), into Col's alpha ----
def bake(objs):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.render.bake.target = "VERTEX_COLORS"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("World")
    scene.world.light_settings.distance = 0.006  # a soft shade in the joints, never a black line
    for o in bpy.data.objects:
        o.hide_render = o not in objs
    for obj in objs:
        mesh = obj.data
        mesh.materials.append(bpy.data.materials.new("Bake"))
        ao = mesh.color_attributes.new("AO", "FLOAT_COLOR", "CORNER")
        mesh.color_attributes.active_color = ao
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.bake(type="AO", target="VERTEX_COLORS")
        col = mesh.color_attributes["Col"]
        for k in range(len(col.data)):
            a = ao.data[k].color
            col.data[k].color = (1.0, 1.0, 1.0, 1.0 if STYLE == "NOW" else max(0.62, min(1.0, (a[0] + a[1] + a[2]) / 3)))  # today's boxes have no baked shade
        mesh.color_attributes.remove(ao)
        mesh.color_attributes.active_color = mesh.color_attributes["Col"]
        mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    for o in bpy.data.objects:
        o.hide_render = False


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def report(icons):
    total = 0
    for key, objs in icons:
        for obj in objs:
            xs = [v.co.x for v in obj.data.vertices]
            ys = [v.co.z for v in obj.data.vertices]  # Unity y
            zs = [v.co.y for v in obj.data.vertices]  # Unity z
            total += tris(obj)
            print(f"[ui-icons] {STYLE} {obj.name}: {tris(obj)} tris, Unity min ({min(xs):.4f}, {min(ys):.4f}, {min(zs):.4f}) max ({max(xs):.4f}, {max(ys):.4f}, {max(zs):.4f})")
    print(f"[ui-icons] {STYLE} total {total} tris")


# ---- the contact sheet (Blender's renderer) ----
def material(name, rgb, vertex_ao=True):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.55
    if vertex_ao:
        attr = m.node_tree.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "Col"
        mix = m.node_tree.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs["A"].default_value = (*rgb, 1.0)
        m.node_tree.links.new(attr.outputs["Alpha"], mix.inputs["B"])
        m.node_tree.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    return m


def srgb(code):
    def lin(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return tuple(lin(int(code[i:i + 2], 16) / 255) for i in (1, 3, 5))


def preview(icons, path):
    gold = material("Gold", srgb("#F2C83A"))
    slab_m = material("Slab", srgb("#17151F"), vertex_ao=False)
    pale = material("Pale", srgb("#F2F2EB"))
    green = material("Green", srgb("#5CB861"))
    label_m = material("Label", srgb("#FFFFFF"), vertex_ao=False)
    label_m.node_tree.nodes["Principled BSDF"].inputs["Emission Color"].default_value = (1, 1, 1, 1)
    label_m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 1.0
    only = [k for k in os.environ.get("UI_ICON_ONLY", "").split(",") if k]  # a close-up of just these
    if only:
        for key, objs in icons:
            if key not in only:
                for obj in objs:
                    bpy.data.objects.remove(obj)
        icons = [(key, objs) for key, objs in icons if key in only]
    cols = len(icons) if only else 4 if STYLE == "NOW" else 6
    step_x, step_z = 0.125, 0.125
    rows = (len(icons) + cols - 1) // cols
    for k, (key, objs) in enumerate(icons):
        ox = (k % cols - (cols - 1) / 2) * step_x
        oz = -(k // cols) * step_z
        lift = 0.03 if key in ("Pause", "Play") else 0.0  # those two sit under a holder 3 cm up
        for obj in objs:
            obj.location = (ox, 0, oz + lift)
            obj.data.materials.clear()
            part = obj.name.split("_")[-1]
            obj.data.materials.append(slab_m if part in ("Wall", "Door") else pale if part == "White" else green if obj.name == "UiIcon_Call" else gold)
        # the button's slab (0.09 x 0.03 x 0.07 m), softened as in the game
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(ox, 0, oz))
        slab = bpy.context.object
        slab.scale = (0.09, 0.07, 0.03)
        bpy.ops.object.transform_apply(scale=True)
        bev = slab.modifiers.new("Bevel", "BEVEL")
        bev.width, bev.segments = 0.006, 3
        slab.data.materials.append(slab_m)
        bpy.ops.object.shade_smooth()
        bpy.ops.object.text_add(location=(ox, -0.037, oz - 0.011), rotation=(math.radians(90), 0, 0))
        text = bpy.context.object
        text.data.body = key
        text.data.size = 0.014
        text.data.align_x = "CENTER"
        text.data.materials.append(label_m)
    # a seated player looking down at the leaf: in front, a little above
    mid_z = -(rows - 1) * step_z / 2 + 0.02
    cam_data = bpy.data.cameras.new("Cam")
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (0.0, -1.0, mid_z + 0.42)
    look = Vector((0, 0, mid_z)) - cam.location
    cam.rotation_euler = look.to_track_quat("-Z", "Y").to_euler()
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = cols * step_x + 0.03
    scene = bpy.context.scene
    scene.camera = cam
    for name, loc, energy in (("Key", (-0.6, -0.9, 1.0), 3.2), ("Fill", (0.8, -0.6, 0.3), 1.0)):
        light = bpy.data.lights.new(name, "SUN")
        light.energy = energy
        light.angle = math.radians(20)
        o = bpy.data.objects.new(name, light)
        bpy.context.collection.objects.link(o)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*srgb("#2B3550"), 1.0)
    bg.inputs["Strength"].default_value = 0.6
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 64
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 600 if only else 500 if STYLE == "NOW" else 1150
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = path
    for o in bpy.data.objects:
        o.hide_render = False
    bpy.ops.render.render(write_still=True)
    print(f"[ui-icons] preview -> {path}")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    icons = all_icons()
    for o in [o for o in bpy.data.objects if o.name.startswith("tmp")]:
        bpy.data.objects.remove(o)
    for k, (key, objs) in enumerate(icons):
        for obj in objs:
            obj.location = (k * 2.0, 0, 0)
    bpy.context.view_layer.update()
    if STYLE != "NOW":
        for key, objs in icons:
            for obj in objs:
                fuse(obj)
    for key, objs in icons:
        bake(objs)
    for key, objs in icons:
        for obj in objs:
            obj.location = (0, 0, 0)
    report(icons)
    if STYLE != "NOW":
        os.makedirs(os.path.dirname(EXPORT), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, f"UiIcons_{STYLE}.blend"))
        bpy.ops.object.select_all(action="SELECT")
        bpy.ops.export_scene.fbx(
            filepath=EXPORT, use_selection=True, object_types={"MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
            axis_forward="Z", axis_up="Y", bake_space_transform=True,
            mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False,
            colors_type="LINEAR", prioritize_active_color=True)
        print(f"[ui-icons] {sum(len(o) for _, o in icons)} objects -> {EXPORT}")
    if PREVIEW:
        preview(icons, PREVIEW)


main()

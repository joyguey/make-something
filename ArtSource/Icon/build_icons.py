"""
App icon options for Little Echoes (owner request 2026-10-29, after the Version 1 feedback asked for an
official icon). Each option uses the approved character meshes from ArtSource/Characters/*.blend (no
redrawing), flat-shaded like the approved reference, plus simple original shapes drawn here.

Every option is rendered three ways, 1024 x 1024:
  <option>.png             the finished icon
  <option>_background.png  the background layer only (opaque)
  <option>_foreground.png  the characters and details on transparency
The layers feed PICO OS 6's layered launcher icon (see Assets/RoomOrchestra/Editor/AppIconSetup.cs).

The chosen icon (owner, 2026-10-29) is option 5 ("combo") with the title added:
  Blender --background --factory-startup --python ArtSource/Icon/build_icons.py
  python3 ArtSource/Icon/extract_title.py "<key art .png>" ArtSource/Icon/lettering   # once
  python3 ArtSource/Icon/add_title.py ArtSource/Icon/options/5_combo.png ArtSource/Icon/lettering \
      ArtSource/Icon/AppIcon_1024.png --title-layer ArtSource/Icon/AppIcon_title.png
  then copy options/5_combo_background.png to AppIcon_background.png, options/5_combo_foreground.png to
  AppIcon_layer_art.png, and AppIcon_1024.png to Assets/RoomOrchestra/Art/Icon/AppIcon.png, and run
  RoomOrchestra > Little Echoes > Apply App Icon in Unity (AppIconSetup.cs). Options 1-4 were the first
  round of choices.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CHARACTERS = os.path.join(ROOT, "ArtSource", "Characters")
OUT = os.path.join(HERE, "options")
SIZE = 1024


def hex_rgb(h):
    h = h.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb)  # linear


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name in ("BG", "FG"):
        bpy.context.scene.collection.children.link(bpy.data.collections.new(name))


def material(name, colour):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*hex_rgb(colour), 1.0)
    return mat


def flat_shape(name, points, depth, colour, layer):
    """A flat polygon in the picture plane (x right, z up) at `depth` (smaller = nearer the viewer)."""
    bm = bmesh.new()
    verts = [bm.verts.new((x, depth, z)) for x, z in points]
    face = bm.faces.new(verts)
    bm.normal_update()
    if face.normal.y > 0:  # face the camera (it looks along +y), or backface culling hides it
        face.normal_flip()
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(material(f"M_{name}", colour))
    obj = bpy.data.objects.new(name, mesh)
    bpy.data.collections[layer].objects.link(obj)
    return obj


def ellipse_points(cx, cz, rx, rz, turn=0.0, n=96):
    c, s = math.cos(turn), math.sin(turn)
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n
        x, z = rx * math.cos(a), rz * math.sin(a)
        pts.append((cx + x * c - z * s, cz + x * s + z * c))
    return pts


def disc(name, cx, cz, r, depth, colour, layer, rz=None, turn=0.0):
    return flat_shape(name, ellipse_points(cx, cz, r, rz if rz is not None else r, turn), depth, colour, layer)


def star(name, cx, cz, r, depth, colour, layer="FG", rim=None):
    """Chunky four-point sparkle, like Flutter's motif; optional darker rim behind it."""
    def points(radius):
        pts = []
        for k in range(8):
            a = math.pi / 2 + k * math.pi / 4
            rr = radius if k % 2 == 0 else radius * 0.3
            pts.append((cx + rr * math.cos(a), cz + rr * math.sin(a)))
        return pts
    if rim:
        flat_shape(f"{name}_Rim", points(r * 1.16), depth + 0.01, rim, layer)
    return flat_shape(name, points(r), depth, colour, layer)


def music_note(name, cx, cz, s, depth, colour, layer="FG"):
    """An eighth note: tilted oval head, stem and a curved flag."""
    disc(f"{name}_Head", cx, cz, 0.1 * s, depth, colour, layer, rz=0.072 * s, turn=math.radians(25))
    x0 = cx + 0.08 * s
    flat_shape(f"{name}_Stem", [(x0 - 0.024 * s, cz), (x0, cz), (x0, cz + 0.36 * s), (x0 - 0.024 * s, cz + 0.36 * s)], depth, colour, layer)
    top = cz + 0.36 * s
    flag = [(x0 - 0.012 * s, top)]
    for k in range(9):
        t = k / 8
        flag.append((x0 + 0.13 * s * math.sin(t * math.pi * 0.9), top - 0.2 * s * t))
    for k in range(8, -1, -1):
        t = k / 8
        flag.append((x0 - 0.012 * s + 0.08 * s * math.sin(t * math.pi * 0.9), top - 0.08 * s - 0.12 * s * t))
    flat_shape(f"{name}_Flag", flag, depth, colour, layer)


def flower(name, cx, cz, r, depth, petal, heart, layer="FG"):
    for k in range(5):
        a = math.pi / 2 + 2 * math.pi * k / 5
        disc(f"{name}_Petal{k}", cx + 0.55 * r * math.cos(a), cz + 0.55 * r * math.sin(a), 0.45 * r, depth, petal, layer)
    disc(f"{name}_Heart", cx, cz, 0.3 * r, depth - 0.01, heart, layer)


def character(name, x, base, scale, depth, singing=False):
    """Appends the approved character (full-size parts only), stands its feet at `base`."""
    path = os.path.join(CHARACTERS, f"{name}.blend")
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = [n for n in src.objects if n.startswith(f"{name}_") and "_Mini" not in n]
    root = bpy.data.objects.new(f"{name}_Root", None)
    bpy.data.collections["FG"].objects.link(root)
    for obj in dst.objects:
        bpy.data.collections["FG"].objects.link(obj)
        obj.parent = root
        if obj.name.endswith("_Motif"):
            obj.hide_render = True  # sits on the character's side; seen head-on it reads as a stray shape
        if obj.name.endswith("_Mouth_O"):
            obj.hide_render = not singing
        if obj.name.endswith("_Smile"):
            obj.hide_render = singing
    # build_characters.py stores the reference's screen colours; Workbench reads them as linear.
    for mat in {slot.material for obj in dst.objects for slot in obj.material_slots if slot.material}:
        if not mat.get("icon_linear"):
            mat.diffuse_color = (*(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in mat.diffuse_color[:3]), 1.0)
            mat["icon_linear"] = True
    outline = bpy.data.objects[f"{name}_Outline"]
    xs = [v[0] for v in outline.bound_box]
    zs = [v[2] for v in outline.bound_box]
    root.scale = (scale, scale, scale)
    root.location = (x - scale * (min(xs) + max(xs)) / 2, depth, base - scale * min(zs))
    return root


def render(path, layers, transparent):
    scene = bpy.context.scene
    for coll in bpy.context.view_layer.layer_collection.children:
        coll.exclude = coll.name not in layers
    scene.render.film_transparent = transparent
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def setup_camera_and_render(background):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 2.0  # the icon frame is x, z in [-1, 1]
    cam = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = Vector((0.0, -10.0, 0.0))
    cam.rotation_euler = (math.radians(90), 0.0, 0.0)
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_backface_culling = True  # the characters' inverted-hull outlines need it
    scene.display.render_aa = "32"
    scene.render.resolution_x = SIZE
    scene.render.resolution_y = SIZE
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = hex_rgb(background)
    # The background layer is a full square so it is opaque even when rendered on its own.
    flat_shape("Backdrop", [(-1.05, -1.05), (1.05, -1.05), (1.05, 1.05), (-1.05, 1.05)], 5.0, background, "BG")


# ---- The options --------------------------------------------------------------------------------

def trio():
    """All three friends together under a soft spotlight: who the game is about, at a glance."""
    setup_camera_and_render("#173847")
    disc("Glow", 0.0, 0.02, 0.84, 4.0, "#1D4A5A", "BG")
    disc("Spot", 0.0, 0.02, 0.66, 3.9, "#24596A", "BG")
    disc("Shadow", 0.0, -0.56, 0.78, 1.0, "#123039", "FG", rz=0.1)
    character("Flutter", -0.42, -0.52, 0.5, 0.4)
    character("Boom", 0.42, -0.52, 0.5, 0.4)
    character("Bop", 0.0, -0.6, 0.54, 0.0)
    star("Sparkle", -0.6, 0.42, 0.15, -1.0, "#F2CF63", rim="#C9962F")
    star("SparkleSmall", 0.66, 0.4, 0.07, -1.0, "#F2CF63")


def echo_ring():
    """Bop sings in the middle of an echo ring: the loop the music lives on."""
    setup_camera_and_render("#241A44")
    disc("Glow", 0.0, 0.0, 0.62, 4.0, "#2F2458", "BG")
    for k in range(16):
        a = math.pi / 2 - 2 * math.pi * k / 16
        beat = k % 4 == 0
        disc(f"Tick{k}", 0.74 * math.cos(a), 0.74 * math.sin(a), 0.045 if beat else 0.024, 2.0,
             "#ECE6FF" if beat else "#8F82CC", "FG")
    # The play marker (a diamond above 12 o'clock) and three echoes riding the ring.
    flat_shape("Marker", [(0, 0.95), (0.06, 0.88), (0, 0.81), (-0.06, 0.88)], 1.5, "#FFE37A", "FG")
    for k, (body, rim) in ((3, ("#9EE4E6", "#4B9AA8")), (7, ("#F0BB6A", "#C08742")), (12, ("#C79BF0", "#8C66CC"))):
        a = math.pi / 2 - 2 * math.pi * k / 16
        disc(f"Echo{k}_Rim", 0.74 * math.cos(a), 0.74 * math.sin(a), 0.085, 1.6, rim, "FG")
        disc(f"Echo{k}", 0.74 * math.cos(a), 0.74 * math.sin(a), 0.07, 1.5, body, "FG")
    character("Bop", 0.0, -0.4, 0.74, 0.0, singing=True)
    music_note("Note", 0.36, 0.22, 0.6, -1.0, "#FFE37A")


def leaf_stage():
    """The trio on the leaf palette, with a flower: the rainforest that grows with the song."""
    setup_camera_and_render("#FFEBC9")
    disc("Sun", 0.0, 0.08, 0.8, 4.0, "#FFF3DD", "BG")
    leaf = []
    for k in range(64):  # an almond: pointed at both ends
        t = 2 * math.pi * k / 64
        leaf.append((0.92 * math.cos(t), -0.36 + 0.26 * math.sin(t) * abs(math.sin(t)) ** 0.15))
    flat_shape("LeafRim", [(x * 1.04, -0.36 + (z + 0.36) * 1.12) for x, z in leaf], 2.0, "#3E8A3A", "FG")
    flat_shape("Leaf", leaf, 1.9, "#6CC05A", "FG")
    flat_shape("Midrib", [(-0.8, -0.37), (0.8, -0.37), (0.8, -0.35), (-0.8, -0.35)], 1.8, "#D9F2C4", "FG")
    character("Flutter", -0.44, -0.36, 0.44, 0.4)
    character("Boom", 0.44, -0.36, 0.44, 0.4)
    character("Bop", 0.0, -0.42, 0.47, 0.0)
    flower("FlowerA", 0.62, 0.52, 0.13, -1.0, "#FF8CC6", "#FFD86B")
    flower("FlowerB", -0.66, 0.44, 0.09, -1.0, "#B999FF", "#FFD86B")
    star("Sparkle", 0.12, 0.66, 0.08, -1.0, "#F2CF63", rim="#C9962F")


def flutter_sings():
    """One big singing character with notes floating out: bold and readable at the smallest size."""
    setup_camera_and_render("#FF8F7A")
    disc("Glow", -0.08, -0.05, 0.86, 4.0, "#FFA08C", "BG")
    disc("Shadow", -0.12, -0.74, 0.5, 1.0, "#D96A5A", "FG", rz=0.08)
    character("Flutter", -0.14, -0.72, 0.98, 0.0, singing=True)
    music_note("NoteA", 0.5, 0.18, 0.75, -1.0, "#3B2350")
    music_note("NoteB", 0.68, 0.56, 0.5, -1.0, "#3B2350")
    star("Sparkle", -0.66, 0.6, 0.17, -1.0, "#F2CF63", rim="#C9962F")


def combo():
    """Owner's mix (2026-10-29): option 4's coral, option 2's echo ring, option 1's trio on option 3's leaf,
    with the middle left clear for the title (add_title.py writes it)."""
    setup_camera_and_render("#FF8F7A")
    disc("Glow", 0.0, 0.0, 0.88, 4.0, "#FFA08C", "BG")
    ring = 0.83
    # One echo per character on the ring: Flutter (turquoise) right, Bop (purple) left, Boom (orange) at the bottom.
    echoes = {3: ("#9EE4E6", "#4B9AA8"), 13: ("#C79BF0", "#8C66CC"), 8: ("#F0BB6A", "#C08742")}
    for k in range(16):
        if k in echoes:
            continue
        a = math.pi / 2 - 2 * math.pi * k / 16
        beat = k % 4 == 0
        disc(f"Tick{k}", ring * math.cos(a), ring * math.sin(a), 0.04 if beat else 0.022, 2.0,
             "#FFF4EA" if beat else "#FFD0C2", "FG")
    flat_shape("Marker", [(0, 0.99), (0.05, 0.93), (0, 0.87), (-0.05, 0.93)], 1.5, "#FFE37A", "FG")
    for k, (body, rim) in echoes.items():
        a = math.pi / 2 - 2 * math.pi * k / 16
        disc(f"Echo{k}_Rim", ring * math.cos(a), ring * math.sin(a), 0.075, 1.6, rim, "FG")
        disc(f"Echo{k}", ring * math.cos(a), ring * math.sin(a), 0.06, 1.5, body, "FG")
    leaf = []
    for k in range(64):
        t = 2 * math.pi * k / 64
        leaf.append((0.62 * math.cos(t), -0.52 + 0.15 * math.sin(t) * abs(math.sin(t)) ** 0.15))
    flat_shape("LeafRim", [(x * 1.05, -0.52 + (z + 0.52) * 1.18) for x, z in leaf], 2.0, "#3E8A3A", "FG")
    flat_shape("Leaf", leaf, 1.9, "#6CC05A", "FG")
    flat_shape("Midrib", [(-0.52, -0.53), (0.52, -0.53), (0.52, -0.515), (-0.52, -0.515)], 1.8, "#D9F2C4", "FG")
    character("Flutter", -0.29, -0.53, 0.3, 0.4)
    character("Boom", 0.3, -0.53, 0.3, 0.4)
    character("Bop", 0.0, -0.57, 0.32, 0.0)
    star("Sparkle", -0.42, 0.68, 0.07, -1.0, "#F2CF63", rim="#C9962F")  # above the title, clear of it
    # A few white notes floating in the open spaces (owner): right of the title and beside the leaf.
    music_note("NoteA", 0.5, 0.21, 0.27, -1.0, "#FFFFFF")  # between "Little"'s e, Flutter's echo and "Echoes"' s
    music_note("NoteB", -0.64, -0.26, 0.26, -1.0, "#FFFFFF")
    music_note("NoteC", 0.56, -0.38, 0.22, -1.0, "#FFFFFF")


OPTIONS = [("1_trio", trio), ("2_echo_ring", echo_ring), ("3_leaf_stage", leaf_stage), ("4_flutter_sings", flutter_sings),
           ("5_combo", combo)]


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, build in OPTIONS:
        reset()
        build()
        render(os.path.join(OUT, f"{name}.png"), {"BG", "FG"}, False)
        render(os.path.join(OUT, f"{name}_background.png"), {"BG"}, False)
        render(os.path.join(OUT, f"{name}_foreground.png"), {"FG"}, True)
        print(f"[icons] {name}")


if __name__ == "__main__":
    main()

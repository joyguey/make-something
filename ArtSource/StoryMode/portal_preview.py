"""
Story mode, Level 1 "The Seed": concept render (owner, 2026-11-22). The player's real bedroom (a passthrough frame);
a round portal floating beside the nightstand opens onto a faded, misty meadow with a giant World Tree on the horizon;
Flutter flies out toward the player's open left palm, where a glowing note floats; the right hand flicks it toward a
Discord (an original noisy creature) drifting out of the portal. Glow-level finish is the target: depth, haze, warm
low sun, life. Cycles; a concept, not the game's renderer.

  Blender -b -P portal_preview.py -- <out.png> <room.jpg>
"""
import bpy, bmesh, math, random, sys
from mathutils import Vector, noise, Euler

argv = sys.argv[sys.argv.index("--") + 1:]
OUT, ROOM = argv[0], argv[1]
sys.path.insert(0, "/Users/joy/Desktop/PICO Project/ArtSource/StoryMode")
import friends_cycles as fc

rng = random.Random(11)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

def link(o): sc.collection.objects.link(o); return o

def mesh_obj(name, bm, mat=None, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me))
    if mat: o.data.materials.append(mat)
    if smooth:
        for p in o.data.polygons: p.use_smooth = True
    return o

def principled(name, color, rough=0.5, emit=None, strength=0.0, sss=0.0, alpha=1.0, transmission=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1); b.inputs["Roughness"].default_value = rough
    if sss: b.inputs["Subsurface Weight"].default_value = sss
    if emit: b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = strength
    if transmission: b.inputs["Transmission Weight"].default_value = transmission
    if alpha < 1: b.inputs["Alpha"].default_value = alpha
    return m

def emission(name, color, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.remove(nt.nodes["Principled BSDF"])
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = (*color, 1); e.inputs["Strength"].default_value = strength
    nt.links.new(e.outputs[0], nt.nodes["Material Output"].inputs["Surface"])
    return m

def ghost(name):
    """PICO-style hand: see-through white, brighter at the edges."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.remove(nt.nodes["Principled BSDF"])
    lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.35
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = (0.92, 0.95, 1.0, 1); e.inputs["Strength"].default_value = 1.6
    t = nt.nodes.new("ShaderNodeBsdfTransparent")
    ramp = nt.nodes.new("ShaderNodeMath"); ramp.operation = "MULTIPLY_ADD"; ramp.inputs[1].default_value = 0.55; ramp.inputs[2].default_value = 0.42
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lw.outputs["Facing"], ramp.inputs[0]); nt.links.new(ramp.outputs[0], mix.inputs["Fac"])
    nt.links.new(t.outputs[0], mix.inputs[1]); nt.links.new(e.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], nt.nodes["Material Output"].inputs["Surface"])
    m.blend_method = "BLEND"
    return m

# ---------------- camera: the player's eyes (the room photo's view) ----------------
cam = bpy.data.cameras.new("Eye"); cam.sensor_fit = "HORIZONTAL"; cam.lens = 15.5  # ~100 deg wide, like the headset capture
eye = link(bpy.data.objects.new("Eye", cam)); sc.camera = eye
eye.location = (0, 0, 0); eye.rotation_euler = (math.radians(90), 0, 0)  # looking along +Y, Z up

# ---------------- the portal: a round opening, upright, beside the nightstand ----------------
PC = Vector((0.45, 1.55, -0.15)); PR = 0.5
# the room around the opening: a holdout (shows the photo), the hole shows the world behind
bm = bmesh.new()
outer = bmesh.ops.create_circle(bm, cap_ends=False, segments=128, radius=6.0)["verts"]
inner = bmesh.ops.create_circle(bm, cap_ends=False, segments=128, radius=PR)["verts"]
for i in range(128):
    bm.faces.new((outer[i], outer[(i + 1) % 128], inner[(i + 1) % 128], inner[i]))
wall = mesh_obj("PortalHoldout", bm, None, False)
wall.rotation_euler = (math.radians(90), 0, 0); wall.location = PC
hold = bpy.data.materials.new("Holdout"); hold.use_nodes = True
hn = hold.node_tree; hn.nodes.remove(hn.nodes["Principled BSDF"])
hn.links.new(hn.nodes.new("ShaderNodeHoldout").outputs[0], hn.nodes["Material Output"].inputs["Surface"])
wall.data.materials.append(hold)
wall.visible_shadow = False; wall.visible_diffuse = False; wall.visible_glossy = False

# the rim: a soft golden ring of light, wrapped in leaves and small flowers
rim_gold = emission("RimGold", (1.0, 0.78, 0.42), 6.0)
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=False, segments=8, radius1=0.01, radius2=0.01, depth=0.01)
ring = bpy.data.curves.new("RimCurve", "CURVE"); ring.dimensions = "3D"; ring.bevel_depth = 0.009
sp = ring.splines.new("POLY"); sp.points.add(127)
for i, p in enumerate(sp.points):
    a = i / 128 * math.tau; r = PR + 0.004 * math.sin(a * 9)
    p.co = (PC.x + math.cos(a) * r, PC.y, PC.z + math.sin(a) * r, 1)
sp.use_cyclic_u = True
rim = link(bpy.data.objects.new("Rim", ring)); ring.materials.append(rim_gold)
leaf_mats = [principled(f"RimLeaf{i}", c, 0.5, sss=0.4) for i, c in enumerate([(0.28, 0.55, 0.22), (0.4, 0.68, 0.3), (0.2, 0.45, 0.25)])]
petal_mats = [principled(f"RimPetal{i}", c, 0.45, emit=c, strength=0.6, sss=0.3) for i, c in enumerate([(1.0, 0.62, 0.75), (1.0, 0.9, 0.5), (0.8, 0.7, 1.0), (1.0, 1.0, 0.96)])]
for i in range(150):
    a = rng.uniform(0, math.tau); r = PR + rng.uniform(-0.012, 0.03)
    if 0.25 < (a % math.tau) / math.tau < 0.38 and rng.random() < 0.7: continue  # a gap where the Discord slips out
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=1.0)
    big = rng.random() < 0.75
    leaf = mesh_obj(f"RimLeaf{i}", bm, rng.choice(leaf_mats) if big else rng.choice(petal_mats))
    s = rng.uniform(0.012, 0.026) if big else rng.uniform(0.006, 0.011)
    leaf.scale = (s, s * 0.18, s * 0.45) if big else (s, s * 0.6, s)
    leaf.location = (PC.x + math.cos(a) * r, PC.y + rng.uniform(-0.03, 0.01), PC.z + math.sin(a) * r)
    leaf.rotation_euler = (rng.uniform(-0.4, 0.4), a + rng.uniform(-0.6, 0.6), rng.uniform(0, math.tau))
glow = bpy.data.lights.new("PortalLight", "AREA"); glow.shape = "DISK"; glow.size = PR * 2; glow.energy = 60; glow.color = (1.0, 0.9, 0.75)
gl = link(bpy.data.objects.new("PortalLight", glow)); gl.location = PC + Vector((0, -0.02, 0)); gl.rotation_euler = (math.radians(-90), 0, 0)

# ---------------- the world behind: a faded, misty meadow at golden hour, a giant World Tree far off ----------------
W0 = PC.y + 0.3  # everything of the world lies beyond the opening
def terrain(name, size, height, offset_y, z, color_a, color_b, seg=160, scale=0.012):
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=seg, y_segments=seg, size=size / 2)
    for v in bm.verts:
        p = Vector((v.co.x, v.co.y + offset_y, 0)) * scale
        v.co.z = height * (noise.noise(p) * 0.7 + noise.noise(p * 3.1) * 0.25 + noise.noise(p * 9) * 0.05)
    o = mesh_obj(name, bm, None)
    m = bpy.data.materials.new(name + "Mat"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    n = nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value = 0.08; n.inputs["Detail"].default_value = 10
    r = nt.nodes.new("ShaderNodeValToRGB"); r.color_ramp.elements[0].color = (*color_a, 1); r.color_ramp.elements[1].color = (*color_b, 1)
    nt.links.new(n.outputs["Fac"], r.inputs["Fac"]); nt.links.new(r.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.95
    o.data.materials.append(m)
    o.location = (0, offset_y, z)
    return o
terrain("Meadow", 600, 7, W0 + 300, -5.0, (0.32, 0.47, 0.22), (0.5, 0.6, 0.3))
terrain("FarHills", 2400, 90, W0 + 1300, -30, (0.45, 0.5, 0.5), (0.6, 0.62, 0.62), 120, 0.004)

def tree(name, x, y, z, h, crown, mat_c, mat_t):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=h * 0.05, radius2=h * 0.025, depth=h)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, h / 2)); t = mesh_obj(name + "T", bm, mat_t); t.location = (x, y, z)
    for k in range(4):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=crown * rng.uniform(0.6, 0.9))
        for v in bm.verts: v.co += v.co.normalized() * crown * 0.15 * noise.noise(v.co * 3 + Vector((k, x, y)))
        c = mesh_obj(name + f"C{k}", bm, mat_c); c.location = (x + rng.uniform(-0.3, 0.3) * crown, y + rng.uniform(-0.3, 0.3) * crown, z + h + rng.uniform(-0.2, 0.4) * crown)

def ground(x, y):
    p = Vector((x, y, 0)) * 0.012
    return -5.0 + 7 * (noise.noise(p) * 0.7 + noise.noise(p * 3.1) * 0.25 + noise.noise(p * 9) * 0.05)

MEADOW = "/Users/joy/Desktop/PICO Project/ArtSource/Meadow/Meadow.blend"
KINDS = ["Meadow_TreeRound0", "Meadow_TreeRound1", "Meadow_TreePine0", "Meadow_TreePine1", "Meadow_TreeBlossom0",
         "Meadow_Bush0", "Meadow_Bush1", "Meadow_Bush2", "Meadow_Rock0", "Meadow_Rock1", "Meadow_Flowers0",
         "Meadow_Flowers1", "Meadow_Flowers2", "Meadow_Grass0", "Meadow_Grass1", "Meadow_Cloud0", "Meadow_Cloud1"]
with bpy.data.libraries.load(MEADOW) as (src, dst):
    dst.objects = [n for n in KINDS if n in src.objects]

def vcol_material(name, saturation):
    """The meadow's look: vertex colour (stored sRGB) times its baked shade (alpha); `saturation` < 1 = faded."""
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    a = nt.nodes.new("ShaderNodeVertexColor"); a.layer_name = "Col"
    g = nt.nodes.new("ShaderNodeGamma"); g.inputs["Gamma"].default_value = 2.2
    hs = nt.nodes.new("ShaderNodeHueSaturation"); hs.inputs["Saturation"].default_value = saturation
    mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs["Factor"].default_value = 1.0
    nt.links.new(a.outputs["Color"], g.inputs["Color"]); nt.links.new(g.outputs["Color"], hs.inputs["Color"])
    nt.links.new(hs.outputs["Color"], mul.inputs[6]); nt.links.new(a.outputs["Alpha"], mul.inputs[7])
    nt.links.new(mul.outputs[2], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.85
    b.inputs["Subsurface Weight"].default_value = 0.1
    return m

NEAR, FAR = vcol_material("MeadowNear", 1.0), vcol_material("MeadowFar", 0.45)  # colour is coming back near the opening
src_objs = {o.name: o for o in dst.objects}

def place(kind, x, y, scale, turn, z=None):
    base = src_objs.get(kind)
    if base is None: return None
    o = base.copy(); link(o)
    o.data = base.data
    o.location = (x, y, ground(x, y) if z is None else z)
    o.scale = (scale, scale, scale); o.rotation_euler = (0, 0, turn)
    near = (x - PC.x * 3) ** 2 + (y - W0) ** 2 < 45 ** 2
    o.material_slots[0].link = "OBJECT"; o.material_slots[0].material = NEAR if near else FAR
    return o

trees = KINDS[:5]
for i in range(420):  # woods and copses over the rolling meadow
    x = rng.uniform(-160, 160); y = W0 + rng.uniform(10, 420)
    if abs(x - (PC.x / PC.y) * (y - 0.0)) < 4 + (y - W0) * 0.09: continue  # a clear sightline through the portal to the far tree
    place(rng.choice(trees + ["Meadow_TreeRound0", "Meadow_TreePine0"]), x, y, rng.uniform(1.4, 2.6), rng.uniform(0, math.tau))
for i in range(500):  # bushes, rocks, flowers and grass near the opening
    x = rng.uniform(-25, 25); y = W0 + rng.uniform(1.5, 45)
    place(rng.choice(KINDS[5:15]), x, y, rng.uniform(1.0, 2.2), rng.uniform(0, math.tau))
for i in range(14):  # clouds
    place(rng.choice(KINDS[15:]), rng.uniform(-260, 260), W0 + rng.uniform(150, 600), rng.uniform(8, 18), rng.uniform(0, math.tau), rng.uniform(60, 130))

# the World Tree: the landmark (owner, 2026-11-22: "1"). Far off in the haze and fully inside the window: a flared,
# twisting trunk on great roots, branches reaching out, a canopy of the meadow's own leafy clusters, golden lanterns
# among the leaves, mist at its feet, birds wheeling round it.
TD = 900.0
WT = Vector(((PC.x / PC.y) * TD * 0.78, TD, 0)); WT.z = ground(WT.x, WT.y) - 2
wbark = bpy.data.materials.new("WorldBark"); wbark.use_nodes = True
wn_ = wbark.node_tree; wb = wn_.nodes["Principled BSDF"]
wt = wn_.nodes.new("ShaderNodeTexWave"); wt.wave_type = "BANDS"; wt.bands_direction = "Z"; wt.inputs["Scale"].default_value = 0.08
wt.inputs["Distortion"].default_value = 6.0; wt.inputs["Detail"].default_value = 4
wr = wn_.nodes.new("ShaderNodeValToRGB"); wr.color_ramp.elements[0].color = (0.22, 0.17, 0.14, 1); wr.color_ramp.elements[1].color = (0.46, 0.38, 0.32, 1)
wn_.links.new(wt.outputs["Fac"], wr.inputs["Fac"]); wn_.links.new(wr.outputs["Color"], wb.inputs["Base Color"])
wbump = wn_.nodes.new("ShaderNodeBump"); wbump.inputs["Strength"].default_value = 0.6
wn_.links.new(wt.outputs["Fac"], wbump.inputs["Height"]); wn_.links.new(wbump.outputs["Normal"], wb.inputs["Normal"])
wb.inputs["Roughness"].default_value = 0.9

# trunk: stacked rings, flaring at the foot, gently twisting and leaning
rings, seg = 26, 28
bm = bmesh.new(); prev = None
for i in range(rings + 1):
    t = i / rings; h = t * 95
    r = 9 + 22 * (1 - t) ** 3.2 - 3 * t
    lean = Vector((math.sin(t * 2.2) * 4, 0, 0))
    ring = []
    for k in range(seg):
        a = k / seg * math.tau + t * 0.9
        bump = 1 + 0.18 * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, t * 6))) + (0.25 * (1 - t) ** 4 if k % 4 == 0 else 0)
        ring.append(bm.verts.new((lean.x + math.cos(a) * r * bump, math.sin(a) * r * bump * 0.85, h)))
    if prev:
        for k in range(seg):
            bm.faces.new((prev[k], prev[(k + 1) % seg], ring[(k + 1) % seg], ring[k]))
    prev = ring
trunk = mesh_obj("WorldTrunk", bm, wbark); trunk.location = WT
sub = trunk.modifiers.new("Smooth", "SUBSURF"); sub.levels = 1; sub.render_levels = 2

def limb(name, pts, r0, r1, mat):
    cu = bpy.data.curves.new(name, "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 1.0; cu.bevel_resolution = 4
    spl = cu.splines.new("BEZIER"); spl.bezier_points.add(len(pts) - 1)
    for k, (q, co) in enumerate(zip(spl.bezier_points, pts)):
        q.co = co; q.handle_left_type = q.handle_right_type = "AUTO"
        q.radius = r0 + (r1 - r0) * k / (len(pts) - 1)
    o = link(bpy.data.objects.new(name, cu)); cu.materials.append(mat); o.location = WT
    return o

for k in range(7):  # great roots spreading over the land
    a = k / 7 * math.tau + rng.uniform(-0.2, 0.2); L = rng.uniform(45, 70)
    limb(f"WorldRoot{k}", [(math.cos(a) * 14, math.sin(a) * 12, 10), (math.cos(a) * L * 0.55, math.sin(a) * L * 0.5, 3), (math.cos(a) * L, math.sin(a) * L * 0.9, -1)], 7, 1.2, wbark)
crown_tips = []
for k in range(9):  # branches reaching up and out
    a = k / 9 * math.tau + rng.uniform(-0.25, 0.25); up = rng.uniform(0.0, 1.0)
    start = Vector((math.sin(1.9) * 4 + math.cos(a) * 6, math.sin(a) * 5, rng.uniform(60, 88)))
    reach = rng.uniform(45, 75)
    tip = start + Vector((math.cos(a) * reach, math.sin(a) * reach * 0.7, 18 + up * 28))
    mid = start.lerp(tip, 0.5) + Vector((0, 0, 10))
    limb(f"WorldBranch{k}", [tuple(start), tuple(mid), tuple(tip)], 5.5, 1.0, wbark)
    crown_tips.append(tip)
crown_tips.append(Vector((math.sin(1.9) * 4, 0, 120)))

# canopy: the meadow's own leafy bushes, huge, clustered on every branch tip and over the top
leafy = [src_objs[n] for n in ("Meadow_Bush0", "Meadow_Bush1", "Meadow_Bush2") if n in src_objs]
canopy_mat = vcol_material("WorldCanopy", 0.8)
lantern = emission("Lantern", (1.0, 0.82, 0.45), 40)
for i, tip in enumerate(crown_tips):
    for k in range(7 if i < len(crown_tips) - 1 else 14):
        base = rng.choice(leafy); o = base.copy(); link(o); o.data = base.data
        sc_ = rng.uniform(34, 52) * (1.25 if i == len(crown_tips) - 1 else 1.0)
        off = Vector((rng.uniform(-1, 1), rng.uniform(-0.8, 0.8), rng.uniform(-0.3, 0.6))) * sc_ * 0.55
        o.location = WT + tip + off; o.scale = (sc_, sc_, sc_ * 0.8); o.rotation_euler = (0, 0, rng.uniform(0, math.tau))
        o.material_slots[0].link = "OBJECT"; o.material_slots[0].material = canopy_mat
    for k in range(6):  # golden lanterns among the leaves
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=1.4)
        mesh_obj(f"Lantern{i}_{k}", bm, lantern).location = WT + tip + Vector((rng.uniform(-22, 22), rng.uniform(-30, -5), rng.uniform(-14, 6)))
tl = bpy.data.lights.new("TreeGlow", "POINT"); tl.energy = 4.0e6; tl.color = (1.0, 0.8, 0.5); tl.shadow_soft_size = 30
link(bpy.data.objects.new("TreeGlow", tl)).location = WT + Vector((0, -40, 110))

# mist pooled at its feet
mist = bpy.data.materials.new("Mist"); mist.use_nodes = True
mt = mist.node_tree; mt.nodes.remove(mt.nodes["Principled BSDF"])
mv = mt.nodes.new("ShaderNodeVolumePrincipled"); mv.inputs["Density"].default_value = 0.02; mv.inputs["Color"].default_value = (0.95, 0.92, 0.9, 1)
mt.links.new(mv.outputs[0], mt.nodes["Material Output"].inputs["Volume"])
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
mb_ = mesh_obj("FootMist", bm, mist, False); mb_.location = WT + Vector((0, 0, 6)); mb_.scale = (420, 260, 22)

# birds wheeling round the crown
bird = principled("Bird", (0.12, 0.1, 0.1), 0.8)
for k in range(26):
    a = rng.uniform(0, math.tau); r = rng.uniform(70, 140)
    bm = bmesh.new()
    v0 = bm.verts.new((-1.6, 0, 0.5)); v1 = bm.verts.new((0, 0.3, 0)); v2 = bm.verts.new((1.6, 0, 0.5)); v3 = bm.verts.new((0, -0.4, 0.05))
    bm.faces.new((v0, v1, v3)); bm.faces.new((v1, v2, v3))
    b_ = mesh_obj(f"Bird{k}", bm, bird, False); b_.location = WT + Vector((math.cos(a) * r, math.sin(a) * r * 0.5 - 60, rng.uniform(95, 160)))
    b_.scale = (1.2, 1.2, 1.2); b_.rotation_euler = (0, rng.uniform(-0.4, 0.4), a)

# near the opening, colour is returning: a few flowers glow back to life
for i in range(70):
    x = PC.x + rng.uniform(-6, 6); y = W0 + rng.uniform(2, 14)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=5, radius=rng.uniform(0.06, 0.14))
    f = mesh_obj(f"WildFlower{i}", bm, rng.choice(petal_mats)); f.location = (x, y, ground(x, y) + 0.2); f.scale = (1, 1, 0.5)

# sky: a low golden sun; haze through the whole world (atmospheric depth)
world = bpy.data.worlds.new("Sky"); sc.world = world; world.use_nodes = True
wn = world.node_tree
sky = wn.nodes.new("ShaderNodeTexSky"); sky.sky_type = "NISHITA"; sky.sun_elevation = math.radians(9); sky.sun_rotation = math.radians(200)
sky.altitude = 200; sky.air_density = 1.4; sky.dust_density = 5.0; sky.sun_elevation = math.radians(6)
wn.links.new(sky.outputs["Color"], wn.nodes["Background"].inputs["Color"]); wn.nodes["Background"].inputs["Strength"].default_value = 0.18
sun = bpy.data.lights.new("Sun", "SUN"); sun.energy = 1.4; sun.color = (1.0, 0.82, 0.6); sun.angle = math.radians(1)
so = link(bpy.data.objects.new("Sun", sun)); so.rotation_euler = (math.radians(80), 0, math.radians(200))
haze = bpy.data.materials.new("Haze"); haze.use_nodes = True
hz = haze.node_tree; hz.nodes.remove(hz.nodes["Principled BSDF"])
vol = hz.nodes.new("ShaderNodeVolumePrincipled"); vol.inputs["Density"].default_value = 0.0011; vol.inputs["Color"].default_value = (0.92, 0.88, 0.85, 1)
vol.inputs["Anisotropy"].default_value = 0.5
hz.links.new(vol.outputs[0], hz.nodes["Material Output"].inputs["Volume"])
bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
hb = mesh_obj("HazeBox", bm, haze, False); hb.location = (0, W0 + 900, 150); hb.scale = (3600, 1798, 500)

# ---------------- in the room: Flutter flying out toward the open left palm ----------------
fl = fc.friend(sc, "Flutter", (-0.06, 0.9, -0.1), 0.11, look_at=(-0.1, -0.5))
fl.rotation_euler = (math.radians(14), math.radians(-8), fl.rotation_euler.z)  # leaning into the flight
spark = emission("Spark", (1.0, 0.92, 0.65), 25)
trail_pts = [PC + Vector((-0.2, 0.02, 0.05)), Vector((0.1, 1.2, 0.02)), Vector((-0.02, 0.95, -0.05))]
for i in range(36):
    t = rng.uniform(0, 1)
    p = trail_pts[0].lerp(trail_pts[1], t * 2) if t < 0.5 else trail_pts[1].lerp(trail_pts[2], t * 2 - 1)
    p += Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03)))
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=rng.uniform(0.0015, 0.004))
    s = mesh_obj(f"FlutterSpark{i}", bm, spark); s.location = p

# ---------------- hands: metaballs, PICO-style see-through ----------------
GHOST = ghost("Ghost")
def hand(name, loc, rot, pose, scale=1.0):
    """A soft stylised hand from a skeleton (skin + subdivision): fingers along +Y, palm facing +Z.
    pose = curl per finger (index, middle, ring, little) and the thumb, radians per joint."""
    verts, edges, radii = [], [], []
    def v(co, r):
        verts.append(Vector(co)); radii.append(r); return len(verts) - 1
    # a solid palm: a tree of joints from the wrist (a closed loop would skin into a ring with a hole)
    wrist = v((0, -0.055, -0.004), 0.022)
    centre = v((0, -0.005, 0), 0.03)
    palm = [v((-0.02, -0.02, 0), 0.022), v((0.02, -0.02, 0), 0.021), v((-0.018, 0.028, 0), 0.018), v((0.018, 0.028, 0), 0.017)]
    for i, j in ((wrist, centre), (centre, palm[0]), (centre, palm[1]), (centre, palm[2]), (centre, palm[3])):
        edges.append((i, j))
    lengths = (0.075, 0.082, 0.077, 0.062)
    for k, fx in enumerate((-0.024, -0.008, 0.009, 0.025)):
        base = Vector((fx, 0.035 + (0.004 if k in (1, 2) else 0), 0.0))
        prev = v(base, 0.0105 - k * 0.0006)
        edges.append((palm[2] if k < 2 else palm[3], prev))
        d = Vector((fx * 0.15, 1, 0)).normalized()
        for seg, part in enumerate((0.45, 0.32, 0.23)):
            d = Euler((pose[k], 0, 0)).to_matrix() @ d
            base = base + d * lengths[k] * part
            cur = v(base, (0.0095 - seg * 0.0012) - k * 0.0006)
            edges.append((prev, cur)); prev = cur
    tb = Vector((-0.03, -0.015, -0.004)); prev = v(tb, 0.012); edges.append((palm[0], prev))
    d = Vector((-0.75, 0.55, 0.2)).normalized()
    for seg, part in enumerate((0.03, 0.026, 0.02)):
        d = Euler((pose[4], 0, -0.25)).to_matrix() @ d
        tb = tb + d * part
        cur = v(tb, 0.0105 - seg * 0.0012); edges.append((prev, cur)); prev = cur
    me = bpy.data.meshes.new(name); me.from_pydata([tuple(x) for x in verts], edges, [])
    o = link(bpy.data.objects.new(name, me))
    o.modifiers.new("Skin", "SKIN")
    for i, r in enumerate(radii):
        me.skin_vertices[0].data[i].radius = (r, r * 0.8)
    me.skin_vertices[0].data[wrist].use_root = True
    sub = o.modifiers.new("Smooth", "SUBSURF"); sub.levels = 2; sub.render_levels = 3
    o.data.materials.append(GHOST)
    o.location = loc; o.rotation_euler = rot; o.scale = (scale, scale, scale)
    return o

# left hand: open, palm up, the note floating above it
left = hand("LeftHand", (-0.17, 0.5, -0.25), (math.radians(-12), math.radians(-10), math.radians(-18)), (0.18, 0.12, 0.14, 0.18, 0.05))  # open, palm up
# right hand: index flicking, the others curled
right = hand("RightHand", (0.03, 0.47, -0.24), (math.radians(-35), math.radians(10), math.radians(62)), (0.25, 0.85, 0.95, 1.0, 0.45))  # index cocked to flick

# the note above the palm: a glowing eighth note with a halo
note_gold = emission("NoteGold", (1.0, 0.82, 0.38), 9)
NP = Vector((-0.168, 0.53, -0.17))  # floating just above the open palm
# the note: solid shapes (a tilted round head, a stem, a curved flag), parented to one holder
note = link(bpy.data.objects.new("Note", None))
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=0.016)
head = mesh_obj("NoteHead", bm, note_gold); head.scale = (1.3, 0.85, 0.95); head.rotation_euler = (0, math.radians(-22), 0); head.parent = note
bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.0035, radius2=0.0035, depth=0.05)
bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, 0.025))
stem = mesh_obj("NoteStem", bm, note_gold); stem.location = (0.017, 0, 0.004); stem.parent = note
flag = bpy.data.curves.new("NoteFlag", "CURVE"); flag.dimensions = "3D"; flag.bevel_depth = 0.0035
fp = flag.splines.new("BEZIER"); fp.bezier_points.add(2)
for q, co in zip(fp.bezier_points, [(0.017, 0, 0.054), (0.032, 0, 0.042), (0.03, 0, 0.022)]):
    q.co = co; q.handle_left_type = q.handle_right_type = "AUTO"
fo = link(bpy.data.objects.new("NoteFlag", flag)); flag.materials.append(note_gold); fo.parent = note
note.location = NP; note.rotation_euler = (math.radians(-10), math.radians(-12), math.radians(14))
for k in range(10):  # a soft ring of light motes circling the note
    a = k / 10 * math.tau
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=0.0018)
    mesh_obj(f"NoteMote{k}", bm, emission("NoteMote", (1.0, 0.9, 0.6), 20)).location = NP + Vector((math.cos(a) * 0.045, math.sin(a) * 0.02, 0.025 + math.sin(a * 2) * 0.008))
nl = bpy.data.lights.new("NoteLight", "POINT"); nl.energy = 6.0; nl.color = (1.0, 0.8, 0.45); nl.shadow_soft_size = 0.02
link(bpy.data.objects.new("NoteLight", nl)).location = NP
# the flick's path: a faint golden arc from the note toward the Discord
DP = PC + Vector((PR * 0.55, -0.22, PR * 0.45))
arc = bpy.data.curves.new("Arc", "CURVE"); arc.dimensions = "3D"; arc.bevel_depth = 0.0016
sp = arc.splines.new("BEZIER"); sp.bezier_points.add(2)
for p, co in zip(sp.bezier_points, [NP + Vector((0.02, 0.0, 0.02)), (NP + DP) / 2 + Vector((0, 0, 0.22)), DP + Vector((-0.04, -0.01, -0.02))]):
    p.co = co; p.handle_left_type = p.handle_right_type = "AUTO"
for k, p in enumerate(sp.bezier_points): p.radius = 1.0 - 0.6 * k / 2
arcobj = link(bpy.data.objects.new("Arc", arc)); arc.materials.append(emission("ArcGold", (1.0, 0.8, 0.4), 1.2)); arc.bevel_depth = 0.0007
for i in range(22):  # dotted sparkles along the arc
    t = (i + 0.5) / 22
    a, b2, c = NP, (NP + DP) / 2 + Vector((0, 0, 0.22)), DP
    p = a * (1 - t) ** 2 + b2 * 2 * (1 - t) * t + c * t * t
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=0.0025 * (1 - t * 0.5))
    mesh_obj(f"ArcSpark{i}", bm, spark).location = p

# ---------------- the Discord: an original noisy little creature drifting out of the portal ----------------
dbody = principled("DiscordBody", (0.16, 0.15, 0.2), 0.35)
dcrack = emission("DiscordCrack", (1.0, 0.18, 0.45), 9)
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=4, radius=0.06)
for v in bm.verts:
    v.co += v.co.normalized() * 0.06 * (0.25 * max(0, noise.noise(v.co * 70)) ** 1.5 * 3 + 0.08 * noise.noise(v.co * 30))
disc = mesh_obj("Discord", bm, dbody); disc.data.materials.append(dcrack)
for p in disc.data.polygons:
    if noise.noise(p.center * 60) > 0.38: p.material_index = 1
disc.location = DP
deye = emission("DiscordEye", (1.0, 0.92, 0.6), 22)
for s in (-1, 1):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.007)
    ey = mesh_obj(f"DiscordEye{s}", bm, deye); ey.location = DP + Vector((s * 0.021, -0.058, 0.012)); ey.scale = (1.3, 0.8, 1.0)
for i in range(9):  # jagged static it sheds
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=3, radius1=rng.uniform(0.004, 0.01), radius2=0, depth=rng.uniform(0.01, 0.025))
    sh = mesh_obj(f"Static{i}", bm, dcrack); a = rng.uniform(0, math.tau)
    sh.location = DP + Vector((math.cos(a) * rng.uniform(0.06, 0.1), rng.uniform(-0.03, 0.03), math.sin(a) * rng.uniform(0.05, 0.09)))
    sh.rotation_euler = (rng.uniform(0, 3), rng.uniform(0, 3), rng.uniform(0, 3))

# fireflies drifting out into the room
fire = emission("Firefly", (1.0, 0.9, 0.55), 30)
for i in range(30):
    a = rng.uniform(0, math.tau); r = PR * rng.uniform(0.6, 2.2)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=rng.uniform(0.0012, 0.0028))
    mesh_obj(f"Firefly{i}", bm, fire).location = PC + Vector((math.cos(a) * r, rng.uniform(-0.6, -0.05), math.sin(a) * r * 0.8))

# warm room light on the things in the room (matching the lamp), cool-gold portal light from the opening
room = bpy.data.lights.new("RoomLamp", "AREA"); room.energy = 25; room.size = 0.8; room.color = (1.0, 0.72, 0.45)
rl = link(bpy.data.objects.new("RoomLamp", room)); rl.location = (-0.3, 1.2, 0.6); rl.rotation_euler = (math.radians(-130), math.radians(-20), 0)

# ---------------- composite over the room photo ----------------
sc.render.film_transparent = False  # the holdout alone opens the room; the sky shows in the portal
sc.use_nodes = True
nt = sc.node_tree
for n in list(nt.nodes): nt.nodes.remove(n)
rlay = nt.nodes.new("CompositorNodeRLayers")
img = nt.nodes.new("CompositorNodeImage"); img.image = bpy.data.images.load(ROOM)
scale = nt.nodes.new("CompositorNodeScale"); scale.space = "RENDER_SIZE"; scale.frame_method = "CROP"
glare = nt.nodes.new("CompositorNodeGlare"); glare.glare_type = "FOG_GLOW"; glare.quality = "HIGH"; glare.threshold = 1.0; glare.size = 7
over = nt.nodes.new("CompositorNodeAlphaOver")
out = nt.nodes.new("CompositorNodeComposite")
nt.links.new(img.outputs["Image"], scale.inputs["Image"]); nt.links.new(scale.outputs["Image"], over.inputs[1])
nt.links.new(rlay.outputs["Image"], glare.inputs["Image"]); nt.links.new(glare.outputs["Image"], over.inputs[2])
nt.links.new(over.outputs["Image"], out.inputs["Image"])

sc.render.engine = "CYCLES"
pr = bpy.context.preferences.addons["cycles"].preferences; pr.compute_device_type = "METAL"; pr.get_devices()
for d in pr.devices: d.use = True
sc.cycles.device = "GPU"; sc.cycles.samples = 192; sc.cycles.use_denoising = True
sc.cycles.volume_step_rate = 4; sc.cycles.volume_bounces = 0
sc.view_settings.view_transform = "AgX"; sc.view_settings.look = "AgX - Punchy"
sc.render.resolution_x, sc.render.resolution_y = 2560, 1440
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[portal] rendered", OUT)

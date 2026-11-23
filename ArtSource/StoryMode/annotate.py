import bpy, sys
args = sys.argv[sys.argv.index("--") + 1:]; SRC, OUT = args[0], args[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; W, H = 2560, 1440
sc.render.resolution_x, sc.render.resolution_y = W, H
cam = bpy.data.cameras.new("C"); cam.type = "ORTHO"; cam.ortho_scale = W / 100.0
co = bpy.data.objects.new("C", cam); sc.collection.objects.link(co); sc.camera = co; co.location = (W / 200.0, H / 200.0, 10)
def mat(name, c, strength=1.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.remove(nt.nodes["Principled BSDF"])
    e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = c; e.inputs["Strength"].default_value = strength
    nt.links.new(e.outputs[0], nt.nodes["Material Output"].inputs["Surface"]); return m
white, plate = mat("W", (1, 1, 1, 1)), mat("P", (0.1, 0.07, 0.19, 1))
def label(text, x, y):  # x, y: top-left in image pixels
    cu = bpy.data.curves.new(text, "FONT"); cu.body = text; cu.size = 0.42
    t = bpy.data.objects.new(text, cu); sc.collection.objects.link(t); t.data.materials.append(white)
    bpy.context.view_layer.update()
    w = t.dimensions.x
    t.location = (x / 100.0 + 0.18, (H - y) / 100.0 - 0.45, 1)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(x / 100.0 + w / 2 + 0.18, (H - y) / 100.0 - 0.32, 0.5))
    p = bpy.context.active_object; p.scale = (w + 0.36, 0.66, 1); p.data.materials.append(plate)
for text, x, y in [("1 - Foreground (moves fastest)", 1450, 1330), ("2 - The path: the friends journey right  >", 1140, 1065),
                   ("3 - Woods and hills", 1300, 700), ("4 - World Tree (barely moves)", 1330, 440),
                   ("A Discord blocks the way", 1820, 1000), ("A Discord escapes into your room!", 1700, 300),
                   ("Flick the note from your palm", 520, 1110)]:
    label(text, x, y)
sc.render.engine = "BLENDER_EEVEE_NEXT"; sc.render.film_transparent = True; sc.view_settings.view_transform = "Standard"
sc.use_nodes = True; nt = sc.node_tree
for n in list(nt.nodes): nt.nodes.remove(n)
rl = nt.nodes.new("CompositorNodeRLayers"); im = nt.nodes.new("CompositorNodeImage"); im.image = bpy.data.images.load(SRC)
ao = nt.nodes.new("CompositorNodeAlphaOver"); out = nt.nodes.new("CompositorNodeComposite")
nt.links.new(im.outputs["Image"], ao.inputs[1]); nt.links.new(rl.outputs["Image"], ao.inputs[2]); nt.links.new(ao.outputs["Image"], out.inputs["Image"])
sc.render.filepath = OUT; bpy.ops.render.render(write_still=True); print("[annotate] ok")

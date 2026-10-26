"""Front-view preview render of a built character (flat colours), for comparing with the reference.
  Blender --background ArtSource/Characters/<Name>.blend --python ArtSource/Characters/preview_character.py -- <out.png>
"""
import math
import sys

import bpy
from mathutils import Vector

out = sys.argv[sys.argv.index("--") + 1]
scene = bpy.context.scene
for obj in scene.objects:
    if obj.name.endswith("_Mouth_O"):
        obj.hide_render = True  # surprise mouth is parked inside the body in Unity until used

cam_data = bpy.data.cameras.new("PreviewCam")
cam_data.type = "ORTHO"
cam_data.ortho_scale = 1.9
cam = bpy.data.objects.new("PreviewCam", cam_data)
scene.collection.objects.link(cam)
cam.location = Vector((0.0, -4.0, 0.05))
cam.rotation_euler = (math.radians(90), 0.0, 0.0)
scene.camera = cam

scene.render.engine = "BLENDER_WORKBENCH"
shading = scene.display.shading
shading.light = "STUDIO"
shading.color_type = "MATERIAL"
shading.show_backface_culling = True  # needed for the inverted-hull outline, as in Unity
scene.render.resolution_x = 600
scene.render.resolution_y = 600
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new("W") if scene.world is None else scene.world
scene.world.color = (0.02, 0.02, 0.02)
scene.view_settings.view_transform = "Standard"  # show material colours as authored
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print(f"[preview] {out}")

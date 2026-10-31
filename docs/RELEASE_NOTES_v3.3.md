# Little Echoes — Version 3.3

**More detailed furniture, and the texture investigation written up.**

**Tag:** `v3.3` (branch `feat/little-echoes-core`), 2026-10-30. Previous: `v3.2` (RELEASE_NOTES_v3.2.md).
**App version:** 0.3.3 (Android version code 7), package `com.roomorchestra.app`. Installs over Version 3.2.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan), AR Foundation 6.3.5.
**Tested on:** PICO Emulator 6.1 and automated tests only (EditMode 212, PlayMode 86). **Not yet tested on a headset.** No APK attached to this tag; build from the project.

## New since Version 3.2
### More detailed 3D models (Version 1 feedback: "more detailed 3D models")
- New table, chair and lamp parts modelled in Blender at their real sizes (`ArtSource/Furniture/build_furniture.py`): a table top of five planks with grooves and an apron, tapered legs with feet; a cushioned chair seat, a slatted back with a stepped top rail, tapered round legs; a stepped lamp base and a collared pole (with the drum shade from 3.2). Sizes, tap areas, collisions and the rainforest vines are unchanged.

### Textures (Version 1 feedback: "better textures"): investigated, not shipped
- An original wood-grain texture was tried on the virtual room's floor. The exported scene is correct (texture coordinates, the material's base-colour texture and the image are all in the app), but the PICO Emulator 6.1 draws the floor flat (a three-channel image came out as stripes). The texture is not used; the generator (`ArtSource/Textures/gen_textures.py`) is kept for a headset test. **For PICO's developers:** does a URP Lit base map render on a real Project Swan? If so, this is an emulator limitation and textures can come back.

## Known limits
- Emulator only; see Version 3.2 notes for the Echo Battle, real walls and emulator tips.
- The Chinese wording still needs a native-speaker review.

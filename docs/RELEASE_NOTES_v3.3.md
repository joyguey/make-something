# Little Echoes — Version 3.3

**More detailed furniture, a street-style Echo Battle, and the texture investigation written up.**

**Tag:** `v3.3` (branch `feat/little-echoes-core`), 2026-10-30, updated 2026-10-31 with the Echo Battle restyle. Previous: `v3.2` (RELEASE_NOTES_v3.2.md).
**App version:** 0.3.3 (Android version code 7), package `com.roomorchestra.app`. Installs over Version 3.2.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan), AR Foundation 6.3.5.
**Tested on:** PICO Emulator 6.1 and automated tests only (EditMode 212, PlayMode 86). **Not yet tested on a headset.**
**APK:** `RoomOrchestra-v3.3.apk`, attached to the GitHub release `v3.3` (also `Builds/RoomOrchestra-v3.3.apk` in the project folder; not in Git).

## Install
`pico-cli app install RoomOrchestra-v3.3.apk` (or `adb install -r RoomOrchestra-v3.3.apk`), then launch `com.roomorchestra.app`. To build yourself: Unity 6000.3.25f1, Android, only `Assets/RoomOrchestra/Scenes/RoomOrchestra.unity` enabled.

## New since Version 3.2
### More detailed 3D models (Version 1 feedback: "more detailed 3D models")
- New table, chair and lamp parts modelled in Blender at their real sizes (`ArtSource/Furniture/build_furniture.py`): a table top of five planks with grooves and an apron, tapered legs with feet; a cushioned chair seat, a slatted back with a stepped top rail, tapered round legs; a stepped lamp base and a collared pole (with the drum shade from 3.2). Sizes, tap areas, collisions and the rainforest vines are unchanged.

### Echo Battle: street style, for teens and adults
- The hidden two-headset battle card is restyled to feel less like a kids' game: a poster pasted on a small brick wall, a dark asphalt panel, a 3D sprayed **ECHO//BATTLE** title (回声对决 in Chinese) with paint drips, tape at the corners, and tilted sticker buttons **HOST** (pink), **JOIN** (blue), **LEAVE** (orange), each with a white edge so colour isn't the only cue. Shorter, more direct wording ("GO! Drop your beat", "Their call. Listen up...", "YOU 4 vs 2 THEM", "Run it back?"). Flutter, Bop and Boom are unchanged.
- To open it: in free play, pick **Flutter, Bop, Boom, Flutter, Bop, Boom** within 6 seconds on the leaf (or with the controller's next-character button). Then one headset taps HOST, the other JOIN, on the same Wi-Fi. Full guide: `docs/ECHO_BATTLE.md`.
- Title font: Sedgwick Ave Display (SIL Open Font License), traced into 3D shapes; credited in `Fonts/LICENSES.md`.
- **For PICO's developers:** pairing two real headsets is untested. If JOIN keeps looking, the network may block broadcast; try a phone hotspot.

### Textures (Version 1 feedback: "better textures"): investigated, not shipped
- An original wood-grain texture was tried on the virtual room's floor. The exported scene is correct (texture coordinates, the material's base-colour texture and the image are all in the app), but the PICO Emulator 6.1 draws the floor flat (a three-channel image came out as stripes). The texture is not used; the generator (`ArtSource/Textures/gen_textures.py`) is kept for a headset test. **For PICO's developers:** does a URP Lit base map render on a real Project Swan? If so, this is an emulator limitation and textures can come back.

## Known limits
- Emulator tip: the PICO Emulator drops to a few frames per second when its window isn't in front on macOS.
- Emulator only; see Version 3.2 notes for the Echo Battle, real walls and emulator tips.
- The Chinese wording still needs a native-speaker review.

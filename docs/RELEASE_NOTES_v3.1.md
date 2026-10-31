# Little Echoes — Version 3.1

**Adds the guided tutorial and the rest of the Version 1 feedback follow-ups** to Version 3.

**Tag:** `v3.1` (branch `feat/little-echoes-core`), 2026-10-30. Previous: `v3` (RELEASE_NOTES_v3.md).
**APK:** none kept (2026-10-31 clean-up kept one APK per major version); install Version 3.3 (`Builds/RoomOrchestra-v3.3.apk`, GitHub release `v3.3`), which includes everything here.
**App version:** 0.3.1 (Android version code 5), package `com.roomorchestra.app`.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan).
**Tested on:** PICO Emulator 6.1 and automated tests only (EditMode 201, PlayMode 82). **Not yet tested on a headset.**

## New since Version 3
### Guided tutorial (feedback: "start with a single piece of furniture", "a tutorial song")
- After the opening only the chair is out. Flutter's speech bubble teaches "Mary Had a Little Lamb" one note at a time; the tile to tap glows. A right note snaps into its place in the bar; any other plays and is taken back ("Almost! Try the glowing one").
- The lamp and then the table appear in a big poof when the song needs them.
- The last note is played in time: a large 3D count, **3** (blue), **2** (purple), **1** (yellow), **GO!** (green), on the bar's beats. Land the lamp's note on GO. Within about a third of a second counts; after two misses it counts anywhere (timing is taught, never required). A tap or the controller's select works like a throw.
- A clear ending: Flutter says the lesson is over, the friends cheer, and a Skip leaf is always there. Undo and Clear step the lesson back.
- Remembered per device: later launches go straight to free play. A **Lesson** button replays it.

### Virtual starting room (feedback: start in a virtual room)
- The title, opening and lesson happen in a simple room; when the lesson ends (or is skipped) it comes apart panel by panel and your own room is the stage.

### Sounds and instruments
- **Wall bounces:** Flutter, Bop and Boom each bounce off walls with their own sound (louder the harder they hit). For now the bounce walls are the virtual room's (real walls need room mapping on a headset). Floor landings stay silent.
- **Instrument packs:** a **Sounds** button switches between **Classic** (the approved set) and **Earthy** (an alternative synthesized set).
- **Any instrument on any furniture:** a **Change** button under each instrument's name gives that furniture the next instrument; the song follows its instruments. Remembered per device.

### Accessibility
- **View** button: Standard, Large text, High contrast (near-black buttons, white text, yellow icons), or both. Remembered per device.

### Fixes
- The title card and the tutorial's parked pieces no longer get left in mid-air when the leaf and ball stand move.

## Known limits
- Emulator only. The tear-down's look against real passthrough, readability, throwing on GO and the bounce sounds need a headset; nobody has listened to the new sounds yet.
- Wall bounces only while the virtual room stands (during the lesson).
- The rainforest's growth still follows instrument types after instruments change furniture.
- The launch log shows `DllNotFoundException: PxrPlatform`: the PICO SDK calls a library its own build step leaves out in PICO Spatial mode. Harmless; Version 3 logs it too.
- The emulator can stall on PICO's loading screen when the computer is short of memory; free memory and cold-start it.
- The Chinese wording still needs a native-speaker review.

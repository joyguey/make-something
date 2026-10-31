# Little Echoes — Version 3.2

**More of the Version 1 feedback answered, readability fixes from the owner's emulator play, and a hidden two-player mode.**

**Tag:** `v3.2` (branch `feat/little-echoes-core`), 2026-10-30. Previous: `v3.1` (RELEASE_NOTES_v3.1.md).
**App version:** 0.3.2 (Android version code 6), package `com.roomorchestra.app`. Installs over Version 3.1.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan). New: Unity AR Foundation 6.3.5 (room meshing).
**Tested on:** PICO Emulator 6.1 and automated tests only (EditMode 211, PlayMode 86). **Not yet tested on a headset.** No APK attached to this tag; build from the project.

## New since Version 3.1
### First-time experience (feedback: "focused start", "teach the main mechanic first")
- The ball stand with Flutter on it is the first thing in front of you at launch, under the title card; it glides to its place beside the leaf during the opening.
- The lesson opens by teaching the throw: "Pick me up from the cloud (follow the arrow) and toss me at the chair", or tap a glowing tile.
- A bright arrow bobs above the ball whenever the lesson wants a throw; the count's GO! now says THROW! under it.

### Real-world geometry (feedback: the tear-down shows "the musical balls interact with real-world geometry")
- While the virtual room stands, the headset scans the room (AR Foundation meshing in PICO Spatial). After the tear-down, balls bounce off your real walls (and the sides of real furniture), each character with its own sound. Nothing from the scan is drawn, saved or sent.

### Readability and looks (owner, emulator; feedback: "higher fidelity")
- Bolder label font; Flutter's speech bubble is a deep plum card with white words; every label on the leaf sits on a dark plate.
- Rounded edges on every button, tile, the title card and the furniture; the lamp has a real lampshade.

### Echo Battle (hidden easter egg, nice-to-have)
- A turn-based two-player mode for two headsets on the same Wi-Fi: call a phrase of up to 4 notes, the other player echoes it, points for matching notes, three rounds each. Unlock in free play: pick Flutter, Bop, Boom, Flutter, Bop, Boom within 6 seconds. It is separate from normal play: your own song is kept and plays on when you leave. Details and testing notes: `docs/ECHO_BATTLE.md`.

### Fixes
- The lesson no longer jams when a note was played during the opening.
- Parked pieces (the count, Flutter's bubble, idle poof clouds) no longer get left in mid-air when the leaf and stand move.

## Known limits
- Emulator only; the Echo Battle has not been tried on two real headsets (the emulator can't reach a second device).
- The real-wall colliders' alignment with the room is unverified (nothing is drawn; check by throwing at a wall).
- The emulator can get stuck on PICO's loading placeholder (`XR_ERROR_LIMIT_REACHED` from its room sensing); a data reset (`pico-cli emulator start --wipe-data`) fixes it.
- The launch log shows a harmless `DllNotFoundException: PxrPlatform` from the PICO SDK (see Version 3.1 notes).
- The Chinese wording still needs a native-speaker review.

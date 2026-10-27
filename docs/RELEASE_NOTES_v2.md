# Little Echoes — Version 2 (in development)

**Status:** work in progress on branch `feat/little-echoes-core`. Not yet sent to PICO. Version 1 is tag `v1-pico-feedback` (see RELEASE_NOTES_v1.md).
**App version:** 0.2.0 (Android version code 2), package `com.roomorchestra.app`.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan).
**Tested on:** PICO Emulator 6.1 only. Not yet tested on a headset.

## New since Version 1
### Controls
- Pause/Play, Clear, Undo and Recenter buttons, all with text labels.
- Volume and mute for each instrument: tap the instrument's name tile.
- Height (Seated / Standing) lifts every control together.
- **English / Chinese:**
  - Automatic from the headset's language.
  - The 中文 / English button switches and remembers the choice.

### Characters
- Flutter, Bop and Boom as 3D characters on the tray, on the echo rings and on the throwable balls.
- They are alive:
  - They breathe, wobble like jelly, hop and blink.
  - They look curious when they land on an object, and glance at a sibling that lands there too.
  - They look scared when they fall toward the floor, then vanish in a poof.

### Echo rings
- The rings are halos that turn to face a player who moves around the room.

### Magical rainforest
Each note added grows the room:
- Vines creep up the table, chair and lamp.
- Toadstools sprout and sway to the beat.
- Butterflies fly out from the lamp once the song is harmonious.
- Sparkles flash on the notes.

Clear sweeps it all away in clouds of smoke.

### Sound
- The instrument sounds are synthesized by the project's own documented script. They are inspired by the udu, mbira and erhu, not recordings of them.
- A safety limiter keeps dense songs from clipping.

## Still open
- Roaming controls that follow a walking player: waiting on PICO's advice (options A–E).
- A native-speaker review of the Chinese wording.
- Real-furniture mapping, wall ripples, UI clean-up, poof sound.
- Headset testing.

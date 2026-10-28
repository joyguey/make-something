# Little Echoes — Version 2

**Status:** Version 2 for dev-team feedback. First tagged `v2` on 2026-10-27; the leaf palette, the cloud ball stand, the flowers and the other polish below were added on 2026-10-27/28 on the same branch (`feat/little-echoes-core`) and are part of this submission. Version 1 is tag `v1-pico-feedback` (see RELEASE_NOTES_v1.md).
**APK:** `Builds/RoomOrchestra-v2-final.apk` (local, not in Git; sha256 09b1a9b93eaa0fea8176aaf182c2a182d8e04695e83e080d2c47f8a82d2e2661). Installs and launches on the emulator. To rebuild, see "Build and try it".
**App version:** 0.2.0 (Android version code 2), package `com.roomorchestra.app`.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan).
**Tested on:** PICO Emulator 6.1 only. **Not yet tested on a headset.**

## Build and try it
1. Open the project folder in Unity 6000.3.25f1 (the PICO Unity SDK is already in `Packages/`).
2. Open `Assets/RoomOrchestra/Scenes/RoomOrchestra.unity`. To rebuild the scene from code: menu **RoomOrchestra → Little Echoes → Set Up Select-and-Place**.
3. Build for Android with only that scene enabled, then install with `pico-cli app install <apk>` and launch `com.roomorchestra.app`.
4. Tests: Unity Test Runner, EditMode and PlayMode, filter `RoomOrchestra.Tests`.

## What you can do
- **Pick a character** (Flutter, Bop or Boom) on the leaf palette, **pick a note** on the column beside an instrument, and **place** it with a tap, or **throw** the ball floating on the cloud. Placing and throwing make the same music.
- Every note joins an **echo ring** around its instrument and repeats each bar (88 BPM, 4/4, 16 steps).
- The three weights change the **timbre**, not the volume: Flutter is light and bright, Boom heavy and round.
- The room grows into a **rainforest** as the song does (see below). **Clear** sweeps it away in smoke.

## New since Version 1
### Controls: the leaf palette
- All controls sit on one green leaf: the characters and Loudness along the back; **Come here**, Undo, Pause, Clear, Recenter, Height and 中文/English along the front.
- **It follows you:** walk about a metre from your seat and the leaf glides after you, keeping its seated layout; sit back down and it goes home. Tap **Come here** to bring it at once. Recenter also sends it home.
- **Volume and mute** for each instrument: tap the instrument's name tile.
- **Height** (Seated / Standing) lifts every control together.
- **English / Chinese:** automatic from the headset's language; the 中文 / English button switches and remembers. In Chinese, notes show in jianpu (1 2 3 5 6) with the key, 1=D, on a small dark plate above each note column.

### The ball stand
- The ball you throw floats on a small cloud instead of standing on a post. Spare balls and poof clouds wait hidden inside it.

### Characters
- Flutter, Bop and Boom as 3D characters on the leaf, on the echo rings and on the throwable balls.
- They breathe, wobble like jelly, hop and blink; look curious when they land on an object and glance at a sibling that lands there too; look scared when they fall toward the floor, then vanish in a (silent) poof.
- The echo rings are halos that turn to face a player who moves around the room.

### Magical rainforest
Nothing grows until the song has **3 notes**. From then on, each note added:
- grows its instrument's vine (table, chair, lamp);
- sprouts the next toadstool (they sway to the beat);
- pops up another **flower** somewhere around the room (20 in all, in five colours).

Butterflies fly out from the lamp once the song is layered and melodic, and sparkles flash on the notes. Clear sweeps it all away in clouds of smoke, and the next song grows its own.

### Sound
- The instrument sounds are synthesized by the project's own documented script. They are inspired by the udu, mbira and erhu, not recordings of them.
- A safety limiter keeps dense songs from clipping.
- A character landing on the floor is silent; Clear's smoke makes a soft whoosh.

## Known limits
- Emulator only; follow speed, label size and tap accuracy on the small leaf buttons need a headset.
- The leaf can't see real walls or furniture yet (no room mapping), so it can follow you into a wall.
- Calling the leaf is by tap only; a controller button for it will be chosen after headset testing.
- The Chinese wording is a draft and needs a native-speaker review.
- The sounds have not been checked on headset speakers (including the limiter).

## Planned for Version 3
- Balls make their own sound when they hit and bounce off the walls.
- Real-furniture mapping (instruments on the player's own table, chair and lamp) and keeping the leaf clear of walls.
- A controller button to call the leaf, chosen after headset testing.
- UI clean-up, plus the remaining accessibility options (motion and scale/contrast settings).

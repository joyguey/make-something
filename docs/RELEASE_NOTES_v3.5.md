# Little Echoes: Version 3.5, last PICO Spatial test (failed)

**This is the last build in PICO Spatial mode.** It contains the attempt to play with the real furniture. That attempt failed on the headset: PICO Spatial mode cannot do reliable furniture mapping, a visible room scan or occlusion. So this build turns automatic furniture placement off and fixes what the owner found on the headset. The next version moves the game to PICO XR mode.

**Tag:** `v3.5` (branch `feat/little-echoes-core`), 2026-11-08. Previous: `v3.4` (RELEASE_NOTES_v3.4.md).
**App version:** 0.3.5 (Android version code 9), package `com.roomorchestra.app`. Installs over Version 3.4.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan), AR Foundation 6.3.5.
**Tested on:**
- automated tests: EditMode 231, PlayMode 104;
- the PICO Emulator: launch, opening, lesson skip, clean quit;
- the owner's headset sessions on 2026-11-07 and 08, run on earlier builds of this code. This exact build hasn't been on the headset.

**APK:** `RoomOrchestra-v3.5-last-pico-spatial-test.apk`, attached to the GitHub release `v3.5`.

## Install
`adb install -r RoomOrchestra-v3.5-last-pico-spatial-test.apk`, then launch Little Echoes from the library.

## Fixed since Version 3.4
- **Quit no longer crashes.** It closes the app without PICO's crashing XR shutdown.
- **The room scan now reaches the app.** The scan permission is declared, and the headset asks for it on first launch.
- **The lesson's music plays.** When the headset pauses the app, the music resumes when you come back. A Pause you press yourself stays.
- **Confirm card.** Quit, Clear, Lesson and Leave ask "Quit Little Echoes?" and similar, with a Quit (or Clear, Restart, Leave) button and a Cancel button. The card stands upright.
- **Tapping a note throws the ball.** The character flies to the instrument and plays the tapped note when it lands.
- **Slingshot.** Pinching the ball and pulling it toward you throws it ahead of you, never behind.
- **A clean start.**
  - Nothing of the lesson shows behind the title card. The chair, table and lamp wait hidden until the lesson brings each one in with a poof.
  - The leaf is out of sight until Start, then appears behind your head and floats down.
  - The leaf's tucked buttons no longer sit on the floor at the start.
- **Character colours.** The ball on the cloud wears each character's own colour. Boom was white on the cloud.

## Failed and switched off
- **Automatic real-furniture placement**, with glowing spots on the furniture found.
  - On the headset the scan's furniture labels were unreliable: a desk was called a bed, a table and "other".
  - Spots appeared all over the room, and there was no way to show the scan as it happened.
  - The code is kept and tested but switched off. Instruments stay on their virtual furniture after the lesson.

## Known issues
- No occlusion: real walls and furniture don't hide virtual objects.
- Real-wall bounces are active, but nobody has confirmed that they line up with the real room.
- The opening's new leaf path and the slingshot release haven't been tried on the headset.

## Next
Version 3.6 moves to PICO XR mode, on branch `feat/pico-xr`. PICO's documentation lists these features there:
- PICO's Room Capture app;
- the room mesh shown as a grid while scanning;
- spatial anchors for player-chosen instrument places;
- depth-based occlusion.

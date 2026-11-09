# Little Echoes — Version 3.4 (before occlusion)

**George's headset feedback on Version 2.1, addressed. This is the snapshot before the room-scan, real-furniture and occlusion work begins.**

**Tag:** `v3.4` (branch `feat/little-echoes-core`), 2026-11-07. Previous: `v3.3` (RELEASE_NOTES_v3.3.md).
**App version:** 0.3.4 (Android version code 8), package `com.roomorchestra.app`. Installs over Version 3.3.
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan), AR Foundation 6.3.5.
**Tested on:** automated tests (EditMode 222, PlayMode 94), the PICO Emulator, and one short session by the owner on a PICO headset (OS 6.2.0) with the same code (see Known issues).
**APK:** `RoomOrchestra-v3.4-before-occlusion.apk`, attached to the GitHub release `v3.4` (also `Builds/` in the project folder; not in Git).

## Install
`adb install -r RoomOrchestra-v3.4-before-occlusion.apk` (or `pico-cli app install ...`), then launch Little Echoes from the library.

## New since Version 3.3 (George's feedback, 2026-11-03)
- **Launch:** opens straight into a full-room Stage; the headset's system log shows no window container being created.
- **Press feedback:** buttons, tiles and characters give a small wobble and swell when a press lands (a brief brighten with reduced motion). Every tap target and the throwable balls show PICO's hover highlight.
- **Confirm before ending or clearing:** Clear (with a song), Lesson, Quit, and LEAVE in an Echo Battle ask "Sure?" on the first tap and act on the second.
- **Quit:** on the title card and at the end of the leaf (a door icon).
- **One thing at a time:** during the lesson, Come here, Clear, Lesson, Sounds and Loudness are tucked under the leaf and pop up when it ends or is skipped. Pause, Undo, Recenter, Height, View, language and Quit always stay.
- **Flowers:** with a room scan, they grow only on flat, clear, visible real surfaces; without one, in their usual places.

## Known issues (found on the headset, 2026-11-07; being fixed after this release)
- **Quit crashes while closing.** The app closes as asked, but PICO OS logs a native crash during XR shutdown (`HMDTracking::stop` in PICO's `libStagePlatform`).
- **No room scan on the headset.** The app doesn't declare the `com.picovr.permission.SPATIAL_DATA` permission (PICO's build step skips it in Spatial mode), so the headset sends no room mesh. Real-wall bounces and real-surface flowers are not active in this build.
- **No true occlusion.** Real walls and furniture don't hide virtual objects.
- **The lesson plays nothing by itself,** and tapping a note plays it directly, so you don't need to throw.
- **Double-tap Quit on the leaf isn't self-explanatory.**

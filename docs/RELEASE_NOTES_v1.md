# Little Echoes — Version 1 (prototype for PICO feedback)

**Build:** `Builds/LittleEchoes-v1-2026-10-23.apk` (64 MB, package `com.roomorchestra.app`, version 0.1.0)
SHA-256 `972ece97d36de5888886b98c6d5ee0dd9eeffb6607b5db97b77ffc56dfed42f3`
**Source:** private repo `joyguey/make-something`, branch `feat/little-echoes-core`, commit `75ed156`
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan)
**Tested on:** PICO Emulator 6.1 only. Not yet tested on a headset.

## What to try
1. Pick a blob in the tray (Flutter, Bop, Boom).
2. Tap a labeled tile beside the table, chair or lamp (or throw a ball from the stand on the right).
3. The note plays and a mini echo joins the ring around that object; it repeats every bar at 88 BPM.
4. Layer notes on all three objects; tap **Loudness** to change volume, **Undo** to step back.

## What works (verified on the emulator)
- Three blobs with distinct tones; five notes per instrument (udu = five drum sounds).
- Echo rings: steady 88 BPM, up to 6 echoes per object, no doubled notes, undo.
- Every control has a text label; everything works by tapping, no throwing required.

## Known issues / questions for PICO
- Sounds are placeholder prototypes (not final, not licensed recordings).
- Controller buttons (A/B/X/Y, stick, grip) and seated comfort are untested on hardware.
- Emulator logs ~48 "controller tracking provider start failed" per second (DeviceNotSupport); once, a short stall skipped some echo repeats. Is this expected without controllers?
- Once, a native crash occurred inside the SDK while the app window was being closed (PXR_Loader Stop -> SpatialInputProvider::Stop -> HMDTracking::stop, SIGSEGV).
- Plain TextMesh did not render in Spatial mode; TextMesh Pro does. Is TextMesh support expected?
- Not yet included: pause/clear buttons, recenter, comfort settings, final character art, real-room mapping, Harmony bloom garden.

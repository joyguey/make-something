# Owner checklist: things to try on the headset

A running list of what needs your eyes, ears or hands on a real PICO headset. I add to it as I work; tick items off (or note what you saw) and I'll act on it. Newest build to install: `Builds/RoomOrchestra-test.apk` (`pico-cli app install Builds/RoomOrchestra-test.apk`, or `adb install -r`). It is a throwaway test build; it gets deleted once you've tried it.

## From George's headset feedback (2026-11-03)

### Launch straight into the room (done 2026-11-07) — please check first
George saw a Shared Space window open first and stay visible all through play. The app's manifest declared a volumetric *window* as its startup container (PICO's plugin writes it from the project's default spatial camera setting, which was PICO's stock "Constrained" one). It now declares a full-room *Stage* named `little_echoes_stage`. On the emulator the system now creates just that Stage, but the emulator never showed the stray window even with the old build, so only the headset can confirm.
- [ ] Open the app from the launcher: does it go straight into the room, with **no** floating window first or left behind during play?
- [ ] Is the real room visible around everything (passthrough), as before?
- [ ] Quit and reopen: same?

### Press feedback and highlights (done 2026-11-06)
- [ ] Aim at a button, note tile or character: does PICO's cyan highlight show?
- [ ] Press one: does it give a little wobble and swell? Is it too much, too little, or just right?
- [ ] Aim at the ball on the cloud: does it highlight? **Can you still pinch and throw it?** (It has a new invisible "trigger" shell for the highlight; I could not test this on the emulator.)

### "Sure?" confirmation and Quit (done 2026-11-06)
- [ ] With a song playing, tap **Clear** once: the label reads "Sure?" and nothing is cleared. Tap it again: cleared. Tap Clear, then something else: it stops asking.
- [ ] Same for **Lesson** (restarts the lesson) and **LEAVE** during an Echo Battle.
- [ ] **Quit** on the title card (bottom left) and at the right end of the leaf: two taps, does the app close cleanly? Does it come back normally when you open it again?
- [ ] Is "Sure?" readable on the small leaf buttons? In Chinese (确定？) too?

### One thing at a time in the first session (done 2026-11-07)
During the lesson the leaf shows only the friends plus Undo, Pause, Recenter, Height, 中文, View and Quit. Come here, Clear, Lesson, Sounds and Loudness wait tucked under the leaf and pop up in small poofs when the lesson ends or you tap Skip; Lesson tucks them away again.
- [ ] First launch (or Lesson): does the leaf feel calmer? Can you see any tucked button peeking out from under the leaf, seated or standing?
- [ ] Finish or skip the lesson: do the five pop up in poofs and work?
- [ ] Quit now shows a little open door (Clear keeps the X). Is it clear what it does?

### Open question for you
- [ ] George asked for a menu you can reach during play. Pause, Lesson and Quit are already on the leaf. Is that enough, or do you want a separate pop-up menu (Resume / Lesson / Quit)?

### Flowers grow on real flat surfaces (done 2026-11-07)
With the room scan, each flower that sprouts moves (while still an invisible speck) to the nearest free spot on a flat, upward-facing real surface near its own place: the floor, a table top, a seat. A spot needs about 20 x 20 cm of level surface, 35 cm of clear space above it (nothing overhanging: not under a table or shelf, not at the foot of a wall), at least 35 cm from other flowers, 70 cm from your seat and 45 cm from the instruments. Without a scan, flowers keep their usual places. Clear frees the spots. The scan stays in memory only.
- [ ] Play a song long enough for flowers: do they come up on your real floor and table tops, not floating or sunk?
- [ ] Any under a table, inside furniture, or against a wall? (Tell me roughly where.)
- [ ] The scan needs a little time: flowers from the first notes may use their usual places. Is that noticeable?

### Real walls and furniture hiding flowers: what's possible (2026-11-07)
True occlusion (a real wall or table hiding a virtual flower behind it) is **not available** to this app today. PICO Spatial mode supports only the standard Lit/Unlit shaders, lists "dynamic occlusion" as unsupported, and PICO's environment-depth occlusion shader is for the other mode (PICO XR). PICO's "Occlusion Surface" lives in the separate PICO Spatial Editor toolchain, which isn't installed. Switching modes or adding that toolchain would be a big change; I haven't done either.
What I did instead: flowers only grow where you can **see** them. Before a flower takes a spot, the line from your eyes to it is checked against the room scan, so none grows behind a real wall, sofa or table edge (where it would wrongly show through).
- [ ] Do any flowers appear to show *through* a real object? (They shouldn't any more; tell me where if one does.)
- [ ] Decision for you: is "only where you can see them" enough, or should I look into the PICO Spatial Editor route for real occlusion? (It needs an extra PICO tool and an experimental export path.)

# Owner checklist: things to try on the headset

A running list of what needs your eyes, ears or hands on a real PICO headset. I add to it as I work; tick items off (or note what you saw) and I'll act on it.

## Version 3.8 (2026-11-08, after the 3.7 test): installed on your headset
- [ ] Nothing pokes out of the cloud at the start (whatever waits inside it is now not drawn).
- [ ] The leaf and cloud sit at your own seated eye height (measured after the opening) and 0.85 m out, or on your desk.
- [ ] Menu: a flat two-step tray over the leaf; **Close** sits where Menu is. Every label reads level.
- [ ] Drum bands read **Deep, Soft, Slap, Wobble, Tap** (was Open, Muted, Slap, Bend, Tap).
- After a session, I can read the whole game log from the headset (`files/logs/session.log`, last 3 sessions kept, nothing sent anywhere).

## Version 3.7 test (2026-11-08, after your morning feedback)
**Already installed on your headset** (0.3.7). APK: `Builds/RoomOrchestra-v3.7-pico-xr.apk`, branch `feat/pico-xr` (committed, not pushed). v3.5 is still the fallback.
Emulator: launch, lesson skip, the room glow and choosing (mbira first) all ran with no errors. The emulator has no controllers and no depth sensing, so **pressing, the note bands, depth occlusion and saved places are untested**.

- [ ] **Start:** no flower, toadstool or vine specks anywhere before music grows them.
- [ ] **Leaf above your desk:** a few seconds after the opening, the leaf and the cloud rise onto your desk and come closer. The ball on the cloud turns to face you.
- [ ] **Leaf buttons:** the characters plus Menu, Undo, Pause and Clear. **Menu** opens a tilted tray with Loudness, Sounds, View, language, Height, Hiding, Come here, Recenter, Places, Lesson and Quit. Press Menu (now "Close") again to fold it.
- [ ] **Choosing places** (after the lesson): Flutter asks in the lesson's order: **mbira (chair), then erhu (lamp), then udu (table)**. Pointing at a real object lights up its scanned shape in the instrument's colour. Press, and the virtual furniture disappears in a poof; the instrument's echo ring floats over the real object, and its vines and sparkles move there. Pressing the virtual furniture itself keeps it.
- [ ] **Notes:** point at a real object and 5 bands show along its longest side (across a table, up a lamp). The band under the ray is brighter and shows its note. Press it and that note plays (the ball flies there). A thrown ball plays the band it hits.
- [ ] **Wall bounces:** a soft bell on top of the bounce, climbing D–F#–A–B–D through a throw (Flutter high, Boom low).
- [ ] **PICO depth occlusion:** after choosing, virtual things behind real furniture, walls or your hands should be hidden. If things vanish wrongly, Menu → **Hiding** turns it off; please tell me what you saw.
- [ ] **Saved places:** quit and start again. After the lesson (or its skip), Flutter should say "Welcome back!" and each instrument should be back on its object. **Menu → Places** chooses again.

## Tonight's test (2026-11-08): Version 3.6, the first PICO XR build
**Install:** `adb install -r Builds/RoomOrchestra-v3.6-pico-xr.apk` (branch `feat/pico-xr`). If it isn't good enough to hand in, submit **v3.5** (`Builds/RoomOrchestra-v3.5-last-pico-spatial-test.apk`, also on GitHub).
- Plug in, put the headset on and accept **Allow USB debugging** (tick **Always allow**). If my tools still say "unauthorized", unplug and replug the cable.
- On first launch the headset asks for **room scan** and **hand tracking** permission: tap **Allow** both times.
- The emulator ran this exact APK: opening, lesson skip, the room glow, choosing begins, clean quit. **Nothing has been tried on a headset**, and the emulator has no controllers or hands, so pointing and pressing are untested.

**Please check, in this order:**
- [ ] **It opens and you see your room through it**, with the title card in front of you. If you see black instead of your room, stop and tell me (passthrough setup).
- [ ] **Nothing distracting at the start:** no leaf, chair or keys before Start. On Start the leaf appears behind you and floats down.
- [ ] **Controller ray:** each controller shows a thin ray, which turns **gold** over anything you can press. The trigger presses. Try Start, a note tile and Pause. Is the ray coming out of the controller where you expect?
- [ ] **Hands:** put the controllers down. Does a ray come from your hand, and does a pinch press?
- [ ] **Throwing:** point at the ball on the cloud, hold the trigger (or pinch), swing and let go. Pulling back toward you should fling it forward, never behind you.
- [ ] **Your size and place:** the stage should start around you, the furniture at sensible sizes, the leaf in reach.
- [ ] **After the lesson (or Skip):** your real room glows see-through blue. Flutter asks where the udu should live.
  - Point at a real table or shelf: a small glowing spot follows the ray. Press, and the udu hops there.
  - Pressing the udu itself keeps it on its virtual furniture.
  - Then the mbira, then the erhu.
  - Does the glow line up with your real room?
- [ ] **Play with the real furniture:** throw at an instrument you placed. Does the real surface play it?
- [ ] **Occlusion:** once you've chosen, real walls and furniture should hide virtual things behind them, but never within about 1.5 m of where you sit, so your own desk never hides the leaf. Does anything disappear that shouldn't, or show through that shouldn't? The emulator showed odd light patches; tell me if you see any.
- [ ] **Quit** closes cleanly.


## Earlier (2026-11-07 evening, PICO Spatial builds; mostly superseded by the list above)
Install the newest `Builds/RoomOrchestra-test.apk`. First, put the headset on and accept **Allow USB debugging**, ticking **Always allow from this computer** (the headset currently shows "unauthorized" to my tools, so I can't install or watch it).

**Fixed from your session** (I read the headset's log while you played):
- [ ] **The lesson's music now plays.** In your session the music was paused the whole time: I launched the app over USB while the headset sat on your desk, the headset paused the app, and the music stayed paused. Now a pause the system causes ends when you come back (your own Pause button still stays paused). During the lesson, do your notes loop round and play by themselves?
- [ ] **Quit no longer crashes.** The old Quit closed the app but PICO logged a crash. Quit now closes it the clean way (emulator: no crash). Quit, then reopen: normal?
- [ ] **Quit, Clear, Lesson and Leave show a card** above the button: "Quit Little Echoes?" with a coral **Quit** and a green **Cancel**. Is it clear? Is the card easy to read and tap?

**Your original vision: the real furniture**
The room scan wasn't reaching the app at all before (a missing permission); it does now. The lesson still happens in the virtual room with its virtual table, chair and lamp. When it ends (or you skip it) and the room comes down, Flutter says "Now look slowly around your room...". After a few seconds, each instrument moves onto a real piece of furniture: the drum (udu) onto a table (or a cabinet, bed or sofa), the mbira onto a chair or sofa, and the erhu onto a lamp. The little virtual piece sits on top of the real one as a marker, and its ring of echoes floats above. Then Flutter says "Found them!". Anything not found stays where it was ("I couldn't find your furniture..." if nothing).
- [ ] Does it find your furniture? Which pieces did it pick? Anything silly (a shelf as a table, something behind you)?
- [ ] Throw a ball at the real furniture: does it play that instrument? Higher up the piece, higher note?
- [ ] Are the little markers clear enough to show which instrument lives where?
- [ ] Lesson: does everything go back to the virtual room?
- [ ] Does the scan stay quiet (no lag) while you play?

**Tapping a note now throws the ball**
- [ ] Tap a note tile: the character on the cloud flies to that instrument and plays that note when it lands. Does that feel better than the instant note? Is the short delay OK?
- [ ] Tap quickly several times: every tap still plays (if no ball is ready, it plays at once).

## Decisions for you (2026-11-07 evening)
- [ ] **Occlusion.** PICO's tools for the mode this app uses have no way to make real walls hide virtual things (no occluder material; only standard materials). Options: (a) **object-level hiding:** I fade or shrink a flower, ring or ball whenever the scan says a real object is between your eyes and it (doable now, a few hours, coarse: whole objects, not edges); (b) **switch the app to PICO XR mode**, which has depth-based occlusion: a big migration of rendering, input and windows, days of work and new risks; (c) leave it: flowers already grow only where you can see them, and now the scan actually works.
- [ ] **Voice notes.** Saying the note before a throw is possible with an on-device recogniser (e.g. Vosk, Apache 2.0, about 50 MB, nothing leaves the headset); about a day's work, as an extra option alongside tapping. Want it? (An online recogniser would upload your voice: not without your approval.)
- [ ] **In-play menu** (from George): still open; Pause, Lesson and Quit are on the leaf.

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

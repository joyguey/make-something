# Little Echoes: what changed from V3 to V4

V3 shipped to the dev team as **v3.14** (app 0.3.14, build 24) on 2026-11-08. V4 is **v4.2** (app 4.2.0, build 40), from 2026-11-15, on PICO OS 6 (PICO XR mode, SDK 6.1.1) and Unity 6000.3.25f1.

**Install note:** the package ID changed from `com.roomorchestra.app` to `com.littleechoes.app`, so V4 installs as a separate app. Uninstall the old one first. The APK is `LittleEchoes-v4.2.apk`.

## The V3 review, point by point
1. **One name: Little Echoes.** The app label, company name, package ID and APK name all say Little Echoes.
2. **Look and pinch.** With bare hands, the pointer follows your eyes (PICO eye tracking, falling back to head direction) and a pinch presses. A pinched ball moves with your hand and flies where you look. Controllers keep their rays.
3. **A meadow instead of a box room, with the characters and music.** The lesson takes place in a low-poly meadow that comes apart afterwards to reveal your room. Flutter, Bop and Boom hop on the title sign, hide in a bush and pop out onto the leaf. An original meadow theme plays in the silences and fades the moment you play.
4. **UI sound effects.** A hover tick, a press pop, menu open and close, confirm and cancel, and a sparkle when an instrument is placed, all in the game's key.
5. **Anti-aliasing.** 4x MSAA at full render resolution.
6. **The headset's recenter.** Holding the PICO button keeps the real room and everything placed in it lined up, and brings the stage, leaf and cloud in front of you.
7. **Ambient occlusion baked in Blender,** object by object, for the meadow, furniture, characters and instrument icons.

## New look: for teens and adults
V3 read as a young children's game. V4 keeps the three characters and restyles everything around them.
- **A meadow that follows the clock:** warm golden-hour light from 6am to 6pm, dusk under a coral-to-indigo sky from 6pm to 6am, read from the headset's clock at launch.
- **Sky:** a smooth gradient, a sun by day, a crescent moon and stars at night, and round cumulus clouds.
- **Painted light:** shapes have a lit side and a shaded side with a small crisp highlight. Characters and furniture catch a warm rim of light, and far hills fade into the sky.
- **Wind:** grass, flowers, bushes and trees sway gently.
- **Colours:** terracotta furniture, navy menu plates on the green leaf, a countdown in yellow with a green GO.
- **Lower, softer interface sounds:** the same notes an octave down, and shorter.

## Title and first run
- **A hanging title sign:** a deep plum plate hangs from a wooden frame on two steel rings. A gust rocks it now and then, and it swings when you push it. It stands at arm's reach (0.74 m) and never moves toward the player.
- **Start for hands:** with controllers, Start is a green button. With bare hands it is a dashed green handprint: look and pinch, or high-five it.
- **Your hands are shown:** the meadow covers the camera's view of the room, so bare hands are drawn (PICO's hand models, in an outline look) while the meadow is up. They go once the real room shows.
- **The friends stay hidden** in the bush until they hop out, from any angle.
- **Nothing grows during the lesson.** The rainforest grows only in your own room.

## Instruments
- **Five more instruments,** all in D major pentatonic like the first three: ocarina, handpan, guzheng, balafon and slit drum. The sounds are original synthesis.
- **A small 3D icon for every instrument.** Tap the icon above a piece of furniture (or the Change tile in the lesson) to cycle through all eight.

## Playing in the real room
- **Room-scan progress bar:** it sits on the right of your view, just above eye level, and fills as the scan covers the room. Choosing furniture starts at 75%.
- **Move kept virtual furniture:** point at it and hold grip (or look at it and make a fist), then drag it across the floor.
- **Note names on real furniture** now show on the headset (they were black boxes in V3).
- **Mushrooms** grow on surfaces you can see, and butterflies loop in front of you.

## Feedback and comfort
- **What you aim at lights up:** the button, tile, character or ball under the ray or your gaze is drawn lighter, on top of the gold ray, the dot and the hover tick.
- **Controller vibration:** a tick on a press, a pulse on grabbing and throwing a ball, and a thump when your note sounds, stronger for heavier balls. There is no menu switch to turn it off yet.
- **Question cards stand clear:** "Quit Little Echoes?" and the other questions never appear inside a sign or menu.
- **Theme music** plays only in silence, and is louder than in the first V4 builds.

## Echo Battle (hidden, still untested on two headsets)
The two-headset mode from V3 is unchanged in how it plays, and it knows the five new instruments. **It has never been tried on two real headsets**, so this is the part I most need tested.

**To open it:** finish or skip the lesson, then tap the three friends on the leaf in the order Flutter (turquoise), Bop (purple), Boom (orange), Flutter, Bop, Boom within 6 seconds. The Echo Battle card appears above the leaf.

**To play:**
1. Install V4 on both headsets and put both on the same Wi-Fi network.
2. On one, tap **HOST**. On the other, tap **JOIN**. They should find each other without IP addresses, accounts or internet.
3. The host gets a 3-2-1-GO and plays up to 4 notes in one bar on any furniture.
4. The other headset hears that phrase on its own furniture and echoes it in the next bar. A matching note scores 2 on the same 16th, 1 if one 16th off.
5. The players swap. Three rounds each; the card shows the round, the score and the winner. **LEAVE** ends it at any time.

**Please watch for:**
- **Pairing:** if JOIN keeps "looking", the network is probably blocking broadcast (guest and some office Wi-Fi do). A phone hotspot is the quickest check.
- **Timing:** does the count feel right, and does the echoed phrase replay in time on the second headset?
- **Scoring:** does a correct echo score as expected on both headsets?
- **Leaving:** does LEAVE return each player to their own song?

It uses plain UDP on port 47777 on the local network only. Nothing goes to the internet.

## Tested
- **Automated:** 261 EditMode and 114 PlayMode tests pass.
- **On a PICO headset (the owner playing):** 85 to 90 fps with the new look. The title sign, the handprint high five, the hand models, the controller vibration, the light on what you aim at, the question cards and the scan bar all worked on the headset on 2026-11-15.

## Known limits
- **Echo Battle** has not been tried on two headsets.
- **Instrument sounds** are original synthesis, not recordings of the traditional instruments.
- **Vibration** cannot be turned off in the menu yet.
- **The room scan** the headset keeps is its own. Start over wipes the game's saved furniture and settings, not the headset's scan.

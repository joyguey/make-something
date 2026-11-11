# Little Echoes v4.1

App 4.1.0, build 31, package `com.littleechoes.app`, for PICO OS 6 in PICO XR mode (SDK 6.1.1) with Unity 6000.3.25f1.
The changes since v3.14 cover every point of the V3 review, followed by the owner's own headset playtests.

## The V3 review, point by point
1. **One name: Little Echoes.** The app label, company name, package ID (`com.littleechoes.app`) and APK name now all say Little Echoes.
2. **Look and pinch.** With hands, the pointer follows your eyes using PICO eye tracking (falling back to head direction); pinch to press. A pinched ball moves with your hand and flies where you look. Controllers keep their rays.
3. **A meadow instead of a box room.** The lesson takes place in a low-poly meadow of grass, flowers, bushes, rocks, trees, rolling hills, clouds and sky, which comes apart after the lesson to reveal your room.
   - **Title:** Flutter, Bop and Boom hop on a wooden sign around the title card.
   - **Opening:** they pop out of a bush and hop onto the leaf as it flies in.
   - **Theme:** an original meadow theme plays in the silences and fades the moment you play.
4. **UI sound effects.** A hover tick, a press pop, menu open and close, confirm and cancel, and a sparkle when an instrument is placed, all in the game's key.
5. **Anti-aliasing.** 4x MSAA at full render resolution. Measured at 88–90 fps on a PICO headset.
6. **The headset's recenter.** Holding the PICO button keeps the real room and everything placed in it lined up, and brings the stage, leaf and cloud in front of you.
7. **Ambient occlusion baked in Blender,** object by object, for the meadow, furniture, characters and instrument icons. A small lit shader applies it.

## Also new
- **Five more instruments,** all in D major pentatonic like the first three:
  - **Ocarina:** a clay vessel flute.
  - **Handpan:** a tuned steel hand drum.
  - **Guzheng:** a Chinese plucked zither.
  - **Balafon:** a West African wooden xylophone.
  - **Slit drum:** a hollow log drum.

  Every instrument has a small 3D icon. To change instruments, tap the picture above a piece of furniture (or the Change tile in the lesson); it cycles through all eight.
- **Room-scan progress bar.** While you look around your room, a bar shows how much is scanned. Choosing furniture starts once three quarters of the room around you is covered.
- **Move virtual furniture.** You can move a virtual piece you kept: point at it and hold grip (or look at it and make a fist), then drag it across the floor.
- **No growth in the lesson.** The rainforest grows only in your own room, as a surprise.
- **Polish:**
  - The note names on real furniture now show on the headset.
  - The rainforest's mushrooms grow on surfaces you can see.
  - Butterflies loop in front of you.
  - The yellow focus disc no longer sits over the ball.

## Known limits
- **Echo Battle** (the hidden two-headset mode) hasn't been tried on two headsets.
- **Instrument sounds** are original synthesis, not recordings of the traditional instruments.

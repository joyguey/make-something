# Little Echoes — Version 3 (in progress)

**Answers the Version 1 build feedback** (`build_feedback.md`: app identity, title screen, intro animation, hover feedback, scale). The guided tutorial is next and will be added to Version 3.

**Tag:** `v3` (branch `feat/little-echoes-core`), 2026-10-29. Previous: `v2.1` (RELEASE_NOTES_v2.1.md).
**App version:** 0.3.0 (Android version code 4), package `com.roomorchestra.app` (unchanged, so it installs over earlier versions).
**Platform:** Unity 6000.3.25f1, PICO Unity SDK in PICO Spatial mode, PICO OS 6 (Project Swan).
**Tested on:** PICO Emulator 6.1 only. **Not yet tested on a headset.**

## New since Version 2.1
### App identity (feedback: "missing a custom icon")
- A custom icon: the three friends on the leaf, on coral, inside an echo ring with one echo per character, white notes, and the "Little Echoes" title in the key-art lettering.
- It shows on the PICO OS 6 home screen as a **layered spatial icon** (background, art and title lift apart when looked at), and the app is now called **Little Echoes** there (it was "Room Orchestra").

### Title screen and opening (feedback: "boot into a proper title screen", "a brief intro animation")
- The game opens on a **title card**: the name, "A mixed-reality musical playground", and a **Start** button. Until Start, nothing else reacts. The controller's select button also starts.
- Start tucks the card into the ball stand's cloud, then the **opening** plays:
  - the floor cracks open into a black hole and the leaf's stalk grows out of it;
  - the leaf glides in on a swoop, rocking like a falling leaf, and lands on the stalk, and the hole closes;
  - Flutter, Bop and Boom bounce up from the floor, one at a time, and land on the leaf.

### Hover feedback (feedback: "unclear if buttons are tracking eye gaze")
- Everything you can tap lights up under your gaze or pointer (PICO's own hover highlight): every button, the characters, the note and volume tiles, the echoes on the rings and the furniture.

### Scale (feedback: "the scale felt uncomfortably large" seated at a desk)
- The app was already shown at real-world scale; the furniture is now set to standard sizes (a 0.75 m table, a chair back topping out at 0.90 m) and sits 20 cm further out, around a seated player rather than over them.

### The growing rainforest
- Toadstools and flowers now start out of sight and grow up out of the ground; flowers grow as a green bud, then bloom.
- New growth starts a little off the beat and eases into step with the rest over a few seconds.

## Known limits
- Emulator only; the swoop's comfort, label sizes and tap accuracy need a headset.
- On the emulator, after many launches in one session the app can hang on PICO's loading screen; restarting the emulator fixes it.
- PICO Spatial did not draw image textures on the emulator, so the title lettering is built from shapes instead.
- The Chinese wording still needs a native-speaker review.

## Coming next
- **Guided tutorial:** start with one piece of furniture and the ball in front, and learn "Mary Had a Little Lamb" step by step before the other instruments join.
- Then: the virtual starting room that tears down to reveal your real room; balls that make their own sound off walls; instrument packs.

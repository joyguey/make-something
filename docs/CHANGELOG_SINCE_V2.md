# Little Echoes: what changed since v2.1

v2.1 shipped to the dev team on 2026-10-28. The current build is **v3.14** (app 0.3.14, build 24), from 2026-11-08, on PICO OS 6 (PICO XR mode, SDK 6.1.1) and Unity 6000.3.25f1.

## Platform
- **Moved from PICO Spatial to PICO XR mode** (2026-11-08). This gives the game the room mesh and spatial anchors. v3.5 is the last PICO Spatial build and stays as a fallback.
- **Hand tracking:** point and pinch work when the controllers are put down.
- **On-headset session log:** the game writes its own log to `files/logs/session.log` (the last 3 sessions). It stays on the headset, and nothing is uploaded.

## First run
- **Title and opening:** the new app icon, name and title card. After Start, a stalk splits the floor open, the leaf swoops in and Flutter, Bop and Boom bounce onto it.
- **Guided lesson:** "Mary Had a Little Lamb" teaches the throw first, one thing at a time, and ends on a 3-2-1-Go. The headset remembers that the lesson is done, and the Lesson button replays it.
- **Virtual starting room:** the lesson takes place in a virtual room, which comes apart when the lesson ends.
- **Clean slate:** once Flutter finishes the closing line, the lesson's song is cleared and the flowers, toadstools and vines it grew vanish in a puff of smoke. The player then chooses furniture and starts their own music from nothing.
- **Clean start:** only the title card, the cloud and its ball are shown at the title.

## Playing in the real room
- **Room scan after the lesson:** the player looks slowly around the whole room for about 20 seconds while the scan glows and fills in.
- **The player chooses the furniture:** the player points at a real object for each instrument, in this order: mbira, erhu, udu.
  - An object's box never reaches into the player's own space (a chair scanned with the player sitting in it is cut back).
  - The virtual furniture is then removed.
  - The instrument uses the scanned shape of the real object.
  - The rainforest vines grow around it.
- **Notes along the object:** five note bands run along the object's longest side.
  - Pointing at the object labels every band with its note, and the band under the pointer gets the largest label.
  - Tapping a band plays it. Throwing a ball at a band also plays it.
- **Places remembered between sessions:** each chosen object gets a PICO spatial anchor, saved on the headset only. "Choose furniture" in the menu re-chooses them.
- **Occlusion:** real walls and furniture hide game objects behind them, using the scanned room mesh (the same shapes the game picks furniture from). Nothing within 0.6 m of the player hides anything, and things sitting on a real object stay visible. It can be switched off with Hiding in the menu.
- **Real walls:**
  - Balls bounce off the real room, but only after the lesson, so the lesson's throws reach the virtual furniture.
  - Each bounce plays a soft chime in the song's key.
- **Flowers** grow only on flat, clear real surfaces that the player can see.

## Reach and comfort
- **The leaf and the cloud fit the player:** both are placed from the player's head height at each start.
  - They refit when the player stands up, sits down or walks away and stays there for 3 seconds. Bending down never refits them. The player can also grab the side of the leaf to readjust the placement & the cloud will realign to match the leaf.
  - When standing, the leaf sits lower and further out, near elbow height.
  - Over a desk, the leaf sits just above the desktop, not above the monitor.
- **The cloud follows the leaf:** it travels with the leaf but sits beside it, slightly higher.
- **Come here** (first button on the leaf) calls the leaf and the cloud to the player.

## Controls
- **Leaf:** Choose furniture, Come here, Menu, Undo, Pause, Clear.
- **Menu (flat, two steps):** Sounds, Text size, 中文, Height. Then Close, Recenter, Lesson, Start over, Quit.
- **Start over (new in v3.14):** wipes the saved furniture, its anchors, the settings and the lesson, then begins again from the title, room scan included.
- **Focus disc:** the yellow focus disc shows only over the lesson's virtual furniture. On real objects, the glow and the note labels show the target.
- **Confirm card:** Clear, Lesson, Start over, Quit and Leave (in Echo Battle) ask first.
- **Feedback on every control:** a hover highlight on everything tappable, and a jiggle on every press.

## Sound
- **Bounce sounds:** each character has its own wall-bounce sound, plus the harmonised bounce chime.
- **Sound packs and placement:** two sound packs (Classic and Earthy), and any instrument can be put on any furniture.
- **Plain udu sound names:** Deep, Soft, Slap, Wobble and Tap.
- **Fix:** the music no longer stays paused after the system pauses the app.

## Reading and access
- **High contrast by default:** near-black buttons, white text and bright yellow icons from the first frame. The Text size button switches between standard and large labels.
- **Labels:** bold labels on dark plates, plus a dark speech bubble.
- **Languages:** English and Chinese throughout. Chinese labels use numbered notation (jianpu).
- **Input:** controller select or tap works everywhere. Pause, Undo and Clear can always be reached on the leaf.

## Art
- **Furniture:** modelled in Blender at real-world sizes.
- **Shapes:** rounded edges on the controls, card and furniture, and a real lampshade.
- **Rainforest:** it grows with the music.

## Echo Battle (hidden)
- **A two-headset, turn-based mode** over local Wi-Fi, opened by a secret. Tap blue -> purple -> yellow, blue -> purple -> yellow.
- **Card restyle:** the card has a street and graffiti look.

## Known limits (honest status)
- **Object recognition:** finding the real object only partly works. Sometimes the scan's box is bigger or smaller than the object.
- **Occlusion:** sometimes things get in the way of each other and it's hard to position the palette so that it's not in the way of the furniture
- **Labels:** I can't seem to get the labels to appear by the instruments themselves after the tutorial to show the different notes and style of drum taps

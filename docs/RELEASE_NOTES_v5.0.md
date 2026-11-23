# Little Echoes v5.0.1 (version 5.0.1, code 43), 2026-11-22

## New
- **Play together** on the menu (two-people icon, back row): brings out the Echo Battle card. No secret needed; the secret still works.
- **Joining is automatic:** the joining player's furniture takes the host's instruments at once (pieces that already match keep theirs), then play starts. The player's own instruments stay saved and come back after the battle.
- **Nobody found after 20 s:** the card says to check the Wi-Fi.
- **Gameplay restyle** (owner-approved style A, 2026-11-19): toy buttons with a cream ring and plum cap that sink when pressed; a modelled leaf and stalk; note tiles tinted by instrument; a squash, petals and a ripple on a live hit; a cushion on the chair. The friends are unchanged.

## Platform
- PICO Unity SDK **6.2.0** (was 6.1.1). PICO CLI **0.7.0**.
- Store builds are signed with the owner's release key (`-releaseKey`; key and password kept outside the project, in `~/Keys`). Debug-key builds are marked as tests.

## Evidence
- Automated (Unity 6000.3.25f1, batch mode, Editor simulation): EditMode 277/277, PlayMode 122/122. These include the automatic join and the still-looking message.
- **Not yet confirmed on the headset:** frame rate, the restyle in motion, SOLO, the new menu button, SDK 6.2.0 at runtime. HOST/JOIN on two real headsets were last tried by PICO's developers on 2026-11-16 (before the automatic join).

## After the headset playthrough (2026-11-22)
- **Room scan after Skip:** skipping the lesson now leads to "look around" at once (the bar fills as the scan arrives); Menu > Choose furniture scans first when there is no scan; the room scan restarts once room-data access is allowed.
- **Moving virtual furniture is deliberate and visible:** a bare-hand fist must be held about 0.7 s on the same piece (the controller's grip acts at once); the piece lifts 5 cm with a poof and a rising chime while held, and settles back with a poof and sparkle; a brief flicker open doesn't drop it. Flutter explains the gesture on its own line after the furniture is chosen.
- **Choosing a note tile (the udu's Deep, Soft, Slap, Wobble, Tap):** with look-and-pinch, the gaze rests about 0.15 s before the highlight moves, and the pinch acts on the lit tile; choosing plays its own bright two-note chime. The hover tick and the hit ripple are kept.
- **Fewer flowers and mushrooms:** the floor fills with how complete the three-instrument song is (a full ring on each instrument); one instrument alone grows a third; full is 12 flowers (was 20) and all 7 toadstools.
- **An x on Flutter's bubble** closes it (lines after the lesson); Play together closes it too, so the card is never behind it.
- **Not yet confirmed on the headset:** these fixes.

## Second headset round (2026-11-22, signed 5.0.0 build)
- **Furniture grabs only once everything is placed**, never while choosing; a pinch that curls up never counts as a fist. With hands, a held piece follows the hand's own movement (a little more for a far piece), no longer the gaze's spot on the floor, which sent the lamp 5.5 m into a wall. Nothing goes more than 3.5 m from the seat.
- **Choosing furniture can't run away:** after each choice, presses wait 0.8 s; Menu > Choose furniture asks "Sure?" first.
- **No dark rectangles under the note names on real furniture:** the letters carry a dark plum outline instead (Resources/NoteLabelOutline, kept in the build).
- **The room scan after Skip** starts only on a device that can scan (fixed a regression in the editor tests).
- Tests: EditMode 277/277, PlayMode 123/123. Signed with the owner's release key.

## v5.0.1 (2026-11-22)
- **Furniture follows the hand, never the eyes:** with bare hands, choosing where an instrument lives and taking hold of a piece use the hand's own pointing ray; buttons and note tiles keep look-and-pinch (owner: "Didn't I get to choose where the furniture went with my hand and pointing?").
- **SOLO removed** (owner: "so unnecessary"): the battle card has HOST, JOIN and LEAVE again, as before.
- Saved for later (owner): walls never hiding a held piece, a four-way move arrow, and no scanned-shape flash on each note.

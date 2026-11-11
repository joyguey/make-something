# Little Echoes V4 roadmap (final version, due Sunday 2026-11-15)

Goal: answer every point of the V3 review (rated 3.5/5 in the PICO system) and reach 5/5.
Branch `feat/v4`. Status: done (code + tests), headset (checked on the owner's headset), open.

| # | V3 review point | V4 answer | Status |
|---|---|---|---|
| 1 | App title is Room Orchestra, submission is Little Echoes | The app is **Little Echoes** everywhere a player or reviewer sees it: app label (already), company name (was "DefaultCompany"), APK file names (`LittleEchoes-…apk`). Package ID renamed to `com.littleechoes.app` (owner, 2026-11-11: not registered on PICO's platform yet). | done |
| 2 | Eye tracking to aim at the UI, pinch to confirm | Hands: the pointer follows the eyes (PICO eye tracking, head direction as fallback), shown as a dot where you look; pinch presses; a pinched ball moves with the hand and flies where you look. Controllers keep their rays. | done, headset open |
| 3 | Main menu too simple; the room is just a box: a meadow with the characters and background music | The lesson's box room becomes a low-poly meadow (hills, trees, bushes, flowers, rocks, sky, clouds) that comes apart after the lesson like the room did. The title has the three friends bouncing on a wooden sign, and an original background theme plays on the title, the opening and while choosing furniture. | done, headset open |
| 4 | Needs UI sound effects | Hover tick, press pop, menu open/close, confirm/cancel, furniture placed: synthesised in the song's key (D major pentatonic). | done, headset open |
| 5 | Aliasing on all objects | 4x MSAA and full render resolution (was 80%). | done, headset open |
| 6 | Holding recenter on the headset does nothing | The game answers PICO's recenter: the real room and everything placed in it stays put, and the stage, leaf and cloud come in front of the player. | done, headset open |
| 7 | Bake ambient occlusion in Blender per object | Meadow pieces, furniture and characters get AO baked per object into vertex colours in Blender; a small lit shader applies it. | done, headset open |
| 8 | (owner) A library of 5 more instruments that sound good together, including the ocarina and lesser-known instruments | In progress. | open |

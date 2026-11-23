# Story mode (started 2026-11-22; final deadline 2026-12-06)

Little Echoes gets three modes: **Story** (new), **Sandbox** (everything built through v5.0.1: free play), **Battle** (the two-headset Echo Battle). The title sign offers the three; preview it for the owner before changing it.

## The game
- A **portal** floats in the player's room (fixed spot). Inside, a **side-scrolling journey**: Flutter, Bop and Boom travel a path through the meadow; painted parallax layers (foreground, path, woods and hills, the far **World Tree**). The portal **widens each level** (porthole, window, wall); the finale breaks out into the room.
- **Discords** (noisy creatures) come along the path and **out through the portal into the room**.
- **The friends are the ammo.** The current friend sits curled on the **open left palm**; the right hand **flicks** it (pinch-to-aim is the accessible alternative).
- **Powers and matches:** Flutter pierces lines (beats flying **Squeaks**); Bop ricochets off real walls and furniture (beats hiding **Wobblers**; fallback: portal walls); Boom's heavy shockwave (beats armoured **Clangs**). Wrong match: sour note, the Discord grows. Later: two-hit **Chord** Discords.
- **Queue:** shuffled sets of all three; palm = current, wrist = next two; **Hold** (owner's choice): flip the hand palm-down and up to swap with the held friend, once per flick.
- **Loss:** escaped Discords drift in the room and fill the **Discord meter**, shown as the room greying (PICO passthrough colour controls if available, else a grey static mist) and the music detuning. Full = level lost. Beating one in the room relieves it.
- **Music:** on-beat flicks hit harder and fill **Harmony**; full Harmony = **Chorus** (all three friends clear the screen). A boss Discord per world, phases weak to different friends.
- **Accessibility:** pinch-to-aim; assist (slower Discords, slower meter); Discord kinds told apart by shape, icon and sound, never colour alone.

## Plan
| Dates | Milestone | Owner |
|---|---|---|
| Nov 23-25 | Feel prototype: palm friend, flick, Hold, one Discord kind (grey-box) | **Headset checkpoint Nov 25** (flick feel) |
| Nov 26-29 | Three powers and kinds, wrong-match, escape into the room, room greying, portal layers, Level 1 | Checkpoint Nov 29 |
| Nov 30-Dec 2 | Levels 2-3, Harmony/Chorus, mini-boss, pinch-aim + assist, sounds | Checkpoint Dec 2 |
| Dec 3 | **Feature freeze** | |
| Dec 3-5 | Headset testing, frame rate, fixes, signed build (owner's key) | Play and report |
| Dec 6 | Submit | |
Cut order if late: Chord Discords, boss phases, Level 3. Sandbox and Battle must keep working.

## Done so far
- 2026-11-22: concept renders in `~/Desktop/Story mode preview/` (Level 1 portal; side-scroller layout, plain and annotated); scripts in `ArtSource/StoryMode/`.
- 2026-11-22: `Core/StoryRules.cs`: `DiscordKind`, `StoryRules.Beats/Matches`, `FriendQueue` (shuffled sets, next two, Hold once per flick), `DiscordMeter` (per loose Discord per second, relief when beaten, assist). Tests `Tests/EditMode/StoryRulesTests.cs`. **Written at the end of a session and not yet compiled or run**: run the EditMode tests first next time.

## Next
1. Run EditMode tests (fix anything in StoryRules).
2. Feel prototype in the scene: a palm anchor on the left hand (hand tracking and controller), the current friend shown there (real model, small), a flick detector (fingertip speed and direction from the right hand; pinch-release fallback), a launched friend flying out, one Discord kind spawning from a placeholder portal, hit/miss, Hold by palm flip (palm normal turning down then up).
3. Build to the headset for the Nov 25 checkpoint.

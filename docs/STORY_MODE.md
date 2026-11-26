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
- 2026-11-22: `Core/StoryRules.cs`: `DiscordKind`, `StoryRules.Beats/Matches`, `FriendQueue` (shuffled sets, next two, Hold once per flick), `DiscordMeter` (per loose Discord per second, relief when beaten, assist). Tests `Tests/EditMode/StoryRulesTests.cs` (pass).
- 2026-11-24: **feel prototype** (`Scripts/StoryDirector.cs`, `Scripts/FlickDetector.cs`, commit 4f31a0e): "Story test" sticker beside the leaf; portal ring; Squeak/Wobbler/Clang placeholders; real friend model on the left palm, next two at the wrist; flick = quick right-hand move from beside the friend (pinch/trigger = aim and launch); left pinch/trigger = Hold (palm flip after the checkpoint); wrong friend = Discord grows; loose Discords thicken a grey mist (meter); both fists = leave. EditMode 282/282, PlayMode 123/123. APK `Builds/LittleEchoes-story-proto1.apk` (owner's key). **Not yet on the headset.**

- 2026-11-25: headset checkpoint 1 (owner): the side sticker was hard to find; the story closed itself (controller grips read as both fists); pinch launched instead of a flick; the sandbox showed behind; Hold by palm flip missing; wants hand outlines. Owner then asked for "a polished lvl 1 ... an entirely new feature and not affect the rest of the game".
- 2026-11-25: **Level 1, The Seed** (uncommitted on feat/story-mode; APK `Builds/LittleEchoes-story-level1.apk`, owner's key):
  - Entry: a yellow "Play Story" sign with the three friends, standing beside the leaf's right end.
  - Separate from the sandbox: all built at runtime; sandbox renderers switched off (kept off each frame, after the sandbox) and restored to the state the sandbox last wanted; its input and song pause. Hand outlines show in story (`HandOutline.Wanted`). Shared code touched only additively: `PointerFrame.Joints/IndexTip/PalmUp` (XrPointerInput).
  - Portal with five painted parallax layers (sky, World Tree, hills, woods, path; procedural textures), leaves and buds, ring pulsing on the beat, Harmony (right, gold) and Discord (left, grey) arcs; it widens per wave and for the Grumble.
  - Waves (`Core/StoryLevel.cs`): 4 Squeaks, 4 Wobblers, 4 Clangs, 9 mixed (shuffled sets), then the Grumble (3 shells: spikes/Flutter, jelly/Bop, armour/Boom, 2 hits each; spits minions). Discords: own shapes, faces, motion and sound; each carries a little badge of the friend that beats it.
  - Flick = right index fingertip (FlickDetector: 1.0 m/s, 20 cm, jumps over 5 m/s ignored; near misses logged with their peak speed). Hand pinch does NOT launch (owner) unless Easy aim; controllers: trigger aims and launches. Hold = palm over and back within 1.4 s (`PalmFlip`, logs the palm axis), controllers: left trigger.
  - Powers: Flutter pierces; Bop ricochets off real walls (RealWalls layer) and curves to Wobblers; Boom lobs and bursts (0.5 m shockwave cracks Clangs, knocks others back). Wrong friend: sour note, the Discord grows.
  - Music (`StoryAudio`): 88 BPM on DSP, the room's own instrument voices (drum, then pluck from wave 2), each Discord beaten plays the next melody note on the next half-beat; souring/muffling with the Discord meter. Story sounds synthesised (`Core/StorySoundSynth.cs`, placeholders).
  - Harmony: matches (more on the beat) fill it; full = Chorus (all three friends home in on everything).
  - Buttons under the portal (eyes+pinch or controller ray): Pause/Resume (Try again / Play again at the end), Easy aim (saved), Leave. (The both-fists exit was removed 2026-11-26: it ended the story by accident.) Win: stars (StoryScore). Lose: Try again.
  - Tests: StoryLevelTests (7), Flick/PalmFlip (+3), StoryPlayModeTests (2: sandbox restored exactly; Easy remembered). EditMode 292/292, PlayMode 125/125. Rendered in-editor and checked.
  - Not yet: Chinese text for story, the three-mode title chooser (preview first), Levels 2-3.

- 2026-11-25, headset rounds 2-4 and fixes (uncommitted; APK `Builds/LittleEchoes-story-level1d.apk`):
  - Level hidden "except through the outline of my hand": transparent story effects overwrote the frame's alpha (URP Unlit's alpha blend defaults to One Zero), so PICO showed the real room over everything. Fixed: alpha blends One/OneMinusSrcAlpha (test).
  - Sandbox hidden by the **camera**: story objects live on layer 29 (`StoryArt.Layer`); during story the headset camera's culling mask is that layer only, the hands (PlayerHands) are moved onto it and restored after. No sandbox object is touched (the old switch-off-renderers approach lost to scripts re-enabling theirs each frame).
  - Theme music silenced during story (`BackgroundMusic.ShouldPlay`).
  - Flick: must start within 16 cm of the friend; after a flick the next friend waits until the finger settles (below 0.35 m/s for 0.15 s). Feedback: the friend swells and the palm ring brightens when the finger is close enough; a flick bursts a white ring and sparkles; the next friend pops in. A dotted aim line runs from the palm friend to the nearest Discord it beats; a flick roughly that way locks on.
  - The preview's look: the portal is a stencil window (`Shaders/StoryPortalMask`, `StoryPortalWorld`; materials in `Resources/`) onto a real 3D meadow built from the game's own meadow pieces (`Scripts/StoryDiorama.cs`): path, flowers, grass, round, blossom and pine trees, hills, clouds, the glowing World Tree with sparkles, warm painted light and depth haze; it scrolls sideways (the journey). White glowing ring with cream petals; fireflies in the room.
  - Friends face the player (copies' faces are on -z), with live CharacterFace (blink, beam, surprise, scared). Queue along the forearm (wrist joint). Harmony bar on the forearm with streaks ("Streak x3", "3 in a row!"; `HarmonyMeter.Streak`).
  - Level 1 gentler: meter assist 1.6 (Easy 3). Portal pulled in front of a near wall.

- 2026-11-25, round 5 (APK `Builds/LittleEchoes-story-level1f.apk`): flick = finger loaded against the thumb (or curled) beside the friend, then snapping out relative to the palm (`FlickHand`); must reload between flicks; hand pinch never launches. Harmony watch on the right wrist. Owner: "more of a connection of what's happening in the middle ... and the storyline":
  - Story: the Grumble stole the meadow's song; its Discords drain the colour. The friends travel to the World Tree to sing it back.
  - The meadow starts grey (`_StoryBloom` in StoryPortalWorld); every Discord beaten sends a glowing note into the portal that brings colour back; full colour and the World Tree blazing at the win; drained grey on a loss.
  - The ring's size follows the meadow's colour (0.7 to about 1.15), wide for the Grumble, widest at the end.
  - The side-scroll is the journey: Flutter, Bop and Boom (portal-world copies) walk the path between waves; they stop and face the player during a wave; the World Tree comes nearer each wave.
  - Discords walk up the meadow path inside the portal (portal-world materials, `StoryArt.ToPortal`) and hop out through the opening into the room.
  - Room greying: the mist rises with Discords in the room (not only escaped ones), pulses when one escapes; the fireflies turn to flickering grey static.
  - Streak: a wrong friend (including Boom's shockwave on non-Clangs) costs 0.15 Harmony and ends the streak; a friend that hits nothing ends the streak; the watch flashes red and says "streak lost".
  - Grumble: 4 hits per shell (12 in all); a Chorus breaks one shell at most; a shell counter above it ("Shell 1 of 3: needs Flutter ||||").

- 2026-11-25, rounds 6-8 (APK `Builds/LittleEchoes-story-level1k.apk`): Level 1 restores the **Udu** (`Core/StoryLore.cs`; real facts only, checked against Wikipedia "Udu", the Spencer Museum of Art's Igbo udu record and okwuid.com; disputed origins left out). The level's drum starts cracked (faint, out of tune) and mends with the meadow's colour; the game's 3D Udu icon sits on the meadow path, glowing as it mends. An **intro card** (the Udu turning, its story, hand instructions) waits for **Begin**; after a win a card to **Collect** opens all the facts and saves a bonus ("Restored: Udu" and a gold card icon by the Play Story sign that shows the card in the sandbox). Nothing in the sandbox is locked. Leave asks first; hand pinches don't press buttons while flicking is on; the sign turns to face the player. The ground dips into a hollow just behind the opening (green to the ring's bottom, never in front of the cards). The room's grey mist is back to its earlier look. `StoryLevel.MissCost`: Harmony lost per miss (0 for Level 1; harder levels later).

- 2026-11-26: **Level 2, The Two Strings: the Erhu** (owner: Erhu before the Mbira). Facts checked against Wikipedia "Erhu", Britannica "huqin", the Met's erhu record and China Daily; unconfirmed claims left out (e.g. Abing). Levels are data now (`StoryLevel.All`, `StoryLore.Levels`): Level 2 is mixed from wave 1, 42 Discords, 1.2x speed, a miss costs 0.06 Harmony, the room greys faster, and the Grumble has four shells (Clang, Squeak, Wobbler, Squeak) of five hits. The meadow is at dusk (the game's dusk pieces, an evening sky). The Erhu's snapped strings are heard in the melody (thin and out of tune, mending with the colour). Story opens on the first level still to mend; the intro card has level arrows; winning offers "Next level". APK `Builds/LittleEchoes-story-level2a.apk`. Not committed yet.

- 2026-11-26: **three levels** (owner: "I only want 3 levels"): 1 The Seed (Udu, the beat), 2 The Two Strings (Erhu, the tune, at dusk), 3 **The World Tree (Handpan, the chords, at night under the stars; hardest: 52 Discords, 1.35x, miss 0.1, the Grumble's five shells)**. Handpan facts checked against Wikipedia "Hang (instrument)"/"Handpan", PANArt's history and swissinfo.ch; the card says its makers call it the Hang and ask it not be called a "hang drum"; the game's instrument is a handpan. Winning Level 3 ends the journey.
- 2026-11-26: **the handpan replaces the mbira in both modes** (owner's choice: new default trio, the mbira kept in the library). `Instruments.Original` = Udu, Handpan, Erhu; `Instruments.Chair` = Handpan; the scene setup gives the chair the handpan (`HandpanTakesTheChair`); the lesson, place prompts (new `UiTerm.PlaceHandpan`, appended), meadow growth, Echo Battle defaults ("UHE") follow. A device whose saved arrangement is the old default (Udu, Mbira, Erhu) gets the new one. Measured: a handpan note rings about 2.4 s against the mbira's 1.1 s; owner to listen to a full sandbox song for muddiness. Tests updated where they assumed the mbira on the chair. APK `Builds/LittleEchoes-story-3levels.apk`.

- 2026-11-26: the World Tree stays rooted on the ground as it nears (it floated). Flutter is gentler on the handpan only (owner: "a bit too chirpy"; `VoiceDesign.ForOrb(..., ringingMetal)`): less grit, a softer top, a little more ring; still the lightest of the three. APK `Builds/LittleEchoes-story-3levels-c.apk`.

- 2026-11-26: v6.0 (code 44): the both-fists exit removed (owner: "I don't think the two fists to leave is a good idea anymore"); Leave (asks first) and Pause on the buttons.

## Next
1. Owner listens to the handpan (and the gentler Flutter on it) in a full sandbox song; shorten its ring if muddy.
2. The title sign's Story / Sandbox / Battle chooser (preview first); Chinese text for story mode.
3. The reviewer's Echo Battle feedback (memory: reviewer-feedback-v5).
2. (Done in grey-box) Feel prototype in the scene: a palm anchor on the left hand (hand tracking and controller), the current friend shown there (real model, small), a flick detector (fingertip speed and direction from the right hand; pinch-release fallback), a launched friend flying out, one Discord kind spawning from a placeholder portal, hit/miss, Hold by palm flip (palm normal turning down then up).
3. Build to the headset for the Nov 25 checkpoint.

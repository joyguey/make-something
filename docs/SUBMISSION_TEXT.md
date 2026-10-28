# PICO OS 6 — CONCEPT SUBMISSION TEXT (PROPOSED, not an implementation claim)

## Application / 250-word overview
**Room Orchestra: Little Echoes — Play your space.**

Little Echoes is a whimsical mixed-reality music playground that transforms everyday-looking virtual furniture into an orchestra you can touch. Three adorable musical companions—Flutter, a light turquoise blob; Bop, a springy lavender sphere; and Boom, a warm amber dumpling—respond to the way you move them. Each object has a distinct instrument-inspired voice: a Nigerian udu percussion tone, Zimbabwean mbira plucks and a Chinese erhu melody.

Rather than tapping a conventional sequencer, players toss or gently place the characters onto objects. Their collisions create visible, smiling musical echoes that orbit each object and replay on a shared beat. Position an echo higher to change pitch, change its weight to alter timbre, slide it around the orbit to change rhythm, or add another echo to build complexity. The characters react to one another, turning musical discovery into a delightful social performance. Repeated interactions produce understandable outcomes, so players discover pitch, rhythm, dynamics and layering through motion and listening, without needing to read a tutorial on music theory.

Accessibility is intrinsic to the concept: seated and one-handed players can create equivalent music using controller selection and placement rather than throwing, while visual pulse, shape identifiers, reduced-motion settings and adjustable control sensitivity support different needs.

The project builds on an existing Unity/PICO OS 6 blockout. The proposed next milestone is a polished three-character, three-instrument playable demo with synchronized orbit loops, reliable collision feedback and accessibility-tested input alternatives. We welcome PICO's technical feedback on Unity Spatial/OS 6 integration and ergonomic testing on the target headset.

## Current status — updated 2026-10-28 for Version 2
- **Implemented and tested in Unity (EditMode and PlayMode tests) and on the PICO Emulator 6.1:** three characters (Flutter, Bop, Boom), three instruments (udu, mbira, erhu), select-and-place and throwing, five notes per instrument, echo rings on one DSP clock at 88 BPM, Undo / Pause / Clear / Recenter, per-instrument volume and mute, Seated / Standing height, English / Chinese, controls on a leaf that follows a walking player, and a rainforest that grows with the song. Details: `docs/RELEASE_NOTES_v2.md`.
- **Not yet done:** headset testing; dragging an echo around its ring with the hand (today an echo is moved a step at a time in the controller's edit mode); a reduced-motion setting in the menu and scale/contrast settings; real-furniture mapping.
- **APK:** Version 2 build, emulator-tested only.

## Reviewer questions
1. Which OS 6 Unity app mode is recommended for stationary shared-space instrument play with virtual-object assignment?
2. What accessibility and device-ergonomics checks are most useful for a 4-star-plus PICO assessment?
3. What controller/hand input and spatial audio behavior should be evaluated on emulator versus physical device?
4. Which practical design/performance issues have most often limited similar creative spatial apps?

## 45-second demo story
1. See Flutter, Bop, Boom at near reach. 2. Place/toss Flutter to lamp: sound+echo. 3. Duplicate Flutter: friendly social face interaction+rhythm. 4. Add Bop and Boom: musical blend. 5. Nudge one orbit echo and adjust pitch. 6. Demonstrate one-hand seated alternative. 7. Stop/clear. [Only describe footage actually captured.]

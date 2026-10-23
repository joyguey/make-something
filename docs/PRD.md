# PRODUCT REQUIREMENTS DOCUMENT — Room Orchestra: Little Echoes
**Version 3 / 2026-10-22 / proposed MVP, not an implemented-game claim**

## Elevator pitch
Little Echoes turns a small mixed-reality arrangement of familiar virtual furnishings into a living, smiling orchestra. Toss, tap, or place one of three expressive weighted balls to discover percussion, melody, rhythm, musical layering and harmony. Each musical collision leaves a miniature looping echo visibly orbiting the object; rearrange the echoes and you rearrange the music. No scores, no wrong answers, no conventional sequencer or prerequisite music theory.

## Audience / benefit
Curious creators and non-musicians (including younger/family audiences where age/product requirements permit), players who enjoy low-pressure creativity, and people who benefit from visual, kinesthetic learning. Accessibility includes seated, one-hand and low-motion play rather than forcing identical controls for everyone.

## 90-second magical session
00–15 seconds: friendly balls hover at comfortable reach; select seated/standing and preferred input; one line of guidance, 'Try waking one up.'
15–30: place or throw Flutter at the lamp. A clear erhu-like sung note, character squish and first orbiting echo. Notice pitch changes if higher/lower or use pitch petals.
30–45: repeat Flutter at lamp; mini echo peers at first, greets it, and repeats at a second orbit position, forming rhythmic response (not higher BPM).
45–65: Bop wakes chair/mbira, Boom wakes table/udu; now three cultural instrument families contribute to a coherent groove. All events visible as mini faces on their respective orbits.
65–80: grab/select an echo and shift it along its orbit; the 'da-da' spacing changes; move vertically/choose petal and the pitch responds.
80–90: room celebrates through gentle motion; player can pause, undo, clear, and keep experimenting. No grading.

## Repeatability / depth
Repeatable musical *rules* with variable compositions: object chooses instrument; height/petal chooses 5-note pitch (except udu percussive timbres); ball weight selects articulation/tone; impact speed or virtual slider controls dynamics; orbit angle/time chooses rhythmic onset; more echoes = more subdivision; shared pulse stays at 88 BPM unless player deliberately taps a tempo heart. Various object positions change spatial staging, not scale. First version uses D major pentatonic to reduce clashes, but complementing pitches and sound balance still need design.

## MVP MUST (Gates 1–3)
- Flutter/Bop/Boom models referencing approved image, silhouette + patterns + accessible names.
- Table=udu-inspired percussion; chair=mbira-inspired plucked note; lamp=erhu-inspired bowed note. Accurate names/regions, synthesized fallback clearly labeled.
- Simple throw AND selection/placement equivalent, usable one-handed while seated; aim assist and adjustable sensitivity.
- Five pitch bands D E F# A B for pitched voices; udu uses drum articulations, not artificially identical keyboard pitches.
- DSP stable synchronized three-orbit loop: 4 beats, 16 quantized positions/bar, 88 BPM default; 6 echoes maximum per target, visible fixed play marker.
- One action => one event; collision debounce; no accidental looping duplicate, immediate expressive feedback.
- Friendly expressive face states tied to event logic, not randomly changing faces.
- Move or remove an echo via accessible selection; undo; pause/clear/recenter; adjustable visual/audio settings and reduced motion/flash.
- Clear optional instrument origin labels that never mischaracterize the underlying tradition.
- Two-minute guided first encounter and optional skip; user never forced into a score or rigid timed challenge.
- Automated unit/PlayMode tests; emulator status; physical headset tests explicitly pending if not done.

## MAY if time (Gate 4)
- Tap a large pulse heart 3–4 times to change global BPM (for everyone) at a bar boundary.
- One open-palm vertical swell gesture only with verified SDK/devices, with a controller/menu equivalent.
- Separate object placement and limited reassignment, only if it preserves safe small-space comfort.
- Polished transition animations / recorder, after reliable core mechanics.

## NOT FOR THIS MVP
AI-generated free melodies, generic DAW interface, complex scale/key selection, more colors/characters, online account, user-generated file export, automatic furniture classification, room-scale movement, multiplayer, achievements, save-to-cloud, full hand-tracking-only mode. These may form a future product roadmap.

## Product acceptance criteria
- With hand tracking OFF, a seated player can complete the full create-change-remove-reset loop using ONE controller at arm-rest height.
- A person who cannot throw can place two Flutter echoes in the same orbit and hear two reliable evenly spaced or intentionally off-beat notes.
- Same recorded event parameters yield same note and instrument behavior across repeats. DSP loop drift is measured, not assumed.
- There is no invisible-only beat information; each event and pulse has visual correspondence. Color-blind differentiation works in grayscale.
- No uncontrolled flash, motion, loudness jump, requirement to stand or rotate; calibration and reset present.
- A new tester understands 'add a note and hear it repeat' within 30 seconds, and can modify rhythm without explanation within 90 seconds (observe and log; not yet verified).
- No Severity 1/2 bugs; all build/test claims link to evidence.

## App-review positioning
Spatial creativity is the main differentiator: players sculpt music around objects through position, collision, weight and physical choice instead of flat screens. Musical theory emerges through play. Cultural instruments are introduced respectfully with honest sound provenance. Inclusivity is designed into the very same musical interactions.

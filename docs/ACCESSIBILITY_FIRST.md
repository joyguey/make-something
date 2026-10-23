# ACCESSIBILITY-FIRST DESIGN & TEST PLAN
**Status: concrete requirements to implement and verify; not a claim of current accessibility certification.**

## Design stance
Accessibility is expressed as more than an options screen: equivalent ways to *make the same musical choices*. Consider motor, vision, hearing, neurodiversity, vestibular sensitivity, photosensitivity, cognitive load and fatigue. Do not presume that every alternative is equally accessible to every person. Consult disabled testers rather than declaring blanket compliance.

## MUST ship in first polished MVP
| Concern | Design requirement | measurable acceptance check |
|---|---|---|
| Motor/reach | 'Place' alternative to throw: choose orb -> target -> emit exact same event. Adjustable virtual target distance/height and large target hit volumes. | Seated with one controller, hands low: produce 3-note loop without a throw; no floor/head-above reach. |
| One-handed | Entire create/edit/undo/reset/volume flow accessible with one controller; no simultaneous pinches. | Complete all steps with either controller alone on supported device; document if single-controller switching unsupported. |
| Input parity | Do not require hand tracking; controller pointer / trigger/select and back navigation. Desktop/editor simulator for regression testing. | Turn off hands; complete entire main experience. |
| Low dexterity | Aim assist, sticky target highlights, adjustable throw/impact thresholds, optional steady hold/select, presets for intensity. | Can land or place events without timed gestures; adjustable sensitivity persists. |
| Seated calibration | Comfortable front near-field virtual arc; recenter button; initial height setup; no chasing ball; automatic safe recall. | Seated trial completes without leaning/turning or touching real furnishings. |
| Hearing / partial deafness | Pulse/beat visible, event position visible, pitch up/down visual and/or labeled notes, independent volume slider and per-instrument mute; captions for explanatory speech. | With volume muted, player can identify current beat, 3 active voices, relative high vs low note and rhythm density. **Not a claim of full equivalent experience for Deaf players.** |
| Color & vision | Shape + symbol + optional text labels beyond color: Flutter tall/sparkle, Bop sphere/dot, Boom wide/ripple. Contrast and scale options; clear focus ring; motion not required to see events. | Convert screenshot to grayscale; three balls still distinguishable; legible UI at typical seat distance, test on headset. |
| Vestibular | Anchored stationary scene; no forced camera movement or virtual locomotion; minimal peripheral effects and reduced-motion option. | Can play with all nonessential object/halo movement reduced or static; no moving camera. |
| Photosensitivity | No strobes, seizure-inducing beat flashes, rapid full-screen contrast cycling. Reduce flash option defaults conservative. | Manually review max flashes, scene changes, brightness; visual pulses subtle, not full-screen. |
| Cognitive / sensory | Progressive disclosure, one-action discoverability, optional short labels, no timers/score/fail sounds; allow pause/replay/reset; calm/low-effects mode. | A new player completes first event in 30s without lengthy instructions, with skip/hint always available (needs user test). |
| Fatigue | 10–15 minute comfortable sessions, easy stop/resume, no held poses, one click alternative to repeated throws. | 5-minute stationary trial without mandatory rapid/repetitive movement. |
| Audio comfort | No surprising loud attack, master volume and per-voice controls, normalizer/headroom; mute works at once. | Instrument changes don't clip and do not produce abrupt spikes (measure audio). |

## Accessible onboarding
- Offer simple first choice **Play with controller** (default proven path) or **Play with hands** (if actually supported), then seated/standing and comfortable reach calibration; settings editable at any time.
- One sentence at a time: `Pick a friend` -> `Wake an object` -> `Listen/see the echo` -> `Try another`.
- All tooltips optionally dismissible; don't block the first fun interaction with a tutorial wall.
- For low/no audio, offer visible count-in beat markers, pitch petals and orbit replay icons. Text labels are optional, not forced.

## Alternate input routes
Path A: comfortable toss. Path B: controller direct select-and-place. Path C: select a preexisting echo and nudge its orbit slot and pitch petal. Path D: optional hand-pinch/gesture once tested in OS 6. All end in *the same normalized MusicalHitEvent* and persist across identical arrangements. Do not write separate music engines for each route.

## Test cases (record screenshots/video/logs)
1. `A11Y-01`: one-handed/controller-only/seated/throw disabled: create two Flutter on lamp, one Bop on chair, one Boom on table, edit timing, undo, clear. PASS only if fully completed.
2. `A11Y-02`: grayscale: distinguish Flutter/Bop/Boom via silhouettes and icon badges. Manual review.
3. `A11Y-03`: audio muted: locate next orbit play marker, count 4 beats, discriminate high/low by pitch display; don't assume visual == full sonic participation.
4. `A11Y-04`: reduced motion and minimal effects: no critical cue disappears; each orbit can show static progress position/beat meter if animated orbit disabled.
5. `A11Y-05`: input device change/loss: no softlock, recover via controller navigation.
6. `A11Y-06`: no rapid tapping/throw to accomplish loop; choose slots by discrete buttons.
7. `A11Y-07`: reset view from seated location, without physical approach to real lamp/table/chair.
8. `A11Y-08`: left/right-handed alternatives, scale/contrast and readable floating labels.
9. `A11Y-09`: pause audio at any time, transitions safe, all sounds manageable by mixer.
10. `A11Y-10`: physical comfort trial by tester—latency, fatigue, simulator sickness, perceptual readability; emulator cannot certify.

## Priority and honest limitations
Accessible core mechanics are a RELEASE BLOCKER; complex hand gesture and eye-tracking enhancements are NOT. A complete accessible pathway for Deaf users may require co-design and alternative experience design; do not overclaim compliance with WCAG or ISO XR standards merely because a checklist exists.

## External reference (consult, don't invent conformance)
- W3C XR Accessibility User Requirements (XAUR): https://www.w3.org/TR/xaur/
- XR Access: https://xraccess.org/resources/
- PICO hand tracking/current Unity environment: https://developer.picoxr.com/document/unity/hand-tracking/

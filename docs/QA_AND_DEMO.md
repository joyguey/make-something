# QUALITY / ACCESSIBILITY / DEMO ACCEPTANCE RUBRIC
This is an INTERNAL aspirational 5-star-quality standard, NOT PICO's proprietary or official star rating rubric.

## Release gates (fail a gate = don't report ready to launch)
**G0 Version/safety:** existing Unity scene preserved, SDK version documented, private repository backup confirmed or not-yet-enabled disclosed, no tokens in committed files.
**G1 Delight/learning:** a newcomer triggers a note in <=30 sec without a long tutorial; understands duplicate echo -> more notes (not tempo shift); can hear and see distinct voices.
**G2 Timing/music:** same action produces same note/attack. Orbits and audio remain synchronized for 5 minutes; no unintended double event; reliable stop/undo. Report quantitative timing tolerance and how measured rather than arbitrary invented limit.
**G3 Accessibility:** complete A11Y-01 controller-only seated one-handed session; A11Y-02 grayscale differentiation; A11Y-03 visible rhythm; A11Y-04 reduced motion; A11Y-09 immediate mute. Record exceptions; physical headset comfort pending if untested.
**G4 Visual audio art:** user-approved three silhouettes retained, visibly responding to repeated same-type throws; licensed/synthetic-labeled instrument voices; no excessive sensory stimulation; no clipping observed.
**G5 PICO compatibility:** real dependency audit, build logs, emulator launch if supported; any APK only if path verified. Hardware input/comfort still requires physical review.

## Automated test matrix
| ID | Test | Expected |
|---|---|---|
| MT-01 | repeat identical input | same pitch/velocity/orb articulation/event outcome |
| MT-02 | each pentatonic height/petal | stable D E F# A B target pitch mapping |
| MT-03 | udu hit | one of five drum timbre choices; not falsely a note |
| MT-04 | collision bounce spam | one event for one intentional interaction |
| MT-05 | duplicate Flutter same lamp | two echo IDs, 2 positions, BPM unchanged |
| MT-06 | drag/nudge echo | event shifts slot and audible onset next bar |
| MT-07 | three targets | coherent synchronized 4/4 ring, individually mute-able |
| MT-08 | six-cap reached | 7th deliberately handled with clear user affordance |
| MT-09 | pause / reset / recenter | no stuck voice, state reset correctly |
| MT-10 | sleep/app resume if testable | no runaway scheduled notes or clock discontinuity |
| MT-11 | two different input adapters | same normalized event for comparable intent |
| MT-12 | color-blind/reduced-motion modes | no music rule changes; clear visible equivalents |

## 45-second evaluator capture (only after real working scene)
0–5 s: three approved-looking expressive balls and safely placed virtual furniture, title 'Play your space'.
5–12 s: Flutter toss OR controller place to lamp, erhu note, mini echo appears.
12–18 s: second Flutter, two orbit faces glance at each other; faster note spacing visibly *without global tempo acceleration*.
18–26 s: Bop to chair, Boom to table: three distinct voices with spatial layering.
26–35 s: move echo to a different orbit position, hear and see timing change; raise note / select pitch petal to hear pitch shift.
35–42 s: demonstrate seated/controller/one-hand select-and-place parity and reduced-motion label mode.
42–45 s: pause/clear, finish 'Everyone can make music with their space.'

## Evidence index for agent
`docs/AGENT_PROGRESS.md` includes command exact invocations, versions, test count/results, logs, screenshot/video paths, hardware presence. Mark each row: PASS, FAIL, NOT TESTED, BLOCKED. Do not write fake URLs or invented numerical performance.

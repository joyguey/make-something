# UNITY / PICO ENGINEERING PLAN
**Do not assume installed SDK/class names: detect before implementation.** Screenshot suggests Unity 6.3 LTS / Android / PICO package, but read local configuration.

## Audit before touching anything
Project root? `Assets`, `Packages`, `ProjectSettings`; Unity editor exact version in `ProjectSettings/ProjectVersion.txt`; package versions manifest and lock; XR loader configuration; PICO Unity SDK OS6 mode; existing scenes/prefabs/controllers; current sound file list and licenses; existing tests; existing `.git`, remote/branch. Run `pico --help` or `pico-cli --help` only if binary exists; follow detected help. Inspect Unity MCP server by actual method list. Inspect Blender MCP similarly; no fabricated tool names. Snapshot baseline and compile/log/test screenshot before any refactor.

## Architecture modules (Pure C# first where possible)
- `OrbDefinition` ScriptableObject (flutter/bop/boom: silhouette ID, nominal mass/drag, animation, timbre map)
- `InstrumentDefinition` ScriptableObject (target: udu/mbira/erhu, sample set/synth patch, pitch/drum actions, mapping, provenance)
- `MusicalHitEvent` immutable data (single normalized action from throw/place/tap; stable GUID, target, orb, height/petal, velocity preset, slot)
- `AccessibleInteractionAdapter` (throw, select-place, orbit nudge, pitch petal, controller/optional hands, editor simulator; same core event pathway)
- `CollisionDeduplicator` (one intended hit -> one music event despite multiple contacts or collisions)
- `NoteMapping` (5 pitches or percussive articulations, weight envelope, dynamic tier, deterministic)
- `MusicClock` DSP bar/beat/step mapping 4/4 x 16 slots @ 88 BPM; future Heart tempo at boundary
- `EchoSequencer` (3 synchronized orbits, cap 6/target, add/remove/move/undo; event identity)
- `VoicePool` (precise DSP scheduled one-shots, fade/stop/mute, headroom & channel limits)
- `OrbitRenderer` (positions from clock; proper visual correspondence with sound; reduced-motion mode)
- `CharacterExpressionController` state machine with deterministic social reactions
- `AccessibilitySettings` persistent local settings (seated calibration, handedness, aim, labels, contrast, reduced motion, audio)
- `PicoRuntimeBridge` wraps installed SDK-specific input/platform calls for mock tests

## Phases and testable gates
P0 Protect: baseline copy, report root, installed versions, git status, 0 changes until backup; existing project still opens and baseline screenshot captured.
P1 Playable event: existing ball launch not broken; virtual targets respond; alternative place works; normalized hit tests. Multiple collisions don't double-trigger.
P2 Musical semantics: five notes or five udu articulations map deterministically, weights affect timbre, dynamics affect volume; no random note calls. Audio provenance logged.
P3 Orbit magic: 3 visually readable 16-step rings, one DSP clock, multiple echoed hits at chosen timing/slot, fixed marker and interval repeat, edit/remove undo; unit and PlayMode tests.
P4 Inclusive controls: seated calibration, controller-only one-hand full session, reachable pitch petals, accessible rhythm preset/step nudge, visual pulse, motion & flash controls, audio mute; A11Y-01 to -09.
P5 Whimsy polish: reference-accurate low-poly Flutter/Bop/Boom plus mini echoes and face state; gentle effects, meaningful sound feedback, auditable licenses, no performance regressions.
P6 Optional enrichment: only if gates pass—tempo Heart + gestures behind feature flag; never replace controller path.
P7 Integration: reliable scene, real emulator logs/video when available, input switching, audio DSP drift and voice budget, profile frame performance, save baseline, truthful APK claim.
P8 Submission: concise actual status, PRD/first-play clip, screenshots, tests, reviewer questions, no unverified release quality.

## Testing
EditMode: mapping `0.00`..`1.00`, petal equivalence, udu percussion mode, duplicate events, step wrap, cap, no negative step, tempo limits, reduced motion unchanged data. PlayMode: three targets, one action/one recorded event, repeat after whole bar, undo/remove, shift offset, input parity via simulator, pause/mute. Integration: Unity console errors, emulator video, sustained loop 5min, audio clip guards, `git diff` review. Manual device: comfortable reach, tracking reliability, hand/controller behaviour on actual OS6 build, sound quality, spatial audio, photosensitivity/vestibular and one-hand seated experience. Do NOT conflate editor PlayMode with device input.

## Source links for agent to inspect (avoid wholesale copy/paste)
https://developer.picoxr.com/resources/?platform=unity
https://developer.picoxr.com/document/unity/
https://github.com/Pico-Developer/PICO-Unity-SDK-Skills
https://docs.unity3d.com/ScriptReference/AudioSource.PlayScheduled.html
https://docs.unity3d.com/ScriptReference/AudioSettings-dspTime.html
https://github.com/github/gitignore/blob/main/Unity.gitignore

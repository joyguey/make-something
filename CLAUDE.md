# CLAUDE CODE OPERATING INSTRUCTIONS — LITTLE ECHOES V3
**The following instructions are authoritative.** Read all relevant docs before edits. Ignore superseded V1/V2 design documents if they contradict V3. Work on the user's EXISTING Unity PICO OS 6 RoomOrchestra project, not a fresh toy scene. Character art reference: `docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png` (or `docs/reference/...`).

## User's non-negotiable product vision
A joyful, low-poly, approachable mixed-reality musical playground inspired by Blob Opera's *immediacy*, not its exact characters or implementation. Three balls only: Flutter (turquoise/tall/light), Bop (purple/round/medium), Boom (orange/wide/heavy). Preserve approved reference shapes/face/colors; do not clone another existing product. Furniture is initially *virtual targets* in a safe reachable stage: table/udu percussion, chair/mbira pluck, lamp/erhu melody. Users discover theory from what movement/weight/timing **does**, not a lecture. Every action should have understandable musical consequence. Accessibility is part of the gameplay contract from first implementation.

## Work autonomously and be truthful
- Inspect existing project structure, git, package versions, SDK configuration, working throw/collisions, input, sound, Unity MCP, PICO CLI and optional Blender MCP *before changing code*. Validate command help/version; never invent packages, API classes or CLI commands.
- Implement one reviewable phase at a time. Reproduce -> fix -> compile -> run automated tests -> inspect Unity console -> document real evidence. Retry up to 3 evidence-informed fixes per distinct issue, then report blocker. Do not claim simulator equals headset or that you listened to sound.
- Only ask owner about destructive changes, irreversible remote Git operations, spending, credential access, legal/licensing decisions, unsupported headset physical confirmation, and major unsettled scope choices. Default to safe non-destructive behavior. Do not ask routine coding questions.
- Never use `git reset --hard`, `git clean -fdx`, delete the current scene, rewrite the project from scratch, upgrade Unity/PICO SDK automatically, or overwrite an existing remote without explicit approval. Do not silently create/push GitHub repositories or make projects public.
- At start, copy/merge `docs/AGENT_PROGRESS_TEMPLATE.md` into `docs/AGENT_PROGRESS.md` and update each phase. `docs/DECISION_LOG.md` keeps assumptions and decisions. Before changes, print path and `git status`/`git remote -v` results. If already git-tracked, commit a baseline after owner approves and sensitive file scan passes.
- Never commit or print secrets, access tokens, .env files, proprietary third-party assets without license, or sensitive room-tracking data. No uploads or user telemetry unless approved. Use local-only project state.

## Priorities, in this exact order
1. Protect current project and build a private Git safety net.
2. One action -> one well-defined music event with immediate audiovisual feedback; alternate select-and-place input works.
3. Three weights change timbre/attack/physics safely and deterministically. Height or five approachable petals change pitch; strength changes dynamics but no physical strength required.
4. Single DSP master clock, 4/4, 88 BPM; three visible 16-slot echo-orbits. One revolution is one bar. Orbs on the orbit visibly *are* the musical events. Duplicate hits add/subdivide notes; **do not** change global BPM.
5. Understandable facial reactions, polished yet low-cost stylized art/audio, no sensory overload.
6. Accessible modes, undo/recenter/clear/pause, captions/visual music and input parity. Validate each mode.
7. Optional drag-to-edit and tap-tempo if stable; optional supported hand gesture later. Do not block release over advanced features.
8. Honest submission evidence and scope labeling.

## Accessibility is a hard gate
No mechanic requires throwing, fast reflexes, sustained pinch, high overhead reach, room-scale mobility, or two hands. All critical actions must work via a supported controller select/tap interface with adjustable reach. Seated mode and height calibration in first run. One-handed flow. Pause and reset available any time. Color is never the sole way to differentiate characters, and music has readable visual feedback. Adjustable flash/motion/effects, interface scale/contrast, and independent instrument level/mute. Do not promise equivalence for fully deaf players without meaningful user research; music is audio-led, visual cues can make theory and timing intelligible. Full requirements `docs/ACCESSIBILITY_FIRST.md`.

## Engineering boundaries
- Inspect `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, XR loaders/mode and actual PICO SDK docs. For OS 6 Unity SDK, verify PICO XR vs PICO Spatial and do not blindly migrate. On current project screenshot Unity 6.3 LTS, verify locally.
- Separate `MusicalHitEvent`, `InstrumentDefinition`, `OrbPersonality`, `NoteMapping`, `MusicClock`, `EchoSequencer`, `VoicePool`, `CollisionDeduplicator`, `AccessibleInteractionAdapter`, `UIFeedback`, and `SceneConfiguration` behind testable interfaces.
- All audio scheduling must derive from `AudioSettings.dspTime` or validated DSP scheduling equivalent; audio must never depend on animation framerate. 16-step/orbit UI is projection of music events. First hit feels instant. Unit test map boundaries, collisions/duplicate bounces, drift, bar quantization, tempos, removal, pause and reset.
- Multiple sound sources pool, no clipping, no unlicensed claims of traditional recorded instruments. Placeholder samples clearly marked until verified.
- Mobile performance, pass-through readability, no strobing, audio mixing and comfort are part of the build gate. Emulator evidence != real headset evidence.
- Use a smaller implementation that works rather than claim automatic furniture recognition, room occlusion or full studio recording not yet supported.

## Skill dispatch
Use `.claude/skills/pico-unity-guardian/SKILL.md` when working on Unity/PICO; `.claude/skills/accessibility-auditor/SKILL.md` for each interaction; `.claude/skills/music-systems-test/SKILL.md` for timing/audio; `.claude/skills/character-art-pipeline/SKILL.md` for Blender/art; `.claude/skills/git-safety/SKILL.md` before Git operations. These are project-specific instructions, not evidence that any MCP is installed.

## Phase finish report
- **Implemented** (only verified functionality)
- **Actual tools/commands used and versions**
- **Tests passed/failed with logs**
- **Artifact paths/screenshots**
- **Accessibility parity verified vs unverified**
- **Risks and next phase**
- **Single critical approval needed**, if any.

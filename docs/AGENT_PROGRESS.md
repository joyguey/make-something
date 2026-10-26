# AGENT_PROGRESS — living log
Date/time and timezone: 2026-10-22 (local Mac time)
Unity project root: /Users/joy/Desktop/PICO Project
Git branch / current commit / remote privacy confirmed?: `main` @ f4f51c2e0b349c97d2e5a9aa1c69c01a5eca733e; origin https://github.com/joyguey/make-something.git; PRIVATE confirmed via `gh repo view`
PICO SDK / Unity exact installed versions: Unity 6000.3.25f1. Packages from git #main: com.bytedance.pico.xr@4e028cc115f0, com.bytedance.pico.spatialadapter@819e7b187e65, com.bytedance.pico.mcp-extensions@07b3a224881a (unpinned branch refs, so not upgraded). XR mode: PICO **Spatial** (SpatialAdapter, Android defines PICO_SPATIALIZER;ENABLE_PICO_INTERACTION_PACK;PICO_MS_SDK). Input System 1.20.0, XRI 3.3.2, XR Hands 1.7.3, Test Framework 1.6.0, URP 17.3.0.
Connected tools verified (Unity MCP / PICO CLI / Blender MCP): pico-cli 0.6.0 (`npx @picoxr/pico-cli --version`); pico-dev-knowledge MCP works (PICO SDK 6.1 docs vault queried). unity-mcp is configured but NOT approved for this project (`/mcp` needed), so Unity was driven with batchmode instead. Blender MCP: not configured or checked yet (Prompt 06).
Current phase: Prompt 02 (functional core + input parity) COMPLETE in editor/batchmode on branch `feat/little-echoes-core`. Prompt 00 audit and Prompt 01 backup done.

## Changed files
- Prompt 02: see `git show --stat` on branch feat/little-echoes-core.
- `docs/AGENT_PROGRESS.md` (new, from template)
- `docs/DECISION_LOG.md` (new, from template)
- `.git/` created by `git init -b main` (folder was not a repository)
- No changes to Assets/, Packages/, ProjectSettings/, .gitignore or .gitattributes

## Actual commands and outcomes
- `git --version` → 2.50.1 (Apple Git-155)
- `git status` → "not a git repository" → `git init -b main` is allowed
- `gh --version` → not installed; `git lfs version` → not installed
- Credential helper: osxkeychain; no github.com credential stored
- `git ls-remote https://github.com/joyguey/make-something.git` (non-interactive) → failed: no credentials
- Unity: Asset Serialization = Force Text (m_SerializationMode: 2); Version Control = Visible Meta Files
- Sizes: Assets 3.3M, Packages 24K, ProjectSettings 136K; Library 11G and Builds 50M (both ignored)

## Tests and results (PASS/FAIL/NOT TESTED/BLOCKED)
- Secret filename scan (.env, keystore, pem, key, token, credential): PASS — none found
- Secret content scan (api key / token / password / private key patterns): PASS — matches were only in docs that mention these words
- Large file scan (>5 MB outside Library/Builds/Logs): PASS — none
- Every Assets item has a .meta: PASS
- Binary YAML check: only ProjectSettings/XRSettings.asset is non-YAML, and it is JSON text, so it is safe with `text eol=lf`
- CRLF check in Assets/ProjectSettings: PASS — none
- `brew install gh` → gh 2.103.0; owner ran `gh auth login --web` → logged in as joyguey; `gh auth setup-git`
- Pre-push: `gh repo view` → PRIVATE, isEmpty=true, viewerPermission=ADMIN
- Owner approved push; `git remote add origin https://github.com/joyguey/make-something.git`; `git push -u origin main` (no force) → new branch main
- Post-push verification: PASS — remote main SHA == local HEAD f4f51c2e0b349c97d2e5a9aa1c69c01a5eca733e; GitHub tree has 248 blobs (= local tracked), Assets 179 (103 .meta, 12 .cs), Packages 2, ProjectSettings 26, 0 files under Library/Temp/Logs/Builds/UserSettings; repo still PRIVATE

## Prompt 00 audit (2026-10-22)
- VERIFIED: Unity root /Users/joy/Desktop/PICO Project; scene Assets/RoomOrchestra/Scenes/RoomOrchestra.unity (only enabled build scene); 10 gameplay scripts (Ball, BallLauncher, AimArc, InstrumentTarget, InstrumentInputRouter, LoopStation, LoopControlButton, InstrumentButton, ColorFlash, InstrumentType); 24 pooled balls, aim assist, 2-bar 90 BPM LoopStation; 15 WAVs (Udu/Mbira/Erhu x5).
- VERIFIED: baseline batchmode compile of the untouched project: 0 `error CS`, "Exiting batchmode successfully".
- VERIFIED from PICO 6.1 docs: in Spatial mode, controller ray pinch arrives as Spatial Input touches (`SpatialInputSupport.GetInputState`), and controller buttons are read via `UnityEngine.XR.InputDevices` (trigger, A/B/X/Y, stick).
- UNVERIFIED: headset behavior, emulator behavior of the new code, existing APK still matching the source.
- No prior tests existed.

## Prompt 02 — functional core + input parity (2026-10-22)
Implemented (verified in editor/batchmode only):
- `RoomOrchestra.Core` asmdef (pure C#, no engine refs): OrbType/OrbProfile/OrbCatalog (Flutter/Bop/Boom data: silhouette, motif, mass, bounce, speed clamp, proportions), MusicalHitEvent + PitchInput + IntensityTier + InputPath, IntensityMapping, CollisionDeduplicator, MusicalHitFactory, SelectionModel, ControllerCommandMapper, ReachEnvelope. InstrumentType moved here (GUID kept).
- `RoomOrchestra.Runtime` asmdef around existing scripts (script GUIDs unchanged; scene refs intact). New: MusicalHitPipeline (the single path), OrbSelector (select-and-place), OrbToken, TierButton, ControllerButtonInput (+ XRControllerSource).
- Ball: per-orb proportions/mass/bounce/color; one throw = one action = at most one event (later bounces AND ricochets ignored; previously a ricochet onto another instrument played again). Dynamics = 3 tiers from impact speed.
- Tap on furniture now places the selected orb (same event as a throw). Tap tray token = select orb; tap intensity button = cycle tier. Controller: stick L/R orb, U/D target, A/X place, B/Y intensity, either hand. Editor simulator: arrows/Enter/Q, 1/2/3 place on table/chair/lamp.
- Scene (via `LittleEchoesSceneSetup.ApplyAndSave`): LittleEchoes root with orb tray at (0,0.55,1.1), 3 tokens with shape+motif, intensity button (1-3 raised bars), focus markers over targets, pool balls pre-skinned as Flutter. Semantic scene diff: 0 objects removed, 84 added, only intended ball fields changed.
- Not changed: note selection is still the old PingPong order (Prompt 03), LoopStation still 90 BPM/2 bars (Prompt 04).

Commands (Unity = /Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity, Editor closed):
- `Unity -batchmode -nographics -quit -projectPath . -buildTarget Android -executeMethod RoomOrchestra.EditorTools.LittleEchoesSceneSetup.ApplyAndSave`
- `Unity -batchmode -nographics -projectPath . -buildTarget Android -runTests -testPlatform EditMode -assemblyNames RoomOrchestra.Tests.EditMode -testResults <xml>`
- same with `-testPlatform PlayMode -assemblyNames RoomOrchestra.Tests.PlayMode`
- `-executeMethod RoomOrchestra.EditorTools.PlayerCompileCheck.CompileAndroid` (Android player script compile, no APK)
- `-executeMethod RoomOrchestra.EditorTools.SceneSnapshot.Capture -snapshotPath docs/evidence/phase2-start-view.png`

Results:
- EditMode: PASS 52/52 (orb catalog, intensity boundaries, dedup/bounce spam, repeat window, ids, intent parity, selection wrap, controller mapper incl. no auto-repeat/hysteresis/phantom press/either hand, reach boundaries, real-scene seated reach + occlusion + wiring).
- PlayMode: PASS 8/8 (real-physics bouncing ball -> exactly 1 event; every orb works with the same gentle throw; place reproduces toss intent; controller-only one-handed (left) place with no throw; repeated places are separate actions; orb selection re-skins ready ball).
- Android player script compile: PASS ("Android player compile OK", RoomOrchestra.Core.dll + RoomOrchestra.Runtime.dll included).
- Bugs found and fixed: (1) float rounding rejected a tap exactly one debounce window later — fixed with 1e-9 tolerance; (2) seated occlusion: tray at 0.8 m blocked the seated view of the chair seat (test FAILED: "TraySlab blocks the view of Chair from eye height 1.15 m"); tray lowered to 0.55 m, test now passes.
- NOT TESTED: emulator, headset, real controller buttons in Spatial mode, Spatial renderer drawing of the new tray/markers, audio by ear.

## Screenshots, video, audio, build artifact paths (verify existence)
- docs/evidence/phase2-start-view.png — Unity offline render from EditorPreviewCamera (not PICO Spatial renderer, not headset).

## A11Y acceptance coverage (A11Y-01 through -10)
- A11Y-01 partial (automated, editor): controller-only, one hand, no throw: select orb + target + intensity + place verified in PlayMode. Edit timing/undo/clear do not exist yet (Prompt 04). Not a full PASS.
- A11Y-02 partial: orbs differ by silhouette + motif (tested in data and visible in render); grayscale manual review NOT done.
- A11Y-05 partial: controller disconnect/reconnect produces no phantom press (unit test). Softlock recovery NOT TESTED.
- A11Y-06 partial: placing needs no rapid input; held buttons never repeat. Slot selection does not exist yet.
- A11Y-07 partial: all interactive elements pass the seated reach + no-occlusion test; recenter NOT implemented.
- A11Y-03, -04, -08, -09, -10: NOT TESTED / not implemented yet.

## Known issues and risks
- 15 WAV files in Assets/RoomOrchestra/Audio (Udu/Mbira/Erhu) have no source entry in docs/AUDIO_ASSET_LICENSES.md. They look like they were synthesized in an earlier session (batch-created 2026-10-15 21:28, no metadata), but this has NOT been verified. They are uploaded to the PRIVATE repo only; treat them as unverified placeholders.
- Git LFS is not installed; not needed now (largest tracked file < 100 KB).
- Git commit identity comes from the global config (user.name "joy", existing global email).
- GitHub only backs up committed files. Library/, Builds/ (including the shared APK) and Logs/ are not uploaded. Keep an offline copy too.

## Next action / minimal necessary approval
- Prompt 03: deterministic note mapping (height/petal -> D E F# A B; udu articulations), replacing PingPong.
- Before trusting the device: build + emulator run to confirm the Spatial renderer draws the tray/markers and that A/B/X/Y + stick reach InputDevices in Spatial mode.
- Approved scope addition (2026-10-22): Room mapping phase after Prompt 05 (see DECISION_LOG).
- Owner approved removing the old LoopStation (to be replaced by echo orbits).

## Emulator verification + Spatial export fix (2026-10-22/23)
- First emulator run of the Prompt 02 build: front row misdrawn (red column, missing loop station/dispenser/ball). Old Oct 17 APK drew correctly on the same emulator.
- Fix attempt 1 (single moving focus marker, no near-zero scales): no change. Bisect attempt: invalid (strip step did nothing).
- Decisive test: rebuilding untouched `main` (f4f51c2) today was ALSO broken -> not caused by Little Echoes code.
- Root cause (verified by diffing GLB node lists inside the APKs): SpatialAdapter exports the EditorOnly-tagged EditorPreviewCamera, which the player build strips -> every later node off by one at runtime. Fixed via EditorPreviewOnly component + untag; test NoEditorOnlyObjectsInSpatialExportedScene.
- Result: 3/3 emulator launches draw the full scene (docs/evidence/phase2-emulator-fixed.png). EditMode 54/54, PlayMode 8/8.
- Emulator injects phantom taps/throws after launch (also with the Oct 17 APK) — not an app bug.
- Generated SpatialAdapter export files untracked + gitignored.
- Still NOT verified: orb tray drawing (below the default emulator view), controller buttons, audio by ear, headset.

## 2026-10-23 | Loop station removed, tray moved (live Editor via Unity CLI 1.0.0-beta.13 + com.unity.pipeline)
- Workflow: Unity Editor open; driven with `unity command` (recompile, eval, run_tests, build). No batchmode, no MCP.
- LoopStation scene object removed (scripts kept); orb tray + intensity button at (-0.55, 0.8, 1.1).
- EditMode 55/55 PASS in live Editor (incl. new RetiredLoopStationIsGone, reach, occlusion, no-EditorOnly).
- PlayMode via live Editor run_tests: FAILED TO START ("An unexpected error happened while running tests"); runtime code unchanged in this step, last PlayMode result 8/8 (batchmode). Investigate later or run PlayMode in batchmode with Unity closed.
- Build from live Editor (`build` command): Succeeded, 0 errors. Exported GLB: no LoopStation, OrbTray at (-0.55, 0.8, 1.1).
- Emulator: first attempt black (PICO system shell com.picoxr.worldspace ANR'd and was killed — emulator 8 GB + Unity 4 GB on host); after emulator restart, run 2 shows tray, tokens (shape+motif), intensity bars, dispenser ball, aim arc, focus marker: docs/evidence/phase2-emulator-tray.png. Run 1 screenshot itself timed out.
- Note: Boom renders paler (yellowish) than amber under PICO lighting — revisit in art phase.

## 2026-10-23 | Prompt 03 — deterministic musical grammar (live Editor via Unity CLI)
Implemented (editor tests + emulator logs; NOT listened to):
- NoteMapping: clamp(floor(h*5),0,4) / petal 0-4; mbira D4..B4, erhu D5..B5; udu = Open/Muted/Slap/Bend/Tap articulations. MusicalHitEvent.Note. Old PingPong/Random order removed.
- VoiceDesign: per-orb voices rendered at Start from the samples (Flutter bright+short, Bop plain, Boom two-pole LP 900 Hz + longer fade); udu articulations incl. pitch-bend. Normalised to <= 0.9 peak, fade-out to silence.
- Pitch petals: 5-step staircase in front of the tray (tap / stick click / editor P); placing uses the selected petal.
- All sound labelled SOUND PROTOTYPE; docs/AUDIO_ASSET_LICENSES.md lists the 15 WAVs as NOT VERIFIED.
Tests (live Editor `unity command run_tests`):
- EditMode 82/82 PASS: exact 0/1 and band boundaries, petal/height equivalence, scale steps, udu = articulations not note names, same input same note, orb/tier never change note, rendering deterministic, headroom + no end click, Flutter >= 1.2x Bop >= 1.2x Boom brightness, pitch period unchanged, readable clips in the real scene, petal button reach.
- PlayMode 12/12 PASS (ran this time in the focused live Editor): each petal plays its note with the orb's voice, same input -> same voice, udu tap from B2 sample and shorter than open, toss note follows contact height.
- Fixed during testing: Flutter not bright enough (1.12x) -> high-shelf; Boom not dark enough (1.17x) -> two-pole LP; Boom sub-octave removed (shifted heard pitch an octave); flawed zero-crossing pitch test -> autocorrelation.
- Build from live Editor: Succeeded, 0 errors (Builds/RoomOrchestra-phase3.apk).
- Emulator: 2/2 launches log "Erhu/Mbira/Udu voice bank: 15 voices"; scene draws (docs/evidence/phase3-emulator.png). No hit occurred, so note choice on device is NOT verified there.
- NOT TESTED: audio by ear, mix levels/clipping across 3 instruments, controller stick click on hardware.
- Known: petal staircase is partly below the default emulator view (bottom edge) -> revisit in Prompt 05 layout.

## 2026-10-23 | Prompt 03 follow-up — orb timbre fix after owner listening test
- Owner: Flutter and Bop sounded the same on the lamp. Cause: placeholder samples are near-pure tones; overtone filters had little to act on; tests had used a synthetic tone. Redesigned (time shape: Flutter short blip, Boom soft attack + echo tail + darker); tests now on the real WAVs. Owner listening re-test on emulator: APPROVED.

## 2026-10-23 | Prompt 04 — DSP clock + echo orbits (live Editor via Unity CLI)
Implemented:
- MusicClock (4/4, 88 BPM, AudioSettings.dspTime, times from bar/step indices, pause/resume keeps phase); EchoSequencer (nearest-16th quantize, repeat each bar via PlayScheduled with 0.3 s lookahead, first repeat never doubles the live hit, 6 per target with 7th refused, collisions -> nearest free step, remove/nudge/clear + undo, skip-not-burst after hitches).
- Orbit ring per target facing the player: 16 ticks (beats larger), fixed diamond marker at 12 o'clock, pooled orb-shaped minis travel clockwise and pass the marker as they sound; tap a mini to select it.
- Echo edit mode (grip / editor E): stick nudge/select, A remove, B undo; undo button; editor C clear, Z undo, K pause.
- Scheduled voices tracked + cancellable; scheduled voices never stolen; echoes don't flash the target (photosensitivity).
Tests (live Editor): EditMode 114/114, PlayMode 18/18 — incl. 1000-bar irregular-frame no-drift, two echoes same BPM, cap, collisions, three-target layer, pause/resume phase, remove cancels scheduled repeat, undo, mini at marker on its step. Fixed during testing: short samples rendered empty Flutter voices (crash).
Emulator (owner hands-on, 2026-10-23): rings render in PICO Spatial; 15 hits -> echoes on lamp/chair/table; repeats/bar grew 1 -> 11 at constant 88 BPM; each echo's first repeat one bar after its hit; lamp 6-cap refused hits #11-#14 ("orbit is full; played live only"); undo stepped back 11 -> 7 echoes. Owner: "this is great ... great progress".
Known / follow-ups:
- Diagnostic bar log over-counted after edits and at bar boundaries (audio unaffected) — fixed after the test (LogBar before Collect; cancelled re-queues subtracted).
- Owner did not know what the tray-side buttons do (intensity bars, petal staircase, undo). Discoverability is a real cognitive-accessibility gap; address in Prompt 05 WITHOUT text labels by default (owner dislikes text labels): icon/shape cues, audio/visual preview on focus, optional labels.
- Not testable on emulator: grip edit mode, nudge, remove, pause (controller buttons). Not verified: timing by ear vs marker crossing (owner did not report issues), headset.

## 2026-10-23 | Prompt 05 (part 1) — labeled pitch columns + clear text labels
- Owner could not tell what the tray-side controls did and did not understand the pitch staircase; asked for CLEAR LABELS (supersedes old no-labels preference).
- Staircase removed. Each target has a pitch column beside it: 5 labeled tiles low->high (D E F# A B; udu Open/Muted/Slap/Bend/Tap); one tap plays that note with the chosen orb and plants an echo. Labels on orbs, Loudness, Undo, instrument names.
- PICO Spatial did NOT render plain TextMesh (dynamic font); switched to TextMesh Pro (runtime syncs TMP_Text); imported TMP Essential Resources (Liberation Sans, OFL). TMP RectTransform lost its saved height without a UI parent -> plain holder transforms.
- Tests: EditMode 116/116, PlayMode 19/19 (tile tap plays its note on its own target).
- Emulator (owner hands-on): labels render; lamp column tapped B/A/F#/E/D -> exact notes, melody on the ring; chair F# tile; udu + mbira layered; lamp cap refusal logged. Owner: "wow, it sounds great".
- Observed once: native crash INSIDE PICO SDK shutdown (PXR_Loader Stop -> SpatialInputProvider::Stop -> HMDTracking::stop SIGSEGV) right after the emulator reset/closed the app window ~6 s after launch. App code not on the stack. Watch for recurrence on app close; report to PICO if reproducible.

## 2026-10-25 | Prompt 05 (part 2) — Pause/Play, Clear, Recenter
- Pause/Play + Clear buttons (A11Y-09), front control row right of Undo, tap only, seated reach. Pause silences at once; label AND icon say what the next tap does: two bars + "Pause" while playing, a right-pointing triangle + "Play" while paused (owner request after emulator test). Clear empties every orbit; Undo restores. The hidden icon is parked inside the slab (PICO Spatial draws inactive objects); triangle is a saved prism mesh (Assets/RoomOrchestra/Meshes/PlayTriangle.asset).
- Emulator (owner hands-on, 2026-10-25, phase5b APK): Pause/Clear work ("everything works"). Phase5c (triangle) / phase5d (recenter): built; triangle seen only in Unity render (scratchpad close-ups), not yet on PICO.
- Recenter (A11Y-07): Recenter button (ring-and-dot icon, label), left controller Menu button, editor R. Moves the whole stage root (StageAnchor on `RoomOrchestra`) so the start point is under the head and the stage faces the gaze; height unchanged (furniture stays on the floor); works when looking straight down/up. Head pose: SpatialAdapterRuntime.GetHMDPose (scene camera config RoomOrchestraFullSpace = Unconstrained, so supported). Never runs at startup.
- Tests: EditMode 123/123, PlayMode 21/21 (recenter math, menu once per press, layout/reach/label, stage + interpolated rigidbody move together).
- Emulator (owner hands-on, 2026-10-25, phase5d APK): Pause shows the play triangle + "Play" and back; Recenter brings the stage in front after turning away. Owner: "great, that works".
- NOT verified: recenter on a headset (GetHMDPose return code meaning undocumented; logged); right-controller recenter (PICO right controller has no app menu button: right-hand users use the tappable button or the system Home recenter); seated comfort on hardware.

## Resume here (next session)
1. Prompt 05 (rest): per-instrument volume/mute, reduced motion/flash setting, height calibration (seated/standing) in first run, then the controller-only A11Y-01..09 walkthrough with PASS/FAIL. 

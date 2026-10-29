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

## 2026-10-25 | Prompt 05 (part 3) — per-instrument volume/mute + pointer-ray layout fix
- Volume/mute (A11Y-09 mixer): each instrument's name tile under its pitch column is its volume button, one tap per step 100% -> 60% -> 30% -> Muted -> 100%, applied at once to ringing and scheduled voices; echoes kept while muted. Master volume = headset keys. Owner (emulator, phase5e): "yes it works".
- Bug found by owner: tapping the mbira volume tile ran Clear (log: "Echoes cleared" then "mbira volume: Vol 60%"). Cause: PICO Spatial picks hits with a ray from the hand/controller; the front row (y 0.8, z 1.0) sat between hands and the low tiles. Reproduced by new test EveryControlIsTheFirstThingAPointerRayHits (6 origins: seated/standing eyes and hands): 19 shadowed controls (pitch tiles, volume tiles, Loudness behind Undo).
- Fix: the whole front row (blob tray, Loudness, Undo, Pause, Clear, Recenter) lowered to 0.5 m (still >= 0.3 m seated minimum, 1 m away); Undo moved beside Loudness instead of in front. Test now passes. EditMode 126/126, PlayMode 22/22.
- Follow-up noted: when PICO sends the app to the background, Unity freezes the scene mid-motion (seen on emulator); pause the music and show Play on focus loss.
- Owner: part of a Boom ball stuck out of the dispenser post. Cause: post is 12 cm wide, Boom 13 cm, and parked balls kept their landing tilt. Fix: parked balls stand upright in the narrow Flutter shape (8.5 cm; the authored shape, so startup is a no-op — verified 0 of 24 scene balls change); handed-out balls get their orb's shape back. PlayMode test parks a tilted Boom.
- Auto-pause: going to the background (OnApplicationPause) pauses the music; it stays paused on return (button shows Play). PlayMode test. EditMode 126/126, PlayMode 24/24.

## 2026-10-25 | Prompt 05 (part 4) — Calm motion (A11Y-04)
- Background auto-pause (part 3): owner reports music did not pause; log shows PICO never sent the app to the background in that test (no onPause), so the path did not run. Still UNVERIFIED on device.
- Motion toggle at the end of the front row ("Motion / Full" <-> "Motion / Calm", three-dot icon). Calm: furniture never flashes; echo minis hold still at their step's place on the ring (clockwise from the top), and the top marker steps once per beat (4 per bar), so pattern and beat stay readable with no sweeping motion. Full is default (startup unchanged).
- Ball stand moved to (0.62, 0, 1.15) to make room ((0.72, 1.25) passed tests but hid part of the erhu volume tile from the emulator camera). The pointer-ray test is now strict (ANY collider in the way counts, e.g. the stand's post); a tried position (0.8, 1.45) failed it (post blocked erhu tiles from the left hand) and was rejected.
- Tests: EditMode 127/127, PlayMode 25/25 (calm: minis still, marker on beat 1/3 only on beats, no flash; back to full restores travel/flash).
- NOT done in calm mode yet: aim-arc dots and ball flight (only while the player throws), the focus marker; no per-orbit beat meter beyond the stepping marker.

## 2026-10-25 | Prompt 05 (part 5) — Height (Seated/Standing) + A11Y-01..09 walkthrough
- Height toggle at the end of the front row ("Height / Seated" <-> "Height / Standing", up-down arrow). Every tappable control (blob tray, Loudness, Undo, Pause, Clear, Recenter, Motion, Height, pitch columns + volume tiles) now lives under one `Controls` holder (ControlsRig); Standing lifts it 0.3 m, Seated puts it back. Furniture, orbits and the ball stand stay on the floor. Seated is the authored default (startup unchanged); session-only (restoring Standing at launch would move objects before PICO Spatial connects).
- 0.4 m was tried first: the raised tray shadowed the udu volume tile from the standing left hand (strict pointer-ray test). 0.3 m = the seated-to-standing hand rise, so every hand-to-control line keeps its seated clearance.
- Lift is a preset, not read from the headset (unverified whether the Spatial origin is the floor on hardware). Each Height press logs the head height for that check.
- Front row respaced 0.14 m (Pause 0.12, Clear 0.26, Recenter 0.40, Motion 0.54, Height 0.68) to keep Height inside the 35° seated view.
- Tests: EditMode 131/131 (Height labeled/reachable/starts seated, every control under the holder, Standing: all controls in the standing envelope + unshadowed from standing/tall hands + no target occluded; posture math). PlayMode 35/35 incl. Height press lifts a tile and a same-frame ray hits it there.

### A11Y walkthrough (Editor simulation on the REAL scene, `A11yWalkthroughPlayModeTests`, 2026-10-25)
One simulated controller through ControllerButtonInput (stick/A/B/grip/menu) plus pointer taps cast from a seated hand position (how PICO Spatial picks taps; each tap asserts the control is the FIRST collider on the ray). No throwing, no hand tracking. NOT headset evidence.

| Case | Result | Evidence / gap |
|---|---|---|
| A11Y-01 controller-only, one hand, seated, no throw: 2 Flutter on lamp, 1 Bop chair, 1 Boom table, edit timing, undo, clear | PASS (editor) | Left-only and right-only runs; nudge +1 step then undo restores; Clear by pointer tap; Undo restores all 4. Same loop by pointer taps only (seated and standing). |
| A11Y-02 grayscale silhouettes/badges | see emulator check below | Shapes (tall/round/wide) + motifs (sparkle/dot/ripple) + text names. |
| A11Y-03 muted: beat, voices, high/low | PASS (editor, partial) | All 3 volume tiles to Muted: echoes still shown and travelling; in Calm the marker still steps per beat; pitch read from labeled tiles. Not a claim of equal experience for Deaf players. |
| A11Y-04 reduced motion | PASS (editor) | Motion button tapped: echoes hold still, every orbit keeps its beat marker, no flashes. Gaps: aim-arc/ball flight, focus marker. |
| A11Y-05 device loss | PASS (editor) | Right controller lost mid-edit with grip held; left controller leaves edit mode and keeps placing; pointer controls still answer. |
| A11Y-06 no rapid tapping/throw | PASS (editor) | Every step is one discrete press; holding never repeats (mapper tests); slots chosen by single nudges. |
| A11Y-07 reset view from the seat | PASS (editor) | Menu with the head turned 90°: stage start point under the seat, facing the player, chair back in seated reach. Owner emulator check earlier (phase5d). |
| A11Y-08 left/right hand, scale/contrast, readable labels | PARTIAL / FAIL | Handedness PASS; label size/facing tests PASS; **no interface scale or contrast option yet — FAIL, next item.** |
| A11Y-09 pause any time, mixer | PASS (editor) | Pause tapped mid-edit: every target silent, nothing queued, stays silent, resume keeps the loop. Per-instrument volume/mute (part 3). |
| A11Y-10 physical comfort trial | NOT TESTED | Needs a person on a headset. |

- Test infra: loading the real scene in play mode made the SpatialAdapter Play-to-Device check pop a modal dialog and set `m_enableLoadingFromScene` (Assets/Settings/SpatialAdapterP2DSettings.asset). The setting was restored (editor-only, unused by APK builds); the test now unhooks that editor dialog for its own run and unloads the scene afterwards.

## 2026-10-25 | Owner direction — Calm motion removed, priorities before more accessibility UI
- Owner: Calm motion "feels unnecessary"; "too many buttons at the bottom"; UI clean-up later. Removed the Motion button, the calm branch in OrbitView (minis still/marker stepping) and the ColorFlash skip. Height stays, now at x 0.54 in the front row.
- A11Y-04 is now NOT IMPLEMENTED (no reduced-motion option); its walkthrough test was removed. CLAUDE.md lists adjustable motion as a hard gate: revisit during UI clean-up (e.g. inside one settings control rather than another front-row button).
- Owner wants core features before more accessibility UI, because headset look may differ from the emulator.
- Tests: EditMode 130/130, PlayMode 33/33.

## 2026-10-25 | Prompt 06 — Characters (Flutter, Bop, Boom) and expressions
- Tools: Blender 4.2.1 LTS headless (`--factory-startup`), no Blender MCP connected; procedural script `ArtSource/Characters/build_characters.py`, previews `preview_character.py`; editable `ArtSource/Characters/{Flutter,Bop,Boom}.blend`; FBX meshes in `Assets/RoomOrchestra/Art/Characters/` imported mesh-only (project materials). Provenance: `Assets/RoomOrchestra/Art/Characters/LICENSES.md` (no external assets).
- Prototype ONE first: Flutter built, imported (first export faced away from the player -> export axis fixed; test pins facing and left/right eyes), Unity render checked, then Bop and Boom batched. Boom first came out pancake-flat; raised to the reference's dome (~1.2x Bop wide, ~0.86x tall).
- Colours sampled from the reference image (fill/rim/face/sparkle); orb body colours in OrbCatalog changed to match (paler turquoise/lavender/amber than before).
- Where: tray tokens = full characters (~3k tris each: body, inverted-hull rim, eyes, smile, parked "oh" mouth, shine, motif sparkle/dot/ripple). Orbit echo minis (54) = light version (~600 tris: body, rim, eyes, smile), 6 cm. Throwable balls = Bop body + rim/eyes/smile recoloured per character by ApplyOrb (no runtime mesh swaps).
- Expressions (CharacterFace + CharacterDirector, rules in Core/CharacterReactions): idle blink; first character on an object -> curious (wide eyes, "oh" mouth on tray characters) and the tray character reacts too; another of the same character on the same object -> both glance at each other; 4+ echoes on an object -> small squash bounce on each new bar. Reduced motion (static flag, no switch since Calm was removed) keeps face changes, drops blink/bounce. Nothing moves until the first reaction (startup unchanged).
- Evidence: Unity close-ups (scratchpad tray_all2.png, ball_top.png, minis.png); PICO emulator phase6a: the ready ball on the stand renders with face/rim/eyes/smile (screenshot p6a.png), i.e. FBX meshes + project materials render under PICO Spatial. Parked minis verified per vertex inside hubs/lamp shade.
- Tests: EditMode 135/135 (parts, facing, mirroring, parked "oh", tri budget, hidden minis, ball faces, wiring, reaction rules); PlayMode 40/40 (face poses, bounce, reduced motion, ball recolour, real-scene curious -> sibling glance).
- NOT verified: expressions seen on PICO (needs taps), readability of 6 cm mini faces at 2 m on a headset, performance on hardware. Not done: "changed note -> puzzled nod" (bible, optional).

## 2026-10-25 | Prompt 06 (part 2) — owner feedback: "completely static", brighter turquoise
- Owner (emulator, phase6b): no visible expression changes; wants brighter turquoise and livelier characters (wobble when dragging the ball). Log showed the reactions did run (echoes planted by throws and taps): they were too small/short to notice and nothing moved between events.
- Added JellyBody (personality per character in Core/CharacterMotion: Flutter light/floaty, Bop springy, Boom slow/heavy): idle breathing + sway, damped-spring lean and jiggle from drag/flight/orbit motion, impact splat, hops. Each orbit mini hops and beams (smiling eyes) when its own note plays; tray characters hop when chosen and when their note lands; reactions bigger/longer (wide eyes 1.45x, 1.6 s). Faces blink on their own. All life starts 3 s after load (Spatial startup pose race); parked balls/minis sleep in their authored pose (stay hidden).
- Flutter colour brighter (#5CEDF2 body, rim #219EB3); character bodies get a soft self-glow (URP emission; needs emissive GI flags or URP strips the keyword) and a matte finish.
- Emulator evidence: three screenshots 0.4 s apart: ready ball region changed 744 and 1333 of 16500 sampled pixels while a static wall/bed region changed 0 (animation renders under PICO Spatial). Ball colour measured #7EA9A9 (before) -> #A0C3C4 (glow) -> #A5C6C7 (matte). The emulator mutes every colour (chair #C72E33 renders #935252), so true brightness needs a headset.
- Tests: EditMode 137/137, PlayMode 42/42 (blink + breathing on their own, dragged jelly leans then wobbles past upright and settles, visible hop, asleep = authored pose, reduced motion holds still, note-crossing maths).

## 2026-10-25 | Prompt 06 (part 3) — scared fall and poof (owner request)
- Owner loved the alive characters; asked: a ball rolling off a surface looks a little scared, then vanishes in a poof cloud when it hits the ground.
- Scared: while in flight, falling (> 0.8 m/s down) and the first thing along its path is the floor (`GroundSurface` on FloorCollider): wide eyes looking down at the drop, "oh" mouth (balls now carry Bop's parked oh-mouth), nervous shiver. Relaxes 0.3 s after a safe landing (short drops onto furniture are not scary).
- Poof: first floor contact -> a PoofCloud bursts at the contact point (big middle puff billows up, six puffs ring it, big/small in turn, swell then shrink, 0.6 s) and the ball returns to the stand at once. 3 clouds x 7 puffs authored in the scene, parked inside the ball stand's post (verified per vertex); no runtime objects. Log line "[RoomOrchestra] <orb> landed on the floor: poof".
- Before: balls rolled on the floor until their 15 s lifetime.
- Tests: EditMode 138/138 (ground marked, clouds hidden in the post, ball oh-mouth parked); PlayMode 44/44 (real physics: rolls off a table -> scared -> expires on the floor -> poof where it landed -> cloud back in the stand; dropping onto furniture is neither scary nor a poof). Unity render of the cloud mid-burst checked (scratchpad poof2.png).
- NOT verified on PICO yet (needs a throw on the emulator). No poof sound (no licensed asset; Prompt 07).

## 2026-10-25 | Prompt 06 (part 4) — owner feedback on the fall and the eyes
- Owner (emulator, phase6f): could not see the scared face when a ball rolled off the chair; Bop/Boom tray characters' eyes "bulge out" when hit; not on Flutter.
- Log: 13 floor landings, all poofed; scared ran but the fall off the chair lasts ~0.3 s, the ball tumbles (face away) and vanished on contact. Flutter: one Flutter echo planted (tray reacted while the owner watched the throw); the later Flutter hit met a full mbira ring (6/6), so no echo and no reaction (correct).
- Bug: eye meshes are built around the body centre, so scaling an eye also pushed it outward (bulge) and blinks pulled it toward the centre. Fix: eyes scale about their own mesh centre, in the face plane only (x, y; never depth).
- Fall made readable: scared starts at 0.3 m/s downward; thrown/rolling balls keep the face turned to the player (Viewer: headset pose on device, camera in Editor, seated eye otherwise); on the floor the character sits squashed and startled for 0.35 s, then poofs.
- Tests: EditMode 138/138; PlayMode 45/45 (eye grows in place, no thicker, scared eyes look down; roll-off -> scared -> startled pause on the floor -> poof).

## 2026-10-25 | Prompt 06 (part 5) — earlier shock, magical rainforest (first pass)
- Owner: faces need not always face the player (balls may roll freely); the shock should start well before the edge. Ball facing reverted. New: while rolling on furniture, the ball looks 0.15/0.3/0.5 s ahead along its roll; if a spot ahead has only the floor beneath (drop > 15 cm), it is scared already. Only while touching a surface (a ball flying at a target never panics). Test: scared while still on the table, before the edge.
- Owner: start butterfly trails, sparkles and vines to turn the room into a magical rainforest, imagery similar to MapleStory. Built ORIGINAL procedural assets in that bright, chunky, outlined style (nothing copied; `Assets/RoomOrchestra/Art/Rainforest/LICENSES.md`): `ArtSource/Rainforest/build_rainforest.py` -> Rainforest.fbx (~9k faces).
  - Vines with outlined leaves, pink flowers and curly tips, shaped to the furniture: lamp pole, ball stand post, both front table legs, draped over the chair back.
  - 3 butterflies (pink, sky, lemon; ~22 cm) in lazy figure-eight loops behind/above the furniture, wings in a flapping V with white spots, each trailing 8 glowing sparkles.
  - 15 twinkling four-point sparkles, 5 around each instrument, that flare when that instrument plays (live hit or an echo passing the top of its ring: new OrbitView.NotePassed event).
  - Decoration only: no colliders (taps/balls pass through), starts 3 s after load, authored at its start pose, reduced-motion flags freeze it.
  - Bug found in Unity render: first wing outline doubled back on itself (no wing face); rebuilt as one simple closed lobe shape; wings first lay flat (edge-on to the player) and flapped downward -> V rest angle, sign fixed.
- Tests: EditMode 139/139 (decor tap-through, triangle budget, vines present, butterflies at path start with wings up, 5 sparkles per instrument); PlayMode 48/48 (butterflies fly/flap/trail behind, sparkles flare only for their instrument, reduced motion still, early edge scare).

## 2026-10-25 | Prompt 06 (part 6) — the jungle grows with the music
- Owner: aim dots mismatched; shock still hard to see; vines and butterfly trails only when a harmonious song has been developed; vines creep up table legs, chair and lamp as more notes play; toadstools that sway to the beat; several easter eggs so the room comes alive as music is composed. Later: no need to fit vines perfectly to the virtual furniture (they will go on real furniture).
- Aim dots: the held arc kept showing the last throw after that ball poofed away; now released when that ball is gone (PlayMode test).
- Shock: logs "[RoomOrchestra] <orb>: eek! the floor is coming" when it starts (device check); slower rolls count (0.08 m/s).
- Jungle (rules in Core/JungleGrowth, driven by JungleDirector from every note heard, live or echo):
  - Vines in 8 pieces each, folded inside their furniture (verified per vertex) and creeping out with springy pops: table legs with udu, chair (now climbs a back leg and along the top of the back) with mbira, lamp pole with erhu, ball stand with every note (slower). 32 notes per vine; flowers bloom when a vine is full grown.
  - Butterflies wait inside the lamp shade; 1 flutters out when two instruments play together (4+ echoes), 2 with all three (6+), 3 with a rich melodic loop (10+ echoes, 4+ different notes); they fly home when the song is cleared. Trails as before.
  - 7 toadstools (red/pink/purple spotted caps, sleepy faces) wait as buds around the stage edge and sprout one by one (6..85 total notes), then sway and squash on every beat of the master clock.
  - Chorus easter egg: all vines grown + 3 butterflies out -> toadstools hop on every beat.
  - Growth is kept for the session (clearing a song does not uproot the forest). Each stage is logged ("Jungle: ...").
  - UPDATE (owner): Clear now sweeps the whole jungle away in big smoke clouds (4x the landing poof, over each grown vine, sprouted toadstool and butterfly out); under the smoke (0.2 s) vines fold away, toadstools become buds, butterflies vanish into the lamp, growth resets. 16 pooled clouds in the ball stand.
- Pipeline note: FBX objects with a location import with a 180° Y turn and mirrored position while vertex data is already correct; pivots are un-turned in RainforestArt.Pivot.
- Room-mapping note: VineGrowth is shape-agnostic (pieces + hiding pivots); real furniture will need vines generated around the pinned leg/pole, not these hand-placed ones.
- Tests: EditMode 140/140 (rules; everything waits hidden/as buds at startup); PlayMode 52/52 (vines creep and bloom, butterflies home -> out -> home with trail tucked, toadstools sprout and dance on the beat, each note grows its own vine, aim-dot release).

## 2026-10-26 | Jungle tuning (owner)
- Owner (emulator, phase6j): Clear-in-smoke and the jungle "all worked well"; the vine should not keep growing unless more notes are added.
- Growth now counts only notes ADDED to the song (EchoConductor.NoteAdded on each new echo), never loop repeats. Retuned for that: full vine at 6 notes on its instrument (a full ring), ball-stand vine at 16 total, toadstools at 1/3/5/7/9/12/15 notes added. Butterflies unchanged (follow the current song). Sparkles still flare on every repeat.
- Test: a real conductor loop repeating for over a bar adds nothing; the next new note grows the vine again.

## 2026-10-26 | Halo rings that face a roaming player
- Owner asked what happens when the player walks around (rings were upright clock faces turned once toward the seat: edge-on from the side, reversed from behind; tray/buttons stay at the start). Owner had first imagined a horizontal ring (raise the hand to lift the ring for volume later) and wants roaming play (throw at other objects/walls).
- Built: every echo ring is now a halo tipped 35° from flat toward the player (OrbitView.HaloTilt; 90 = old upright ring), and it turns smoothly (yaw only, 0.6 s, 4° slack so a seated player's small head moves never rewrite it) to face the player's head (Viewer: headset pose on device), so echoes always read clockwise from where the player stands with the marker on the far side. Minis and the marker stand upright facing the player.
- Tests: EditMode 141/141 (halo tilt, marker on the far side, upright minis facing the seat; existing occlusion/pointer tests still pass); PlayMode 55/55 (a camera walks round to the side: the ring turns to face it and still reads clockwise).
- Open: tray/buttons for a roaming player. Options A leaf-pad companion, B wrist bloom, C whistle call, D character runners, E leaf pads per instrument (recommended A + C). Owner is asking the PICO team (2026-10-26); do not build until decided.

## 2026-10-26 | Prompt 07 (part 1) — sounds with documented origin, mix safety
- The 15 prototype WAVs have no known creator or licence (arrived with the original project; no generator anywhere). Made a replacement set synthesized from scratch with documented provenance: `ArtSource/Audio/synthesize_instruments.py` -> `Assets/RoomOrchestra/Audio/Synth/` (udu-inspired clay "bloop", mbira-inspired plucked tine with inharmonic partials/beating/buzz, erhu-inspired bowed nasal tone with blooming vibrato). Deterministic (hashes identical on a second run; ledger in docs/AUDIO_ASSET_LICENSES.md). Measured: pitch within ±14 cents, loudness matched per instrument, no DC.
- `SoundSets` (Editor) switches the scene between sets and trims each instrument's volume by measured average-to-peak ratio (game renders voices to one peak): mbira −5.2 dB, erhu −1.9 dB, udu +0.3 dB. Comparison APKs: `Builds/RoomOrchestra-phase7-synth.apk` (new sounds) vs `Builds/RoomOrchestra-phase6l.apk` (approved sounds). The committed scene keeps the APPROVED set until the owner chooses by ear.
- Mix safety finding (MixSafetyTests, offline, real voices): the densest possible song (6 echoes per instrument on back-to-back steps + a live note, all Strong, all three instruments, no distance attenuation) peaks at +8.5 dBFS with the approved set (+7.1 with the new set): it would clip. Fix: `PeakLimiter` (Core, unit-tested: untouched below 0.9, linked stereo, never above threshold) on the final mix via `MasterLimiter` (OnAudioFilterRead on the AudioListener); logs when it engages. Test: limited peak <= 0.9.
- NOT verified: limiter on PICO (OnAudioFilterRead under Spatial; watch for "Mix limiter held..." in the log); how either set sounds (owner listening).
- Test status: EditMode 143/143. PlayMode 54/55: `PlacedHitPlantsAnEchoThatRepeatsExactlyOnTheGrid` fails ("one repeat scheduled within a bar: 0") in this session while the Editor's audio clock was observed stalling (dspTime frozen/crawling; AudioSettings.Reset restarted it briefly). The test does not touch the new code (own rig, no limiter, no scene). Then the Unity Editor closed (no crash report; log ends 13:23). Re-run the PlayMode suite after reopening the Editor before trusting it.

## 2026-10-26 | Sound set decision and A's origin
- The owner compared the two sound sets by ear on the emulator and kept **A** (the original sounds). B (`Audio/Synth/`) is kept as an unused alternative.
- **Correction to part 1:** A's generator does exist. `ArtSource/Audio/gen_world_instruments.py` was recovered from the 2026-10-15 session log, and it regenerates all 15 WAVs byte for byte. The ledger now lists A as project-made synthesis (docs/AUDIO_ASSET_LICENSES.md).

## 2026-10-26 | English / Chinese controls (owner request)
- **What changed:** every control label can show Simplified Chinese.
  - The text lives in `Core/UiText.cs`, one table.
  - `Localization` holds the current language; it is remembered on the headset only.
  - `LocalizedLabel` covers fixed labels. Pause/Play, Height and the volume tiles relabel themselves.
  - `LanguageButton` sits at the end of the front row and is labeled `中文` / `English`.
  - `LanguageDirector` applies the saved choice, or the headset's system language, 3 s after start.
  - Chinese tiles show jianpu numbers (1 2 3 5 6).
  - Editor: the L key toggles.
  - Details in DECISION_LOG.
- **Font:** a Noto Sans SC subset (OFL 1.1, 75 KB, `ArtSource/Fonts/subset_label_font.py`) as one static TMP atlas, `Fonts/LittleEchoesLabels SDF.asset`. License: `Fonts/LICENSES.md` and `OFL.txt`.
- **Tests:**
  - EditMode 151/151. New: UiTextTests and SceneLanguageTests (atlas has every character, labels authored in English, button reachable).
  - PlayMode 57/57. New: LanguagePlayModeTests (switch and back across every label kind; saved choice applied after the delay). `PlacedHitPlantsAnEchoThatRepeatsExactlyOnTheGrid` passes again: the earlier failure was the Editor's audio stall.
- **NOT verified:**
  - Chinese text rendering under PICO Spatial on a headset.
  - System-language detection on a PICO headset set to Chinese.
  - Native-speaker review of the wording.

## 2026-10-27 | Prompt 07 (part 2) — even weight levels, poof sound
- **Weights:**
  - Every voice was set to the same peak, so short Flutter blips sounded about 6 dB quieter than Bop over the first 200 ms, and Boom about 2 dB louder.
  - Weight should change timbre; strength changes loudness. Flutter now decays more slowly (0.12 to 0.2 s) and peaks a little higher. Boom on udu and mbira peaks 20% lower; erhu Boom is unchanged because a bowed note already swells in softly.
  - Measured on the real voices after the change: Flutter is 1–3.4 dB below Bop; mbira Boom is level with Bop. Lengths are unchanged.
  - New test: `WeightChangesTimbreNotLoudness` (7 notes). The densest-loop mix safety test still passes (-0.9 dBFS after the limiter).
- **Poof sound:**
  - `ArtSource/Audio/gen_poof.py` makes `Audio/Effects/Poof.wav` (a soft puff, 0.45 s) and `Smoke.wav` (Clear's smoky whoosh, 1.2 s, swells in). Both are deterministic and listed in the ledger.
  - Each of the 16 poof clouds has a 2D AudioSource at volume 0.45 that goes through the master limiter.
  - Clouds that burst within 0.15 s of each other share one sound, so Clear plays one whoosh instead of a stack.
  - Three small pitch steps take turns, so repeated poofs don't sound identical.
- **Tests:** EditMode 159/159 (new: poof sound wiring and clip peak/onset, weight loudness). PlayMode 58/58 (new: `PoofSoundPlayModeTests`).
- **Build:** `Builds/RoomOrchestra-v2-poof.apk` (66 MB, versionName 0.2.0 / versionCode 2), installed and launched on the PICO Emulator 6.1. Voice banks loaded, no app exceptions.
- **NOT verified:**
  - How either change sounds; this needs the owner's listening.
  - Poof sound on the emulator or a headset (needs a throw onto the floor).
  - The limiter on PICO.

## 2026-10-27 | Jianpu key "1=D" (owner request)
- In Chinese, a "1=D" label now sits above the mbira and erhu note columns, the way printed jianpu shows its key. English shows nothing there, because the English tiles already name the notes. The udu column has no key, since its tiles are drum sounds.
- `UiTerm.KeyOfD` and `UiText.ChineseOnly`; the label is sized from its Chinese text to the tile numbers' height (0.045 m).
- Unity render check: the first version was oversized and faint because it was measured from blank text; fixed and re-rendered (scratchpad keylabel4.png).
- Tests: EditMode 159/159 (1=D only above mbira and erhu; blank allowed only for Chinese-only labels). PlayMode 58/58.
- Build: `Builds/RoomOrchestra-v2-jianpu.apk`, installed on the emulator (opens in English, the owner's saved choice).
- NOT verified: the label in Chinese on the emulator or a headset; native-speaker review; readability of white text without a backing against a light real wall.

## 2026-10-27 | Dark plate behind 1=D; Version 2 tagged
- Owner: add a small dark backing behind 1=D. `ChineseOnlyBacking` moves a dark plate (9 x 6 cm, no collider) behind the label in Chinese and parks it inside the top tile in English. Unity renders checked in both languages (scratchpad backing-zh.png / backing-en.png).
- Tests: EditMode 159/159 (plate parked wholly inside the tile, no collider, two of them); PlayMode 58/58 (plate follows the language both ways).
- `Builds/RoomOrchestra-v2.apk` (0.2.0 / code 2, sha256 7fd98b4f…) installed and launched on the emulator; voice banks loaded.
- Owner approved: push the branch and tag it `v2`.

## 2026-10-27 | Roaming controls: leaf pad (A) + call (C) — after the v2 tag
- **Owner chose A + C.**
- **Leaf pad:** a new `LeafPad` holder under Controls carries the ball tray, Loudness, the front-row buttons and a new call leaf. `RoamingTray` also carries the ball stand (BallDispenser) and the poof park.
  - Thrown balls stay where they are: the BallPool is held still in the room while its parent moves.
  - The ready ball and the spare balls re-seat on the stand (`BallLauncher.FollowStand`). Waiting poof clouds follow their park.
  - Pitch columns stay beside their instruments. Height still lifts everything.
- **Rules** (`Core/TrayFollow`, pure and unit-tested):
  - Follow starts once the head is more than 0.9 m from the seat. The pad origin goes under the head, turned the way the player faces, so the controls sit exactly as they do at the seat.
  - Dead zone: it only re-settles after 0.5 m of movement or a 45° turn.
  - Back within 0.5 m of the seat for 1.5 s: it returns home. Recenter sends it home at once (new `StageAnchor.Recentered` event).
  - Glides with SmoothDamp: 0.6 s, at most 1.5 m/s. A call is quicker (0.25 s).
  - Nothing moves before the 3 s start delay.
- **Call (C):** a green leaf labeled "Come here" / 过来, left of the tray (`TrayCallButton`). Tapping it from anywhere brings the pad to the player, or home if they're at the seat. Editor key: G.
  - Every controller button is already used, so there is no controller call yet.
  - The font subset was regenerated: 136 characters, ledger updated.
- **Tests:**
  - EditMode 167/167. New: `TrayFollowTests` (6); the pad carries the right things and not the note columns; the call leaf is labeled; every pointer-line reach test still passes.
  - PlayMode 61/61. New: `RoamingTrayPlayModeTests` (the stand travels and stays on the floor, a thrown ball stays, a glide not a jump, the call is quicker, Recenter goes home).
- **Editor Play mode on the real scene** (a test head camera):
  - Walking to (1.8, 0.6) facing +X: the pad followed, the stand, ready ball and waiting cloud went with it, and the pool stayed.
  - Back at the seat: home after the dwell.
  - Turned round 0.7 m away, then a tap on the call leaf: it came, facing the player. Render in scratchpad `called.png`.
- **Build:** `Builds/RoomOrchestra-v2-roaming.apk`, installed and launched on the emulator. No app errors, and nothing moved at startup.
- **NOT verified:**
  - Following on the emulator or a headset (needs the owner to walk or move the emulator view; watch for "Leaf pad:" in the log).
  - Comfort of the follow speed.
  - Collisions with real walls and furniture (there is no room mapping yet; the pad can go into a wall).
  - Throwing while the pad follows.

## 2026-10-27 | Leaf palette: the controls condensed onto one leaf (after the v2 tag)
- **Owner:** following works on the emulator; condense the controls so they look like they're on a leaf palette (sketch: green oval leaf, the three balls along the top, a midrib, small grey buttons along the bottom). The controller call button is deferred until the owner has a headset and controller to test with.
- **Layout** (`LittleEchoesSceneSetup`):
  - One flat green leaf (`Leaf`, 0.95 × 0.42 m, centred at (-0.2, 0.49, 1.17)) with a pale midrib and a green stalk. Decoration only: no colliders.
  - Back row: Flutter, Bop, Boom resting on the leaf, then Loudness (shrunk to 0.11 m).
  - Front row: seven small buttons (0.09 m slabs, 0.105 m apart): Come here (a small green leaf icon), Undo, Pause, Clear, Recenter, Height, 中文/English. The language label now sits on its slab. Labels are 1.7 cm tall.
  - Everything was 1.7 m wide; it is now 0.95 m. The ball stand is unchanged and still travels with the pad.
- **Layout fixes found by the tests:**
  - Hand pointer rays to the udu volume tile hit Bop and Boom, so the characters now rest on the leaf (0.03 m lower).
  - The leaf's bounding box clipped the standing sight line to the chair, so the back edge was trimmed (depth 0.48 → 0.42 m).
- **Tests:**
  - EditMode 167/167.
  - PlayMode: 60/61 in the full run. `RollingOffATableLooksScaredThenPoofsOnTheFloor` failed, then passed 2 of 3 times on its own. It builds its own table scene, unrelated to this layout; it is an intermittent timing test, worth hardening later.
- **Renders:** scratchpad `leaf-close.png` (English) and `leaf-zh.png` (Chinese).
- **NOT verified:** label readability and tap accuracy on the small buttons on the emulator or a headset.

## 2026-10-28 | Cloud ball stand: the ready ball floats on a cloud, not a post
- **Owner request:** instead of a tower holding the ball to launch, it floats on top of a cloud.
- **Built** (`LittleEchoesSceneSetup.CreateCloudStand`):
  - The scene's `Pedestal` becomes the cloud's core: an egg 0.12 × 0.16 m at 0.915 m with a short upright capsule collider (as narrow as the old post, so hand pointer rays to the erhu notes still pass beside it). Eight flat-bottomed puffs around it, without colliders, give the cloud shape. New `StandCloud` material, soft white with a gentle glow.
  - The ready ball sits on top (spawn point unchanged at 1.06 m). The spare balls and the waiting poof clouds hide inside the core; spares are now authored there too (they were at the old post height, 0.5 m, and would have hung in mid-air because PICO Spatial keeps startup poses).
  - The cloud still travels with the leaf pad and still stops throws, as the post did.
  - **Retired:** the rainforest vine that climbed the post (no post to climb). The table, chair and lamp vines are unchanged.
- **Tests:** EditMode 168/168 (new `TheReadyBallFloatsOnACloudNotAPost`: no post, puffs never block, the ball sits just on top, nothing reaches the floor, spares wait inside the cloud). PlayMode 61/61. The first pass found the wider core blocking hand rays to three erhu tiles and the erhu volume tile; fixed by narrowing the solid core.
- **Emulator:** `Builds/RoomOrchestra-v2-quietpoof.apk` installed and launched; the ready ball shows on the cloud at startup, no app errors. Screenshot in scratchpad `cloud-emu.png`.
- **NOT verified:** on a headset; whether the lamp pole behind the cloud (from the seat) reads as a stick holding it up.

## 2026-10-28 | Landing poofs are silent; only Clear's smoke makes a sound
- **Owner request:** no sound when a ball drops and poofs; a sound only when Clear is pressed.
- **Changed:** `PoofCloud.PlaySound` plays only for clouds at least `SmokeSize` (Clear's smoke); a landing poof still shows its cloud but is silent. The landing clip was unwired (`poofSound` field removed); `Audio/Effects/Poof.wav` is kept in the project, unused and not in the build, in case it's wanted back.
- **Tests:** EditMode 168/168 (`ClearsSmokeHasAGentleSoundAndLandingsAreSilent`), PlayMode 61/61 (`ALandingIsSilentAndClearsSmokeIsOneWhoosh`).
- **Emulator:** `Builds/RoomOrchestra-v2-quietpoof.apk` installed and launched, no app errors. Not listened to.

## 2026-10-28 | Growth waits for 3 notes; flowers round the room; polish for the Version 2 submission
- **Owner:** the first toadstool and the vines shouldn't grow until at least 3 notes; more random flowers around the room; wall-bounce sounds are for V3; polish this build and list anything missing before submitting it as V2 and pushing for dev-team feedback.
- **Growth** (`Core/JungleGrowth`): `NotesBeforeGrowth = 3`. Vines show nothing before the 3rd note, then catch up (every vine is updated on each note, so ones fed earlier grow together). Toadstools now sprout at notes 3, 4, 6, 8, 10, 13, 16 (was 1, 3, 5, …). The retired ball-stand vine's rule was removed.
- **Flowers:**
  - New Blender part `Flower_*` in `ArtSource/Rainforest/build_rainforest.py` (stem, two leaves, five flat outlined petals, heart; 352 triangles). The retired stand vine is no longer exported.
  - `RainforestArt.BuildFlowers`: 20 flowers scattered with a fixed seed (2026) over x -2.1..2.1, z 0.6..3.2, kept off the player's spot, the furniture, the leaf palette, the ball stand, the toadstools and each other; five petal colours; 24-36 cm grown; buds at startup like the toadstools.
  - One more flower sprouts with each note from the 3rd; they sway to the beat and hop in the chorus; Clear smokes them away (after the bigger growth gets its clouds).
  - The toadstool's grow/sway behaviour moved into a shared base, `FloorSprout`, used by `Toadstool` and the new `FloorFlower` (Toadstool keeps its script GUID).
  - Rainforest total 48,396 triangles (budget test: under 50,000). The first flower design was 1,196 triangles each (65k total) and was redrawn with flat petals.
- **Polish:** Undo arrow sized to its small slab; stale "post" comments updated (Ball, BallLauncher, AimArc, PoofCloud, JellyBody, scene setup); a misplaced doc comment in Ball fixed; the flaky `RollingOffATableLooksScaredThenPoofsOnTheFloor` hardened (the ball starts further from the edge, ~0.35 s window instead of ~0.17 s; game unchanged).
- **Docs:** `RELEASE_NOTES_v2.md` rewritten for everything in this submission, with build steps and a Version 3 list (wall-bounce sounds first). `SUBMISSION_TEXT.md` status section updated to facts.
- **Tests:** EditMode 168/168, PlayMode 61/61; the hardened test passed 4 of 4 repeat runs on its own (a fifth was stopped unfinished when the Editor stalled out of focus).
- **Renders:** scratchpad `meadow-grown.png`, `meadow-buds.png`, `flower-close.png`.
- **Build:** `Builds/RoomOrchestra-v2-final.apk` (sha256 09b1a9b9…2661), installed and launched on the emulator; buds only at startup, no app errors (scratchpad `final-emu.png`).
- **Incident:** the Unity Editor quit during a fifth repeat test run (its package-manager helper hit a broken pipe during the Play-mode reload). Reopened with `unity open`; the saved scene was intact. The test runner had left Player Settings → Run In Background on; set back to off. Unity's `Assets/_Recovery/` crash backup and a second `InitTestScene…` were left untracked, not committed.

## 2026-10-28 | Version 2.1: tag, release notes, APK on GitHub
- Release notes renamed and rewritten as `docs/RELEASE_NOTES_v2.1.md`: everything since Version 1 (checked against the 39 commits since `v1-pico-feedback`), build/install steps, known limits, Version 3 plans.
- Submission overview: reduced-motion and control-sensitivity claims removed (owner); "slide an echo" reworded to what exists (a step at a time).
- App version 0.2.1 (code 3). `Builds/RoomOrchestra-v2.1.apk` (sha256 ca009ad94ca8d884bfc54c8dc6d3a128e21bee9daa5b14040795f826f3380c96) installed on the emulator: versionName 0.2.1, versionCode 3, launches with no app errors.
- Deleted the crash leftovers (`Assets/_Recovery`, two `InitTestScene…` files); not in the build.
- Tag `v2.1` and a GitHub release with the APK attached (owner approved).

## 2026-10-29 | App icon and name (Version 1 feedback: "missing a custom icon")
- **Owner:** the evaluator's Version 1 feedback (`build_feedback (1).md`) asked for an official icon. The owner chose a combination of four rendered options: the trio on the leaf (options 1 and 3), option 4's coral, option 2's echo ring with one echo per character (Bop purple, Flutter turquoise, Boom orange), a few white notes, and the title in the hand lettering from the design-document cover, on two lines with the "L" and "E" apart and the swoosh over the "s" removed.
- **Art** (`ArtSource/Icon/`): `build_icons.py` (Blender, the approved character meshes, flat-shaded; colours converted so they match the reference), `extract_title.py` (lifts the cream lettering out of the owner's key art "Little Echoes_ A Mixed-Reality Musical Playground.png", recolours its shading without the room's purple), `add_title.py` (rounded plum outline). Outputs: `AppIcon_1024.png` (flat), and three launcher layers `AppIcon_background.png`, `AppIcon_layer_art.png`, `AppIcon_title.png` (no baked shadow, per PICO's layered-icon spec).
- **Finding:** the PICO OS 6 home screen ignores the flat Android icon for this app and showed the Unity logo; it wants a layered spatial icon. PICO's Icon Configurator says it doesn't support Spatial mode, and its Apply rewrites both Android manifests, which SpatialAdapter's "Setup Project" check (an error-level build rule) requires to stay stock.
- **Solution** (`Editor/AppIconSetup.cs`, menu RoomOrchestra > Little Echoes > Apply App Icon): runs only the Configurator's resource export into `Assets/Plugins/Android/IconConfigurator.androidlib` (layers, SDF maps, `drawables_3d.xml`) and declares `icon.3d.list` / `icon.sdf.list` in that library's own manifest, which Gradle merges into the app. The SpatialAdapter manifests are untouched. Also sets the flat icon (Legacy and Round) and the product name **Little Echoes** (was "Room Orchestra"; package id unchanged).
- **Evidence:** the APK's merged manifest has both entries and label "Little Echoes"; on the emulator's home screen the app shows the new icon and name (`docs/evidence/launcher-icon-2026-10-29.png`); the app still launches with no errors. EditMode 168/168.
- **NOT verified:** the hover lift of the layers on the home screen (the emulator screenshot is static); a headset.

## Resume here (next session)
1. Dev team evaluating `v2.1` (`Builds/RoomOrchestra-v2.1.apk`, GitHub release `v2.1`) on a headset; then act on their feedback. Owner checks of the same build: growth waits for 3 notes, flowers round the room, the leaf palette and cloud stand (are the small labels readable, are the small buttons easy to tap, does the cloud read as floating), the 1=D plate in Chinese, the weight levels and Clear's smoke sound (landings are now silent). Following was confirmed by the owner on the emulator. Released as `v2.1` (app 0.2.1); `v2` is the earlier cut. Revisit moving an echo by hand after the headset test. Open: limiter on device, keeping the pad clear of walls once room mapping exists. Ask the owner about a controller call button only once they have a headset and controller to test with. Then Prompt 08. Deferred: UI clean-up, A11Y-04 motion and A11Y-08 scale/contrast options.

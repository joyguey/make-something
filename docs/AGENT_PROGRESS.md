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

## Resume here (next session)
1. Remove old LoopStation (owner approved) and move the orb tray + intensity button to its spot (front-left, ~0.8 m); rerun occlusion/reach tests; rebuild; confirm tray draws on emulator.
2. Prompt 03: deterministic note mapping.

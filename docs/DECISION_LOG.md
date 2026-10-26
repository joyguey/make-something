# DESIGN AND ENGINEERING DECISION LOG
Use entries of form:
## YYYY-MM-DD | phase | decision
- Alternatives and evidence:
- Decision + reason:
- Accessibility impact:
- Costs/privacy/licensing impact:
- Owner approval required? Y/N, if Y provide status:
- What is reversible:

## 2026-10-22 | Phase 1 | Initialize Git locally and back up to the existing private repo
- Alternatives and evidence: `git status` reported "not a git repository". Remote target is the owner-specified joyguey/make-something. `gh repo create` was rejected because the repo already exists.
- Decision + reason: `git init -b main`; keep the existing project .gitignore (matches GitHub's Unity template plus secret patterns); add `origin` only because none exists; push with `git push -u origin main` (no force).
- Accessibility impact: none.
- Costs/privacy/licensing impact: uploads source and the 15 unverified-provenance placeholder WAVs to a PRIVATE repo. No secrets found in scans. Library/Builds/Logs/UserSettings are excluded.
- Owner approval required? Y — first push. Status: APPROVED 2026-10-22; pushed f4f51c2 and verified.
- What is reversible: local `.git` can be removed; the remote push can be deleted by the owner in GitHub. No force-push is used.

## 2026-10-22 | Phase 1 | Include root agent/tool config files
- Alternatives and evidence: .mcp.json, .pico-env.json and .pico-cli/config.json contain no secrets (keys inspected with values hidden). They do contain local paths and tool setup.
- Decision + reason: track them so the agent/tool setup is reproducible. Backups/ (7 old .cs files) and CLAUDE-original.md are also tracked as history.
- Owner approval required? Covered by first-push review.
- What is reversible: `git rm --cached` in a later commit.

## 2026-10-22 | Prompt 02 | Assemblies: pure Core + Runtime asmdef around existing scripts
- Alternatives and evidence: tests cannot reference Assembly-CSharp. Options: leave scripts in Assembly-CSharp (no PlayMode tests of real components) or add an asmdef. The SpatialAdapter runtime DLL is auto-referenced (isExplicitlyReferenced: 0), so an asmdef needs no special reference.
- Decision + reason: `RoomOrchestra.Core` (noEngineReferences) for testable logic; `RoomOrchestra.Runtime` asmdef on the existing Scripts folder. Script GUIDs unchanged; Android player compile verified.
- Accessibility impact: enables automated parity/reach tests.
- Owner approval required? N. Reversible: delete the asmdef files.

## 2026-10-22 | Prompt 02 | One throw = one event, including ricochets
- Before: one note per instrument per throw, so a ricochet onto a second instrument played twice.
- Decision: strict "one intended action -> one event" per CLAUDE.md priority 2; later contacts in the same throw are ignored.
- Reversible: MusicalHitFactory / Ball.OnCollisionEnter.

## 2026-10-22 | Prompt 02 | Orb weight = mass, bounce, speed clamp, proportions; same gravity and no drag
- Reason: aim assist computes ballistic arcs; drag or per-orb gravity would make heavier/lighter orbs miss, making them unequally playable. Weight is shown by shape and bounce and (Prompt 03) by sound.
- Orb data is a static C# catalog, not ScriptableObjects yet: deterministic and unit-testable. Can be moved to assets later if designers need it.

## 2026-10-22 | Prompt 02 | Controller-only path uses A/X, B/Y and stick; trigger stays with Spatial Input
- Evidence: PICO 6.1 docs: Spatial mode reports controller ray pinch as Spatial Input touches and exposes buttons via UnityEngine.XR.InputDevices.
- Decision: stick L/R = orb, U/D = target, A/X = place, B/Y = intensity; one command per press, no hold, either hand. UNVERIFIED on device.

## 2026-10-22 | Prompt 02 | Orb tray at 0.55 m in front of the chair
- Evidence: the first placement at 0.8 m blocked the seated sight line to the chair seat (automated test failed). 0.55 m passes seated (1.15 m) and standing (1.6 m) occlusion tests and stays at least 1 m from the start position (earlier Spatial renderer issue).
- Placement petal defaults to the middle petal (2) until petal UI exists (Prompt 03/05).

## 2026-10-22 | Scope | Room mapping added as a phase after Prompt 05 (owner approved)
- Owner request: instruments should map onto the player's real furniture; owner will test on a PICO headset.
- Evidence (PICO SDK 6.1 docs): PICO Spatial mode supports AR Foundation Session, Anchors and Meshing on Project Swan / PICO OS 6 in Full Space. Scene capture with Table/Chair/Sofa semantic labels is documented for PICO XR / OpenXR modes only; UNVERIFIED in Spatial mode.
- Decision: new phase "Room mapping" after the accessibility gate (Prompt 05): player-guided pinning (point at real table/chair/lamp -> anchor + mesh snap, persisted locally); optional label-based suggestions only if verified on device; virtual furniture stays as the fallback (seated play, rooms without furniture, emulator). Far real furniture must keep a within-reach option (no room-scale walking required).
- Privacy: room mesh/anchors stay on device; never uploaded or committed (CLAUDE.md: no sensitive room-tracking data).
- Requires headset testing; emulator only simulates scanning. Owner approval: YES (2026-10-22).

## 2026-10-23 | Prompt 03 | Deterministic note grammar and orb timbre
- Note: noteIndex = clamp(floor(h*5),0,4) or petal 0-4. Mbira D4 E4 F#4 A4 B4, erhu D5 E5 F#5 A5 B5 (game arrangement, not a claim about traditional tuning). Udu index = articulation (Open, Muted, Slap, Bend, Tap), never note names. Old PingPong/Random note order removed.
- Timbre per orb rendered offline in C# from the existing samples at Start (no runtime GameObjects, nothing visible changes): Flutter brighter/shorter, Bop plain, Boom darker/longer. Measured by tests: Flutter >= 1.2x Bop and Bop >= 1.2x Boom brightness; pitch (autocorrelation period) unchanged.
- Rejected: AudioLowPassFilter components (one filter per GameObject; per-voice child objects would mean runtime GameObjects, risky under PICO Spatial). Rejected: Boom sub-octave layer (shifted the heard pitch an octave down).
- Pitch petals: 5-step staircase in front of the tray (tap / controller stick click / editor P). Tossed notes come from contact height on the target's colliders; with aim assist, throws mostly land on top faces, so petals are the main pitch control for now.
- All sounds are labeled SOUND PROTOTYPE (docs/AUDIO_ASSET_LICENSES.md). Owner approval: not required (no new assets, no licensing decision).

## 2026-10-23 | Prompt 03 | Orb timbre redesigned after owner listening test
- Owner on emulator: two orbs "seemed to produce the same sound on the lamp". Measured on the real WAVs: the placeholder samples are near-pure tones (udu ~0% overtones, mbira ~13%, erhu B5 ~7%), so overtone filters had little to act on; earlier tests used a synthetic overtone-rich tone and passed anyway. Also my slope-based "brightness" metric mis-scored saturation; replaced by a Goertzel harmonic-energy measure.
- Decision: orb identity rests on time shape, which works on any sample — Flutter = short light blip (0.5 s, fast decay, saturation adds overtones; bowed erhu starts from its swell), Bop = plain note, Boom = darker (two-pole LP at 1.5x f0), soft 40 ms attack and a fading echo tail (+0.45 s). Filters placed relative to each note's pitch.
- OWNER LISTENING TEST 2026-10-23 (emulator, all 3 orbs x 3 objects, hits #1-#11): approved ("Perfect yes, let's go with this"). Further sound refinement deferred to Prompt 07.
- Evidence: EditMode RealSampleTimbreTests on 7 real WAVs (udu, mbira, erhu): Flutter < 0.6x Bop length, Boom >= Bop + 0.4 s, Boom overtones < 0.6x Bop where measurable. Not claimed: Flutter always brighter (on erhu D5 it is not). Final judgement is by ear (owner).

## 2026-10-25 | Prompt 05 | Pause icon swap and Recenter
- Pause button shows bars while playing and a play triangle while paused (owner request). Swap by moving icons (hidden one parked inside the slab), not SetActive or runtime objects — both are unreliable under PICO Spatial.
- Recenter moves the stage, not the player: stage origin under the head, yaw from gaze (head-up vector when looking straight down/up), height untouched so furniture stays on the floor. Height calibration is a separate later step. Only on request (tap, left Menu, editor R); never at startup (PICO Spatial startup pose race).
- Controller: Menu = Recenter. PICO's right controller has no app menu button, so the mapper is hand-agnostic but on hardware only the left Menu triggers it; right-hand users have the tappable button and system Home recenter. Not verified on hardware.

## 2026-10-25 | Prompt 05 | Height setting (Seated/Standing)
- Height lifts the CONTROLS, not the stage: furniture must stay on the real floor in passthrough, and only the controls are what the player reaches for. One holder (`Controls`) moves as a block so the pointer-ray clearances proven for seated play carry over.
- Preset lift (0.3 m = seated-to-standing hand rise), not a head-height measurement: the headset's Spatial origin height is unverified, and a wrong reading would put controls out of reach. Head height is logged on each press so a headset run can justify a measured calibration later.
- Session-only, Seated default: restoring Standing at launch would move objects before PICO Spatial connects (known startup pose race). Revisit with a delayed restore after a headset test.
- Test-only: the walkthrough loads the real scene in play mode; the SpatialAdapter Play-to-Device validator then opens a modal dialog that changes a project setting. The test unhooks that editor-only callback instead of changing the setting.

## 2026-10-25 | Owner | Calm motion removed; core features before more accessibility UI
- Owner decision: remove Calm motion (felt unnecessary) and stop adding front-row buttons; UI clean-up later. Accessibility options still required by CLAUDE.md (motion, scale/contrast) are deferred to that clean-up, not dropped, and recorded as NOT IMPLEMENTED until then.

## 2026-10-25 | Prompt 06 | Character pipeline choices
- Blender CLI (installed 4.2.1) instead of MCP (not connected); procedural script so shapes are reproducible and editable; .blend sources kept outside Assets so Unity never needs Blender to import.
- Separate meshes per face part + project materials (no blend shapes, no texture swaps): expressions move parts; the spare "oh" mouth parks inside the body, the pattern already proven under PICO Spatial.
- Balls share one face recoloured per character rather than swapping meshes at runtime (unverified under PICO Spatial). Orbit minis use a lighter mesh set and keep their face on a child so the orbit's selection scale and the bounce never fight.
- Outline is an inverted hull (geometry only, standard Lit material), not a custom shader, so it survives PICO Spatial's material conversion — confirmed on the emulator for the ready ball.


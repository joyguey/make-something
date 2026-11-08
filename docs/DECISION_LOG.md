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


## 2026-10-26 — Owner keeps sound set A
After listening on the emulator (phase6l = A vs phase7-synth = B), the owner prefers A, the prototype samples. A stays active. The synthesized set B stays in `Audio/Synth/` as a documented fallback. Same day, A's origin was resolved: `ArtSource/Audio/gen_world_instruments.py` was recovered from the 2026-10-15 session, and it regenerates all 15 active WAVs byte for byte. A is project-made synthesis, not a recording.

## 2026-10-26 — English / Chinese controls
- **Owner request:** both English and Chinese, as an easy toggle, built now.
- **How the language is chosen:**
  - The headset's system language decides on first launch (any Chinese → Chinese, everything else → English).
  - A button at the end of the front row (`中文` / `English`) switches. It is labeled with the language a tap switches to, in that language, so a player who reads only one of them can find it.
  - The player's choice is remembered on that headset (PlayerPrefs, local only).
- **Startup rule:** the scene stays authored in English, and the switch happens after `JellyBody.StartDelaySeconds` (3 s). This keeps the "nothing changes at startup" rule for PICO Spatial. On a Chinese headset, labels show English for about 3 s, then switch.
- **Chinese:**
  - Simplified Chinese. Traditional can be added later as a third column in `Core/UiText.cs`.
  - Mbira/erhu tiles show jianpu numbers (1 2 3 5 6, 1 = D) instead of letter names, because that is how erhu music is usually written in China.
  - Character names: 飘飘 (Flutter), 波波 (Bop), 嘭嘭 (Boom).
  - All the Chinese wording is the agent's draft and needs a native-speaker review before release.
- **Font:**
  - Noto Sans SC (SIL OFL 1.1), cut down to the 134 characters used (75 KB), as one static TextMesh Pro atlas for both languages. Every label uses it, so switching changes only text, never fonts.
  - English labels now render in Noto's Latin letters instead of Liberation Sans.
  - A new Chinese term needs `ArtSource/Fonts/subset_label_font.py` re-run. An EditMode test fails if any character is missing.

## 2026-10-27 — Roaming controls: A (leaf pad follows) + C (call leaf)
- **Decision (owner):** build the A + C mix. The PICO team's advice was not waited for.
- **What follows:** the tray, its buttons and the ball stand travel together. The pad keeps the seated layout relative to the player, so nothing new has to be learned. The note columns stay at the instruments; they are tied to the furniture.
- **Lazy follow:** it starts at 0.9 m from the seat, with a 0.5 m / 45° dead zone, a smooth glide (at most 1.5 m/s) and a 1.5 s dwell before going home. These are comfort guesses and need headset tuning.
- **Call:** a tappable leaf, because tapping is the input that works under PICO Spatial and every controller button already has a job. Adding a controller call is an open question.
- **Known gap:** without room mapping, the pad can't avoid walls or furniture.

## 2026-10-27 — Leaf palette
- **Owner request (with a sketch):** condense the roaming controls so they look like they sit on a leaf palette.
- **Decision:** one green leaf carries everything. The characters and Loudness go along the back; seven small buttons go along the front, Come here first.
  - No control was removed: pause, undo, clear, recenter, height and language stay one tap away (accessibility gate).
  - The leaf stays left of the chair sight line where it is tall, and at least 1 m out (the PICO Spatial near-draw limit).
- **The purple double arc in the sketch** was read as decoration and is not built. It can be added if it was meant to be a control.
- **Controller call button:** deferred by the owner until a headset and controller are available to test with.

## 2026-10-28 — Cloud ball stand
- **Owner request:** the ball to launch floats on a cloud instead of a post.
- **Decision:** the post's solid stays only as a small hidden core inside the cloud (it hides spare balls and poof clouds and stops throws). The visible cloud is collider-free puffs.
- **Trade-off:** the rainforest vine that climbed the post is retired. A replacement on the cloud (for example a rainbow or flowers as the song grows) is possible if the owner wants one.

## 2026-10-28 — Growth threshold and floor flowers
- **Owner:** nothing grows until the song has at least 3 notes; more random flowers around the room.
- **Decision:** at the 3rd note everything wakes together (vines catch up with notes already added, the first toadstool and the first flower appear), then one more flower per new note, 20 in all. A fixed seed keeps the scatter the same on every build, so tests and screenshots are repeatable.
- **Budget:** flowers are drawn flat (352 triangles each) to keep the rainforest under 50,000 triangles for a standalone headset.
- **Version 3 (owner):** balls make their own sound when they hit and bounce off walls.

## 2026-10-28 — Version 2.1 for dev-team evaluation
- **Owner:** tag this work `v2.1` (option B: the published `v2` tag stays where it is), upload the APK to GitHub, and write release notes covering everything since Version 1.
- **App version** 0.2.1, version code 3, so the APK matches the tag and installs over Version 1 or 2.
- **APK on GitHub:** attached to the `v2.1` release in the private repo (owner approved the upload). Builds stay out of Git itself.
- **Submission text (owner):** the reduced-motion setting and adjustable control sensitivity are removed from the overview (neither is built; the Calm motion toggle was already removed on 2026-10-25 as too many buttons). Note: CLAUDE.md lists adjustable motion as an accessibility requirement; the owner has chosen not to offer it in this version.
- **Moving an echo by hand** around its ring: to revisit after the dev team's headset test. Today an echo moves a step at a time in the controller's edit mode.
- **Leftovers deleted:** two test-runner scenes and Unity's crash backup (`Assets/_Recovery`), none used by the build.

## 2026-10-29 — App icon, name and layered launcher icon
- **Icon:** the owner's combination (see AGENT_PROGRESS), with the cover's hand lettering lifted from the owner's own key art (no font file exists for it). If that cover lettering came from an AI image tool, its terms should allow use in a published app icon (flagged to the owner).
- **Name:** the app is now called "Little Echoes" on the device (was "Room Orchestra"). The package id `com.roomorchestra.app` is unchanged, so updates still install over earlier versions.
- **Layered icon without the Configurator's manifest edits:** to keep SpatialAdapter's required stock manifests, the launcher entries live in the generated Android library's manifest instead. If PICO adds Spatial-mode support to the Icon Configurator, switch to it.

## 2026-10-29 — Plan for the Version 1 feedback (owner's choices)
- **This round:** hover feedback on every tappable thing, a title screen with a brief intro, a focused start (ball stand in front) with a step-by-step tutorial that starts with one piece of furniture and a guided tutorial song, and scale tweaks for seated desk play.
- **Tutorial song (owner: a well-known melody):** "Mary Had a Little Lamb" (public domain). It uses only scale degrees 1, 2, 3 and 5 (D, E, F#, A), all in the instruments' five notes; "Twinkle Twinkle" needs the 4th (G), which the D pentatonic set does not have.
- **Next version:** start in a fully virtual room that tears down to reveal the real room, once the tutorial works in mixed reality.
- **Version 3:** instrument packs and assigning any instrument to any furniture (with wall-bounce sounds).

## 2026-10-29 — Furniture scale (Version 1 feedback; developer: "the virtual furniture looked big")
- **Feedback text:** "When playing seated at a desk, the scale of the environment and objects felt uncomfortably large." The developer added (via the owner) that everything should scale like PICO's virtual room environments.
- **Checked:** the spatial camera config `RoomOrchestraFullSpace` is Unconstrained, which PICO documents as one Unity metre = one metre (no system scaling; `worldscaletype=2` is fixed scale). The furniture was close to real size: table 1.20 x 0.83 m tall, chair 1.08 m tall with a 0.46 m seat, floor lamp 1.54 m. An emulator screenshot cross-check (our 0.55 m chair at 2.0 m against the emulator bedroom's bed) puts the bed at about 2 m wide and 2.2 m away, so the emulator room and our furniture share a real-world scale. PICO's documentation gives no dimensions for its virtual rooms.
- **Decision (owner: true real-world sizes):** standard sizes, a 0.75 m table top (was 0.83) and a chair back topping out at 0.90 m (was 1.08; seat unchanged); the lamp is already standard. All three moved 0.2 m further out (table and lamp at z 2.5, chair at 2.2) so they sit around a seated player rather than over them. Set in code (`LittleEchoesSceneSetup.FitFurnitureToRealSizes`); the table-leg and chair vines were re-fitted in Blender, and the vines now follow the furniture's position.
- **Not yet done:** the virtual starting room (next version) is where the reviewer suggested setting the initial scale.

## 2026-10-30 — Guided tutorial
- **Plays every launch, with Skip** (so the dev team sees it each time). Remembering completion per device is a small follow-up if the owner wants it.
- **Wrong notes are taken back** (they still sound) so the tutorial song stays correct; free play resumes after the song or Skip.
- **Timed finish (owner: rhythm-game count):** only the last note ("lamb") is timed, landing on Go (beat 4) with +/-2 sixteenths (about +/-0.34 s at 88 BPM); after 2 misses it counts anywhere. Accessibility gate: no mechanic requires throwing or fast reflexes, so a tap or the controller's place counts like a throw, and timing is never required to finish.
- **Any lamp note counts on Go:** a thrown ball's pitch comes from the height it hits, so requiring F# would make throwing frustrating; F# glows as the suggestion. Owner may prefer to require F#.
- **Colours are never the only cue:** the count's numbers carry the meaning; blue/purple/gold/green are as asked.

## 2026-10-30 — Virtual room, bounces, packs, View
- **Virtual room** comes apart only at the lesson's end or Skip (returning players: straight after the opening). The Lesson replay happens in the real room (no room again).
- **Wall bounces** play only off `BounceSurface` walls: the virtual room's walls and ceiling until real walls can be mapped on a headset. Floor landings stay silent (owner 2026-10-28).
- **Packs:** "Classic" (approved) and "Earthy" (the alternative synthesized set); names are placeholders the owner may change. Places and pack are remembered locally; the lesson always uses the first arrangement and refuses Change while running.
- **View (A11Y-08):** one button cycling four settings rather than separate size and contrast buttons (the leaf has no more room); large text skips the pitch columns (already large, auto-fit).
- **PxrPlatform launch error:** not patched (SDK git package); documented.


## 2026-11-08 | PICO Spatial to PICO XR mode (owner decision)
- Owner, after the second headset session: automatic furniture placement was unusable (plates all over the room, wrong furniture), and there was no visible scan and no occlusion. Owner asked why PICO XR hadn't been used from the start.
- Evidence: PICO SDK 6.1 docs (`unity-getting-started_functionalities-supported-by-each-development-mode.md`). PICO Spatial mode has no spatial anchors, Scene Capture, Spatial Mesh rendering, Environment Depth or haptics; PICO XR mode has all of them. PICO's Room Capture app (`com.pvr.roomcapture`) is on the owner's headset.
- My mistake, recorded honestly: on 2026-10-22 this log already noted that Scene Capture labels are XR-mode only, and I didn't raise switching modes then.
- Decision (owner): release v3.5 as the last PICO Spatial build (automatic placement off), then move to PICO XR on branch `feat/pico-xr`. Work overnight; the owner tests in the evening before the 2026-11-08 23:59 APK deadline and falls back to v3.5 if needed.
- Done:
  - `PicoXrMode.Apply` does the Portal's "Apply All" in batch mode.
  - The Spatial package and its 143 scene components are removed.
  - XR rig: passthrough, floor tracking, recentre at start.
  - Controller and hand pointer rays.
  - The scanned room glows while the player chooses where each instrument lives (no automatic guessing).
  - A depth-only room occluder, never within 1.5 m of the seat.
- Not done: PICO's Environment Depth occlusion (its shaders are Built-in pipeline only, and the game uses URP); Room Capture furniture boxes as hints; spatial anchors to remember places between sessions (places last one session).

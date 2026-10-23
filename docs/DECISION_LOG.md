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

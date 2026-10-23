# LITTLE ECHOES — ROOM ORCHESTRA / Claude Code handoff V3
**PICO OS 6 | Existing Unity project | 2026-10-22 | Design-approved: Flutter, Bop, Boom**

This folder is a set of *instructions and reference assets* to copy into your **existing Unity project**. It is NOT a Unity project, a compiled app, or a completed headset test. Do not drag this into `Assets` wholesale: place this pack's `CLAUDE.md`, `.claude/skills`, `docs/`, `docs/reference/`, `.gitignore` and optionally `.gitattributes` in the Unity PROJECT ROOT (alongside `Assets/`, `Packages/`, `ProjectSettings/`). Merge carefully if any files already exist. Do not overwrite an existing `CLAUDE.md` without reviewing differences. If you prefer, keep the assets/reference files under `docs/reference/` to avoid accidental Unity imports.

## The shortest safe path (high-school-friendly)
1. **Close Unity.** Duplicate the entire existing Unity project folder as an offline backup on another drive or parent folder. Never copy `Library` into Git (offline backups can include it if convenient).
2. **Find the Unity root:** folder with `Assets`, `Packages`, `ProjectSettings`. This is where Claude Code must run.
3. Unzip this pack somewhere separate. Copy the new `CLAUDE.md`, `.claude`, and `docs` folders into the Unity root. Copy `docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png` to `docs/reference/` in the Unity project. Merge, not overwrite. Preserve the new `docs/` content if old project has outdated instructions (inspect conflicts).
4. **Back up to your verified PRIVATE GitHub repository `joyguey/make-something`**, following `docs/GITHUB_FIRST_BACKUP_VERIFIED_REPO.md` and `docs/GITHUB_BACKUP_GUIDE.md`; do NOT publish code, secrets or artwork without approval. If the project already has Git, do NOT run `git init` again.
5. Open Terminal in Unity root; run `claude` (or your usual Claude Code launcher). Verify `pwd` points to the folder with `Assets`.
6. Paste **Prompt 00** from `docs/CLAUDE_PROMPTS_IN_ORDER.md`. It audits and protects the working project. Stop only for a critical approval.
7. After Prompt 00 passes and backup is confirmed, paste prompts 01 through 09 sequentially. Keep full logs in `docs/AGENT_PROGRESS.md`. The agent may complete routine subtasks and tests autonomously; it must not pretend to have tested a headset.
8. Share `SUBMISSION_OVERVIEW.pdf` or `.docx` in this package for this evaluation round; only attach APK if a real build exists and has been tested appropriately.

## Hard limits and what we mean by accessibility
- First playable: 3 balls, 3 virtual targets, deterministic pitch, varied timbre, visible repeating echoes, rhythm by spacing, basic accessibility options, guided 60-second onboarding, edit-mode/emulator validation.
- An accessible alternative to throwing is **select-and-place**, including seated and one-handed controller play. Hand tracking/gestures are optional progressive enhancements; do not gate the game on hand tracking.
- Do not assume semantic recognition of real chairs/lamps or physical impact haptics with bare hands.
- Cultural accuracy is an explicit acceptance requirement: udu = percussion, mbira = lamellophone, erhu = bowed string. Samples must be licensed and reviewed; synthesized stand-ins must be labeled.
- No guarantee of a '5-star' store review; design toward quality, then test with users.

## Document map
- `CLAUDE.md`: agent operating constitution and absolute rules
- `docs/PRD.md`: complete product definition, scope and review brief
- `docs/INTERACTION_AND_MUSIC_SYSTEM.md`: precise tactile grammar, notes, DSP scheduling and repeatability
- `docs/ACCESSIBILITY_FIRST.md`: functional requirements, alternate input mapping and test matrix
- `docs/ART_AND_AUDIO_BIBLE.md`: preserve visual reference, emotional states, models, sources
- `docs/TECHNICAL_ARCHITECTURE.md`: Unity/PICO/Blender/CLI integration and quality gates
- `docs/CLAUDE_PROMPTS_IN_ORDER.md`: exact copy/paste instructions from zero to submission
- `docs/GITHUB_FIRST_BACKUP_VERIFIED_REPO.md`: personalized backup steps for your private `joyguey/make-something` repo
- `docs/GITHUB_BACKUP_GUIDE.md`: general safe GitHub steps
- `docs/QA_AND_DEMO.md`: quality, accessibility and performance test protocol
- `docs/SUBMISSION_TEXT.md`: evaluator-ready overview; distinguish future roadmap from working features
- `docs/CREATIVE_ORIGIN_AND_IP.md`: provenance/originality note and visual protection checklist
- `docs/AGENT_PROGRESS_TEMPLATE.md`, `docs/DECISION_LOG_TEMPLATE.md`: agent logs
- `.claude/skills/*/SKILL.md`: project-specific Claude skills (not official PICO skills)

## A successful first session
A player can place OR toss Flutter at the lamp and hear a repeatable erhu-inspired note. An expressive echo appears on a small orbit. They duplicate Flutter at a different pitch/beat, watch faces react, add Bop to chair and Boom to table, and hear a coherent layered pattern. They can move an echo, stop sound, undo, and reset *without menus, scores, or a traditional DAW*.

# EXACT CLAUDE CODE PROMPTS — FOLLOW IN ORDER
Run inside the root of YOUR ORIGINAL RoomOrchestra Unity project (`Assets/`, `Packages/`, `ProjectSettings/`). One prompt at a time. Let Claude attempt routine automated fixes, but do not consent to unsafe pushing/upgrades/installation without reviewing. Paste prompts as ordinary messages (not shell commands).

## PROMPT 00 — Protect and audit before edits
```text
You are joining my existing Unity RoomOrchestra game for PICO OS 6. Read CLAUDE.md and docs/README_START_HERE.md if present, then ALL V3 docs before edits. This is not a new project. Find the actual Unity root. Identify Unity, PICO SDK, Unity Spatial/OpenXR mode, input bindings, sounds, functioning ball physics, scenes, prefabs, Unity MCP, Blender MCP and PICO CLI. Confirm Git baseline `git status`, current branch, remote and whether this is a private GitHub repo. If there's no safe baseline, STOP before making code changes and give me exact steps for private backup. Don't upload code or use `git init` inside an existing repository. Create docs/AGENT_PROGRESS.md from template, capture baseline compile/editor screenshot and log if available. Report what is VERIFIED versus UNVERIFIED.
```

## PROMPT 01 — Make GitHub safe
```text
Read docs/GITHUB_BACKUP_GUIDE.md and the git-safety skill. Use the real current repo state from Prompt 00. Inspect .gitignore, secret risk, tracked Unity files/meta, asset sizes, Git remote and branch. Recommend the least disruptive private GitHub backup. Do NOT create repos, push, change visibility, force-push or upload without my approval. If authenticated tools and my explicit approval exist, create/verify the private backup, report remote URL/commit and protect it. Otherwise show exactly the single action I must take.
```

## PROMPT 02 — Functional core + input parity (P0/P1)
```text
Read the PRD, music grammar, accessibility-first, technical architecture and applicable skills. Preserve my current throwable ball and 3 target objects. Implement one common MusicalHitEvent path used by BOTH safe underhand toss and one-handed controller select-and-place. Add no new world-scale movement. Introduce Flutter, Bop, Boom as 3 data-driven orb definitions (no finished art yet) with forgiving hit zones; one intended action can only produce one event despite bounce collisions. Stub hardware input if necessary using editor simulator; never claim actual headset. Add EditMode/PlayMode tests for collision debounce, input parity and seated reach. Compile/test/fix, report evidence and commit on feature branch only after safe Git setup.
```

## PROMPT 03 — Musical cause-and-effect (P2)
```text
Replace random notes with deterministic musical grammar from docs/INTERACTION_AND_MUSIC_SYSTEM.md. Table=udu-style PERCUSSION (five articulations), chair=mbira five notes D4 E4 F#4 A4 B4, lamp=erhu five notes D5 E5 F#5 A5 B5 or sample-compatible register. Orb type changes articulation/timbre; impact strength or accessible quiet/medium/strong setting changes dynamics; impact height OR 5 big pitch petals sets note. Same input must produce same musical answer. Write tests for exact 0 and 1 boundaries, scale steps, clip availability and udu percussive mapping. Mark synthetics as sound prototypes. Run tests and inspect logs.
```

## PROMPT 04 — The magical musical orbits (P3)
```text
Implement a single 4/4 88 BPM DSP master clock; three visible one-bar orbit rings with fixed playback marker, 16 snapped onset slots/bar, max six mini-echo faces per object. On a musical hit, plant one deterministic EchoEvent (orb type, target, note/articulation, intensity, step, stable ID). Each mini echo repeats its sound exactly as it reaches the marker, synchronized to AudioSettings.dspTime/validated scheduled playback. Duplicate Flutter on lamp: two interacting echoes, rhythm density changes, GLOBAL BPM DOES NOT. Provide remove and undo; accessible discrete slot select/nudge, not timing-sensitive only. Avoid double audio from instant preview. Automated tests: two echoes, cap, sync/drift, pause, reset, third object layer, scheduling errors. No DAW grid. Prove it via logs, screenshots and editor/emulator evidence.
```

## PROMPT 05 — Accessibility release gate (P4)
```text
Read docs/ACCESSIBILITY_FIRST.md and the accessibility-auditor skill. Implement selectable seated reach and recenter, one-handed controller-only create/edit/clear, aim assist or select-place, large pitch petals and discrete step nudge, alternate volume/intensity control, visible beat and note information, accessible labels+icon/silhouette IDs beyond color, reduced motion/flash, clear focus/contrast/scale, pause and mute. Keep three weight personalities equally playable without force. In editor simulated controller-only mode actually walk through A11Y-01 to -09 and record PASS/FAIL/NOT TESTED. Never claim actual headset validation. Address blockers before art polish.
```

## PROMPT 06 — Preserve and model approved characters (P5)
```text
Use the original reference at docs/reference/Flutter_Bop_Boom_USER_APPROVED_REFERENCE.png, consult docs/ART_AND_AUDIO_BIBLE.md and character-art-pipeline skill. Do not redesign faces/colors/silhouettes. If verified Blender MCP exists, create editable low-poly Flutter (tall turquoise+sparkle), Bop (lavender round+dot) and Boom (amber wide+ripple) with friendly simple expressive faces; otherwise use Blender CLI/procedural Unity meshes. First prototype ONE character through export/import, then batch three after import validation. Keep source meshes/scripts and LICENSES.md, no external image/model downloads. Implement face expressions: idle, first-hit curious, second same-type echo recognizing sibling, rhythmic excitement, reduced-motion static alternative. Use pools/small meshes for orbit minis. Review colliders and test FPS/console. Capture screenshots and show deviations from approved art explicitly.
```

## PROMPT 07 — Sound quality & culturally honest art (P5)
```text
Refine udu-inspired low percussion, mbira-inspired plucked tone and erhu-inspired bowed voice using only sources whose licenses are verified, or mark synthesized prototypes. Populate docs/AUDIO_ASSET_LICENSES.md with creator, URL, license, conditions, file hash and use. Do not claim traditional sound authenticity without sample/expert review. Mix at safe levels, no clipping or startling peaks. Each orb weight must sound materially different but keep recognizable instrument identity. Compare same pitch different weights, 2 Flutter duplicates, three instruments full loop. Test with audio peak metrics where possible. Ask owner only for paid/licensed sample decisions; don't pretend to have heard the mix if you lack audio rendering or human review.
```

## PROMPT 08 — Optional magic, only after gates pass (P6)
```text
Review all current phase-gate evidence. If reliable, add ONE optional interaction: a large tappable heartbeat for global tempo (60–140 BPM) with a controller-accessible +/- option, applied cleanly at a bar boundary. Make sure adding duplicate echoes still never changes global BPM. Only if headset SDK/hardware clearly supports hand input, prototype a gentle open palm rise/fall swell feature BEHIND A FLAG with equal controller-slider operation. Skip unsupported gestures; preserve accessibility. Write tempo rescheduling tests, input switching test, no-phantom-input test. If previous gates not met, fix them instead and explain why.
```

## PROMPT 09 — Integration QA & submission (P7/P8)
```text
Run all tests and inspections in docs/QA_AND_DEMO.md. Use verified local Unity/PICO CLI commands and emulator where actually available; don't invent paths or test results. Capture a 45s demo only if functioning, record real log/screenshot paths and clearly label emulator versus physical PICO device. Test DSP drift, event predictability, clipping, object import, accessibility alternate path, pose/recenter, no severe errors. Fix high severity bugs. Update docs/SUBMISSION_TEXT.md based on WHAT REALLY WORKS with implemented/roadmap distinction, outline missing headset checks, optional APK only if built and verified. Do NOT submit or publish anything on my behalf.
```

## WHEN CLAUDE STOPS / ASKING CRITICAL QUESTIONS
```text
Do not stop for routine errors or ask me to open code files. Diagnose, implement the smallest correction, rerun tests (up to three evidence-driven attempts per distinct issue), record proof and continue. Ask only for a critical design fork; cost/license; permissions/credentials; any destructive or remote operation; or a real headset interaction that cannot be simulated. Present a recommended safe default and one concise question. Do not claim an MCP command or build succeeded without logged evidence.
```

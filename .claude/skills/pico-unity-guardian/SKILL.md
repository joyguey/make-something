---
name: pico-unity-guardian
description: Inspect installed PICO Unity SDK and prevent destructive Unity project changes.
---
# pico-unity-guardian
Read CLAUDE.md + docs/TECHNICAL_ARCHITECTURE.md. Detect real Unity project root, manifest, XR loader, scene and actual PICO SDK packages before edits. Check official PICO docs and actual CLI/Unity MCP help, no invented APIs. Snapshot baseline, compile, preserve GUIDs, no new project unless approved. Test simulator and report unverified headset items. Test controller input even if hands implemented.

## Deliverable
Return actual commands/tool versions, files changed, test evidence, explicit untested claims, and next smallest non-destructive action. Update `docs/AGENT_PROGRESS.md`.

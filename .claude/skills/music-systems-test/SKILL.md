---
name: music-systems-test
description: Build deterministic musical grammar and validate DSP timed orbit loops.
---
# music-systems-test
Read docs/INTERACTION_AND_MUSIC_SYSTEM.md. Object->instrument, orb->articulation, height/petal->pitch, force/preset->dynamics, orbit step->rhythm, Heart->global BPM. No random notes. Write pure EditMode tests for note indexes and cap, scheduled audio lookahead, duplicate bounce suppression, 5minute drift, stop/clear and undo. Never equate rhythmic density to faster tempo.

## Deliverable
Return actual commands/tool versions, files changed, test evidence, explicit untested claims, and next smallest non-destructive action. Update `docs/AGENT_PROGRESS.md`.

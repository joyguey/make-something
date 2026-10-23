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

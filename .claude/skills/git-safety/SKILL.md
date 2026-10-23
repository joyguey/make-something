---
name: git-safety
description: Protect existing Unity repository and private IP before remote operations.
---
# git-safety
Read docs/GITHUB_BACKUP_GUIDE.md. Read-only inspect first: pwd/git status/branch/remote/gitignore secrets; git init ONLY if no repo exists. Confirm private ownership and permissions; no push/create/force/visibility changes without owner approval. Unity meta files and Asset/Packages/ProjectSettings tracked, Library ignored; LFS optional after installation; never expose auth tokens.

## Deliverable
Return actual commands/tool versions, files changed, test evidence, explicit untested claims, and next smallest non-destructive action. Update `docs/AGENT_PROGRESS.md`.

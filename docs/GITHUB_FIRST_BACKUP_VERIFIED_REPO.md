# FIRST GITHUB BACKUP — VERIFIED REPOSITORY

GitHub connection confirmed on 2026-10-22. The connected account can access **private, currently empty** repository **`joyguey/make-something`**:
- Browser: https://github.com/joyguey/make-something
- Clone/push: `https://github.com/joyguey/make-something.git`
- Repository visibility: **PRIVATE**; repository size reported 0.

**Important:** ChatGPT can inspect the connected GitHub account, but it has NOT read or uploaded the current Unity source folder on your Mac. This document is a safe local procedure Claude Code can carry out, with your permission. Do NOT upload just this Agent Pack and assume your Unity project is backed up.

## Step 1 — Make an offline copy
Close Unity. In Finder duplicate the REAL RoomOrchestra project folder (containing `Assets`, `Packages`, `ProjectSettings`). Preserve it on another drive/location before Git work.

## Step 2 — In Terminal, navigate to THAT existing Unity root
```
pwd
ls Assets Packages ProjectSettings
git status
git remote -v
```

- If `git status` works, the folder ALREADY uses Git. **Do not `git init`.** Inspect any origin before changing it.
- If `git status` says `not a git repository`, follow Step 3.
- If origin already points to `https://github.com/joyguey/make-something.git`, do not add another remote.
- If origin points somewhere else, **stop and ask**. Do not rewrite it.

## Step 3 — Only if NOT already a Git repository
First put the supplied Unity `.gitignore` at project root (merge with any existing .gitignore). Before `git add`, scan for `.env` files, API keys, credentials, license-restricted external assets, giant recordings, and secret IP that you do not wish to upload. Git LFS may be necessary for large binary assets; verify installation and quota before tracking those assets.
```
git init -b main
git status --short
git add .
git status --short
git commit -m "chore: backup original RoomOrchestra Unity baseline"
```

## Step 4 — Connect only when `origin` does not already exist
```
git remote add origin https://github.com/joyguey/make-something.git
git remote -v
```

## Step 5 — First private push after explicit review
Confirm the remote is indeed your private repo and your code/asset upload is approved. Do not push an uncommitted project or secrets. If `origin` is correct and the remote is still empty:
```
git push -u origin HEAD:main
```

This pushes the current local branch to `main` without forcing. GitHub CLI `gh auth login` or GitHub Desktop may help authenticate. **Never paste personal access tokens in Claude conversations.** If push rejects or remote is no longer empty, stop: do NOT force-push.

## Step 6 — Verify in your browser
Open https://github.com/joyguey/make-something and confirm you can see your real Unity `Assets/`, `Packages/`, `ProjectSettings/`, script files and their `.meta` files, but not `Library/`, `Temp/`, or secrets. Record the commit SHA.

## Paste this into Claude Code (running INSIDE your existing Unity root)
```text
I connected GitHub and my intended backup destination is the existing PRIVATE and initially empty repository joyguey/make-something (https://github.com/joyguey/make-something). Read CLAUDE.md, docs/GITHUB_FIRST_BACKUP_VERIFIED_REPO.md, docs/GITHUB_BACKUP_GUIDE.md, and the git-safety skill. Work from my CURRENT Unity project root with Assets, Packages and ProjectSettings. First inspect git status, current branch, remote, Unity .gitignore, secret exposure risks, and large asset files. Never initialize an already initialized repo. Never replace a different origin. Prepare the safe backup changes and ask for my authorization before the first private push. Once approved, commit and push to this exact repository without force and verify files and commit SHA. Do not change the game's behavior during this backup step.
```

## Why GitHub is only part of backup
Git is version history; private GitHub is a remote copy for committed files. Offsite credentials, large sound libraries not tracked, ignored files and local editor caches are NOT automatically backed up. Retain the independent offline copy as well.

# HOW TO BACK UP YOUR REAL UNITY PROJECT ON GITHUB
**Start here BEFORE Claude changes Unity code.** We have NOT created a GitHub repo or pushed any of your local code for you. The suggested GitHub connection must be installed/authorized before in-chat repository operations can occur; Claude Code can use Git/GitHub CLI locally if authenticated.

## A. Find your actual Unity project root
The correct folder contains: `Assets/`, `Packages/`, `ProjectSettings/`. Your downloaded handoff pack is NOT your Unity repo. Open Terminal in this Unity root and run:
```bash
pwd
ls Assets Packages ProjectSettings
git status
git remote -v
```
If `git status` shows normal branch info and `git remote -v` shows a GitHub URL, **you already have a repository**. Do NOT run git init or create another repo. Go to section C. If `git status` says not a Git repository, go to B. If repo exists but remote absent, use section D.

## B. Existing local code not yet version controlled
1. Physically duplicate your existing Unity project folder for rollback. Close Unity while copying.
2. In Unity Editor settings, set Asset Serialization = **Force Text** and Version Control = **Visible Meta Files** where available. Check current Unity menus before changing settings.
3. Place `.gitignore` from this pack at the Unity root. If one exists, compare/merge; never blindly replace. Unity's `Library/`, `Temp/`, `Obj/`, `Logs/`, `Builds/`, `UserSettings/` are ignored; retain `Assets/**/*.meta` and `ProjectSettings`/`Packages` and scene/prefab files.
4. Do a secrets and huge-file scan BEFORE `git add .`: check `.env`, API keys, credentials, large purchased binary assets and copyrighted IP sharing rights. Check `git status --ignored` to confirm paths. *Do not write tokens in a README or prompts.*
5. Check `git --version`. Then only for a genuinely non-Git folder:
```bash
git init -b main
git status --short
git add .
git status --short
git commit -m "chore: backup existing RoomOrchestra Unity baseline"
```
If authentication/identity fails, configure it via normal Git tools, not a personal token pasted into chat.

## C. If GitHub remote already exists (recommended)
1. Check `git status`, `git branch --show-current`, `git remote -v`.
2. Never push current work until confirming the GitHub owner/repo and whether it is PRIVATE. If unrelated or shared org repo, seek approval.
3. Commit staged safe changes with a meaningful description (or tell Claude to do it after review).
4. Push your CURRENT branch only after owner approval; never force push. Example `git push -u origin main` *if and only if* current branch really is `main`.
5. Verify on github.com that Assets, Packages and ProjectSettings exist; `Library/` and secrets do not.

## D. Create or link PRIVATE GitHub repository for existing local folder
**CLI path** (first install GitHub CLI from https://cli.github.com/ if absent; `gh auth login` with your own account):
```bash
gh auth status
# in Unity project root AFTER your baseline local commit:
gh repo create RoomOrchestra-LittleEchoes --private --source=. --remote=origin --push
```
This uploads private IP off your machine. **Run only after reviewing repository name/account and explicitly choosing to publish privately.** If `origin` already exists, do NOT run the command; see C or update with `git remote add origin <ACTUAL_REPO_URL>` only when no remote exists and after owner confirms exact URL.

**Point-and-click alternative:** GitHub Desktop -> File -> Add local repository -> choose Unity project root -> publish repository, UNCHECK 'Keep this code private' is WRONG; ensure **Keep this code private = checked**. Inspect list of changed files before clicking Publish.

## Git LFS / large audio and 3D assets
- If 3D source, audio recordings or texture assets become large, consider Git LFS *before adding them*. `git lfs install`, then `git lfs track "*.blend" "*.fbx" "*.wav" "*.psd" "*.glb"` as appropriate, review generated `.gitattributes`, then commit. Do not add LFS rules if LFS is not installed, and do not rewrite old history without permission. GitHub quotas apply.
- Unity files `.unity`, `.prefab`, `.asset`, `.meta` are generally text-YAML when Force Text is used; track them normally. Do not use Git LFS for these by default.

## Safe Claude prompt for GitHub (do not allow it to push automatically)
```text
Read docs/GITHUB_BACKUP_GUIDE.md and the git-safety skill. Inspect the REAL Unity project root, `git status`, `git branch --show-current`, `git remote -v`, .gitignore and potential secrets. Determine whether a GitHub repository already exists. Do not run git init if Git exists. Do not create remote repo, alter remote, stage sensitive files, change visibility or push without my explicit authorization. Give me a 5-line recommendation and exact safe commands for MY CURRENT STATE. If I approve a private repo/push, execute, verify the remote and report the commit hash/URL without exposing secrets.
```

## What GitHub is and is not
Git = version history and teamwork; GitHub = hosted remote copies. It is not a complete backup for huge/raw assets unless actually committed, and does not replace an external offline backup. It does not prove exclusive IP rights. Branch per feature: `feat/orbit-clock`, `feat/accessibility-controller` etc; protected `main` when collaborators grow.

## Official sources
https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github
https://github.com/github/gitignore/blob/main/Unity.gitignore
https://cli.github.com/manual/gh_repo_create

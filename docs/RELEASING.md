# Releasing SecureFile

## Where a release lives

| What | Where | Why |
|---|---|---|
| **The exact source of the release** | branch `release/v<version>-<label>` (for example `release/v0.1.0-beta1`) and the matching annotated tag `v<version>-<label>` | A branch plus a tag survive even if the working branches are cleaned up, and the tag never moves |
| **The programs for each system** | built from that branch with the scripts below; attach them to a GitHub Release for the tag | Programs are 6 to 58 MB each and do not belong in git history (every version would stay in the repository forever) |
| The decisions and the history behind it | `CHANGELOG.md`, `docs/PROJECT_PLAN.md`, `docs/REVIEW_BACKLOG.md`, `docs/SUGGESTIONS.md` | |

The release branch is a **frozen snapshot**: no feature work goes there. A fix to a released version is a new commit on the
release branch (fast-forward only) and a new tag (`v0.1.1`).

## Build the programs for every system

From a clone checked out at the release branch:

```powershell
pwsh scripts/publish-beta.ps1                  # Windows app (window), win-x64   -> artifacts/beta
pwsh scripts/publish-cli.ps1                   # command line for win-x64, linux-x64, linux-arm64, osx-arm64, osx-x64 -> artifacts/cli
```

| System | Window | Command line |
|---|---|---|
| Windows 10/11 x64 | `SecureFile-<v>-<label>-win-x64.zip` | `securefile-<v>-<label>-win-x64.zip` |
| Linux x64 and arm64 | not built yet (Avalonia, plan Step 7.1) | built, **held back from releases** until the window exists (Steps 7.1 and 7.2) |
| macOS Apple silicon and Intel | not built yet (Avalonia, plan Step 7.1) | built, **held back from releases** until the window exists (Steps 7.1 and 7.2) |

Each script prints SHA-256 hashes (`artifacts/cli/SHA256SUMS.txt`, `artifacts/beta/*.sha256`). Send them with the files.
None of the programs is code-signed or notarized yet, so Windows SmartScreen and macOS Gatekeeper warn; the READMEs inside
the archives explain the one-time steps.

## Checklist before cutting a release branch

1. `docs/TESTPLAN.md` has been run on this build and every row is ✅ or ➖ (or has a recorded, fixed bug).
2. `scripts/verify.ps1` passes (it also runs on every push).
3. The CI run for the commit is green on Windows, Ubuntu (and macOS for a pull request).
4. `CHANGELOG.md` has the version; `Directory.Build.props` has the same version.
5. The programs were built **from that commit** and the CLI archives were tried on at least Windows and one of macOS or Linux.

## Cut it

```powershell
git switch <branch-with-the-final-commit>
git branch release/v0.1.0-beta1
git tag -a v0.1.0-beta1 -m "SecureFile 0.1.0-beta1"
git push origin release/v0.1.0-beta1 v0.1.0-beta1
```

Name a release that has passed the checklist `release/v0.1.0` (no label): that is the "stable" one. `-beta1` means "good enough
for friends to try, not yet proven".

## What protects it, and what does not

GitHub's branch protection and rulesets are not available for a private repository on the free plan (the API answers
"Upgrade to GitHub Pro or make this repository public"), so GitHub itself will not stop someone with write access from
deleting the branch on the website. What exists instead:

- The local `.githooks/pre-push` hook (enabled with `git config core.hooksPath .githooks`) refuses deleting any remote
  branch or tag, force-pushes and non-fast-forward updates, and pushing straight to `main`. It protects against mistakes on
  a clone that has the hook enabled; it can be skipped with `--no-verify`.
- The **tag** is a second pointer to the same commit, and a GitHub Release with the programs attached keeps the binaries.
- To make it permanent: make the repository public, or upgrade the plan and add a branch ruleset for `release/*` that blocks
  deletion and force-pushes, and a tag ruleset for `v*`.

## Release history

| Release | Contents | Notes |
|---|---|---|
| `v0.1.0-beta1` (branch `release/v0.1.0-beta1`) | **Windows app only** (`SecureFile-0.1.0-beta1-win-x64.zip` and its SHA-256) | Decision of 2026-11-24: nothing for macOS and Linux is released until the desktop windows exist (plan Steps 7.1 and 7.2). The command-line archives were built and tried (Windows) but are not attached. The zip's README was reworded after the tag was cut (macOS/Linux row) and the zip rebuilt from the release branch; the program itself is the same. GitHub treats asset names as case-insensitive, so `SecureFile-...` and `securefile-...` zips cannot share a release: name command-line zips `securefile-cli-...`. |

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
pwsh scripts/publish-beta.ps1                  # Windows app (window), win-x64 and win-arm64 -> artifacts/beta
pwsh scripts/publish-cli.ps1                   # command line for win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64, osx-x64 -> artifacts/cli
pwsh scripts/publish-desktop.ps1               # Avalonia window for linux-x64, linux-arm64, osx-arm64, osx-x64 -> artifacts/desktop
```

| System | Window | Command line |
|---|---|---|
| Windows 10/11 x64 | `SecureFile-<v>-<label>-win-x64.zip` | `securefile-cli-<v>-<label>-win-x64.zip` |
| Windows 11 ARM64 | `SecureFile-<v>-<label>-win-arm64.zip` (**built, never run**) | `securefile-cli-<v>-<label>-win-arm64.zip` (**built, never run**) |
| Linux x64 and arm64 | `SecureFile-<v>-<label>-linux-x64.tar.gz` / `-linux-arm64.tar.gz`: built, **held back from releases** until tried on a real Linux desktop | built, **held back** with the window |
| macOS Apple silicon and Intel | `SecureFile-<v>-<label>-osx-arm64.tar.gz` / `-osx-x64.tar.gz` (a `SecureFile.app`): built, **held back from releases** until tried on a real Mac | built, **held back** with the window |

**Why macOS and Linux are still held back (decision of 2026-11-24, kept in Stage 7):** the windows now exist (plan Steps
7.1, 7.2) and pass automated tests on Ubuntu and macOS in CI (headless window test, cross-OS golden files), but nobody has
yet started them on a real Mac or Linux desktop: there is no Mac here, no Linux PC, and WSL has no distribution installed.
Native dialogs, Gatekeeper, the app bundle and fonts can only be judged on a real system. Release them only after the
checks in `docs/NEXT_STEP_PROMPT.md` ("Things only the user can do") were done on at least one Mac and one Linux desktop.
The osx-arm64 program carries an ad-hoc code signature added by the .NET SDK (checked in the file), which Apple silicon
requires; it is **not** signed with an Apple Developer ID nor notarized (ADR-15 covers only Windows so far).

Each script prints SHA-256 hashes (`artifacts/cli/SHA256SUMS.txt`, `artifacts/beta/SHA256SUMS.txt`, `artifacts/desktop/SHA256SUMS.txt`, `artifacts/beta/*.sha256`).
Send them with the files.
None of the programs is code-signed or notarized yet, so Windows SmartScreen and macOS Gatekeeper warn; the READMEs inside
the archives explain the one-time steps. The signing decision is ADR-15 (proposed, waiting for the user).

Run the scripts from **PowerShell** (not Git Bash: Git's `tar` cannot read `D:` paths) and, if the execution policy blocks
unsigned scripts, as `powershell -ExecutionPolicy Bypass -File scripts/publish-beta.ps1`. A virus scanner that kills
programs writing many large files can break a build or a test run: exclude the repository folder instead of turning it off.

**win-arm64 is built but has never been run** (there is no ARM machine here; the exe header was checked as ARM64). Say so in
every release note until someone has started it on a real ARM PC. Sizes of the last build (0.1.0-beta1): WPF zip 56.2 MB
x64 and 52.0 MB arm64; CLI zips 5.3 to 5.9 MB each.

## Release candidate (what to do for `-rc1`)

1. All checks below are done on the commit to be released; the Windows Sandbox test (`docs/SANDBOX_TEST.md`) is run on the
   x64 zip and its table is filled in.
2. `pwsh scripts/publish-beta.ps1 -Label rc1` and `pwsh scripts/publish-cli.ps1 -Label rc1` (the label is only in the file
   names; the version comes from `Directory.Build.props`).
3. Collect `artifacts/beta/SHA256SUMS.txt` and `artifacts/cli/SHA256SUMS.txt`, check one zip by hand with `Get-FileHash`,
   and put the hashes in the release notes.
4. **Ask the owner before anything outward-facing**: pushing the branch or tag, creating the GitHub Release, uploading the
   zips, sending links to testers. The scripts never upload anything. `artifacts/` is git-ignored; never commit a zip.
5. Attach to the release only what is ready (today: the Windows zips; macOS and Linux windows and command lines stay held
   back until tried on real systems, see above).

## Checklist before cutting a release branch

1. `docs/TESTPLAN.md` has been run on this build and every row is ✅ or ➖ (or has a recorded, fixed bug).
2. `scripts/verify.ps1` passes (it also runs on every push).
3. The CI run for the commit is green on Windows, Ubuntu (and macOS for a pull request).
4. `CHANGELOG.md` has the version; `Directory.Build.props` has the same version.
5. The programs were built **from that commit** and the CLI archives were tried on at least Windows and one of macOS or Linux.
6. For a release that includes macOS or Linux: that system's window archive was started on a real machine of that system.

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
| `v0.1.0-beta1` (branch `release/v0.1.0-beta1`) | **Windows app only** (`SecureFile-0.1.0-beta1-win-x64.zip` and its SHA-256) | Decision of 2026-11-24: nothing for macOS and Linux is released until the desktop windows exist (plan Steps 7.1 and 7.2). The command-line archives were built and tried (Windows) but are not attached. Only documents changed between the tag and the release branch head (the macOS/Linux wording), no program code; the zip was built from commit `4e9c693`, so the exe embeds that commit id (`ProductVersion 0.1.0+4e9c693…`) and its hash differs from a build at the tag. GitHub treats asset names as case-insensitive, so `SecureFile-...` and `securefile-...` zips cannot share a release: name command-line zips `securefile-cli-...`. |

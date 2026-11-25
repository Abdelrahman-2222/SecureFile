# Next step: copy-paste prompt for a new chat (Phase 6, Stage 8: stakeholder suggestions)

Last finished (2026-11-25): Stage 7 (Avalonia windows for macOS and Linux), plan rows 7.1-7.5, on branch
`feature/stage-7-avalonia` (PR against `main`). 7.1 and 7.2 are done; 7.3 is amber until the PR's CI is green on all three
systems; 7.4 and 7.5 are amber because nobody has run the macOS/Linux windows on a real system and Native AOT was not
measured. See `docs/RELEASING.md` (held back), `docs/QUALITY_REPORT.md` §7.5, `docs/DESKTOP_README.md`, ADR-16.

## Things only the user can do (Stage 7)

- [ ] **macOS (any Mac with macOS 14+; Apple silicon → `osx-arm64`, Intel → `osx-x64`):** run
  `pwsh scripts/publish-desktop.ps1 -Runtimes osx-arm64` (or take the archive from a CI-free build), copy the `.tar.gz`
  to the Mac, follow `docs/DESKTOP_README.md`. Check: Gatekeeper "Open Anyway" works; the window opens with the right
  theme; Browse shows the Finder dialog; drag a file from Finder onto the drop zone; encrypt with a password + confirm;
  Save over an existing file asks once to replace; Open folder opens Finder; Cmd+O browses; F1/About; close while
  encrypting a large file asks first; decrypt the result; a file encrypted on Windows decrypts on the Mac.
- [ ] **Linux desktop (Ubuntu 24.04 or similar; or WSL: `wsl --install -d Ubuntu`, then WSLg shows windows):** same list
  with `linux-x64`; also note whether the Save dialog keeps the main window blocked (backlog C19) and whether the
  replace prompt appears (GTK or portal).
- [ ] Mark plan rows 7.3 (after CI is green), 7.4 and 7.5 green or record what failed; then decide whether macOS/Linux go
  into a release (`docs/RELEASING.md`).
- [ ] Optional: Native AOT size (`docs/QUALITY_REPORT.md`, "Things only the user can do").
- [ ] Still open from Stage 6: Sandbox run (`docs/SANDBOX_TEST.md`), ADR-15 decision, screenshots, any GitHub release.

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: do Stage 8
"Stakeholder suggestions" (plan row 8.1) in this one chat, then open the pull request. Do not read docs/PROJECT_PLAN.md or
docs/LEARNING_LOG.md in full: grep the rows you need. Terse chat updates.

STEP 0: git status, git fetch --prune. If the Stage 7 PR (feature/stage-7-avalonia) is merged, branch
feature/stage-8-suggestions from main; otherwise from feature/stage-7-avalonia and target that branch (check it still exists
on origin first). Build and run all tests first.

KNOWN ENVIRONMENT: Windows 11, Kaspersky kills programs that write many large files (about 1 full test run in 4 fails with a
"...LeavesNothing" test: capture the name, re-run); never disable the antivirus. Scripts need `powershell -ExecutionPolicy Bypass
-File ...` or pwsh, run from PowerShell, not Git Bash. No Mac, no Linux PC, WSL has no distribution, no ARM machine, no C++
build tools (so no Native AOT).

8.1 Refresh docs/SUGGESTIONS.md (plan §13): what, why, benefit, priority, effort for each suggestion, marking which are
    already built (CLI, Avalonia windows, cross-OS golden files, size comparison) and which are new. Use docs/REVIEW_BACKLOG.md
    (B and C items, C19-C21 from Stage 7) and the Stage 5-7 findings in docs/QUALITY_REPORT.md as the source; keep it short and
    stakeholder-readable (no code). Check every claim against the repo (grep), never from memory.

CARRY-OVER (keep listed, do the cheap ones if time allows):
- Waiting for the user: Stage 7 real-system checks (docs/NEXT_STEP_PROMPT.md top), Stage 6 Sandbox run, ADR-15 decision,
  screenshots, any GitHub release; Stage 5 "Things only the user can do" (cold start, VS profiler, SonarQube, class coupling,
  decision C1 EnableCompressionInSingleFile 134 -> 65 MB, Native AOT size).
- Backlog: C13 (CPU/disk flush overlap), C14 (symlink alias on Linux/macOS: CI now runs there), C15 (deterministic TryDeleteFile
  retry test), C16 second half (owner-only ACL on the temp file), C17 (--password-stdin / PowerShell 5.1 non-ASCII), C18 (inject
  TimeProvider into MainViewModel throttle), C19 (Linux portal Save dialog modality), C20 (Avalonia Caps Lock / select file),
  B1 WPF items, B3/B4, C2/C6/C8.
- Stryker on the CLI's pure logic (parser, resolvers) was never run.
- One unexplained flaky test failure from the antivirus.

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; edit with Edit/Write
(no Python or heredoc rewriting of source); Conventional Commits, first line <= 72 characters, NO AI attribution lines; never commit
secrets, big binaries or artifacts/; ask before any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`:
do not run it by hand per commit. Wrap-up: append ONE short "Stage 8" section to docs/LEARNING_LOG.md (append only, do not read it),
CHANGELOG lines, plan rows marked honestly (amber for anything waiting on the user), replace this file with the Stage 9 hand-off,
one reviewer agent on the whole diff, then the PR and the ccd_pr tools.
~~~

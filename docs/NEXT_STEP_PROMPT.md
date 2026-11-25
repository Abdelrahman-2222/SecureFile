# Next step: copy-paste prompt for a new chat (Phase 5, Stage 7: Avalonia windows for macOS and Linux)

Last finished (2026-11-25): Stage 6 (Publishing), plan rows 6.1-6.5, on branch `feature/stage-6-publishing` (PR open; base is
`feature/stage-5-quality` because Stage 4 and 5 are not in `main` yet; retarget when they merge). Stage 6 is amber, not green:
the Windows Sandbox run, the ADR-15 decision (installer/signing), the README screenshots and any real release are **waiting for
the user**; `win-arm64` is built but never run. See `docs/SANDBOX_TEST.md`, `docs/adr/0015-installer-and-code-signing.md`,
`docs/SCREENSHOTS_NEEDED.md`, `docs/RELEASING.md`.

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app + command line;
layers Wpf/Cli -> Presentation -> Core. Task: do Stage 7 "Avalonia windows for macOS and Linux" (plan rows 7.1-7.5) in this one
chat, one commit per step, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep the
rows you need. Terse chat updates.

STEP 0: git status, git fetch. Find which of the Stage 4/5/6 PRs are merged into main; branch feature/stage-7-avalonia from main if
Stage 6 is merged, otherwise from feature/stage-6-publishing (PR against it). Build and run all tests first.

KNOWN ENVIRONMENT: Windows 11, Kaspersky kills programs that write many large files (about 1 full test run in 4 fails with a
"...LeavesNothing" test: capture the name, re-run); never disable the antivirus. Scripts need `powershell -ExecutionPolicy Bypass
-File ...`, run from PowerShell, not Git Bash. No Mac and no Linux machine; WSL2/WSLg may exist (check). No ARM machine.

7.1 Avalonia app (src/SecureFile.Avalonia) reusing MainViewModel; same features as the WPF window (Browse, drop, password +
    confirm, strength bar, Encrypt/Decrypt, progress, cancel, status line, About).
7.2 Avalonia service implementations (StorageProvider dialogs, launcher, clipboard) behind the existing Presentation interfaces.
7.3 CI matrix Windows/Ubuntu/macOS and a cross-OS "golden file" test (a file encrypted on one OS decrypts on all).
7.4 Publish osx-arm64, osx-x64, linux-x64, linux-arm64 windows (scripts, archive names securefile-... vs SecureFile-...: keep
    names distinct); test Linux in WSLg if present; macOS cannot be tested here: say so, add it to the user's checklist.
7.5 Size comparison WPF (untrimmed) vs Avalonia (trimmed/AOT): numbers in docs/QUALITY_REPORT.md.
Keep RELEASING.md "held back" wording true: macOS/Linux releases stay held until windows exist AND were tried on real systems.

CARRY-OVER (keep listed, do the cheap ones if time allows):
- Waiting for the user from Stage 6: Sandbox run (docs/SANDBOX_TEST.md), ADR-15 decision, screenshots, any GitHub release.
- Stage 5 "Things only the user can do" in docs/QUALITY_REPORT.md (cold start, VS profiler, SonarQube, class coupling, screenshots,
  decision C1 EnableCompressionInSingleFile 134 -> 65 MB).
- Backlog: C13 (CPU/disk flush overlap), C14 (symlink alias on Linux/macOS: now testable via 7.3), C15 (deterministic TryDeleteFile
  retry test), C16 second half (owner-only ACL on the temp file), C17 (--password-stdin / PowerShell 5.1 non-ASCII), C18 (inject
  TimeProvider into MainViewModel throttle), B1 WPF items, B3/B4, C2/C6/C8.
- Stryker on the CLI's pure logic (parser, resolvers) was never run.
- One unexplained flaky test failure from the antivirus.

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; edit with Edit/Write
(no Python or heredoc rewriting of source); Conventional Commits, first line <= 72 characters, NO AI attribution lines; never commit
secrets, big binaries or artifacts/; ask before any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`:
do not run it by hand per commit. Wrap-up: append ONE short "Stage 7" section to docs/LEARNING_LOG.md (append only, do not read it),
CHANGELOG lines, plan rows marked honestly (amber for anything waiting on the user), replace this file with the Stage 8 hand-off,
one reviewer agent on the whole diff, then the PR and the ccd_pr tools.
~~~

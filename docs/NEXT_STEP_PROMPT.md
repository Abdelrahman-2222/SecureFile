# Next step: copy-paste prompt for a new chat (Phase 6, Stage 6: Publishing)

Last finished (2026-11-24): Stage 5 (Quality), plan rows 5.1-5.6, on branch `feature/stage-5-quality` (PR open; base is
`feature/stage-4-testing` until the Stage 4 PR merges, then retarget to `main`). Numbers and verdicts are in
`docs/QUALITY_REPORT.md`; threats in `docs/THREAT_MODEL.md`; open items in `docs/REVIEW_BACKLOG.md`.

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app + a command-line
version; layers Wpf/Cli -> Presentation -> Core. Task: do Stage 6 "Publishing" (plan rows 6.x) in this one chat, one commit per
step, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep the rows you need.
Terse chat updates.

STEP 0: git status, git fetch. If the Stage 5 PR is merged into main, switch to main, pull, create feature/stage-6-publishing;
otherwise branch from feature/stage-5-quality and open the PR against it. Build and run all tests first.

KNOWN ENVIRONMENT: Kaspersky kills programs that write many large files and makes backup copies of temp files (about 1 full test
run in 4 fails with a "...LeavesNothing" test: capture the name, re-run). Never disable the antivirus; ask the user to exclude a
folder. Scripts need `powershell -ExecutionPolicy Bypass -File ...` (policy blocks unsigned scripts); run publish-cli.ps1 from
PowerShell, not Git Bash (Git's tar cannot read D: paths).

6.1 win-arm64: add the win-arm64 build of the WPF app and the CLI to the publish scripts; the build is possible here, running it is
    not (no ARM machine): say so in the docs. Record sizes.
6.2 Windows Sandbox test: write scripts/sandbox-test.wsb (mapped read-only folder with the published exe, no network) and a click
    list; running it is a USER task if Windows Sandbox is not enabled (needs Windows 11 Pro feature + reboot): put the exact steps in
    the checklist and continue.
6.3 Installer and signing decision: write an ADR comparing (a) zip + SHA-256 only, (b) MSIX, (c) Inno Setup/WiX installer, (d)
    code-signing options (Azure Trusted Signing, OV/EV certificate, self-signed for tests). Recommend one; do not buy or upload anything.
    The decision is the user's: record it as "proposed".
6.4 README: user-facing README (what it is, download, verify the SHA-256, SmartScreen note, limits, forgotten password = lost),
    screenshots listed for the user to supply (not invented).
6.5 Release candidate: scripts/publish-beta.ps1 and publish-cli.ps1 produce the archives; docs/RELEASING.md updated; do NOT publish a
    GitHub release or upload anything without asking.

CARRY-OVER (from Stage 5; keep listed, do the cheap ones if time allows):
- QUALITY_REPORT "Things only the user can do": cold start after reboot, VS profiler run, SonarQube for IDE, class coupling, screenshots,
  decision C1 (EnableCompressionInSingleFile: 134 -> 65 MB, +0.2 s start).
- Backlog C13 (encrypt/decrypt do not overlap CPU with the disk flush), C14 (symlink alias of the input on Linux/macOS), C15
  (deterministic test of TryDeleteFile retry), C16 (explicit asInvoker in the manifest, owner-only ACL on the temp file), C17
  (--password-stdin and Windows PowerShell 5.1 turn non-ASCII into "?"), C18 (inject a clock into MainViewModel's progress throttle),
  and the B1 WPF items, B3/B4, C2/C6/C8 (need people or decisions).
- Stryker on the CLI's pure logic (parser, resolvers) was not run in Stage 5.
- One unexplained flaky test failure from the antivirus (see above).

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; edit with Edit/Write
(no Python or heredoc rewriting of source); Conventional Commits, first line <= 72 characters, NO AI attribution lines; never commit
secrets, big binaries or artifacts/; ask before any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`:
do not run it by hand per commit. Wrap-up: append ONE short "Stage 6" section to docs/LEARNING_LOG.md (append only, do not read it),
CHANGELOG lines, plan rows 6.x marked honestly (amber for anything waiting on the user), replace this file with the Stage 7 hand-off
(Avalonia windows for macOS/Linux), one reviewer agent on the whole diff, then the PR and the ccd_pr tools.
~~~

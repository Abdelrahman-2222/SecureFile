# Next step: copy-paste prompt for a new chat (Stage 9.0: backlog burn-down before the final review)

Last finished (2026-11-25): Stage 8 (stakeholder suggestions), plan row 8.1, PR #7 (squash-merged into `main`). After it, every open item in `docs/REVIEW_REPORT_2026-11-24.md` and `docs/REVIEW_BACKLOG.md` was checked against
the code and triaged into "fix now" (step 9.0, below), "if time" and "later". The rest of Stage 9 (9.1 full review of
all PRs, code and logic; 9.2 `docs/FINAL_GUIDE.md`; 9.3 presentation with the v2 / future-releases roadmap; 9.4 the
user's demo video) follows in a later chat.

## Things only the user can do (still open)

- [ ] Merge the hand-off PR (`docs/stage-9-handoff`) before starting 9.0 (PR #7 is already merged).

**Note for every Stage 9 chat: many small commits.** One commit per fixed item (or per tight group of the same item, such as
the fix and its test), never one big commit per stage, so each change can be traced to its backlog item, reviewed and
reverted on its own. Put the item id in the commit message, e.g. `fix(core): compare file identity on Unix (C14)`.
- [ ] **Antivirus (C4):** add a Kaspersky exclusion for `%TEMP%\SecureFile*` (do not disable it). Stage 8's push failed
  3 times in a row on `...LeavesNothing`-style Core decryption tests because of its upper-case
  `.~OUTPUT.BIN.*.SECUREFILE-TMP.tmp` copies.
- [ ] **Stage 7 real systems (plan 7.4):** the macOS/Linux windows are out as a preview in `v0.1.0-beta2`; collect tester
  answers to the checklist in the release notes (or run it yourself on a Mac and a Linux desktop, backlog C19/C21), then
  mark 7.4 and decide whether to drop the "preview" label (`docs/RELEASING.md`).
- [ ] **Stage 6:** Windows Sandbox run (`docs/SANDBOX_TEST.md`), ADR-15 decision, screenshots
  (`docs/SCREENSHOTS_NEEDED.md`). The beta2 release notes are now also on `main` (CHANGELOG, RELEASING).
- [ ] **Stage 5** (`docs/QUALITY_REPORT.md`, "Things only the user can do"): cold start, VS profiler, SonarQube, class
  coupling, decision C1 (compression 134 -> 65 MB), optional Native AOT size.
- [ ] **Usability (C6):** a person runs `docs/TESTPLAN.md` and the friends session in `docs/USABILITY.md`.
- [ ] Decisions: C7 (code signing costs money), C9 (public repository for CodeQL and branch protection).

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9.0 "Backlog
burn-down" (plan row 9.0) in this one chat, then open the pull request. Do not read docs/PROJECT_PLAN.md,
docs/LEARNING_LOG.md or docs/REVIEW_REPORT_2026-11-24.md in full: grep the rows or item numbers you need (numbers like #9
are review-report items, B/C ids are in docs/REVIEW_BACKLOG.md). Terse chat updates.

STEP 0: git status, git fetch --prune. Branch feature/stage-9-burndown from main (check the docs/stage-9-handoff PR is
merged; if not, branch from it and target it). Build and run all tests first.

COMMITS: many small commits, not one per stage. One commit per item below (or the item and its test together), with the
item id in the message, e.g. `fix(core): compare file identity on Unix (C14)`, so every change traces to its backlog line.
Docs updates for an item go in that item's commit; the wrap-up docs get their own commit.

KNOWN ENVIRONMENT: Windows 11, Kaspersky makes backup copies of temp files (about 1 full test run in 4, sometimes several
in a row, fails a "...LeavesNothing" test with an upper-case `.~OUTPUT.*.SECUREFILE-TMP.tmp`: capture the name, re-run);
never disable the antivirus and never weaken those tests. Scripts need `powershell -ExecutionPolicy Bypass -File ...` or
pwsh, run from PowerShell, not Git Bash; capture full output to artifacts/*.log so failing test names are not cut off.
No Mac, no Linux PC, WSL has no distribution, no ARM machine, no C++ build tools. CI runs Windows, Ubuntu and macOS.

FIX NOW (each with a test where it makes sense; order = risk first):
 1 C14  same-file check on Linux/macOS: a directory symlink or case alias of the input can pass and the rename replaces the
        input. Compare file identity (resolve links; case-insensitive volumes), add tests that run on the Ubuntu/macOS CI.
 2 C16  Windows: owner-only ACL on the temp file and result (the Unix side already sets UnixCreateMode owner-only).
 3 #29  error log: no full paths and no stack trace for expected failures (wrong password, tampered file); keep crashes.
 4 #24  WPF visible password box: UndoLimit=0; "Show password" resets for the next file.
 5 C17  docs/CLI_README.md: Windows PowerShell 5.1 pipes non-ASCII as `?`; recommend pwsh or
        `$OutputEncoding = [Text.UTF8Encoding]::new()`.
 6 #9   WPF mnemonics `_Password` / `Co_nfirm` must target the visible box when "Show password" is on.
 7 #17/#31  drop zone text "Drop a file here or Browse…", drag-over highlight, first-run text not repeated three times
        (WPF and Avalonia).
 8 #12 (rest)  a dropped folder says it is a folder; several dropped files say only one is used; no copy cursor while busy.
 9 #16/#33  file card: tooltip with full name, folder, last-modified date, exact size.
10 #15  error dialogs name the log file (`FileErrorLog.LogFilePath`) and have the main window as owner.
11 #11/#30, #7  Enter in the first password box moves to Confirm; Cancel focused while working; focus after a cancel.
12 #25 (rest)  success message names the folder and makes clear the unencrypted original is still there.
13 C15  deterministic test of AtomicFileWriter.TryDeleteFile retry + Core test of the same-file branch.
14 C18  inject TimeProvider into MainViewModel's progress throttle; add the 100 ms boundary, second-operation, late-report tests.
15 B5   architecture rule: SecureFile.Cli references no UI or network assemblies.
16 B6   PROJECT_PLAN §11.1 and FileErrorLog.LogFilePath remarks match the app; review-report statuses updated for every item fixed here.
IF TIME: #18 (count text elements, not UTF-16 units, for the 8 minimum and 1,024 limit), #19, #21, #22, #28, #34 free-disk-space
check, B3 code part (live-region announcements, automation names). LATER (do not do; keep in docs/SUGGESTIONS.md): #6/#34 format
v2, Argon2id, #2 startup sweep, #35, #36-#38, C13, C19-C21, C2, C6, C1/C7/C9 decisions.

For every item: update its line in docs/REVIEW_BACKLOG.md and the review report status, and the matching row in
docs/SUGGESTIONS.md (built / not built). Avalonia shares the ViewModel: check each Presentation change there too.

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; complexity <= 10,
maintainability >= 60; ConfigureAwait(true) in Presentation, (false) in Core; ADR-13 exception rules; edit with Edit/Write (no
Python or heredoc rewriting of source); `dotnet format SecureFile.slnx whitespace` after creating files; Conventional Commits, first
line <= 72 characters, NO AI attribution lines; never commit secrets, big binaries or artifacts/; ask before any outward-facing
action. The pre-push hook runs scripts/verify.ps1 on `git push`: do not run it by hand per commit. Wrap-up: append ONE short
"Stage 9.0" section to docs/LEARNING_LOG.md (append only, do not read it), CHANGELOG lines, plan row 9.0 marked honestly (amber for
anything waiting on the user), replace this file with the hand-off for 9.1-9.3 (full review of all PRs, code and logic against the
Definition of Done and §15, Stryker on Presentation and CLI, FINAL_GUIDE.md, presentation with v2 / future-releases roadmap), one
reviewer agent on the whole diff, then the PR and the ccd_pr tools.
~~~

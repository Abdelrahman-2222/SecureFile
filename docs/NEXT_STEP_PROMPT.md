# Next step: copy-paste prompt for a new chat (Stage 9: final deliverables)

Last finished (2026-11-25): Stage 8 (stakeholder suggestions), plan row 8.1, on branch `feature/stage-8-suggestions` (PR
against `main`). `docs/SUGGESTIONS.md` now lists what is built (amber where only CI has run it) and 36 new suggestions
with priority, effort and backlog reference. Stage 9 is meant to run **after everything is tested and confirmed**, so the
user checks below should come first; 9.1 will mark any that are still open.

## Things only the user can do (still open)

- [ ] **Stage 7 real systems (plan 7.4):** macOS 14+ (`pwsh scripts/publish-desktop.ps1 -Runtimes osx-arm64`, follow
  `docs/DESKTOP_README.md`) and a Linux desktop (`linux-x64`; or WSL with `wsl --install -d Ubuntu`). Check: the window
  opens, Browse, drag and drop, encrypt with password + confirm, Save over an existing file asks once (backlog C19), Open
  folder, F1/About, close while encrypting asks first, decrypt, a file encrypted on Windows decrypts there. Then mark 7.4
  and decide whether macOS/Linux go into a release (`docs/RELEASING.md`).
- [ ] **Stage 6:** Windows Sandbox run (`docs/SANDBOX_TEST.md`; also a Definition of Done item), ADR-15 decision,
  screenshots (`docs/SCREENSHOTS_NEEDED.md`), any GitHub release (branch `release/v0.1.0-beta2` has one docs commit,
  e71adc1, that is not on `main`).
- [ ] **Stage 5** (`docs/QUALITY_REPORT.md`, "Things only the user can do"): cold start, VS profiler, SonarQube, class
  coupling, decision C1 (compression 134 -> 65 MB), optional Native AOT size.
- [ ] **Usability:** a person runs `docs/TESTPLAN.md` and the friends session in `docs/USABILITY.md` (backlog C6).
- [ ] 9.4 demo video (2 minutes) can only be recorded by the user.

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9 "Final
deliverables" (plan rows 9.1-9.3; 9.4 is the user's video) in this one chat, then open the pull request. Do not read
docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep the rows or sections you need (Stage 9 rows, Definition of Done,
§14 and §15). Terse chat updates.

STEP 0: git status, git fetch --prune. If the Stage 8 PR (feature/stage-8-suggestions) is merged, branch
feature/stage-9-deliverables from main; otherwise from feature/stage-8-suggestions and target that branch (check it still
exists on origin first). Build and run all tests first. Ask the user which "Things only the user can do" in
docs/NEXT_STEP_PROMPT.md are now done, and record the answers.

KNOWN ENVIRONMENT: Windows 11, Kaspersky kills programs that write many large files (about 1 full test run in 4 fails with a
"...LeavesNothing" test: capture the name, re-run); never disable the antivirus. Scripts need `powershell -ExecutionPolicy Bypass
-File ...` or pwsh, run from PowerShell, not Git Bash. No Mac, no Linux PC, WSL has no distribution, no ARM machine, no C++
build tools (so no Native AOT).

9.1 Final review against the Definition of Done (plan, under Stage 9) and the traceability matrix (§15): every R1-R6 and
    D1-D7 has a passing test (name it), 0 warnings, 0 vulnerable packages, §8 targets met or the gap explained (use
    docs/QUALITY_REPORT.md §5.5). Tick only what is proven; anything waiting on the user stays open and amber.
9.2 docs/FINAL_GUIDE.md (§14.1): the detailed explanation guide, so the user can explain and defend every file and line:
    the why and the alternatives refused, linking the ADRs, THREAT_MODEL, QUALITY_REPORT and SUGGESTIONS instead of copying them.
9.3 Team Lead presentation (§14.2), built around the six grading criteria, with a Q&A preparation section; use
    docs/SUGGESTIONS.md §5 as the story. Ask the user which format (pptx file or a slides artifact) before building it.
    No screenshots are invented: leave clearly marked placeholders for the ones in docs/SCREENSHOTS_NEEDED.md.

CARRY-OVER (keep listed, do the cheap ones if time allows):
- Backlog: C13, C14, C15, C16 (second half), C17 (document PowerShell 5.1 piping in docs/CLI_README.md: cheap), C18, C19,
  C20, B1 WPF items, B3/B4, C2/C6. docs/SUGGESTIONS.md maps the user-visible ones to a stakeholder line (C15 is a test
  gap only, so it is not there).
- Stryker on the CLI's pure logic (C22) and on Presentation (C18) was never run.
- One unexplained flaky test failure from the antivirus: hunt it in final hardening (capture the test name).

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; edit with Edit/Write
(no Python or heredoc rewriting of source); Conventional Commits, first line <= 72 characters, NO AI attribution lines; never commit
secrets, big binaries or artifacts/; ask before any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`:
do not run it by hand per commit. Wrap-up: append ONE short "Stage 9" section to docs/LEARNING_LOG.md (append only, do not read it),
CHANGELOG lines, plan rows marked honestly (amber for anything waiting on the user), replace this file with the final hand-off
(what is left for the user only), one reviewer agent on the whole diff, then the PR and the ccd_pr tools.
~~~

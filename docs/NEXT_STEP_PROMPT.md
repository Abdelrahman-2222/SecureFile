# Next step: copy-paste prompt for a new chat (Stage 9.1-9.3: final review, guide, presentation)

Last finished (2026-11-25): Stage 9.0 (backlog burn-down), plan row 9.0, branch `feature/stage-9-burndown`: 17 "fix now"
items (C14, C16, #29, #24, C17, #9, #17/#31, #12, #16/#33, #15, #11/#30, #7, #25, C15, C18, B5, B6), one commit each;
646 tests green on Windows. The "if time" items (#18, #19, #21, #22, #28, #34, B3 code part) were not started and stay in
`docs/REVIEW_BACKLOG.md`. Row 9.0 is amber until the user's checks below are done.

## Things only the user can do (still open)

- [x] Merge the Stage 9.0 PR (`feature/stage-9-burndown`): done 2026-11-25 (PR #9, squash), CI green on Windows,
  Ubuntu and macOS.
- [ ] Run the new TESTPLAN rows by hand on the WPF app: B8-B10 (Alt mnemonics with Show password, Cancel focus, Esc
  with no operation), E2-E6 (folder, several files, drag highlight, busy cursor, file-card tooltip), H2 (log has no paths).
- [x] Decided 2026-11-25: **no** "Encrypt anyway?" dialog for a Weak password (the warning is already beside the field;
  a confirm dialog is clicked through by habit). Weak stays an amber caution. Follow-up is backlog C24: refuse passwords
  on the common-password list, like Too short (`docs/SUGGESTIONS.md` §4.2). Do not reopen the dialog question.
- [ ] **Final tester release (user request, 2026-11-25):** after 9.1-9.3, publish the finished build as three separate
  per-OS packages named `SecureFile-beta2-for-testers` for Windows, Linux and macOS, each with notes only for that
  system. Ask the user before publishing (outward-facing).
- [ ] **Antivirus (C4):** add a Kaspersky exclusion for `%TEMP%\SecureFile*` (do not disable it).
- [ ] **Stage 7 real systems (plan 7.4):** collect tester answers for the macOS/Linux preview (C19/C21), then decide on
  the "preview" label (`docs/RELEASING.md`).
- [ ] **Stage 6:** Windows Sandbox run (`docs/SANDBOX_TEST.md`), ADR-15 decision, screenshots (`docs/SCREENSHOTS_NEEDED.md`).
- [ ] **Stage 5** (`docs/QUALITY_REPORT.md`): cold start, VS profiler, SonarQube, class coupling, decision C1.
- [ ] **Usability (C6):** a person runs `docs/TESTPLAN.md` and the friends session in `docs/USABILITY.md`.
- [ ] Decisions: C7 (code signing), C9 (public repository for CodeQL and branch protection).

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9.1-9.3
(plan rows 9.1, 9.2, 9.3) in this chat, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md
in full: grep the rows or sections you need (§13 Definition of Done, §14 deliverables, §15). Terse chat updates.

STEP 0: git status, git fetch --prune. Check that the Stage 9.0 PR (feature/stage-9-burndown) is merged; branch
feature/stage-9-final from main (if not merged, branch from it and target it). Build and run all tests first.

COMMITS: many small commits, one per finding fixed or per document, Conventional Commits, first line <= 72 characters,
NO AI attribution lines.

9.1 FINAL REVIEW: review every merged PR (gh pr list --state merged), the code and the logic against the Definition of
Done and §15. Use reviewer agents in parallel, one per area (Core crypto and file handling; Presentation ViewModel;
WPF + Avalonia windows; CLI; tests and CI/scripts; docs consistency). Each reports findings with file:line, severity and
evidence; verify each yourself before fixing. Fix what is real (one commit each, with a test where it makes sense); record
the rest in docs/REVIEW_BACKLOG.md. Run Stryker on Presentation (C18 rest) and on the CLI's pure logic (C22); record the
scores in docs/QUALITY_REPORT.md; kill surviving mutants with tests or justify them with a Stryker comment.
9.2 docs/FINAL_GUIDE.md (§14.1): the detailed explanation document the user must be able to defend line by line: design,
crypto format, threat model, every ADR with the alternatives, testing strategy, what is built and what is not.
9.3 Team Lead presentation (§14.2): a slide deck (ask which format: .pptx or a slides artifact) with Q&A preparation and a
v2 / future-releases roadmap built from docs/SUGGESTIONS.md (format v2 with chunked authentication and a key check,
Argon2id, startup sweep, passphrase generator, folder/batch, Explorer/Finder integration, code signing).

KNOWN ENVIRONMENT: Windows 11, Kaspersky makes backup copies of temp files (about 1 full test run in 4 fails a
"...LeavesNothing" test with an upper-case `.~OUTPUT.*.SECUREFILE-TMP.tmp`: capture the name, re-run); never disable the
antivirus and never weaken those tests. Scripts need `powershell -ExecutionPolicy Bypass -File ...` or pwsh; capture full
output to artifacts/*.log. No Mac, no Linux PC, no ARM machine. CI runs Windows, Ubuntu and macOS.

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; complexity <= 10,
maintainability >= 60; ConfigureAwait(true) in Presentation, (false) in Core; ADR-13 exception rules; edit with Edit/Write;
`dotnet format SecureFile.slnx whitespace` after creating files; never commit secrets, big binaries or artifacts/; ask before
any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`. Wrap-up: append ONE short "Stage 9.1-9.3"
section to docs/LEARNING_LOG.md (append only), CHANGELOG lines, plan rows marked honestly, replace this file with the
hand-off for 9.4 (demo video) and the release, then the PR and the ccd_pr tools.
~~~

# Next step: copy-paste prompts for new chats (Stage 9.0b production gaps, then 9.1-9.3 final review, guide, presentation)

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

Order: **Prompt A (Stage 9.0b, production gaps) first**, then **Prompt B (Stage 9.1-9.3)**. 9.1 reviews the finished code, so
the last code change (C24) lands before it. Tester answers for macOS/Linux (C19/C21) come in whenever they arrive; the user
brings them to a chat and they are recorded then.

### Prompt A: Stage 9.0b (C24 common passwords, ADR-15 decision, clean-machine and manual test kit)

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9.0b (plan
row 9.0b) in this chat, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep
the rows or sections you need. Terse chat updates.

STEP 0: git status, git fetch --prune, branch feature/stage-9-production-gaps from main. Build and run all tests first.

COMMITS: one per item, Conventional Commits, first line <= 72 characters, NO AI attribution lines.

A1. C24 REFUSE COMMON PASSWORDS (docs/REVIEW_BACKLOG.md C24; decided 2026-11-25, do not reopen the "Encrypt anyway?"
dialog). Today src/SecureFile.Core/Passwords/PasswordPatterns.cs (CommonWords, repeats, runs) caps a password at Weak,
and Weak is allowed. Change: a password that is on the common list (or is only a repeat or a run) gets a new blocking
rating, e.g. PasswordStrength.Common, treated like TooShort: Encrypt is disabled and the message says why and what to do
("a few unrelated words"). A Weak rating from a low score (not on the list) stays an amber caution and stays allowed.
Grow the list to a few hundred entries from a public, licence-compatible source (state the source and licence in the XML
remarks; small embedded resource, no download at run time), plus common Arabic-keyboard/transliterated ones. Rules:
- Encryption only. Decrypt NEVER refuses a password (old files made with a common password must still open). Test this.
- Same rule in all three front ends: ViewModel (WPF and Avalonia share it; check Avalonia's strength colours and the
  WPF DataTriggers: red for Common, like TooShort) and the CLI (encrypt exits with the documented "password rejected"
  code and message; check docs/CLI_README.md exit-code table).
- No file-format change; the minimum length stays 8.
- Tests: Core (theory with list entries, look-alikes such as P@ssw0rd, case, an unusual weak password stays Weak, a strong
  one is unaffected), ViewModel (Encrypt disabled + message for Common; Decrypt allowed), CLI (exit code). Update
  docs/THREAT_MODEL.md (weak-password row), docs/SUGGESTIONS.md row, REVIEW_BACKLOG C24, TESTPLAN rows for it, CHANGELOG.
A2. ADR-15 DECISION (docs/adr/0015-installer-and-code-signing.md, today "Proposed"). Ask the user with one question:
(1) Recommended: Accept "zip + SHA-256, unsigned for beta/testers; sign before any public 1.0 release (Azure Artifact
Signing first, OV cloud-HSM fallback)"; (2) buy signing now; (3) keep it Proposed. Record the answer: status, date,
consequences; update README "Windows protected your PC" and docs/RELEASING.md if anything changes; C7 in the backlog.
Do not register, buy or upload anything.
A3. CLEAN-MACHINE + MANUAL TEST KIT (the user runs it; you prepare and then record). Check scripts/sandbox-test.wsb and
docs/SANDBOX_TEST.md still match the current publish output (paths, zip names); fix them. Then give the user ONE short,
ordered checklist in chat: the Windows Sandbox run, the TESTPLAN rows still open (B8-B10, E2-E6, H2 and the new C24 rows),
and the screenshots from docs/SCREENSHOTS_NEEDED.md taken in the same session. Do not mark anything passed yourself.
When the user reports results, record them in docs/TESTPLAN.md (sign-off) and docs/SANDBOX_TEST.md, file any bug found
as a backlog item (fix it now if small), and update plan rows 6.2, 6.3, 9.0 and 9.0b honestly.

KNOWN ENVIRONMENT: Windows 11, Kaspersky makes backup copies of temp files (about 1 full test run in 4 fails a
"...LeavesNothing" test with an upper-case `.~OUTPUT.*.SECUREFILE-TMP.tmp`: capture the name, re-run); never disable the
antivirus and never weaken those tests. Scripts need `powershell -ExecutionPolicy Bypass -File ...` or pwsh; capture full
output to artifacts/*.log. No Mac, no Linux PC, no ARM machine. CI runs Windows, Ubuntu and macOS. Windows Sandbox needs
the "Windows Sandbox" feature (Windows 11 Pro has it).

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; complexity <= 10,
maintainability >= 60; ConfigureAwait(true) in Presentation, (false) in Core; ADR-13 exception rules; edit with Edit/Write;
`dotnet format SecureFile.slnx whitespace` after creating files; never commit secrets, big binaries or artifacts/; ask before
any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`. Wrap-up: append ONE short "Stage 9.0b"
section to docs/LEARNING_LOG.md (append only), CHANGELOG lines, plan row 9.0b marked honestly (amber while the user's
manual results are missing), in this file remove Prompt A and keep Prompt B, then the PR and the ccd_pr tools.
~~~

### Prompt B: Stage 9.1-9.3 (final review, guide, presentation)

Paste this into a new chat after Prompt A's PR is merged:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9.1-9.3
(plan rows 9.1, 9.2, 9.3) in this chat, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md
in full: grep the rows or sections you need (§13 Definition of Done, §14 deliverables, §15). Terse chat updates.

STEP 0: git status, git fetch --prune. Check that the Stage 9.0b PR (feature/stage-9-production-gaps) is merged; branch
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

# Next step: copy-paste prompts for new chats (Stage 9.0b production gaps, then 9.4 demo video and the release)

Last finished (2026-11-25): Stage 9.1-9.3, plan rows 9.1-9.3, branch `feature/stage-9-final`. 9.1: six reviewer agents
(Core, ViewModel, windows, CLI, CI/tests, docs) found 34 items, none High; 31 fixed with one commit each, 3 (CORE-3, UI-3, UI-4)
recorded in `docs/REVIEW_BACKLOG.md` §F. Stryker: Presentation 83.06 % -> 100 %, CLI pure logic 82.86 % -> 100 %
(`docs/QUALITY_REPORT.md` §9.1). 9.2: `docs/FINAL_GUIDE.md`. 9.3: `docs/SecureFile_TeamLead.pptx` (17 slides, Q&A in
the speaker notes, v2 roadmap). 711 tests, 0 failing on Windows (9 skipped: UI opt-in, large files, Linux/macOS only).

## Things only the user can do (still open)

- [ ] Merge the Stage 9.1-9.3 PR (`feature/stage-9-final`) after CI is green on Windows, Ubuntu and macOS. Check in that
  CI run: the new WPF FlaUI step passes, and the Linux/macOS-only tests ran (device file `/dev/zero`, folder links).
- [ ] Narrator check (UI-2): with Narrator on, a status change (wrong password, success) is read out once.
- [ ] Rehearse the deck once with the speaker notes; add the three demo screenshots (`docs/SCREENSHOTS_NEEDED.md`).
- [ ] Run the new TESTPLAN rows by hand on the WPF app: B8-B10, E2-E6, H2 (and A19-A22 split in Stage 9.1).
- [ ] **Antivirus (C4):** add a Kaspersky exclusion for `%TEMP%\SecureFile*` (do not disable it).
- [ ] **Stage 7 real systems (plan 7.4):** collect tester answers for the macOS/Linux preview (C19/C21).
- [ ] **Stage 5** (`docs/QUALITY_REPORT.md`): cold start, VS profiler, SonarQube, class coupling, decision C1.
- [ ] **Usability (C6):** a person runs `docs/TESTPLAN.md` and the friends session in `docs/USABILITY.md`.
- [ ] Decisions: C7 (code signing, with ADR-15 in Prompt A), C9 (public repository for CodeQL and branch protection).

**Releases are kept (user decision, 2026-11-25):** `v0.1.0-beta1` (first friends build) and `v0.1.0-beta2` stay published
on GitHub, with their tags and zips, as the record of progress. Every later build is a new release next to them; never
delete, edit or reuse an earlier tag or release, and never overwrite an earlier zip in `artifacts/beta`.

Order: **Prompt A (Stage 9.0b)** after this PR is merged, then **Prompt C (9.4 and the release)**, because the release
must contain C24.

### Prompt A: Stage 9.0b (C24 common passwords, ADR-15 decision, clean-machine and manual test kit)

Paste this into a new chat:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line; layers Wpf/Avalonia/Cli -> Presentation -> Core. Task: Stage 9.0b (plan
row 9.0b) in this chat, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep
the rows or sections you need. Terse chat updates.

STEP 0: git status, git fetch --prune. Check that the Stage 9.1-9.3 PR (feature/stage-9-final) is merged; branch
feature/stage-9-production-gaps from main. Build and run all tests first. Also update docs/FINAL_GUIDE.md and the
presentation's password slide for C24 (they were written before it).

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
manual results are missing), in this file remove Prompt A and keep Prompt C (9.4 and the release), then the PR and the
ccd_pr tools.
~~~

### Prompt C: Stage 9.4 (demo video) and the final tester release

Paste this into a new chat after Prompt A's PR is merged:

~~~
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app (Windows),
Avalonia app (macOS/Linux, bonus), command line. Task: plan row 9.4 (2-minute demo video as a backup for the live demo)
and the final tester release, then the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full:
grep what you need (§14.2 demo flow, row 9.4, release row). Terse chat updates.

STEP 0: git status, git fetch --prune. Check that the Stage 9.0b PR is merged; branch feature/stage-9-release from main.
Build and run all tests first.

C1. DEMO VIDEO (row 9.4). You cannot record the screen: write docs/DEMO_SCRIPT.md, a 2-minute shot list with timings
and the exact words to say (select a PDF -> encrypt -> show the .enc is unreadable -> wrong password: plain message, no
file -> right password -> files compare equal -> large file + Cancel: nothing left). Prepare the sample files with a
script under scripts/ (no binaries committed). Tell the user how to record it (Windows: Win+Alt+R with Xbox Game Bar,
or Clipchamp) and where to keep the video (NOT in the repository; attach it to the release or a shared drive). Mark
row 9.4 done only when the user says the video exists.
C2. FINAL TESTER RELEASE (user request): three per-OS packages named SecureFile-beta2-for-testers-<Windows|Linux|macOS>,
each with notes only for that system (what to install, the SmartScreen/Gatekeeper warning, how to verify SHA-256, what
to test, how to report). Use scripts/publish-*.ps1 with a NEW label (ask the user: e.g. beta3 or rc1); the version comes
from Directory.Build.props. Update docs/RELEASING.md "Release history" and CHANGELOG (move [Unreleased] into the new
version). KEEP every earlier release: v0.1.0-beta1 and v0.1.0-beta2 stay published with their tags and zips; the new
build is a new tag and a new GitHub pre-release next to them. Never delete, edit or reuse an earlier tag, release or zip.
Ask the user before creating the tag or the GitHub release (outward-facing) and show them the notes first.

KNOWN ENVIRONMENT: Windows 11, Kaspersky makes backup copies of temp files (about 1 full test run in 4 fails a
"...LeavesNothing" test with an upper-case `.~OUTPUT.*.SECUREFILE-TMP.tmp`: capture the name, re-run); never disable the
antivirus and never weaken those tests. Scripts need `powershell -ExecutionPolicy Bypass -File ...` or pwsh; capture full
output to artifacts/*.log. No Mac, no Linux PC, no ARM machine. CI runs Windows, Ubuntu and macOS.

RULES: no limit or target is loosened to pass; warnings are errors; XML docs on public and private members; edit with
Edit/Write; `dotnet format SecureFile.slnx whitespace` after creating files; never commit secrets, big binaries, videos
or artifacts/; ask before any outward-facing action. The pre-push hook runs scripts/verify.ps1 on `git push`. Wrap-up:
append ONE short "Stage 9.4 and release" section to docs/LEARNING_LOG.md (append only), CHANGELOG lines, plan rows marked
honestly, replace this file with what is left for the user, then the PR and the ccd_pr tools.
~~~

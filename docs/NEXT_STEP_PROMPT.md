# Next step: copy-paste prompt for a new chat (Phase 5, Stage 5: Quality)

Last finished (2026-11-24): Stage 4 code and docs as far as a machine can take them, an independent review (everything open
is in `docs/REVIEW_BACKLOG.md`), the Windows friends build (GitHub pre-release `v0.1.0-beta1`, Windows only), a command-line
version (built, not released), and a release branch `release/v0.1.0-beta1`. Branch now: `feature/stage-4-testing`
(its pull request to `main` is **not opened yet**: it waits for the user's TESTPLAN run and the friends' feedback).

**Model and effort for the next chat:** **Sonnet 5.5, effort medium** (long measuring and test-writing, token budget matters).
Switch that one chat to **Opus 5.5, effort high** only for steps 5.3 (reading mutation survivors) and 5.4 (the threat model and
the security review) if the survivors or findings need deeper reasoning. Start a NEW chat: the previous one was very long.

```text
Follow CLAUDE.md (read it first). Project: SecureFile, AES-256-CBC + HMAC file encryption, .NET 10, WPF app + a command-line
version; layers Wpf/Cli -> Presentation -> Core. Task: do the WHOLE of Stage 5 "Quality" (plan rows 5.1-5.6) in this one chat,
one commit per step, then open the pull request. Do not read docs/PROJECT_PLAN.md or docs/LEARNING_LOG.md in full: grep the
rows/sections you need (plan §8 "Measurements and targets", §1.7 cadence, G12/G13, rows 5.1-5.6). Terse chat updates.

STEP 0 - Branch and baseline
- `git status`, `git fetch`. If the Stage 4 PR (branch feature/stage-4-testing) is already merged into main: `git switch main`,
  pull, create `feature/stage-5-quality`. If NOT merged: create `feature/stage-5-quality` from feature/stage-4-testing and
  open the Stage 5 PR against feature/stage-4-testing (retarget to main after the Stage 4 PR merges).
- `dotnet build SecureFile.slnx -c Release` and `dotnet test --solution SecureFile.slnx -c Release --no-build`. Record the
  reference machine (CPU, RAM, disk type, Windows build, .NET SDK) for the report.
- KNOWN ENVIRONMENT PROBLEM: Kaspersky is installed. It (a) kills test/benchmark executables that write and encrypt many large
  files, and (b) creates backup copies named like `.~OUTPUT.ENC.<id>.SECUREFILE-TMP.tmp` in test folders, so about 1 full test
  run in 4 fails with a different "...LeavesNothing" test. If the pre-push gate fails on such a test, re-run it (capture the
  test name first). If a tool is killed, say so and ask the user to exclude the folder; never disable the antivirus yourself.
- Create `docs/QUALITY_REPORT.md` now with sections: Reference machine, 5.1 Performance (benchmarks), 5.2 Startup/memory/UI,
  5.3 Code metrics/coverage/mutation, 5.4 Security and threat model, 5.5 Summary table (target / measured / verdict), and
  "Things only the user can do" (a checklist you keep updating). Fill sections as you go. Commit raw numbers, never big binaries.

STEP 5.1 - BenchmarkDotNet (plan §8.2)
- New project `benchmarks/SecureFile.Benchmarks` (console, net10.0, add BenchmarkDotNet to Directory.Packages.props; keep the
  build warnings-as-errors rules; if a rule must be relaxed for benchmark code, do it with a scoped NoWarn + reason, and keep
  the project OUT of the pre-push gate's test run). Add `scripts/run-benchmarks.ps1`.
- Measure: encrypt and decrypt throughput (MB/s) for 1 MB, 100 MB and (behind a switch) 1 GB against a baseline of a plain
  buffered file copy on the same disk (target: >= 70 % of the copy); key derivation time p50/p95 (PBKDF2 600,000 iterations,
  target "reported, about 0.3-1 s"); allocations per operation with MemoryDiagnoser (target: no per-chunk buffer allocation).
- Write the tables into QUALITY_REPORT.md with the raw BenchmarkDotNet markdown under docs/quality/benchmarks/. If a target is
  missed, explain why or fix the hot spot (profile before changing code; keep all tests green).
- Commit: `perf(bench): add BenchmarkDotNet suite and first results`.

STEP 5.2 - Startup, memory and UI responsiveness (plan §8.2)
- Cold/warm "time to first window": write `scripts/measure-startup.ps1` (launch the Release exe 10x, poll until the main window
  exists, report median/p95; first run after a reboot or after clearing the file cache counts as cold; target < 1.5 s cold,
  < 0.5 s warm). Measure the published single-file exe (`scripts/publish-beta.ps1`) as well as the plain build.
- Memory vs file size: `dotnet-counters`/`dotnet-gcdump` (already in dotnet-tools.json) while encrypting 1 MB, 100 MB, 1 GB:
  working set must stay flat (< 150 MB). Reuse `scripts/large-file-check.ps1` figures (1 GiB: 96/100 MB/s, 46 MB peak).
- Progress/UI thread: check from the code and a trace (`dotnet-trace`) that progress reports are throttled (<= 10 per second)
  and nothing blocks the UI thread > 50 ms. Anything needing the Visual Studio Performance Profiler (Alt+F2) or WPA is a USER task:
  write the exact click-by-click instructions in QUALITY_REPORT.md ("Things only the user can do") and continue.
- Published size per architecture (win-x64 app, CLI archives): record; try `EnableCompressionInSingleFile` on the WPF build and
  report the size and startup cost of each option (backlog item C1). Do not change the release defaults without telling the user.
- Commit: `perf: startup, memory and size measurements`.

STEP 5.3 - Metrics, coverage, mutation (plan §8.3, §8.4, G12)
- Code metrics: CA1502/CA1505 already fail the build; additionally export the numbers (Microsoft.CodeAnalysis.Metrics or the
  equivalent) for Core, Presentation, Cli, Wpf: worst complexity, lowest maintainability index, class coupling. Table in the report.
- Coverage: run `scripts/verify.ps1` once (it produces the cobertura/HTML report) and record Core lines/branches (targets >= 90 % /
  >= 85 %) and Presentation (>= 85 %, plan row 2.5). Add the missing tests if a target is missed.
- Mutation: `dotnet dotnet-stryker` (tool 5.1.0 is in dotnet-tools.json; there is no config yet: create `stryker-config.json`
  targeting src/SecureFile.Core with tests/SecureFile.Core.Tests, reports in artifacts/stryker). Run the FULL Core run once in the
  background (it takes a long time; do not poll, wait for the notification). Target >= 80 % (goal 100 %). For each survivor:
  kill it with a better test, or add `// Stryker disable once <mutator> : <reason>` only when the mutant is equivalent/unobservable.
  Record the score, the survivors you disabled and why. Then run Stryker on src/SecureFile.Cli's pure logic (parser, resolvers) if time allows.
- Commit(s): `test: raise coverage ...`, `test(mutation): kill survivors, record score`.

STEP 5.4 - Security (plan §8.1)
- Evidence already there: .NET security analyzers (build), NuGet audit, gitleaks in CI, local pattern scan in verify.ps1, known-answer
  tests, tamper/truncation tests. Re-run `dotnet list SecureFile.slnx package --vulnerable --include-transitive` and record 0.
- CodeQL needs a public repo (state this in the report). SonarQube for IDE is a USER task (VS extension): add to the user checklist;
  meanwhile record the analyzer results.
- Write `docs/THREAT_MODEL.md` (STRIDE table: asset/threat, category, mitigation (code + test id), residual risk/limit). Cover at
  least: malicious or truncated .enc (size/iteration fields, DoS), tampering and wrong-password oracle, plaintext in temp files and
  after a crash (hidden temp, empty-on-failure, antivirus-held files), password in memory/logs/clipboard/command line (CLI has no
  --password on purpose; "Show password" copy blocked), path tricks (symlinks, same file as output, overwrite), TOCTOU on the
  output, unsigned binaries and antivirus false positives, supply chain (pinned GitHub Actions SHAs, Dependabot, NuGet audit),
  release integrity (SHA-256, no signing yet), algorithm agility (see docs/SUGGESTIONS.md section 2.1). Every threat has a
  mitigation or a stated limit. Update SECURITY.md to match.
- Run ONE independent security review with a subagent (read-only, one pass, brief like the earlier product-owner/tester one:
  malicious file, path, user, secrets in memory/logs/messages, crypto use). Every finding fixed or answered with a recorded decision.
- Commit: `docs(security): threat model and review results` (+ fixes as separate commits).

STEP 5.5 - QUALITY_REPORT.md
- Complete the summary table (target / measured / verdict / evidence), the "reference machine" section, known limits (antivirus
  flake, unsigned binaries, 4.5 GiB run still blocked, Linux/macOS CLI binaries never run on those systems) and the user checklist.
  Screenshots the user must supply are listed, not invented. Commit: `docs: quality report`.

STEP 5.6 - Burn down the review backlog (time-boxed, plan row 5.6)
- Open docs/REVIEW_BACKLOG.md. Do sections B1 and B5 together (a fix and its test, one commit per item), then C1 (already measured in
  5.2) and C3 (strength-check limits). Leave B3/B4 and C2/C6/C8 (they need people or decisions) but make sure they stay listed.
  Update the backlog and docs/REVIEW_REPORT_2026-11-24.md statuses as you fix items. Then run one more independent review (same
  brief as the first: product owner + tester, read-only) on the changed areas and append its new findings to the backlog.

WRAP-UP
- `docs/LEARNING_LOG.md`: append ONE short "Stage 5" section (what was built, key decisions, the review table, the gate table,
  about 5 self-check questions). Do not read the file; append only.
- CHANGELOG lines; plan rows 5.1-5.6 marked with what is really done (use the amber state for anything waiting on the user).
- Replace THIS file (docs/NEXT_STEP_PROMPT.md) with the hand-off for Stage 6 (publishing: win-arm64, Windows Sandbox test,
  installer and signing decision, README) and the carry-over items below.
- One reviewer agent for the stage (G16-G18) on the whole diff; fix or answer every finding.
- The pre-push hook runs scripts/verify.ps1 on `git push`; do not run it by hand per commit. Conventional Commits, first line
  <= 72 characters, NO AI attribution lines. Open the PR (base per STEP 0), then call the ccd_pr tools (get_status, bind_pr if
  needed), offer Auto-fix, never enable auto-merge, never poll CI yourself.
- Final message: the summary table (target / measured / verdict), what is waiting on the user, and the exact commands run.

RULES THAT APPLY TO EVERY STEP
- Do not raise a limit or loosen a target to make something pass; report real findings. Warnings are errors. XML docs on public
  and private members. Edit with the Edit/Write tools (no Python/heredoc rewriting of source files).
- Never commit secrets, big binaries or artifacts/ (it is git-ignored). Ask before any outward-facing action (uploading, releasing).
- If a step is blocked by something only the user can do, write it into the report's user checklist, say so in one line, and
  continue with the next step. Do not stop the whole stage for it.
```

**Carry-over (waiting on the user, not part of Stage 5):**
1. Run `docs/TESTPLAN.md` (sections A to J) by hand on the current build and report the failures; fix each bug (test first where possible).
2. Friends' feedback on `v0.1.0-beta1` (Windows): collect into `docs/USABILITY.md` (Step 4.4), fix or record each finding.
3. Large-file 4.5 GiB run (T-27): blocked by the antivirus killing the test program; needs a Kaspersky exclusion for the test output
   folder and `D:\Abelrahman\Abdelrahman Current\TestFolder`, then `pwsh scripts/large-file-check.ps1 -WorkFolder "<that folder>"`.
4. Then: Stage 4 reviewer + LEARNING_LOG wrap-up and the Stage 4 PR to `main` (if not yet opened).
5. Everything found but not fixed lives in `docs/REVIEW_BACKLOG.md` (burn-down order inside); release process in `docs/RELEASING.md`;
   macOS and Linux releases wait for Steps 7.1 and 7.2.

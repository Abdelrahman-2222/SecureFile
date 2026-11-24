# SecureFile — Quality Report (Stage 5)

Numbers measured on one reference machine; raw outputs are committed under `docs/quality/`. Targets come from
PROJECT_PLAN §8.

## Reference machine

| Item | Value |
|---|---|
| CPU | Intel Core i7-8750H @ 2.20 GHz, 6 cores / 12 threads |
| RAM | 23.8 GB |
| Disks | Lexar NM620 256 GB NVMe SSD (system) and Toshiba MQ04ABF100 1 TB HDD (RAID controller; the work folder `D:\...\TestFolder` is on D:) |
| OS | Windows 11 Pro, build 10.0.22631 |
| .NET SDK | 10.0.404 |
| Antivirus | Kaspersky (see "Known limits") |

Baseline at the start of Stage 5: `dotnet build SecureFile.slnx -c Release` 0 warnings / 0 errors; all tests pass
(494 passed, 8 skipped, 0 failed).

## 5.1 Performance (benchmarks)


Suite: `benchmarks/SecureFile.Benchmarks`, run with `scripts/run-benchmarks.ps1` (BenchmarkDotNet 0.15.8, 8 iterations
after 1 warm-up, work folder on the D: HDD). Raw output: `docs/quality/benchmarks/`. Not part of the pre-push gate.

**Throughput** (MB/s = size / mean time; "Encrypt net" subtracts the 363 ms key derivation):

| Size | Plain copy (OS cache) | Durable copy (fsync) | Crypto only (CPU, no disk) | Encrypt | Decrypt | Encrypt net of KDF |
|---|---|---|---|---|---|---|
| 1 MB | 0.79 ms | 36.5 ms | 5.9 ms | 420 ms | 425 ms | KDF-dominated |
| 100 MB | 53 ms (1,880 MB/s) | 946 ms (106 MB/s) | 518 ms (193 MB/s) | 1,785 ms (56 MB/s) | 1,728 ms (58 MB/s) | 1,422 ms (70 MB/s) |
| 1 GiB | not rerun here (switch `-Include1GB`); `large-file-check.ps1` earlier: 96 / 100 MB/s encrypt / decrypt, 46 MB peak | | | | | |

**Key derivation** (PBKDF2-SHA256, 600,000 iterations + HKDF split): median 363.2 ms, p95 365.6 ms, 1.31 KB allocated.

**Allocations:** `Encrypt` allocates 6.5 KB for 1 MB and 21.5 KB for 100 MB, i.e. about 150 bytes per 1 MiB chunk
(async bookkeeping). No per-chunk buffer: one 1 MiB buffer is reused (a per-chunk buffer would show up as megabytes).

**Verdicts**
- KDF 0.36 s: inside the intended 0.3-1 s. Pass.
- Allocations: no per-chunk buffer. Pass.
- Throughput >= 70 % of a plain copy: **not met against the OS-cache copy** (about 3 %), and only about 53 % of a
  durable copy including the KDF (66 % excluding it). A plain `CopyTo` writes into RAM (the cache) and returns at
  1.9 GB/s, so it measures memory, not the disk; a copy that must reach the disk (the fair comparison, because the
  atomic writer calls fsync before the rename) runs at 106 MB/s on this HDD.
- Where the time goes: pure AES-CBC + HMAC costs 518 ms per 100 MB (CBC encryption is serial by design, about 193 MB/s),
  the durable disk write about 920 ms, and `Encrypt` net of the KDF takes 1,422 ms: roughly the sum of both. The
  encryptor therefore does not overlap CPU work with the final disk flush; the data sits in the cache and fsync pays
  for it at the end. Overlapping them (periodic flush or a write-behind task) could bring encrypt to about the disk
  speed, but adds complexity to the crash-safety path. Decision: not changed in Stage 5 (the plan says profile before
  changing, and the safety path must not be touched late); recorded as backlog item C13 in `REVIEW_BACKLOG.md`.
- Decrypt shows the same shape (verify pass, then decrypt pass, then fsync).
- Caveat: one machine, a spinning disk, Kaspersky scanning writes. On the NVMe the numbers would differ.

## 5.2 Startup, memory, UI

Scripts: `scripts/measure-startup.ps1` (10 launches, polls until the main window handle exists), `scripts/measure-memory.ps1`
(CLI encrypt and decrypt, peak working set). Raw output in `docs/quality/startup-memory.txt`.

**Time to first window** (10 launches each; the first launch is the coldest this session, but the machine was not
rebooted, so it is "first run", not a true cold start):

| Build | First run | Median (warm) | p95 | Target |
|---|---|---|---|---|
| Plain Release build (`bin/Release`) | 1,856 ms | 643 ms | 1,856 ms | cold < 1.5 s, warm < 0.5 s |
| Published single file (`publish-beta.ps1`, 134 MB) | 2,637 ms | 707 ms | 2,637 ms | same |
| Published single file + `EnableCompressionInSingleFile` (65 MB) | 2,269 ms | 908 ms | 2,269 ms | same |

Verdict: **warm start is missed** (about 0.64-0.71 s against 0.5 s); the first run is above 1.5 s on this machine
(i7-8750H, antivirus scanning the exe). The plain build is the fastest. WPF cold start is dominated by the runtime and
WPF itself (about 0.6 s of an empty WPF window is typical); not tuned in Stage 5. A real cold figure (after a reboot) and
a profile of where the 0.6 s goes need the user (checklist below).

**Peak memory versus file size** (CLI, release build; includes process start; round trip verified by SHA-256):

| File | Encrypt | Decrypt | Peak working set |
|---|---|---|---|
| 1 MB | 1.0 s | 0.5 s | 24.7 / 23.5 MB |
| 100 MB | 2.0 s | 2.7 s | 27.5 / 26.8 MB |
| 1 GiB | 14.5 s (71 MB/s) | 12.3 s (83 MB/s) | 27.3 / 28.1 MB |

Verdict: **flat at about 25-28 MB** (target < 150 MB). The 1 GiB curve equals the 1 MB one: the file is streamed.
(`large-file-check.ps1` on the same code measured 46 MB peak for the test host, which also includes xUnit.)

**Progress and the UI thread**
- The engine reports each whole percent, at most 101 times per operation, but nothing limited the rate: a 100 MB file
  finishes in about 1.4 s, so about 70 updates per second reached the screen. That missed the "<= 10 per second"
  target. Fixed in `MainViewModel.OnProgress`: at most one update per 100 ms, 100 % always shown (test
  `Progress_ManyReportsInstantly_ShowsTheFirstAndAlwaysTheFinalOne`). Core still reports every percent (the CLI line and
  Core tests rely on it).
- UI thread: encryption runs inside awaited Core calls that use `ConfigureAwait(false)`, so the work is on thread-pool
  threads; the UI thread only receives the progress callbacks (now at most 10 per second, each only sets two
  properties). Checked by reading the code, not by a trace: a `dotnet-trace` of a running WPF app does not show
  dispatcher stalls in a form that can be checked here. The real check is the Visual Studio profiler (checklist).

**Published size**

| Artifact | Size |
|---|---|
| Windows app `SecureFile.exe`, win-x64, self-contained single file | 133.6 MB (zip 56.2 MB) |
| same with `EnableCompressionInSingleFile` (backlog C1) | 65.0 MB, but median start 908 ms instead of 707 ms (+200 ms) and the first run 2.3 s |
| CLI win-x64 (trimmed) | exe 12.6 MB, zip 5.6 MB |
| CLI linux-x64 / linux-arm64 | exe 14.5 / 13.8 MB, tar.gz 5.9 / 5.6 MB |
| CLI osx-x64 / osx-arm64 | exe 13.9 / 13.1 MB, tar.gz 5.8 / 5.3 MB |
| Windows app win-arm64 | Stage 6 (not built yet) |

C1 finding: compression halves the download-sized file on disk (134 to 65 MB) at the price of about 200 ms more startup.
The release defaults were **not** changed; the decision is the user's (size versus 0.2 s of start).
Note: `publish-cli.ps1` fails to create the archives when started from Git Bash (Git's `tar` cannot read `D:` paths); it
works from PowerShell.

## 5.3 Code metrics, coverage, mutation

**Code metrics** (exported by running the CA1502/CA1505 analyzers with the thresholds temporarily set to 0 and 101, so
that every method reports its value; raw lines are not committed, the summary is `docs/quality/code-metrics.txt`;
limits stay at 10 and 60 in `CodeMetricsConfig.txt`):

| Project | Worst cyclomatic complexity (limit 10) | Lowest maintainability index (limit 60) |
|---|---|---|
| Core | 7 (`PasswordStrengthEvaluator.Evaluate`) | 60 (`FileEncryptionService.DecryptContentAsync`, exactly at the limit) |
| Presentation | 6 (`MainViewModel.OnProgress`, after the throttle) | 61 (`ProgressTextFormatter.Format`) |
| Cli | 5 (`CliArgumentParser.Parse`) | 61 (`PasswordResolver.Resolve`) |
| Wpf | 6 (`AboutWindow.GetVersionText`) | 61 (`App.OnDispatcherUnhandledException`) |

Class coupling and inheritance depth: the analyzer route (CA1506/CA1501) produced no values, so they are **not
exported**; Visual Studio *Analyze > Calculate Code Metrics* gives them (user checklist). Inheritance depth is
visibly small (no class hierarchies besides exceptions).
Note: `DecryptContentAsync` sits exactly on the limit; any growth of that method fails the build (by design).

**Coverage** (`scripts/verify.ps1`, Microsoft code coverage, one run; raw `docs/quality/coverage-*-summary.txt`):

| Assembly | Lines | Branches | Target | Verdict |
|---|---|---|---|---|
| Core | 98.7 % (457 of 463) | 98.6 % (144 of 146) | >= 90 % / >= 85 % | Met |
| Presentation | 95.8 % | about 91 % (168 of 184, derived: combined minus Core) | >= 85 % (plan row 2.5) | Met |
| Cli, Wpf | not instrumented by the gate | | no target | Covered by CLI tests and the FlaUI UI tests; no number |

**Mutation testing** (Stryker.NET 5.1.0, Core, full run; config `tests/SecureFile.Core.Tests/stryker-config.json`, run
from that folder: `dotnet dotnet-stryker -O ../../artifacts/stryker`; raw `docs/quality/mutation-core.txt`):

| Run | Tested | Killed | Survived | Score |
|---|---|---|---|---|
| First run of Stage 5 | 384 | 367 (+1 timeout) | 16 (+2 no coverage) | 95.34 % |
| After this step | 379 | 378 (+1 timeout) | 0 | **100 %** (target >= 80 %) |

The survivors, and what was done:
- **Killed with new tests (13 of 19):** strength rating needs exactly three kinds (`aB3kqzvm` and two more), a run that
  covers exactly half is predictable (`kqabcdeQ7z`), a zig-zag walk is not a run, four different characters repeated are
  predictable, an even number of one kind does not cancel it (the `|=` to `^=` mutant), every word of the common-word list
  is recognised (one theory case per word, 49 mutants), the padding error message, and a file another program holds open
  for writing is still recognised as encrypted.
- **Disabled with `// Stryker disable once` and a written reason (6):** the retry loop bounds and the last pause in
  `AtomicFileWriter.TryDeleteFile` (only observable with a scanner that lets go at an exact moment; backlog C15), the
  `SetLength` failure branch in `TryEmpty`, the `UnauthorizedAccessException` branch of `StartsWithSignature` (needs a file
  that can be listed but not read), and `throwOnEndOfStream` (equivalent: `EndOfStreamException` is an `IOException`
  caught with the same result).
- Total disabled mutants in Core, including those from earlier steps: 207 (each has a comment with its reason).
- Stryker on the CLI's pure logic (parser, resolvers) was **not run** (time-box); recorded as an open item.

## 5.4 Security and threat model

Full STRIDE table: [THREAT_MODEL.md](THREAT_MODEL.md) (20 threats, each with a mitigation, a test or a stated limit).

| Check | Result |
|---|---|
| .NET security analyzers (`AnalysisLevel=latest-all`, warnings are errors) | 0 warnings; the build passes |
| `dotnet list SecureFile.slnx package --vulnerable --include-transitive` (re-run in Step 5.4) | 0 vulnerable packages in all 11 projects |
| gitleaks (CI, full history) and the local pattern scan in `verify.ps1` | "Secrets: none found" in the Stage 5 run |
| Forbidden-pattern scan (catch Exception, empty catch, async void, System.Random, whole-file reads, console output) | clean |
| Known-answer and tamper/truncation tests | pass (part of the 510 tests) |
| CodeQL | **Not run**: needs a public repository (free there; a private repository needs paid GitHub Advanced Security) |
| SonarQube for IDE | **Not run**: Visual Studio extension, user task (checklist) |
| Independent security review (subagent, read-only, one pass) | 8 findings: 1 doc error fixed, 1 CLI crash fixed, 5 recorded as backlog items C14-C17 or accepted with a reason, 1 out-of-scope line added (THREAT_MODEL §4) |

No open high-severity issue. The medium finding (the threat table claimed the tag is checked before any plaintext is
written) was a documentation error: decryption streams unverified plaintext into the hidden temp file and verifies
before the rename. That is now written down as "verify-then-commit"; an extra HMAC-only pass would double the reads.

## 5.5 Summary table

| # | Metric (plan §8) | Target | Measured | Verdict |
|---|---|---|---|---|
| 1 | Throughput vs plain copy (5.1) | >= 70 % of a plain copy | 3 % of the OS-cache copy; 53 % of an fsync copy (66 % without the KDF) on a HDD | **Not met**; explained (copy measures RAM; encrypt = CPU time + disk time, no overlap); backlog C13 |
| 2 | Key derivation p50 / p95 (5.1) | reported, about 0.3-1 s | 363 / 366 ms | Met |
| 3 | Allocations per operation (5.1) | no per-chunk buffer | about 150 B per 1 MiB chunk (bookkeeping), no buffer | Met |
| 4 | Time to first window, warm (5.2) | < 0.5 s | 643 ms build, 707 ms published single file | **Not met** (+0.14 to +0.21 s) |
| 5 | Time to first window, cold (5.2) | < 1.5 s | first run 1.86 s build, 2.64 s published; true cold after reboot not measured | **Not met / not measured** (user checklist) |
| 6 | Peak memory 1 MB / 100 MB / 1 GiB (5.2) | flat, < 150 MB | 25 / 28 / 28 MB | Met |
| 7 | Progress updates (5.2) | <= 10 per second | was about 70 per second; throttled to 10 per second | Met after fix |
| 8 | UI thread blocked (5.2) | never > 50 ms | by code review: work off the UI thread; profiler run pending | **Open** (user checklist, VS profiler) |
| 9 | Published size (5.2) | reported per architecture | WPF win-x64 134 MB (65 MB compressed); CLI 12.6-14.5 MB exe, 5.3-5.9 MB archives; win-arm64 in Stage 6 | Reported |
| 10 | Complexity / maintainability (5.3) | <= 10 / >= 60 | worst 7 / lowest 60 (Core) | Met |
| 11 | Coverage Core (5.3) | >= 90 % lines, >= 85 % branches | 98.7 % / 98.6 % | Met |
| 12 | Coverage Presentation (5.3) | >= 85 % | 95.8 % lines, about 91 % branches | Met |
| 13 | Mutation score Core (5.3) | >= 80 % (goal 100 %) | 100 % (207 disabled mutants, each with a reason) | Met |
| 14 | NuGet vulnerabilities (5.4) | 0 | 0 | Met |
| 15 | Analyzer warnings / secrets (5.4) | 0 | 0 / none | Met |
| 16 | Threat model (5.4) | every threat has a mitigation or a stated limit | 20 threats, all covered | Met |
| 17 | CodeQL, SonarQube for IDE (5.4) | 0 issues | not run (public repo needed; VS extension) | **Open** (user) |
| 18 | Class coupling, inheritance depth (5.3) | reported | not exported | **Open** (user) |

### Known limits

- **Antivirus (Kaspersky):** it scans every write, makes backup copies of temp files in test folders (about 1 full test
  run in 4 fails here with a different `...LeavesNothing` test; re-run) and it kills the test program for the 4.5 GiB
  run (T-27 is blocked; only 1 GiB is measured). The benchmark, startup and memory scripts ran without being killed.
- **Unsigned binaries:** SmartScreen and antivirus warnings; hash-only integrity (Stage 6 decision).
- **Linux and macOS command-line binaries** were built and sized but never run on those systems (backlog C2).
- **One machine, a spinning disk:** every number is from the i7-8750H laptop; results on an SSD or other CPU differ.
- **Cold start** was not measured after a reboot; the figure above is "first run this session".

## Things only the user can do

- [ ] **Cold start (5.2):** reboot, wait 2 minutes, then run `pwsh scripts/measure-startup.ps1 -Runs 1` once and note the number (target < 1.5 s). Optional: `-Runs 10` afterwards for the warm figure.
- [ ] **Visual Studio Performance Profiler (5.2), click by click:** open `SecureFile.slnx` in Visual Studio, set `SecureFile.Wpf` as startup project, set the configuration to **Release**, press **Alt+F2**, tick **CPU Usage** and **.NET Async** (and **.NET Object Allocation** for a second run), press **Start**. In the app choose a 100 MB file, encrypt it, wait for "Done", close the app. In the report open **Top Functions** and the **timeline**: the UI thread must show no block longer than 50 ms during the encryption; note the main-thread time before the window appears. Screenshot the timeline and save it as `docs/quality/screenshots/profiler-cpu.png`.
- [ ] **Windows Performance Analyzer (optional, 5.2):** `wpr -start GeneralProfile`, start the app, `wpr -stop startup.etl`, open in WPA, expand *Computation > CPU Usage (Sampled)*, filter for `SecureFile.exe`, look at the first second.
- [ ] **Class coupling (5.3):** in Visual Studio, Analyze > Calculate Code Metrics for Solution, note the class-coupling and inheritance-depth columns for Core, Presentation, Cli, Wpf (analyzers could not export them).
- [ ] **SonarQube for IDE (5.4):** install the "SonarQube for IDE" extension in Visual Studio (Extensions > Manage Extensions), open the solution, and note vulnerabilities and security hotspots (target: 0 vulnerabilities, hotspots reviewed).
- [ ] **Microsoft Threat Modeling Tool (optional):** the STRIDE table is in `docs/THREAT_MODEL.md`; redraw it in the tool if the course needs the .tm7 file.
- [ ] **Screenshots to supply:** profiler timeline (`docs/quality/screenshots/profiler-cpu.png`), SonarQube result, Task Manager during a 1 GiB encryption. They are not invented here.
- [ ] **Decision C1:** accept `EnableCompressionInSingleFile` (65 MB, about 0.2 s slower start) or keep 134 MB uncompressed.

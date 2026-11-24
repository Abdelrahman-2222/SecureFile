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

_pending_

## 5.4 Security and threat model

_pending_

## 5.5 Summary table

_pending_

## Things only the user can do

- [ ] **Cold start (5.2):** reboot, wait 2 minutes, then run `pwsh scripts/measure-startup.ps1 -Runs 1` once and note the number (target < 1.5 s). Optional: `-Runs 10` afterwards for the warm figure.
- [ ] **Visual Studio Performance Profiler (5.2), click by click:** open `SecureFile.slnx` in Visual Studio, set `SecureFile.Wpf` as startup project, set the configuration to **Release**, press **Alt+F2**, tick **CPU Usage** and **.NET Async** (and **.NET Object Allocation** for a second run), press **Start**. In the app choose a 100 MB file, encrypt it, wait for "Done", close the app. In the report open **Top Functions** and the **timeline**: the UI thread must show no block longer than 50 ms during the encryption; note the main-thread time before the window appears. Screenshot the timeline and save it as `docs/quality/screenshots/profiler-cpu.png`.
- [ ] **Windows Performance Analyzer (optional, 5.2):** `wpr -start GeneralProfile`, start the app, `wpr -stop startup.etl`, open in WPA, expand *Computation > CPU Usage (Sampled)*, filter for `SecureFile.exe`, look at the first second.
- [ ] **Decision C1:** accept `EnableCompressionInSingleFile` (65 MB, about 0.2 s slower start) or keep 134 MB uncompressed.

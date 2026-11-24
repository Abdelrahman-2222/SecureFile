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

_pending_

## 5.3 Code metrics, coverage, mutation

_pending_

## 5.4 Security and threat model

_pending_

## 5.5 Summary table

_pending_

## Things only the user can do

- [ ] _(kept up to date as the stage goes on)_

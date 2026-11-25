# SecureFile

[![CI](https://github.com/Abdelrahman-2222/SecureFile/actions/workflows/ci.yml/badge.svg)](https://github.com/Abdelrahman-2222/SecureFile/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/Abdelrahman-2222/SecureFile?include_prereleases&label=release)](https://github.com/Abdelrahman-2222/SecureFile/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)

**Lock any file with a password, and unlock it again: entirely on your computer.** No account, no upload, no telemetry,
no internet connection.

SecureFile is a desktop app written in C# on .NET 10. It encrypts with **AES-256-CBC** and seals every file with an
**HMAC-SHA256** tag (encrypt-then-MAC), so a wrong password or a single changed bit is detected before anything is written.
The key comes from your password through **PBKDF2-HMAC-SHA256 (600,000 iterations)** and is split into two independent
keys with **HKDF**. Files of any size are streamed with flat memory (about 28 MB for a 1 GiB file).

One engine, four front ends: a **WPF app for Windows**, an **Avalonia window for macOS and Linux** (preview), a
**command line** for all three systems, and the test suite. A file locked on one system opens on the others; CI proves
it on Windows, Ubuntu and macOS for every pull request.

> **Status: pre-release `0.1.0-beta3` (2026-11-25).** The Windows app and the command line are feature-complete and
> tested. The macOS and Linux windows are a **preview**: CI builds and tests them, but no one has used them on a real
> Mac or Linux desktop yet. Please test on copies of your files. What changed: [CHANGELOG.md](CHANGELOG.md).

## Highlights

- **Detects tampering:** a wrong password, a flipped bit, a cut or extended file and a foreign file each get one plain
  message, and no output file is written. Padding is checked only after the tag (no padding oracle).
- **Never half-written:** the result goes to a hidden, owner-only temp file and is moved into place only when complete
  and verified. Cancel, failure or closing the window leaves nothing behind.
- **Stores nothing:** no key, no password, no recovery back door. Key buffers are wiped after use; the error log holds
  no paths and no passwords.
- **Built to be checked:** 712 automated tests, 97.9 % line coverage in the engine, a 100 % mutation score (Stryker) in
  the engine, the ViewModel layer and the CLI's parsing logic, 0 analyzer warnings, and a threat model with a test per threat.

## Download

Get the files from the [**latest release**](https://github.com/Abdelrahman-2222/SecureFile/releases) (`v0.1.0-beta3`).

| You have | The app | Command line |
|---|---|---|
| Windows 10 or 11 (64-bit) | `SecureFile-0.1.0-beta3-win-x64.zip` | `securefile-cli-0.1.0-beta3-win-x64.zip` |
| macOS 14+, Apple chip (**preview**) | `SecureFile-0.1.0-beta3-osx-arm64.tar.gz` | `securefile-cli-0.1.0-beta3-osx-arm64.tar.gz` |
| macOS 14+, Intel (**preview**) | `SecureFile-0.1.0-beta3-osx-x64.tar.gz` | `securefile-cli-0.1.0-beta3-osx-x64.tar.gz` |
| Linux x64 (**preview**) | `SecureFile-0.1.0-beta3-linux-x64.tar.gz` | `securefile-cli-0.1.0-beta3-linux-x64.tar.gz` |
| Linux arm64 (**preview**) | `SecureFile-0.1.0-beta3-linux-arm64.tar.gz` | `securefile-cli-0.1.0-beta3-linux-arm64.tar.gz` |

Each system has a `READ-ME-FIRST-<system>.txt` in the release: how to start it and what to test. More detail:
[Windows](docs/BETA_README.md), [macOS and Linux](docs/DESKTOP_README.md), [command line](docs/CLI_README.md).
Nothing to install: the .NET runtime is inside every download.

**Verify the download.** `SHA256SUMS.txt` in the release lists the SHA-256 of every file. Compare it with yours:

```powershell
Get-FileHash .\SecureFile-0.1.0-beta3-win-x64.zip -Algorithm SHA256   # Windows
```

On macOS use `shasum -a 256 <file>`, on Linux `sha256sum <file>`. If the codes differ, do not run the file. This proves
the file was not damaged or swapped; it cannot prove who made it, because the builds are not code-signed yet.

**"Windows protected your PC" / "cannot be verified".** The builds are not signed yet (see
[ADR-15](docs/adr/0015-installer-and-code-signing.md)). After checking the SHA-256: on Windows click **More info →
Run anyway**; on macOS open **System Settings → Privacy & Security → Open Anyway**. Do not switch your antivirus off;
if it blocks SecureFile, add an exception for the folder.

## Try it in 60 seconds (Windows)

1. Unzip anywhere and double-click **SecureFile.exe**.
2. **Browse…** (or drop a file on the dashed box), type a password of at least 8 characters twice, click **Encrypt file**.
   A new `yourfile.ext.enc` is saved where you choose; the original is never changed or deleted.
3. To open it again: choose the `.enc` file, type the password, click **Decrypt file** and save it under a new name.

Keys: **Ctrl+O** browse, **Enter** next/start, **Esc** cancel, **F1** About.

Command line:

```bash
securefile encrypt report.pdf            # asks for the password twice, hidden; writes report.pdf.enc
securefile decrypt report.pdf.enc -o report-restored.pdf
```

## How it works

```
password ──PBKDF2-SHA256 ×600,000 + random salt──► master key ──HKDF──► encryption key + authentication key
file ──AES-256-CBC (random IV)──► ciphertext ──HMAC-SHA256 over header + ciphertext──► 32-byte tag

.enc file:  "SFEN" | version 1 | iterations | salt (16) | IV (16) | ciphertext | tag (32)
            └──────────────── 41-byte header ────────────────┘
```

Decryption checks the tag first; until it matches, nothing is released. Every field and limit is pinned by tests
(golden files, NIST and RFC test vectors). The full design: [docs/FINAL_GUIDE.md](docs/FINAL_GUIDE.md).

## Limits you should know

- **If you forget the password, the file is lost.** Nobody can reset it, not even the author. Keep the original until
  you have opened the `.enc` once.
- One file at a time (zip a folder first). The 8-character minimum is a rule; the strength bar is advice.
- The `.enc` hides the content, not that the file exists: its size, name and date are visible.
- No secure delete: SecureFile never deletes your original, and on an SSD a normal delete cannot guarantee wiping.
- After a hard crash a hidden temp file (`.~name.<8 hex>.securefile-tmp`) can remain next to the output; delete it by hand.
- Malware already running as you (a keylogger, a memory dump while the app works) is out of scope.
- Not audited by an independent security firm. Full list: [SECURITY.md](SECURITY.md) and
  [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md).

## Architecture

```
SecureFile.Wpf (Windows) ─┐
SecureFile.Avalonia ──────┼──► SecureFile.Presentation (ViewModels, MVVM) ──► SecureFile.Core (crypto, format, files)
SecureFile.Cli ───────────┘                                                    no UI, runs on every OS
```

The dependency rule is enforced by architecture tests, not by trust. Another app can reuse the engine by referencing
`SecureFile.Core` and calling `IFileEncryptionService.EncryptAsync` / `DecryptAsync`.

| Folder | Contents |
|---|---|
| `src/SecureFile.Core` | Encryption, file format, atomic writer, file identity, password strength |
| `src/SecureFile.Presentation` | ViewModels shared by both desktop windows |
| `src/SecureFile.Wpf` | The Windows app |
| `src/SecureFile.Avalonia` | The macOS and Linux window (preview) |
| `src/SecureFile.Cli` | The command line |
| `benchmarks/SecureFile.Benchmarks` | BenchmarkDotNet throughput and key-derivation measurements |
| `tests/` | Unit, integration, architecture, headless Avalonia and FlaUI (WPF) tests |
| `scripts/` | Quality gate (`verify.ps1`), publishing, cross-OS golden files, measurements |
| `docs/` | Guide, decisions (ADRs), threat model, quality report, test plan, roadmap |

## Build and test

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned in `global.json`). The WPF app and
its UI tests need Windows; everything else builds and tests on macOS and Linux too (CI does both).

```powershell
dotnet tool restore
git config core.hooksPath .githooks          # once per clone: the pre-push hook runs the quality gate
dotnet build SecureFile.slnx -c Release
dotnet test --solution SecureFile.slnx -c Release
pwsh -File scripts/verify.ps1                # the full gate: format, build, tests, coverage, audit, secrets, rules
```

The WPF end-to-end tests drive the real window and take over the keyboard, so they run only with
`SECUREFILE_UI_TESTS=1`. Release builds: `scripts/publish-beta.ps1` (Windows app), `scripts/publish-desktop.ps1`
(macOS and Linux), `scripts/publish-cli.ps1` (command line); the process is in [docs/RELEASING.md](docs/RELEASING.md).

## Quality

| Check | Result |
|---|---|
| Automated tests | 712, 0 failing (Windows; CI also runs Ubuntu and macOS) |
| Engine coverage | 97.9 % lines, 95.8 % branches (gate: ≥ 90 % / ≥ 85 %) |
| Mutation score (Stryker.NET) | 100 % in Core, Presentation and the CLI's parsing logic |
| Analyzers | All .NET rules on, warnings are errors, complexity ≤ 10 and maintainability ≥ 60 per method |
| Supply chain | No vulnerable packages; a secret scan in the pre-push gate and gitleaks in CI |

Measurements and how they were taken: [docs/QUALITY_REPORT.md](docs/QUALITY_REPORT.md).

## For reviewers

Start with [docs/FINAL_GUIDE.md](docs/FINAL_GUIDE.md): design, file format, threat model, every decision with the
alternatives, the testing strategy, and what is built and what is not. Then:

- [Architecture decision records](docs/adr/README.md)
- [Threat model](docs/THREAT_MODEL.md) and [quality report](docs/QUALITY_REPORT.md)
- [Final review findings and open items](docs/REVIEW_BACKLOG.md)
- [Project plan](docs/PROJECT_PLAN.md) and the step-by-step [learning log](docs/LEARNING_LOG.md)
- [Team Lead presentation](docs/SecureFile_TeamLead.pptx) and the [roadmap](docs/SUGGESTIONS.md)
- [Test plan](docs/TESTPLAN.md), [usability check](docs/USABILITY.md), [references](docs/REFERENCES.md)

## Contributing and security

Contributions are welcome: see [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md).
Please report security problems privately as described in [SECURITY.md](SECURITY.md), not in a public issue.

## License

[MIT](LICENSE) © 2026 Abdelrahman H. Atta

# SecureFile

A desktop application that encrypts and decrypts files with **AES-256-CBC**, built with **C# / .NET 10** and **WPF**.
Files are protected with a password, and any tampering or wrong password is detected (HMAC-SHA256).

> **Status:** 🚧 Stage 0 complete — project setup. Features are being built step by step;
> see [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md).

## Features (planned for v1.0)

- Pick any file (Browse or drag-and-drop) and see its name, size and extension
- Encrypt with AES-256-CBC; the key is derived from your password (PBKDF2-HMAC-SHA256, 600,000 iterations)
- Decrypt back to the original file; wrong passwords and modified files are detected
- Choose the output file name and folder
- Works with very large files (streaming, progress, cancel)
- No internet connection, no telemetry

## Run it (end users)

*Available from the first release:* download `SecureFile.exe` from the Releases page and double-click it.
Nothing else needs to be installed.

## Build it (developers)

Requirements: Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (version pinned in `global.json`),
optionally Visual Studio 2026.

```powershell
dotnet tool restore
git config core.hooksPath .githooks   # once per clone: enables the quality-gate hooks
dotnet build SecureFile.slnx -c Release
dotnet test --solution SecureFile.slnx -c Release
```

Before every push, the `pre-push` hook runs the full quality gate. Run it manually any time:

```powershell
pwsh -File scripts/verify.ps1
```

## Repository layout

| Folder | Contents |
|---|---|
| `src/SecureFile.Core` | Encryption, file format, file details — no UI, runs on every OS |
| `src/SecureFile.Presentation` | ViewModels (MVVM) — shared by every UI |
| `src/SecureFile.Wpf` | The Windows desktop application |
| `tests/` | Unit, integration and architecture tests |
| `docs/` | Plan, decisions (ADRs), learning log, test plan, reports |

## Documentation

- [Project plan](docs/PROJECT_PLAN.md) — phases, steps, decisions
- [Learning log](docs/LEARNING_LOG.md) — line-by-line explanation of every step
- [Architecture decisions](docs/adr/README.md)
- [Changelog](CHANGELOG.md)

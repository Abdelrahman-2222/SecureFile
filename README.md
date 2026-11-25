# SecureFile

SecureFile locks a file with a password so nobody else can open it, and unlocks it again. It works completely on your
computer: nothing is uploaded, there is no account, no telemetry and no internet connection is needed.

Under the hood it uses **AES-256-CBC** with an **HMAC-SHA256** check (a wrong password or a changed file is detected), the key is
derived from your password with PBKDF2 (600,000 rounds), and big files are processed in a stream. It is written in C# on
.NET 10; the Windows app uses WPF, and there is a command-line version for Windows, Linux and macOS.

> **Status:** pre-release (`0.1.0-beta`). Windows app and command line work; desktop windows for macOS and Linux are built and tested automatically but not yet released (they have not been tried on a real Mac or Linux desktop).
> Please test on copies of your files. Details: [CHANGELOG.md](CHANGELOG.md), [docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md).

## Download

Get the zip for your computer from the **Releases** page of this repository (nothing is published yet; until then build it
yourself, see "Build it" below).

| You have | Download |
|---|---|
| Windows 10 or 11, normal PC (64-bit) | `SecureFile-<version>-<label>-win-x64.zip` (for example `SecureFile-0.1.0-beta1-win-x64.zip`) |
| Windows 11 on an ARM PC (Surface Pro X, Copilot+ PC) | `SecureFile-<version>-<label>-win-arm64.zip` (built but not yet tried on real ARM hardware) |
| Command line (Windows, Linux, macOS) | `securefile-cli-<version>-<label>-<system>.zip` or `.tar.gz`, see [docs/CLI_README.md](docs/CLI_README.md) |

## Verify the download (SHA-256)

Every release lists a long code (SHA-256) for each file, in `SHA256SUMS.txt`. Check that your copy is identical:

```powershell
Get-FileHash .\SecureFile-0.1.0-beta1-win-x64.zip -Algorithm SHA256
```

The `Hash` it prints must be the same as the line for your file in `SHA256SUMS.txt` (capital or small letters do not
matter). If it differs, do not run it. On macOS use `shasum -a 256 <file>`, on Linux `sha256sum <file>`.
This proves the file was not damaged or swapped on the way; it cannot prove who made it, because the program is not signed.

## Try it in 60 seconds (Windows)

1. Unzip the folder anywhere (for example the Desktop) and double-click **SecureFile.exe**. Nothing to install.
2. **Browse…** (or drop a file on the dashed box), type a password of at least 8 characters twice, click **Encrypt file**.
   A new `yourfile.ext.enc` appears next to the original; the original is never changed or deleted.
3. To open it again: browse to the `.enc` file, type the password, click **Decrypt file** and save it under a new name.

Keys: **Ctrl+O** browse, **Enter** start, **Esc** cancel, **F1** About.

## Windows says "Windows protected your PC"

The program is **not code-signed yet** (a certificate costs money and the project has none, see
[ADR-15](docs/adr/0015-installer-and-code-signing.md)). Windows SmartScreen therefore warns about new, unknown programs.
After you verified the SHA-256 above: click **More info**, then **Run anyway**. Some antivirus programs also distrust
programs that encrypt files (ransomware does too); if yours blocks it, add an exception for the folder or tell me which one
it is. Do not switch the antivirus off.

## Limits you should know

- **If you forget the password, the file is lost. Nobody can reset it, not even the author.** There is no backup key.
  Test with an unimportant file first and keep the original until you have opened the `.enc` successfully.
- It locks **one file at a time**. To protect a folder, put it in a zip first.
- The password rules are only a minimum (8 characters). A short or common password can be guessed; the strength bar is advice.
- The encrypted file hides the content, not that it exists: its size (about the same as the original), name and date are visible.
- There is no secure delete. SecureFile never deletes your original; deleting it for good is up to you, and on an SSD even a
  normal delete cannot be guaranteed to wipe the data.
- While a file is being processed a hidden temporary file is written next to the output; after a crash it may stay behind
  (a hidden file named like `.~yourfile.<8 hex>.securefile-tmp`), and an antivirus may keep its own copy (a `...SECUREFILE-TMP.tmp`
  backup). Delete them by hand.
- Malware on your computer (for example a keylogger) can see the password as you type it. See [SECURITY.md](SECURITY.md) and
  [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) for the full list.
- Windows app: Windows 10/11 only. macOS and Linux windows exist (Avalonia, `scripts/publish-desktop.ps1`) but are not
  released until tried on real systems; see [docs/DESKTOP_README.md](docs/DESKTOP_README.md).
- Not audited by an independent security firm. Use it for learning and everyday privacy, not as your only protection for
  something whose leak would be a disaster.

## Screenshots

Screenshots of the real app are still to be added by the author; the list of what is needed is in
[docs/SCREENSHOTS_NEEDED.md](docs/SCREENSHOTS_NEEDED.md).

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

Release builds (zips and SHA-256 files): `pwsh scripts/publish-beta.ps1` (Windows app) and `pwsh scripts/publish-cli.ps1`
(command line); the process is in [docs/RELEASING.md](docs/RELEASING.md), the clean-PC check in
[docs/SANDBOX_TEST.md](docs/SANDBOX_TEST.md).

## Repository layout

| Folder | Contents |
|---|---|
| `src/SecureFile.Core` | Encryption, file format, file details; no UI, runs on every OS |
| `src/SecureFile.Presentation` | ViewModels (MVVM), shared by every UI |
| `src/SecureFile.Wpf` | The Windows desktop application |
| `src/SecureFile.Cli` | The command-line version |
| `tests/` | Unit, integration, UI and architecture tests |
| `docs/` | Plan, decisions (ADRs), learning log, test plan, reports |

## Documentation

- [Project plan](docs/PROJECT_PLAN.md): phases, steps, decisions
- [Learning log](docs/LEARNING_LOG.md): line-by-line explanation of every step
- [Architecture decisions](docs/adr/README.md)
- [Threat model](docs/THREAT_MODEL.md) and [quality report](docs/QUALITY_REPORT.md)
- [References](docs/REFERENCES.md): every source used, and what it was used for
- [Changelog](CHANGELOG.md)

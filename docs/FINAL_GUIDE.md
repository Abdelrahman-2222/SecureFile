# SecureFile — the final guide

The one document to read before defending the project. It explains what the app does, how it is built, why every
important decision was made (with the alternatives), how it is tested and measured, and what it does **not** do.
Each section points to the file where the detail lives, so any line can be traced back to code or a test.

Version: 0.1.0-beta3 · Written 2026-11-25 (Stage 9.2), updated for the beta3 release · Plan §14.1

---

## Contents

1. [What the app does](#1-what-the-app-does)
2. [How to run it and how to build it](#2-how-to-run-it-and-how-to-build-it)
3. [Crypto concepts in plain words](#3-crypto-concepts-in-plain-words)
4. [The file format, byte by byte](#4-the-file-format-byte-by-byte)
5. [Architecture](#5-architecture)
6. [Code walkthrough](#6-code-walkthrough)
7. [Identifier dictionary](#7-identifier-dictionary)
8. [Every decision with its alternatives (ADRs)](#8-every-decision-with-its-alternatives-adrs)
9. [Threat model in short](#9-threat-model-in-short)
10. [Tests: what each group proves](#10-tests-what-each-group-proves)
11. [Measurements](#11-measurements)
12. [What is built and what is not (honest limits)](#12-what-is-built-and-what-is-not-honest-limits)
13. [Questions a Team Lead will ask, with answers](#13-questions-a-team-lead-will-ask-with-answers)
14. [References](#14-references)

---

## 1. What the app does

SecureFile locks a file with a password and unlocks it again. One window, four steps:

1. **Choose a file** — Browse (Ctrl+O) or drag it onto the window. The file card shows its name, size, type, folder and
   date (R1, R2). The app reads the first four bytes: a file that starts with `SFEN` is offered for **Decrypt**,
   anything else for **Encrypt** (a renamed `.enc` still works).
2. **Type the password** — for Encrypt it must be at least 8 characters, typed twice, with a strength bar
   (Too short → red, Weak → amber, Fair, Strong → green). Caps Lock is warned about (Windows). Copying out of the
   "Show password" box is blocked.
3. **Choose where the result goes** — a Save dialog with a suggestion (`report.pdf` → `report.pdf.enc`, and back) (R5).
4. **Watch it run** — a progress bar with speed and time left; **Cancel** (Esc) stops it and leaves nothing behind.
   A wrong password, a changed file or a damaged file gives one plain message, never a stack trace.

The same engine runs in three front ends: the **WPF** window (Windows, the required deliverable), an **Avalonia**
window (macOS and Linux, bonus) and a **command line** (`securefile encrypt|decrypt`, all three systems, bonus).

Screenshots: `docs/SCREENSHOTS_NEEDED.md` lists the shots still to be taken by hand (light/dark, each state); they are
not in the repository yet.

---

## 2. How to run it and how to build it

**End user (Windows):** download `SecureFile-0.1.0-beta3-win-x64.zip` from the GitHub release, check its SHA-256 against
the line in `SHA256SUMS.txt` (`Get-FileHash`), unzip, double-click `SecureFile.exe`. Nothing to install (self-contained single
file, ADR-11). The binaries are not code-signed, so SmartScreen warns once ("More info" → "Run anyway"); see
`docs/BETA_README.md`. Command line: `docs/CLI_README.md`.

**Developer:**

```powershell
dotnet build SecureFile.slnx -c Release
```

```powershell
dotnet test --solution SecureFile.slnx -c Release --no-build
```

The SDK version is pinned in `global.json`; NuGet versions in `Directory.Packages.props`; tools (Stryker,
ReportGenerator, counters) in `dotnet-tools.json` (`dotnet tool restore`). On macOS/Linux build
`SecureFile.CrossPlatform.slnf` (everything but WPF). `git config core.hooksPath .githooks` turns on the hooks:
`commit-msg` checks Conventional Commits and refuses AI attribution lines; `pre-push` blocks pushes to `main`, refuses a
dirty working tree and runs `scripts/verify.ps1` (format, build with warnings as errors, all tests, Core coverage
≥ 90 % lines / ≥ 85 % branches, vulnerable-package audit, secret scan, forbidden patterns, commit messages).
Publishing: `scripts/publish-desktop.ps1`, `scripts/publish-cli.ps1`, `docs/RELEASING.md`.

---

## 3. Crypto concepts in plain words

| Word | Plain meaning | In SecureFile |
|---|---|---|
| **AES** | A standard way to scramble 16-byte blocks with a secret key (NIST FIPS 197). AES-256 uses a 32-byte key. | `AesCbc` creates the .NET `Aes` object with a 32-byte key. |
| **CBC** (cipher block chaining) | Each block is mixed with the previous scrambled block before it is scrambled, so equal blocks do not give equal output. Required by the task. | `CipherMode.CBC`. It hides content but does **not** detect changes — hence the HMAC. |
| **IV** (initialisation vector) | The "previous block" for the very first block. Must be random and new for every file; it is **not secret**. | 16 random bytes in the header (`RandomNumberGenerator`). |
| **Padding (PKCS7)** | AES works on whole blocks; padding fills the last block (1–16 bytes, each holding the count). An empty file still gives one block. | Added by `TransformFinalBlock` on encrypt; checked and removed on decrypt **only after** the tag matched. |
| **Salt** | Random bytes mixed into the password before the slow function, so the same password gives a different key per file and pre-computed tables are useless. Not secret. | 16 random bytes in the header. |
| **PBKDF2** | A deliberately slow password-to-key function: it runs HMAC-SHA256 600,000 times, so each guess costs an attacker the same ~0.36 s. | `Rfc2898DeriveBytes.Pbkdf2` (the static method; the old constructors are obsolete, SYSLIB0060). |
| **HKDF** | A fast, standard way (RFC 5869) to derive several independent keys from one strong key, each with its own label. | `HKDF.Expand` with "SecureFile v1 encryption" and "SecureFile v1 authentication". |
| **HMAC** | A keyed checksum: without the key nobody can make a valid one, and any changed bit changes it. | HMAC-SHA256 over header + ciphertext, 32-byte tag at the end of the file. |
| **Encrypt-then-MAC** | Encrypt first, then compute the HMAC over what is stored; on decrypt, check the HMAC before trusting anything. The well-studied safe order. | The tag is compared with `CryptographicOperations.FixedTimeEquals` (no timing leak), then the padding is checked. |
| **NFC normalisation** | "é" can be one character or "e" + accent; NFC makes both the same bytes, so a password typed on a Mac works on Windows. | `KeyDerivation.NormalizeAndEncode` (NFC, not NFKC, which would merge different passwords). |

---

## 4. The file format, byte by byte

Version 1, all numbers little-endian (`BinaryPrimitives`, identical on every OS). Source of truth:
`src/SecureFile.Core/Cryptography/EncryptedFileFormat.cs` (every value is pinned by a test).

| Offset | Size | Field | Meaning |
|---:|---:|---|---|
| 0 | 4 | Signature | ASCII `SFEN` ("SecureFile ENcrypted") |
| 4 | 1 | Version | `1` = PBKDF2-SHA256 → HKDF → AES-256-CBC/PKCS7 + HMAC-SHA256 |
| 5 | 4 | Iterations | 600,000 when written; accepted on read only from 100,000 to 10,000,000 (below: damaged/foreign; above: a crafted file could freeze the app) |
| 9 | 16 | Salt | random per file |
| 25 | 16 | IV | random per file |
| 41 | n | Ciphertext | AES-256-CBC, n a multiple of 16, at least 16 |
| 41+n | 32 | Tag | HMAC-SHA256 over bytes 0 … 41+n−1 |

- Header = 41 bytes; smallest file = 41 + 16 + 32 = **89 bytes** (an encrypted empty file).
- Encrypted size = `41 + (⌊size / 16⌋ + 1) × 16 + 32`.
- The original file name is **not** stored (v1); the suggested decrypt name drops `.enc`, or adds `.decrypted`.
- Changing any value breaks every existing file, so a new layout gets a new version number instead.

**Key derivation:**

```
password ──NFC──► UTF-8 bytes ──PBKDF2-HMAC-SHA256(salt, iterations)──► master key (32 bytes)
                                         ├─ HKDF-Expand("SecureFile v1 encryption")     ─► AES key (32)
                                         └─ HKDF-Expand("SecureFile v1 authentication") ─► HMAC key (32)
```

All three key buffers and the password bytes are wiped (`CryptographicOperations.ZeroMemory`) in `finally`.

**Encrypt flow** (`FileEncryptionService.EncryptAsync`): validate arguments synchronously (paths, same file by link-aware
identity, password not empty) → open the input (`FileShare.Read`) → create the temp file
`.~<name>.<8 hex>.securefile-tmp` in the destination folder (`FileMode.CreateNew`, owner-only on every OS) → derive keys
on a thread-pool thread → write the header and add it to the HMAC → loop: read 1 MiB, `TransformBlock` the whole blocks,
HMAC the ciphertext, write, report progress, honour cancel (and stop if the input gives more bytes than its length) →
`TransformFinalBlock` → write the tag → flush to disk → atomic `File.Move` → on any failure the temp file is emptied
and deleted.

**Decrypt flow** (`DecryptAsync`): read and validate the header (signature, version, iteration range, length ≥ 89,
ciphertext a multiple of 16) → derive keys → temp file → loop over exactly n bytes: HMAC the ciphertext, then
`TransformBlock` (the decryptor holds back the last block) → compare tags in constant time → **mismatch**: delete, throw
`AuthenticationFailedException` ("wrong password or changed/damaged file") → **match**: `TransformFinalBlock` (only now
is padding checked), flush, atomic move.

---

## 5. Architecture

```
SecureFile.Wpf ─┐            (Windows, net10.0-windows)
SecureFile.Avalonia ─┼──► SecureFile.Presentation ──► SecureFile.Core
SecureFile.Cli ─────────────────────────────────────► SecureFile.Core
```

- **Core** (`net10.0`): crypto, file format, atomic writing, file details, password strength. References nothing of ours
  and no UI. Throws documented exceptions (ADR-13). Uses `ConfigureAwait(false)` (no UI thread to return to).
- **Presentation** (`net10.0`): `MainViewModel` (CommunityToolkit.Mvvm), error messages, the local error log, and the
  service interfaces the windows implement (`IFileDialogService`, `IFolderOpener`). No WPF or Avalonia types, so the
  same class drives both windows and is unit-tested without a window. Uses `ConfigureAwait(true)`.
- **Wpf / Avalonia**: thin windows. Code-behind only for what binding cannot do (password boxes, drag and drop, focus,
  closing during an operation) plus the global safety nets.
- **Cli**: its own small front end directly over Core (a command line has no view model).
- **Dependency rule** is enforced by `tests/SecureFile.Architecture.Tests` (NetArchTest): Core references no UI,
  Presentation references neither WPF nor Avalonia, nothing references a front end.
- **Composition root**: each front end builds its objects in one place with plain `new` (ADR-09), for example WPF
  `App.OnStartup`: `new MainViewModel(new FileEncryptionService(), new FileDetailsReader(), new WpfFileDialogService(),
  errorLog, new ExplorerFolderOpener())`.

**Why it is reusable:** another app (a service, a different UI, a plugin) references `SecureFile.Core` and calls
`IFileEncryptionService.EncryptAsync/DecryptAsync`. The proof is that three front ends and the tests already do.

---

## 6. Code walkthrough

Every source file, every type and its methods, in reading order. "→" marks what to say in one sentence. XML docs on
every member hold the full reasoning; this is the map.

### 6.1 Core — `src/SecureFile.Core`

| File | Type | Members and what they do |
|---|---|---|
| `SecureFileException.cs` | `SecureFileException` (abstract) | Base of our own exceptions, so a caller can catch "any SecureFile failure" in one place. Three standard constructors. |
| `Cryptography/EncryptedFileFormat.cs` | `EncryptedFileFormat` (static) | All sizes and offsets of §4 as named constants; `Signature` (`"SFEN"u8`, no allocation); `HasSignature` (Encrypt-or-Decrypt choice, never throws); `IsSupportedIterationCount` (the 100k–10M rule); `GetCiphertextLength` (length checks: ≥ 89 bytes, whole blocks). |
| `Cryptography/EncryptedFileHeader.cs` | `EncryptedFileHeader` | `CreateNew` (fresh random salt and IV), `Read` (validates signature → version → iterations, throws `InvalidEncryptedFileException` with a reason), `WriteTo` (the 41 bytes). Private `ThrowIfVersionIsNotCurrent`, `RequireLength`. |
| `Cryptography/KeyDerivation.cs` | `KeyDerivation` (static) | `DeriveKeys` (password + header → `FileKeys`), `ThrowIfInvalidPassword` (empty / lone surrogate, checked at the call), `PasswordToBytes` + `NormalizeAndEncode` (NFC, strict UTF-8), `DeriveMasterKey` (PBKDF2), `ExpandKey` + `SplitMasterKey` (HKDF with the two labels). Every buffer wiped in `finally`. |
| `Cryptography/FileKeys.cs` | `FileKeys : IDisposable` | Holds the AES and HMAC keys in pinned arrays; `Dispose` zeroes both. → keys never outlive the operation. |
| `Cryptography/AesCbc.cs` | `AesCbc` (static) | `CreateEncryptor` / `CreateDecryptor` with CBC + PKCS7 and length checks; the one place the cipher is configured. |
| `Cryptography/PercentageProgress.cs` | `PercentageProgress` | `Advance(bytes)` turns processed bytes into 0–100 and reports only when the percentage changes. |
| `Cryptography/IFileEncryptionService.cs` | `IFileEncryptionService` | The contract: `EncryptAsync`, `DecryptAsync`, with every exception documented. |
| `Cryptography/FileEncryptionService.cs` | `FileEncryptionService` | Public `EncryptAsync` / `DecryptAsync` validate synchronously (`ThrowIfOutputPathIsUnusable`: not a folder, not the input by `FileIdentity`, not existing unless allowed) and start the async core. `EncryptCoreAsync` / `DecryptCoreAsync` open the input (`OpenInputFile`) and hand a content writer to `AtomicFileWriter`. Internal `WriteEncryptedFileAsync` / `WriteDecryptedFileAsync` / `ReadHeaderAsync` are the stream-level steps (tested directly). Loops: `EncryptContentAsync` + `EncryptOneChunkAsync` + `ThrowIfLongerThanExpected`; `DecryptContentAsync` + `AuthenticateAndWriteAsync`; `VerifyTagAsync` (constant-time compare) then `WriteLastBlockAsync` (padding checked only now; bad padding becomes `Damaged`). One 2 MiB pooled buffer, cleared on return. |
| `Cryptography/AuthenticationFailedException.cs` | `AuthenticationFailedException` | Wrong password **or** changed/damaged file — deliberately one message (no oracle). |
| `Cryptography/InvalidEncryptedFileException.cs` + `InvalidEncryptedFileReason.cs` | exception + enum | Not our file / newer version / damaged; `CheckedReason` refuses an undefined enum value. |
| `Files/AtomicFileWriter.cs` | `AtomicFileWriter` (static) | `WriteAsync`: create the temp file (`CreateTempFile`; Windows `CreateOwnerOnlyWindowsFile` with an owner-only ACL and hidden attribute, Unix mode 0600), let the caller fill it (`FillAsync`), flush to disk, `File.Move`. On failure `TryEmpty` (truncate through the open handle) then `TryDeleteFile` (5 tries, 40–160 ms pauses, for antivirus holds). `CreateTempFilePath` builds `.~<name>.<8 hex>.securefile-tmp`. |
| `Files/FileIdentity.cs` | `FileIdentity` (public, Stage 9.1) | `AreSameFile`: rebuilds both paths link by link (`ResolveLinks`, `Step`, `GetLinkTarget`, at most 40 hops, a loop throws `IOException`), then compares NFC + case-insensitively. Used by Core, the ViewModel and the CLI. |
| `Files/FileDetails.cs`, `FileDetailsReader.cs`, `IFileDetailsReader.cs` | record + reader | Name, size, extension, folder, date, "is encrypted" for the file card; a link shows its target's size (`ResolveFinalTarget`); a folder throws `PathIsFolderException`. |
| `Files/FileSizeFormatter.cs` | static | `Format` ("1.4 MB", binary units, culture-aware) and `RoundForDisplay`. |
| `Files/PathIsFolderException.cs` | `PathIsFolderException : FileNotFoundException` | A folder was chosen where a file is needed (existing catches keep working). |
| `Passwords/PasswordStrengthEvaluator.cs` | static | `Evaluate` → TooShort / Weak / Fair / Strong from length and character kinds (`CountCharacterKinds`, `KindOf`), pushed to Weak by `PasswordPatterns`. `MinimumLength = 8`. |
| `Passwords/PasswordPatterns.cs` | static | `IsPredictable`: few distinct characters, mostly a sequence (`IsMostlyASequence`, `LongestRun`), or a common word (`IsCommonWord`: three readings — as typed, look-alikes read as letters, and look-alikes after dropping a digit/symbol suffix). |

### 6.2 Presentation — `src/SecureFile.Presentation`

| File | Type | Members and what they do |
|---|---|---|
| `Main/MainViewModel.cs` | `MainViewModel : ObservableObject` | State machine `MainViewState` (NoFile, FileSelected, Working, Succeeded, Failed). Commands (generated by `[RelayCommand]`): `Browse`, `OpenFile`, `RunPrimary` (Encrypt or Decrypt, whichever fits), `Encrypt`, `Decrypt`, `Cancel`, `OpenFolder`. `RunGuardedAsync` is the **one** catch list (cancel, `SecureFileException`, IO, access, not-supported); anything else goes to the safety net and `ResetToIdle` makes the window usable again. `RunFileOperationAsync` → password rules (`RejectUnusablePassword`, `RejectUnacceptableNewPassword`) → `AskForOutputPathAsync` (Save dialog via `ShowDialogLockedAsync`, same-file refusal) → `ExecuteOperationAsync` (`BeginOperation`, `StartOperation`, `EndOperation`) → `ShowSuccess` (clears the password). `OnProgress` throttles to 10 updates/s with an injectable `TimeProvider`. `IsFileDialogOpen` locks inputs while the Open or Save dialog is open. |
| `Main/MainViewModel.Drop.cs` | partial | `OpenFilesAsync`: exactly one dropped file is accepted; a folder or several files get a plain message. |
| `Main/MainViewModel.FileCard.cs` | partial | `DescribeFile`: the tooltip text (folder, exact size, date). |
| `Main/MainViewModel.Keyboard.cs` | partial | `EnterMovesToConfirm`: Enter in the first box goes to Confirm while a new password still needs confirming. |
| `Main/MainViewState.cs` | enum | The five states. |
| `Main/ProgressTextFormatter.cs` | static | "50% · 2.5 MB/s · about 2 s left" (`FormatRemaining`: s, min s, h min). |
| `Errors/ErrorMessageProvider.cs` | static | Exception → fixed plain sentence (`GetMessage`, `GetInvalidFileMessage`, `GetFileSystemMessage`, `GetIoMessage`: disk full, file in use, by HResult). Never the raw .NET message, never a full path. |
| `Errors/IErrorLog.cs`, `FileErrorLog.cs` | interface + log | `Write` (crash, with stack trace) / `WriteExpected` (no stack trace) append to `%LOCALAPPDATA%\SecureFile\logs\securefile.log` (Unix: the local data folder); rotates at 1 MB (`RotateIfTooBig`); never throws. |
| `Errors/LogEntryFormatter.cs` | static | `Format` (type + message, at most 5 inner exceptions), `RemovePaths` (five source-generated regexes with timeouts; fail closed). |
| `Errors/SafetyNetMessages.cs` | static | `Recovered` / `MustClose` texts for the safety-net dialogs, with the log path. |
| `Dialogs/IFileDialogService.cs`, `IFolderOpener.cs` | interfaces | Async on purpose (Avalonia's dialogs are, ADR-16). |

### 6.3 WPF — `src/SecureFile.Wpf`

| File | What it does |
|---|---|
| `App.xaml(.cs)` | `OnStartup`: registers the three safety nets **first** (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`), then builds the object graph. `MustClose` / `IsFatal`: a second unexpected error soon after the first, or a fatal type (OOM, invalid program), offers only Close. |
| `MainWindow.xaml` | The layout: drop zone, file card, password boxes with access keys, strength bar, progress, status line (live region), result panel. Fluent theme follows Windows (light, dark, high contrast). |
| `MainWindow.xaml.cs` | Password plumbing (`PasswordBox.Password` is not bindable on purpose): `WirePasswordBoxes`, `OnPasswordChanged`, `SetBoxes`, `BoxesDifferFromViewModel`; "Show password" swap (`SwapVisibility`), copy blocked (`BlockCopyOut`), Caps Lock warning, drag and drop (`OnDragOver`, `AcceptsDrop`, `OnFileDropped`), F1 About, `AnnounceStatus` (screen readers). |
| `MainWindow.Behavior.cs` | `OnClosing`: asks before closing during an operation, cancels, closes when idle (and at once if the operation ended during the question). Focus rules after each state (`ChooseFocus`, `FocusAfterFailure`), `OnFileDialogOpenChanged`, `OnFirstPasswordKeyDown`. |
| `AboutWindow.xaml(.cs)` | Version, licence, link to the docs. |
| `Services/WpfFileDialogService.cs` | Microsoft.Win32 Open/Save dialogs; overwrite prompt on. |
| `Services/ExplorerFolderOpener.cs` | `explorer.exe /select,"path"` with `UseShellExecute = false` (no shell, no injection). |

### 6.4 Avalonia — `src/SecureFile.Avalonia`

The same screen over the same `MainViewModel`: `Program.Main` + `App` (safety nets: `Dispatcher.UIThread.UnhandledException`,
AppDomain, TaskScheduler), `MainWindow.axaml(.cs)` and `MainWindow.Behavior.cs` (mirror of WPF), `MessageDialog` (Avalonia
has no message box), `AvaloniaFileDialogService` (StorageProvider, async), `LauncherFolderOpener`, `AboutWindow`.
Trimmed, about 38 MB. Released as a **preview** (never run on a real Mac or Linux desktop by a person).

### 6.5 Command line — `src/SecureFile.Cli`

| File | What it does |
|---|---|
| `Program.cs` | The only file that touches `Console`: Ctrl+C → cancel (a second one ends the program), stdin read as UTF-8 when redirected. |
| `CliApplication.cs` | `RunAsync` → parse → help/version or `RunOperationAsync`: `CheckPaths` (input exists, not the same file — `IsSameFileOrLoop`, folder/invalid name, `--force`), password, `ExecuteAsync` (progress line, expected failures → exit 1 with a plain message). |
| `CliArgumentParser.cs`, `CliOptions.cs` | Hand-written parser (two commands, four options, `--`); deliberately **no** `--password` option. Exit codes 0 / 1 / 2 / 130. |
| `PasswordResolver.cs`, `IPasswordPrompt.cs`, `ConsolePasswordPrompt.cs` | Hidden prompt (twice for a new password) or `--password-stdin` (pipe only); same rules as the window; Weak/Fair advice on stderr. |
| `OutputPathResolver.cs` | Default names (`.enc` added/removed, `.decrypted`) and the link-aware same-file check. |
| `ProgressLine.cs` | "Encrypting… 42 %" on stderr, only on a terminal, so piped stdout stays clean. |

---

## 7. Identifier dictionary

| Name | Meaning |
|---|---|
| `SFEN` / `Signature` | The four magic bytes that mark our file. |
| `CurrentVersion` | Format version written and read (1). |
| `DefaultIterations`, `MinimumIterations`, `MaximumIterations` | 600,000 written; 100,000–10,000,000 accepted. |
| `SaltLength`, `IvLength`, `AesBlockLength`, `AuthenticationTagLength`, `HeaderLength`, `MinimumFileLength` | 16, 16, 16, 32, 41, 89 bytes. |
| `ChunkLength` | 1 MiB read per loop round (buffer is 2 MiB: plaintext half + ciphertext half). |
| `KeyLength` | 32 bytes for every key. |
| `EncryptionKeyLabel`, `AuthenticationKeyLabel` | The HKDF labels "SecureFile v1 encryption/authentication" (domain separation). |
| `FileKeys.EncryptionKey`, `AuthenticationKey` | The two derived keys. |
| `TempFilePrefix`, `TempFileSuffix` | `.~` and `.securefile-tmp`: the recognisable hidden temp name. |
| `DeleteAttempts`, `DeleteRetryStep` | 5 tries, pauses growing by 40 ms. |
| `MaximumLinkHops` | 40 links followed before a loop is declared. |
| `MainViewState` | NoFile, FileSelected, Working, Succeeded, Failed. |
| `IsBusy`, `CanEditInputs`, `IsFileDialogOpen` | Working; inputs editable; a file dialog is open (inputs locked). |
| `RunPrimaryCommand` | The big button: Encrypt or Decrypt depending on the file. |
| `MaxLogBytes`, `MaximumInnerExceptions` | 1 MB log rotation; 5 inner exceptions per entry. |
| `PathPlaceholder` | `<path>`: what a removed path becomes in the log. |
| `ExitCodes.Success/Failure/Usage/Cancelled` | 0, 1, 2, 130. |
| `MinimumLength` | 8 characters for a new password. |
| `CommonWords` | The built-in list of very common passwords and words. |

---

## 8. Every decision with its alternatives (ADRs)

Full tables: `docs/PROJECT_PLAN.md` §4 (ADR-01 … 14) and `docs/adr/` (13, 15, 16).

| ADR | Decision | Alternatives and why not |
|---|---|---|
| 01 UI | **WPF** required track, **Avalonia** bonus | WinForms: weak binding/MVVM, logic ends up in event handlers, not reusable or testable. MAUI: no Linux desktop. Avalonia alone: not what the task names. |
| 02 Key source | **Password → key** | Hard-coded key: anyone with the exe decrypts everything (CA5390). Key file: second thing to lose. DPAPI: Windows-only, tied to one user/machine. |
| 03 KDF | **PBKDF2-HMAC-SHA256, 600,000** (OWASP), stored in the header so it can rise | SHA-256(password): billions of guesses/s. Argon2id/scrypt: stronger (memory-hard) but not built into .NET; the task asks for built-in crypto → future v2. |
| 04 Integrity | **CBC + HMAC-SHA256, encrypt-then-MAC** | CBC alone: tampering undetected, wrong password gives garbage 1 in 256 times. MAC-then-encrypt: padding-oracle history. AES-GCM: best modern choice, but the task requires CBC → v2. |
| 05 Two keys | **PBKDF2 once → HKDF.Expand twice** | 64 bytes from PBKDF2: runs 600k twice, attacker needs only half → slower for us, no gain. Two PBKDF2 runs: same. |
| 06 Streaming | **Own loop, one pooled buffer, `TransformBlock`; final block only after the tag** | `ReadAllBytes`: memory = file size, fails > 2 GB. `CryptoStream` on decrypt: disposing it checks padding before the tag verdict. Per-chunk one-shot CBC: hand-chaining risk. |
| 07 Safe output | **One pass into a hidden temp file in the destination folder, verify, atomic move** | Direct write: broken file on failure. Two passes (verify, then decrypt): no unverified plaintext on disk but reads twice. Accepted trade-off: unverified plaintext exists only in the hidden, owner-only temp file and is emptied/deleted on failure. |
| 08 MVVM | **CommunityToolkit.Mvvm** (source generators) | Hand-written INotifyPropertyChanged: boilerplate. ReactiveUI: steep. |
| 09 DI | **Pure DI** in the composition root | `new` inside the ViewModel: untestable. A DI container: extra concept for ~6 services. |
| 10 Tests | **xUnit v3** on Microsoft.Testing.Platform | MSTest/NUnit/TUnit: valid, no reason to switch. |
| 11 Delivery | **Self-contained single-file exe** + SHA-256 | Framework-dependent: user must install .NET. MSIX/installer: later (ADR-15). |
| 12 Telemetry | **None**, no network calls | A file-encryption tool that phones home is a privacy risk. |
| 13 Exceptions | **Core throws documented exceptions → ViewModel catches the expected ones at the user action → global safety nets log and keep the app usable** | catch-all `Exception`: hides bugs. Catch nothing: app closes. Error codes everywhere: easy to ignore. |
| 14 Organisation | **By feature, interface next to its implementation** | By type (Interfaces/Services/Models): one feature spread over folders. Abstractions assembly: no consumer (YAGNI). |
| 15 Installer/signing | **Proposed**: zip + SHA-256 now; Azure Artifact Signing for a public release | MSIX, Velopack, self-signed (warnings anyway). The user decides (cost). |
| 16 Async UI services | **Dialog and folder-opener interfaces are async** | Sync interfaces: Avalonia's dialogs are async, blocking would freeze or deadlock. |

Stage 9.1 decisions recorded without a new ADR (each is small and reversible): `FileIdentity` made public so all front
ends share one same-file rule; the window locks while **any** file dialog is open; `--password-stdin` is pipe-only.

---

## 9. Threat model in short

Full STRIDE table (20 threats, each with mitigation, test and residual risk): `docs/THREAT_MODEL.md`.

- **Protected:** the file content, the password, the integrity of `.enc` files, no data loss.
- **Against:** someone with a copy of the `.enc` and unlimited time; someone handing the user a crafted file or path;
  another program/user on the same PC; a tampered download.
- **Key mitigations:** encrypt-then-MAC with constant-time compare; padding never examined before the tag (no oracle);
  one message for wrong password and tampering; bounded iteration count (no DoS); 600k PBKDF2 per guess; random salt/IV
  per file; atomic temp + move; owner-only temp files; link-aware same-file check; no password option on the command
  line; log without paths or passwords; SHA-256 next to every release.
- **Out of scope (said openly):** malware already running as the user (keylogger, memory dump during the operation),
  forensic recovery of deleted plaintext on SSDs, swapping one valid `.enc` for another made with the same password,
  unsigned binaries (warnings), weak passwords chosen anyway.

---

## 10. Tests: what each group proves

| Project | Tests | What they prove |
|---|---:|---|
| `SecureFile.Core.Tests` | 474 | Round trips of every size boundary (0, 1, 15, 16, 17 bytes, 1 MiB ± 1); independently computed golden file (fixed salt/IV) and RFC test vectors for PBKDF2/HKDF; every flipped bit detected; wrong password; cut/extended files; header limits; nothing left on any failure or cancel; owner-only temp files; link-aware same-file; password strength and patterns; size formatting. |
| `SecureFile.Presentation.Tests` | 154 | ViewModel state machine, commands enabled/disabled, dialog locks, password rules, progress throttle (injected `TimeProvider`), every exception → its message, log has no paths and rotates. |
| `SecureFile.Cli.Tests` | 60 | Real files through the whole program: exit codes, stdin/prompt passwords, overwrite protection, same-file through a link, parser edge cases, password resolver. |
| `SecureFile.Integration.Tests` | 10 | Real temp folders through the real engine and ViewModel (file-system scenarios, a ViewModel round trip); large files with flat memory (1 GiB and 4.5 GiB, opt-in). |
| `SecureFile.Architecture.Tests` | 6 | The dependency rule (Core, Presentation and CLI reference no UI framework and no network assembly), Core uses no UI namespace, Core's classes are sealed. |
| `SecureFile.Avalonia.Tests` | 3 | The Avalonia window headless: end-to-end encrypt/decrypt, password boxes hide the text and refuse copy, strength colours. |
| `SecureFile.Wpf.UiTests` | 5 | FlaUI drives the real WPF exe: encrypt then decrypt restores the bytes, wrong password message, accessible names, About, and typing in the hidden password box keeps the caret (regression test for a late user report). Run in CI (Stage 9.1) and locally with `SECUREFILE_UI_TESTS=1`. |
| **Total** | **712** | 10 are skipped on a normal Windows run: 5 UI tests need `SECUREFILE_UI_TESTS=1`, 2 large-file runs are opt-in, 3 run only on Linux or macOS (CI). |

Requirements traceability (plan §15): every R1–R6 and D1–D7 has at least one passing test (T-xx IDs in the plan's §9
catalog). Cross-OS golden files: CI encrypts a sample on Windows, Ubuntu and macOS and decrypts every one on every system.

**Mutation testing (Stryker.NET 5.1):** 100 % of tested mutants in Core, Presentation and the CLI's pure logic; the last two first ran
in Stage 9.1 — scores and the survivors' handling are in `docs/QUALITY_REPORT.md` §9.1.

**Manual:** `docs/TESTPLAN.md` (rows A–H) for what only a person can judge; Windows Sandbox clean-machine run
(`docs/SANDBOX_TEST.md`).

---

## 11. Measurements

Reference machine: Intel i7-8750H, 23.8 GB RAM, work folder on a 1 TB HDD, Windows 11, Kaspersky active. Raw data in
`docs/quality/`; method and verdicts in `docs/QUALITY_REPORT.md`.

| Metric | Target | Measured | Verdict |
|---|---|---|---|
| Key derivation p50 / p95 | 0.3–1 s | 364 / 375 ms | Met |
| 100 MB encrypt / decrypt | ≥ 70 % of a plain copy | 56 / 58 MB/s; 53 % of a durable (fsync) copy, 3 % of a cached copy | **Not met** — explained: CBC is serial (193 MB/s CPU), and the final flush does not overlap the CPU work (backlog C13) |
| Peak memory 1 MB / 100 MB / 1 GiB | flat, < 150 MB | 25 / 28 / 28 MB | Met (streaming) |
| Allocations | no per-chunk buffer | ~150 B per 1 MiB chunk | Met |
| Progress updates | ≤ 10/s | 10/s (was 70) | Met after fix |
| First window, warm | < 0.5 s | 0.64 s build, 0.71 s published | **Not met** (+0.14–0.21 s) |
| Published size | reported | WPF 134 MB (cannot be trimmed), Avalonia trimmed ~38 MB, CLI 12.6–14.5 MB | Reported |
| Complexity / maintainability | ≤ 10 / ≥ 60 | worst 7 / lowest 60 | Met (build-enforced) |
| Coverage Core (Core tests alone) | ≥ 90 % lines, ≥ 85 % branches | 97.9 % / 95.8 % | Met |
| Vulnerable packages, analyzer warnings, secrets | 0 | 0 | Met |

How: BenchmarkDotNet (`scripts/run-benchmarks.ps1`), `scripts/measure-memory.ps1` (dotnet-counters), `scripts/measure-startup.ps1`,
`scripts/large-file-check.ps1`. Largest file tested: 1 GiB (the 4.5 GiB run is killed by the antivirus here).

---

## 12. What is built and what is not (honest limits)

**Built:** everything in R1–R6 and D1–D7; WPF app; Avalonia preview for macOS/Linux; CLI for all three; cross-OS golden
files; owner-only temp files; link-aware same-file check; screen-reader announcements (WPF); release packages with
SHA-256; CI on three systems; a final review with every finding fixed or recorded (`docs/REVIEW_BACKLOG.md` §F).

**Not built or not proven:**

- No code signing (ADR-15 proposed; costs money) → SmartScreen/Gatekeeper warnings.
- macOS/Linux windows and CLI were run only by CI, never by a person on a real machine (preview label).
- Windows on ARM build exists but was never run on ARM hardware (still held back in beta3).
- Throughput and warm-start targets missed (explained above).
- Not yet done by a person: screen-reader check, high contrast and small screens (B3), cold start after reboot, UI
  thread profile (< 50 ms blocks), usability session with real users.
- Format v1 limits: a wrong password on a huge file is found only at the end (no key check); no file name inside the
  file; the passphrase is the only factor.
- Open small items: `docs/REVIEW_BACKLOG.md` (for example a FIFO can block on open, UI-3 plain-text box).

**Next (v2, see `docs/SUGGESTIONS.md`):** format v2 with chunked authentication and a quick key check; Argon2id; a
startup sweep for leftover temp files; a passphrase generator; folder/batch encryption; Explorer/Finder "Encrypt with
SecureFile"; code signing and notarization.

---

## 13. Questions a Team Lead will ask, with answers

**Why not AES-GCM?** The task requires CBC. GCM would be the better choice (authenticated in one primitive, parallel);
it is the plan for format v2. CBC + HMAC encrypt-then-MAC gives the same guarantees (confidentiality + integrity) when
done in this order with separate keys.

**Why is the IV not secret?** Its job is to make each encryption start differently, not to hide anything; CBC's security
needs it unpredictable and unique, which `RandomNumberGenerator` gives. Same for the salt. Only the password and keys are secret.

**Why 600,000 iterations?** The OWASP Password Storage Cheat Sheet value for PBKDF2-HMAC-SHA256. It costs ~0.36 s per
try here — invisible to the user, expensive for a guesser. It is stored per file, so it can rise without breaking old files.

**What if the user forgets the password?** The file cannot be opened — by design. There is no back door, no key stored
anywhere, no recovery. The UI makes this clear (confirm box, strength bar).

**Why not store the key?** A stored key protects only as well as the place it is stored; the password *is* the key
material, derived on demand and wiped after use.

**Why verify the HMAC before releasing the output?** Until the tag matches, nothing about the content can be trusted. If
padding were checked first, an attacker could learn plaintext from "bad padding" vs "bad tag" (padding oracle). So the
result file appears only after the tag matched; until then the bytes are in a hidden, owner-only temp file.

**Why HKDF?** One slow PBKDF2 run for the password, then a fast, standard split into two independent keys. Using one
key for AES and HMAC is a known anti-pattern; asking PBKDF2 for 64 bytes doubles our cost but not the attacker's.

**Can the password stay in memory?** The `PasswordBox`/console gives a .NET `string`, which cannot be wiped. We convert
it to bytes once, wipe the bytes and keys right after use, clear the box after success, never log or copy it.
`SecureString` is not recommended by Microsoft for new code. A memory dump during the operation could still show it —
an attacker who can read our process memory is out of scope.

**Why WPF over WinForms?** Data binding lets all logic live in a ViewModel that is unit-tested without a window and is
reused unchanged by the Avalonia window. WinForms logic tends to live in event handlers.

**How does the UI not freeze?** Key derivation runs on the thread pool (`Task.Run`); file I/O is async; progress is
marshalled back through `Progress<T>` and throttled to 10 updates/s; the ViewModel uses `ConfigureAwait(true)` only to
come back to the UI thread for state changes.

**How did you measure performance?** BenchmarkDotNet for throughput, KDF time and allocations; dotnet-counters for memory;
a stopwatch script for startup; all on one documented machine with raw output kept in `docs/quality/`.

**Largest file tested?** 1 GiB end to end with 28 MB peak memory; the format and the loop handle any size (`long`
lengths); a 4.5 GiB run exists but this machine's antivirus kills it.

**How is the code reusable in another app?** Reference `SecureFile.Core` and call `IFileEncryptionService`. The WPF app,
the Avalonia app, the CLI and the tests are four consumers already; the architecture tests keep Core free of UI.

**What would you change with more time?** Format v2 (chunked authentication + key check, AES-GCM if CBC is no longer
required, Argon2id), overlap the disk flush with the CPU work for speed, sign the binaries, and run real usability
sessions and screen-reader checks.

**How do you know the tests are good?** Mutation testing: Stryker changes the code on purpose (flip a `<`, drop a line)
and checks that a test fails. Core kills 100 % of tested mutants; Presentation and the CLI were added in Stage 9.1.

---

## 14. References

Microsoft Learn: `Aes`, `Rfc2898DeriveBytes.Pbkdf2`, `HKDF`, `IncrementalHash`, `CryptographicOperations`; Best practices
for exceptions; WPF `DispatcherUnhandledException`; `AppDomain.UnhandledException`; `TaskScheduler.UnobservedTaskException`.
NIST FIPS 197 (AES), NIST SP 800-38A (CBC), NIST SP 800-132 (PBKDF2), NIST SP 800-63B (passwords). RFC 2104 (HMAC),
RFC 5869 (HKDF), RFC 8018 (PBKDF2), RFC 7914 (test vectors). OWASP Password Storage Cheat Sheet. Full list with links:
`docs/REFERENCES.md`.

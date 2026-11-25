# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Stage 6 (publishing), step 6.1: `scripts/publish-beta.ps1` and `scripts/publish-cli.ps1` also build `win-arm64`
  (cross-compiled on x64; PE header checked as ARM64; **never run**, there is no ARM machine here). Sizes of this
  build: WPF zip x64 56.2 MB / arm64 52.0 MB (exe 133.6 / 144.0 MB); CLI zip 5.6 / 5.5 MB (exe 12.6 / 13.0 MB).
  `publish-beta.ps1` now writes a `SHA256SUMS.txt` next to the zips.
- Stage 5 quality work: BenchmarkDotNet suite (`benchmarks/SecureFile.Benchmarks`, `scripts/run-benchmarks.ps1`),
  `scripts/measure-startup.ps1` and `scripts/measure-memory.ps1`, `docs/QUALITY_REPORT.md` with the raw numbers under
  `docs/quality/`, and `docs/THREAT_MODEL.md` (STRIDE, 20 threats) with the independent security review; `SECURITY.md`
  lists the known limits.
- Strength check reads look-alike characters (`P@ssw0rd`, `l3tmein`) as letters.
- Vertical slice: a first working Windows app window (Browse, password, Encrypt, Decrypt, progress, status text) that
  encrypts and decrypts a real file; `ErrorMessageProvider` turns each expected error into a fixed plain message
  (ADR-13 Layer 2); `MainViewModel` logs and shows them; the WPF app adds a local error log
  (`%LOCALAPPDATA%\SecureFile\logs\securefile.log`) and global safety nets (ADR-13 Layer 3).
- Main window (Steps 3.2–3.5): layout following the UX flow; Fluent theme that follows Windows light/dark; drop a
  file on the window; Ctrl+O to browse and Enter in the password box to encrypt or decrypt; progress with speed and
  time left; Cancel (Esc) that leaves no output file; "Open folder" after success; inputs locked while working.
- App icon (padlock) for the executable and both windows, and an About dialog (button or F1) that shows the version
  read from the assembly (`0.1.0`).
- Integration tests on real files (Step 4.1): overwrite replaces atomically, locked source, source deleted after
  selection, wrong password, plain text given to Decrypt, double encryption, and a committed golden `.enc` file that
  must always decrypt (protects the file format).
- Opt-in large-file round trip (Step 4.2, `scripts/large-file-check.ps1`): 1 GiB passes with a 46 MB peak working
  set; the 4.5 GiB case is still to be run on a machine whose antivirus does not stop the test program.
- Manual test script (`docs/TESTPLAN.md`, Step 4.3) and usability-check protocol with SUS form
  (`docs/USABILITY.md`, Step 4.4); both still have to be run by people.
- The pre-push hook now also refuses pushes straight to main, branch deletions and force-pushes (a local stand-in for
  branch protection, which GitHub does not enforce on private repositories without a paid plan).
- UI tests with FlaUI (`tests/SecureFile.Wpf.UiTests`): the real app encrypts and decrypts a file, shows the friendly
  wrong-password message, every control has an accessible name, and the About dialog opens with F1. They run only
  with `SECUREFILE_UI_TESTS=1`. Accessibility Insights automated checks on the window: 0 failures.
- The wrong-password message now suggests checking Caps Lock, the keyboard language and spaces.
- First integration test: the ViewModel with the real encryption service encrypts then decrypts a file; bytes equal.
- File details for the selected file: name, extension, size in bytes, full path and last change time (UTC)
  (`FileDetails`, `IFileDetailsReader`, `FileDetailsReader`).
- `FileSizeFormatter`: human-readable sizes (`1.5 MB`) with binary units and culture-aware decimals.
- Quality gate: `scripts/verify.ps1` (format, build, tests, Core coverage limits, vulnerable packages, secrets,
  forbidden code patterns, AI-attribution check) and Git hooks (`commit-msg`, `pre-push`) in `.githooks/`.
- Code-metric limits enforced by the build: cyclomatic complexity ≤ 10 (CA1502), Maintainability Index ≥ 60 (CA1505).
- Architecture tests for the layer rule (Core and Presentation free of UI dependencies; Core classes sealed).
- Exception-handling strategy ADR-13 (Core throws, ViewModel handles expected errors, global safety nets).
- Review Board process (PROJECT_PLAN §1.8), design/algorithm standards (§1.9), engineering-principles checklist
  (§1.10: SOLID, DRY, KISS, YAGNI, OOP, patterns, DSA, system design, SDLC) and ADR-14 (organise code by feature).
- CI runs the full quality gate on Windows, a gitleaks secrets scan over the whole history, and pins every action
  to a commit SHA without keeping the token on disk.
- `docs/REFERENCES.md`: every source used and what it was used for.
- Encrypted file format version 1 (`EncryptedFileFormat`): the 41-byte header layout, the `SFEN` signature check
  (`HasSignature`), the accepted iteration range (100,000–10,000,000) and the file-length check.
- `EncryptedFileHeader` (internal): creates headers with a fresh cryptographic random salt and IV, writes them as
  fixed little-endian bytes and reads them back with full validation of untrusted input.
- Error model: `SecureFileException` (base) and `InvalidEncryptedFileException` with a `Reason`
  (not an encrypted file / newer version / damaged), so the app can tell the user what to do.
- Tests with golden bytes, boundary values, property-based round trips and fuzzing (random input never causes an
  undocumented exception).
- Key derivation (`KeyDerivation`, internal): the password is normalised (NFC) and UTF-8 encoded, strengthened with
  PBKDF2-HMAC-SHA256 (600,000 iterations for new files) and split with HKDF-Expand into separate encryption and
  authentication keys. Temporary copies are pinned and wiped.
- `FileKeys` (internal): holds the two keys and overwrites them with zeros when disposed.
- Known-answer tests from RFC 7914 and RFC 5869, plus independently computed golden keys (accents, Arabic, emoji).
- `AesCbc` (internal): creates the AES-256-CBC/PKCS7 encryptor and decryptor for a file's key and IV, with both
  lengths checked; verified against NIST SP 800-38A F.2.5/F.2.6 and OpenSSL.

- File encryption (`IFileEncryptionService`, `FileEncryptionService.EncryptAsync`): streams any file size in 1 MiB
  chunks through AES-256-CBC and an HMAC-SHA256 over header and ciphertext, derives the keys off the UI thread,
  reports whole-percent progress, supports cancel, and never leaves a partial output file.
- `AtomicFileWriter` (internal): writes through a temp file in the destination folder, flushes it to the disk and
  moves it into place; deletes the temp file on any error or cancel.
- `PercentageProgress` (internal): turns processed bytes into at most 101 whole-percent reports.
- Tests: golden file computed with Python and OpenSSL, chunked output equal to a one-call reference at block and
  chunk boundaries, a stream that returns short reads, progress, cancel and failure cleanup.
- File decryption (`FileEncryptionService.DecryptAsync`, on `IFileEncryptionService`): checks the header and length
  before any work, streams the ciphertext through the HMAC and AES in one pass to a temp file that is hidden on
  Windows, compares the tag in constant time and finishes the last block only after it matched; wrong password,
  tampering, truncation and cancel leave no output and no temp file.
- `AuthenticationFailedException`: wrong password, or the file was changed or damaged.
- `AtomicFileWriter` protects sensitive temp files (decryption): hidden on Windows, owner-only (0600) on Linux and
  macOS, and emptied before the delete when anything fails, so a failed delete leaves no plaintext.
- `docs/NEXT_STEP_PROMPT.md`: a ready-to-paste prompt for starting the next step in a new chat.
- `stryker-config.json`: mutation testing ignores `ConfigureAwait` arguments (no test can observe them).
- `SECURITY.md`: how to report a vulnerability privately, and what is in scope.
- Presentation service interfaces: `IFileDialogService` (native Open/Save dialogs; Save confirms replacing) and
  `IErrorLog` (local technical log, never throws).
- `MainViewModel` with one `MainViewState` (no file → working → file selected → succeeded / failed) and a Browse
  command that reads the file details off the UI thread and shows name, extension and size.
- Encrypt and Decrypt commands: enabled only with a file, a password and no running operation; the Save dialog
  suggests `name.ext.enc` / the name without `.enc`; choosing the input itself as output is refused with a message.

### Changed

- Progress on screen is updated at most 10 times per second (it was about 70 per second for a 100 MB file); the time
  left is shown as "2 h 5 min" from one hour on.
- Cancelling the Save dialog clears an old error message; a bad drop or Browse keeps the previous good file.
- The command-line program refuses a folder or an invalid name as `--output` with a plain message instead of crashing.
- Mutation score of Core is 100 % (new killing tests; 7 equivalent or untestable mutants disabled with written reasons).
- Found in the manual test run (Step 4.3): after a successful Encrypt or Decrypt the chosen file and the password are
  cleared (the result message and "Open folder" stay); the result is a green banner and an error a red one; the Save
  dialog's "Save as type" lists the file type and all files; a "Show password" check box reveals the typed password;
  the "if you forget this password" warning
  is shown only when a password is being chosen (an encrypted file shows "enter the password that was used" instead);
  Encrypt is greyed out for an encrypted file and Decrypt for a normal one; "encrypted" is decided by the file's
  content (its SecureFile signature), so a renamed encrypted file is still recognised.
- The "if you forget this password" note is an amber caution box with a warning symbol and bold text while a password is
  being chosen (not red: red is for errors).
- Password rules when encrypting: at least 8 characters, a confirmation box that must match, an advisory strength bar and
  text (`PasswordStrengthEvaluator`, plan Step 1.8), and a Caps Lock warning. Decrypting applies no rules and shows no
  confirmation box. No symbol/digit rules on purpose.
- The window height now follows its content: it grows when text appears (match line, messages, the confirm box) and
  shrinks when it goes away, up to the screen height, after which it scrolls.
- The line under the buttons is a live "next step": it follows the typed password and confirmation (for example "The two
  passwords are not the same yet." or "Ready. Choose Encrypt file."), and typing clears an old error.
- Friends build: `scripts/publish-beta.ps1` makes a self-contained single-file `win-x64` zip of the Windows app with
  `docs/BETA_README.md` and a SHA-256 file (`artifacts/beta/`, not committed); the README has safety notes, a one-minute
  try-out, a "Which computers" table and feedback questions.
- `docs/SUGGESTIONS.md`: why there is one versioned encryption design (not user-chosen, not random), rules for any AI
  feature, and a roadmap by area with priority and effort.
- Command-line version `securefile` (plan Step 7.6, `src/SecureFile.Cli`): `encrypt` and `decrypt` with a hidden password
  prompt (asked twice when encrypting), `--output`, `--force`, `--password-stdin` for scripts (no `--password` on purpose,
  it would stay in the shell history), a progress line on a terminal, Ctrl+C cancels with no leftover file, exit codes
  0, 1, 2 and 130. Same engine, password rules and messages as the Windows app, so files are interchangeable between
  systems. `scripts/publish-cli.ps1` builds self-contained, trimmed single-file archives (about 6 MB each) for win-x64,
  linux-x64, linux-arm64, osx-arm64 and osx-x64 with a `SHA256SUMS.txt`; `docs/CLI_README.md` ships inside each.
  Tests: `SecureFile.Cli.Tests` (39), also in the Ubuntu/macOS CI job.
- Deleting a temp file after a failed or cancelled run is now tried up to 5 times with short pauses (about 0.4 s in all, failure path only), because a virus scanner may hold the just-closed file for a moment (tested).
- Everything found by the independent review that is not fixed is kept in `docs/REVIEW_BACKLOG.md` (working list, with a suggested order and later findings) and `docs/REVIEW_REPORT_2026-11-24.md` (the full report with evidence and a status per item); plan Step 5.6 covers the burn-down.
- Release `v0.1.0-beta1` (GitHub pre-release) contains the **Windows app only**; the command-line archives for Linux and macOS are not
  released until the Avalonia windows exist (plan Steps 7.1 and 7.2). `docs/RELEASING.md` has the process and the history.
- Review pass (product owner and tester review, see `docs/REVIEW_BACKLOG.md`): typed passwords no longer stay on screen after a  success with Show password on; closing the window during an operation asks first and cancels cleanly; the strength check  rates repeats, runs (`abcdefgh`, `12345678`) and common words (`Password1234`) as Weak; the confirm line prompts when empty,  and a mismatch is red; Cancel is a neutral message (no red banner, nothing logged); old progress text is cleared; a failed  Open folder keeps the success message; a `.enc` file with a damaged start explains itself and still allows Decrypt; focus  moves sensibly (Browse at start, password after a failure, Open folder after success); mnemonic Co_nfirm.
- The confirm box, strength bar and the password warning appear only once a normal file is chosen (not with no file, not
  for an encrypted file); the strength and match texts are coloured to match their meaning; a visible password cannot be
  copied or cut (paste still works).
- Encrypt (and Enter) stay disabled until the password has at least 8 characters and matches its confirmation; the
  Encrypt/Decrypt tooltips appear only while the button is off because of the chosen file.
- Decryption reports invalid padding after a valid tag (only possible for a file another program wrote) as
  `InvalidEncryptedFileException(Damaged)` with the original `CryptographicException` inside, so callers handle one
  documented type (Step 1.7). New constructor `InvalidEncryptedFileException(message, reason, innerException)`.
- A password with invalid Unicode (a lone surrogate) is now refused when `EncryptAsync`/`DecryptAsync` is called, like
  every other invalid argument, instead of when the task is awaited.
- ADR-13 has a failure map: every failure in the test catalog, its exception, its message and its test.
- Test code is exempt from the "async methods end in Async" naming rule (test names follow
  `Method_Condition_Result`).
- CI also runs on every push to `feature/**` branches (Ubuntu only there; macOS runs for pull requests, `main` and
  manual runs); the gitleaks scan allows only the published NIST test key.
- File details of a symbolic link now report the size and change time of the file it points to (Windows reported
  0 bytes); a link to a missing file is reported as not found.
- File sizes below 1 KB use the culture's thousands separator (`1,023 bytes`).
- Architecture tests check referenced assemblies (UI frameworks, network) instead of namespace prefixes.
- ADR-06 revised: file data is processed with our own `TransformBlock` loop instead of a decrypting `CryptoStream`,
  so padding is only checked after the authentication tag matches.
- Quality gate: multi-line pattern checks without comments, fail-closed coverage and git-grep handling, JSON-based
  vulnerability audit, broader secret patterns; commit-msg hook checks the whole first line and allows `fixup!`.
- Solution `SecureFile.slnx` with projects `SecureFile.Core`, `SecureFile.Presentation`, `SecureFile.Wpf`
  and test projects for core, presentation, integration and architecture tests.
- `SecureFile.CrossPlatform.slnf` solution filter for building on macOS and Linux.
- Shared build settings (`Directory.Build.props`): nullable, all .NET analyzers, warnings as errors,
  XML documentation, deterministic builds, NuGet vulnerability audit.
- Central package management (`Directory.Packages.props`).
- Code style rules (`.editorconfig`) and Git file handling rules (`.gitattributes`).
- SDK pin (`global.json`, .NET 10.0.404) and local tools (`dotnet-tools.json`): Stryker.NET,
  ReportGenerator, dotnet-counters, dotnet-trace, dotnet-gcdump.
- WPF app manifest: per-monitor DPI awareness and long-path support.
- GitHub Actions CI (Windows full build; Ubuntu and macOS core build), Dependabot, pull request template.
- Documentation: README, project plan, learning log, ADR index.

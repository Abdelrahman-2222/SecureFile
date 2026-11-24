# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

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

### Changed

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

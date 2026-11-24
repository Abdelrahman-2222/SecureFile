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

### Changed

- File details of a symbolic link now report the size and change time of the file it points to (Windows reported
  0 bytes); a link to a missing file is reported as not found.
- File sizes below 1 KB use the culture's thousands separator (`1,023 bytes`).
- Architecture tests check referenced assemblies (UI frameworks, network) instead of namespace prefixes.
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

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

# Contributing to SecureFile

Thank you for helping. Bug reports, test results from real machines (especially macOS and Linux) and pull requests are
all welcome.

## Reporting a bug or an idea

- **Security problem?** Do not open an issue. Follow [SECURITY.md](SECURITY.md).
- **Bug:** open an issue with the *Bug report* template: your system, the SecureFile version (Help → About, or
  `securefile --version`), what you did, what you expected and what happened. A screenshot or the log file helps
  (the log holds no file names and no passwords; where it is: [Windows](docs/BETA_README.md), [macOS and Linux](docs/DESKTOP_README.md)).
- **Idea:** open an issue with the *Feature request* template. Check [docs/SUGGESTIONS.md](docs/SUGGESTIONS.md) first:
  it may already be on the roadmap.

## Setting up

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the exact version is pinned in
`global.json`). The WPF app needs Windows; everything else builds on macOS and Linux too.

```powershell
git clone https://github.com/Abdelrahman-2222/SecureFile.git
cd SecureFile
dotnet tool restore
git config core.hooksPath .githooks     # enables the pre-push quality gate
dotnet build SecureFile.slnx -c Release
dotnet test --solution SecureFile.slnx -c Release
```

## Making a change

1. Create a branch from `main`: `feature/<short-name>`, `fix/<short-name>` or `docs/<short-name>`.
2. Keep the layers: `Wpf` / `Avalonia` / `Cli` → `Presentation` → `Core`. Core knows no UI (architecture tests check it).
3. Add or update tests with the change:
   - **Core:** full unit tests, including failure paths (wrong password, cut file, cancel, nothing left behind).
   - **ViewModel:** the main path, the state it leaves, and one error.
   - **Windows UI:** a FlaUI test for a behaviour bug; run with `SECUREFILE_UI_TESTS=1`.
4. Run `dotnet format SecureFile.slnx whitespace`, then the full gate: `pwsh -File scripts/verify.ps1`.
5. Add a line to `CHANGELOG.md` under `[Unreleased]`.
6. Open a pull request; the template asks what, why and how it was tested. CI must be green on Windows, Ubuntu and macOS.

## Code rules (the build enforces them)

- Warnings are errors; all .NET analyzers are on.
- XML documentation on every member, public and private; `<remarks>` explain *why*.
- Cyclomatic complexity ≤ 10 and maintainability index ≥ 60 per method: split long methods early.
- `ConfigureAwait(false)` in Core, `ConfigureAwait(true)` in Presentation.
- Exceptions ([ADR-13](docs/adr/0013-exception-handling.md)): Core throws documented exceptions, the ViewModel catches
  only the specific expected ones (never `Exception`), the apps have global safety nets. No empty `catch`, no `throw ex;`,
  no `async void` outside UI event handlers.
- Cryptography uses only the built-in .NET APIs; a change to the file format needs a new version number and an ADR.

## Commits

[Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `ci:`,
`build:`, with a scope where it helps (`fix(cli): …`). First line at most 72 characters, written as a command.
One logical change per commit.

## Code of conduct

Everyone taking part follows the [Code of Conduct](CODE_OF_CONDUCT.md).

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).

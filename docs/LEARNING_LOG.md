# Learning Log

The walkthrough for every step: **what was done, why, what each file and line means, and what you
should be able to explain afterwards.** This file is the raw material for the final guide (`FINAL_GUIDE.md`).

---

## Stage 0 — Setup (2026-11-24)

### 0.1 Tools installed

| Tool | Version | Why we need it | How it was installed |
|---|---|---|---|
| **.NET 10 SDK** | 10.0.404 | The compiler and build tools for .NET 10 (LTS = supported for 3 years). The machine had SDK 9 and 11, not 10. | `winget install Microsoft.DotNet.SDK.10` |
| **GitHub CLI (`gh`)** | 2.105.0 | Create the private GitHub repository and push from the command line. | `winget install GitHub.cli` |
| **Git** | 2.45.1 (already installed) | Version control: keeps the history of every change. | — |
| **xUnit v3 templates** | — | Lets `dotnet new xunit3` create test projects. | `dotnet new install xunit.v3.templates` |

> **winget** is Windows' built-in package manager: it downloads the official installer, checks its hash
> (so the file was not tampered with), and installs it.

### 0.2 `global.json` — pin the SDK

```json
{
  "sdk": { "rollForward": "latestFeature", "version": "10.0.404" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

| Line | Meaning |
|---|---|
| `"version": "10.0.404"` | Inside this folder, use the .NET **10** SDK, not 9 or 11 (all three are installed). |
| `"rollForward": "latestFeature"` | Accept newer 10.0 updates (e.g. 10.0.5xx) but never jump to 11. Security patches still arrive. |
| `"test": { "runner": "Microsoft.Testing.Platform" }` | `dotnet test` uses Microsoft's new test platform (MTP). Added by the xUnit v3 template. Faster and the default direction for .NET 10. |

**Why:** the same SDK on your PC, on CI and on the reviewer's PC → identical builds.

### 0.3 `.gitignore` and `.gitattributes`

- **`.gitignore`** (from `dotnet new gitignore`) lists files Git must **never** store: `bin/`, `obj/` (build output,
  re-created on every build), `.vs/` (Visual Studio's private cache), `*.user` (personal IDE settings), test results.
  Storing them would bloat the repository and cause pointless conflicts.
- **`.gitattributes`** tells Git how to treat file contents:
  - `* text=auto` → Git normalises line endings so a file edited on Windows and on macOS does not show "every line changed".
  - `*.cs text eol=crlf` → C# files keep Windows line endings (matches `.editorconfig`).
  - `*.sh text eol=lf` → shell scripts must keep Unix endings or they break on Linux/macOS.
  - `*.enc binary` → **encrypted test files are never modified by Git.** One changed byte would break the HMAC
    and fail the golden-file tests (T-28).

### 0.4 Solution and projects

Created with `dotnet new` (templates) and `dotnet sln add` / `dotnet add reference`.

```
SecureFile.slnx                         ← the solution: the list of projects (new XML format, default in .NET 10)
SecureFile.CrossPlatform.slnf           ← a "solution filter": the subset that builds on macOS/Linux (no WPF)
src/SecureFile.Core                     ← net10.0         (logic, any OS)
src/SecureFile.Presentation             ← net10.0         (ViewModels, any OS)   → references Core
src/SecureFile.Wpf                      ← net10.0-windows (Windows UI)           → references Presentation, Core
tests/SecureFile.Core.Tests             → references Core
tests/SecureFile.Presentation.Tests     → references Presentation
tests/SecureFile.Integration.Tests      → references Core
tests/SecureFile.Architecture.Tests     → references Core, Presentation
```

| Concept | Explanation |
|---|---|
| **`net10.0` vs `net10.0-windows`** | `net10.0` = APIs available on every OS. `net10.0-windows` adds Windows-only APIs (WPF). Core and Presentation use `net10.0` so they also run on macOS/Linux and can be reused by Avalonia. |
| **Project reference** | "This project uses the code of that project." The arrows go one way only: UI → Presentation → Core. Core never knows a UI exists. |
| **Why WPF references Core directly too** | `App.xaml.cs` (the composition root) must create the Core services and hand them to the ViewModels. |
| **Why separate test projects** | Each layer is tested on its own; Core tests run on every OS in CI without needing WPF. |
| **Template placeholders removed** | `Class1.cs` and `UnitTest1.cs` were empty examples from the templates. Removed because no real code is written in Stage 0. |

### 0.5 `Directory.Build.props` — rules for every project

MSBuild automatically imports this file into every project below its folder. Key lines:

| Setting | What it does | Why |
|---|---|---|
| `Product`, `Company`, `Authors`, `Copyright`, `Version` | Stamped into every `.dll`/`.exe` (visible in *File → Properties → Details*). | Professional, traceable binaries. |
| `LangVersion=latest` | Newest C# features. | Modern, shorter code (e.g. file-scoped namespaces). |
| `Nullable=enable` | The compiler warns when something might be `null`. | Prevents `NullReferenceException`, a top cause of crashes. |
| `ImplicitUsings=enable` | Common namespaces (`System`, `System.IO`, …) are imported automatically. | Less noise at the top of each file. |
| `AnalysisLevel=latest-all` | Turns on **all** built-in analyzer rules, including CA53xx security rules. | Phase 1: quality and security are checked by tools, not by memory. |
| `EnforceCodeStyleInBuild=true` | `.editorconfig` style rules run during the build. | Style problems are caught on every build and in CI. |
| `TreatWarningsAsErrors=true` | Any warning fails the build. | Warnings are fixed immediately; they never pile up. |
| `GenerateDocumentationFile=true` | Produces the XML doc file; a public member without `///` docs becomes an error (CS1591). | Enforces the XML documentation standard (§1.4). |
| `Deterministic`, `ContinuousIntegrationBuild` | Same source → same binary; CI builds do not contain local paths. | Reproducible and privacy-friendly builds. |
| `NuGetAudit*` | On restore, every package (also indirect ones) is checked against the known-vulnerability database. | Phase 1 security: 0 vulnerable packages. |

`tests/Directory.Build.props` imports the root file and adds test-only settings:
`OutputType=Exe` (xUnit v3 tests are small programs), `IsPackable=false`, and two relaxed rules —
**CS1591** (test names are their own documentation) and **CA1707** (underscores allowed in names such as
`Decrypt_WithWrongPassword_Throws`). It also ignores exit code 8 ("zero tests ran") **only until Stage 1.1**,
because the test projects are still empty.

### 0.6 `Directory.Packages.props` — one place for all package versions

"Central Package Management": versions are written here once; each project only names the package.

| Package | Version | Used by | Purpose |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | Presentation | MVVM helpers (ADR-08) |
| xunit.v3.mtp-v2 | 4.0.3 | all tests | Test framework (ADR-10) |
| Microsoft.Testing.Extensions.CodeCoverage | 18.12.0 | all tests | Code coverage (`--coverage`) |
| NetArchTest.Rules | 1.3.2 | Architecture.Tests | Layer rules as tests |
| CsCheck | 5.0.1 | Core.Tests | Property-based tests (random inputs) |

All versions were checked against NuGet for the latest stable release on 2026-11-24.
No crypto package is installed: all cryptography is **built into .NET** (`System.Security.Cryptography`), as the task requires.

### 0.7 `.editorconfig` — code style rules

Read by Visual Studio and by the build. Highlights (all tied to PROJECT_PLAN §1.2–1.3):

| Rule | Effect |
|---|---|
| `csharp_style_namespace_declarations = file_scoped` | `namespace SecureFile.Core;` instead of an extra `{ }` level. |
| `IDE0005` | Unused `using` lines fail the build. |
| `var` rules | `var` only when the type is visible on the same line. |
| `csharp_prefer_braces = true` | Always `{ }`, even for one-line `if`. Prevents a classic bug class. |
| `dotnet_style_require_accessibility_modifiers = always` | Always write `public` / `private` / `internal`. |
| Naming rules | `IInterface`, `PascalCase` types/methods/constants, `_camelCase` private fields, `camelCase` locals, `…Async` suffix. |
| `CA5350/5351/5379/5387/5388/5390/5394 = error` | Weak or broken crypto, weak key derivation, hard-coded keys, `System.Random` for security → build error. |
| `CA2007` off for WPF and tests | `ConfigureAwait(false)` is right for library code but wrong in UI code (the UI must continue on the UI thread). |

### 0.8 The WPF project — what changed from the template, and why

The strict rules immediately found issues in the template, which shows the rules work:

| Analyzer | Problem in the template | Fix | Why it is better |
|---|---|---|---|
| **CA1515** | `App` and `MainWindow` were `public`, but nothing outside the app uses them. | `internal` in C# and `x:ClassModifier="internal"` in XAML. | Smallest possible visibility = smaller surface, clearer intent. |
| **CA1852** | Classes could be inherited although nothing inherits them. | `sealed`. | States the intent; lets the runtime optimise calls. |
| **CA1812** | `MainWindow` was created invisibly by `StartupUri` in XAML, so the analyzer thought it was never used. | Removed `StartupUri`; `App.OnStartup` now creates and shows the window in code. | This is the **composition root** (ADR-09): later the window will receive its ViewModel here through the constructor. |
| **IDE0005** | Template files had ~10 unused `using` lines. | Removed. | Clean files. |

`App.xaml.cs` now contains (every line explained):

```csharp
protected override void OnStartup(StartupEventArgs e)   // WPF calls this once when the app starts
{
    base.OnStartup(e);                                   // let WPF do its own start-up work first
    MainWindow mainWindow = new();                       // create the window (type written out: rule §1.2)
    mainWindow.Show();                                   // display it
}
```

`app.manifest` (new file) tells Windows two things before the app starts:
- **Per-monitor DPI awareness (v2):** the window is redrawn sharply at 100/150/200 % scaling and when moved between screens.
- **Long path awareness:** file paths longer than 260 characters work (edge case T-01).

`MainWindow.xaml`: title `SecureFile`, default size 800 × 560, minimum 640 × 480, centred on screen (UX spec §11.2).

### 0.9 Local tools — `dotnet-tools.json`

Tools installed **per repository** (not globally), so everyone uses the same versions. `dotnet tool restore` installs them.

| Tool | Version | Used in | Purpose |
|---|---|---|---|
| dotnet-stryker | 5.1.0 | Stage 5.3 | Mutation testing (do the tests really catch bugs?) |
| dotnet-reportgenerator-globaltool | 5.6.1 | CI, Stage 5.3 | Turns coverage data into an HTML report |
| dotnet-counters | 10.0.750501 | Stage 5.2 | Live memory/GC/CPU numbers of the running app |
| dotnet-trace | 10.0.750501 | Stage 5.2 | Records a performance trace for analysis |
| dotnet-gcdump | 10.0.750501 | Stage 5.2 | Snapshot of memory to find what uses it |

### 0.10 CI — `.github/workflows/ci.yml`

GitHub runs this on every push and pull request to `main`:

| Job | Runs on | Steps |
|---|---|---|
| **Windows (full solution)** | `windows-latest` | checkout → install SDK from `global.json` → restore → **format check** → **build (warnings = errors)** → **tests with coverage** → coverage report (downloadable artifact) |
| **Cross-platform** | `ubuntu-latest`, `macos-latest` | build + test `SecureFile.CrossPlatform.slnf` → proves Core works on Linux and macOS from day one |

Other details: `permissions: contents: read` (the workflow cannot change the repo — least privilege);
`concurrency` cancels outdated runs; telemetry off.
Note: private repositories get a limited number of free Actions minutes per month, and macOS minutes count more
than Linux/Windows minutes.

### 0.11 Other GitHub files

- **`dependabot.yml`:** weekly checks for newer NuGet packages and GitHub Actions; opens a pull request for each,
  with Conventional Commit prefixes (`build(deps): …`, `ci: …`).
- **`pull_request_template.md`:** every pull request description follows the same structure (What / Why / How tested / Checklist).

### 0.12 Verification results

| Check | Command | Result |
|---|---|---|
| Build, all rules on | `dotnet build SecureFile.slnx -c Release` | ✅ 0 warnings, 0 errors |
| Formatting | `dotnet format SecureFile.slnx --verify-no-changes` | ✅ passes |
| Test runner + coverage | `dotnet test --solution SecureFile.slnx -c Release --coverage` | ✅ runs (0 tests yet, expected) |
| Cross-platform filter | `dotnet build SecureFile.CrossPlatform.slnf` | ✅ builds |

### Checkpoint 0 — answer in your own words

1. What does `global.json` do, and why did we need it on this machine specifically?
2. What is the difference between `net10.0` and `net10.0-windows`? Why is Core `net10.0`?
3. Why does Core not reference WPF? What would break if it did?
4. What does `TreatWarningsAsErrors` buy us? What is the cost?
5. Why are package versions in `Directory.Packages.props` instead of each `.csproj`?
6. Why did we remove `StartupUri` and create the window in `OnStartup`?
7. Why must `*.enc` files be marked `binary` in `.gitattributes`?
8. What does the CI do on every push, and why do Ubuntu and macOS only build part of the solution?

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

### 0.13 Packages per layer — and why Core has none

Output of `dotnet list SecureFile.slnx package`:

| Project | Packages | Why |
|---|---|---|
| **SecureFile.Core** | **none** | Everything it needs (AES, HMAC, PBKDF2, HKDF, random numbers, files, streams) is **built into .NET** in `System.Security.Cryptography` and `System.IO`. The task asks for the built-in .NET cryptographic APIs, so a crypto package would go *against* the task. |
| **SecureFile.Presentation** | CommunityToolkit.Mvvm 8.4.2 | MVVM helpers (ADR-08). |
| **SecureFile.Wpf** | none directly; CommunityToolkit.Mvvm appears as **transitive** | WPF itself is part of .NET (`UseWPF`). It gets the MVVM toolkit *through* its reference to Presentation ("transitive" = inherited from a referenced project). |
| Test projects | xunit.v3.mtp-v2, Microsoft.Testing.Extensions.CodeCoverage (+ CsCheck in Core.Tests, NetArchTest.Rules in Architecture.Tests) | Testing, coverage, property tests, architecture rules. |

**Rule: a package is added only when the step that uses it starts.** Every package is code we did not write,
must trust, keep updated, and ship. Fewer packages means a smaller attack surface, a smaller `.exe` (graded: code size)
and fewer things to explain. Packages planned for later steps:

| Package | Added in | For |
|---|---|---|
| FlaUI.UIA3 | Stage 3.7 | Automated WPF UI test (clicks the real window) |
| BenchmarkDotNet | Stage 5.1 | Performance measurements (MB/s, p95, allocations) |
| Avalonia (+ Avalonia.Desktop, Avalonia.Themes.Fluent) | Stage 7 | Cross-platform bonus app |

Packages we deliberately **do not** use: any third-party crypto library (task requires built-in .NET crypto);
a DI container (pure DI is enough, ADR-09); logging frameworks (a small built-in log is enough for one desktop app).

---

## Stage 0 — Lesson: the concepts behind the setup

Read this first; the checkpoint questions after it are a **self-check**, not an exam. If an answer is not clear
after reading, that tells us which part to explain again.

### Lesson 1 — The SDK, the runtime, and `global.json`

- The **runtime** *runs* .NET programs. The **SDK** (Software Development Kit) *builds* them: compiler, `dotnet build`,
  `dotnet test`, templates. Every SDK also contains a runtime.
- Several SDKs can be installed side by side. Your PC has 9, 10 and 11. When you type `dotnet build`, the newest one
  (11) is used **unless** a `global.json` file says otherwise.
- `global.json` is a small note in the project folder: *"in this folder, use SDK 10.0.404 (or a newer 10.0 patch)"*.
- **Why it matters:** the task targets .NET 10 (LTS = Long-Term Support, 3 years of security fixes). Without the pin,
  your PC would build with SDK 11 and GitHub's CI might use another version → "works on my machine" problems.
- **Analogy:** a recipe that says "bake at 180 °C in a gas oven" — not "use whatever oven is in the kitchen".

### Lesson 2 — Target frameworks: `net10.0` vs `net10.0-windows`

- A **target framework** says which set of APIs a project may use.
- `net10.0` = only APIs that exist on **every** OS (files, crypto, math, collections…).
- `net10.0-windows` = all of that **plus** Windows-only APIs such as WPF windows and buttons.
- Core and Presentation are `net10.0`, so the same code runs on Windows, macOS and Linux — that is what lets the CI
  test them on Ubuntu and macOS, and what lets Avalonia reuse them later. Only the WPF project is Windows-only.
- **Analogy:** a phone charger with a universal USB-C plug (net10.0) vs one with a UK-only wall plug (net10.0-windows).
  Put everything you can on the universal plug.

### Lesson 3 — Layers and the dependency rule

- The app is split into **layers**, each with one job:
  - **Core** = *what* the app does (encrypt, decrypt, read file details). No buttons, no windows.
  - **Presentation** = the *screen logic* (which file is selected, is the Encrypt button enabled, progress %) — still no real window.
  - **Wpf** = the *actual window* (XAML layout, colors, dialogs).
- **Dependency rule:** arrows go only one way: `Wpf → Presentation → Core`. Core never knows a UI exists.
- **Why:**
  1. **Reuse** — Avalonia or a command-line tool can use the same Core without changing it (graded: modularity).
  2. **Testing** — Core can be tested with plain code, no window, on any OS.
  3. **Change safety** — redesigning the screen can never break the encryption.
- If Core referenced WPF, Core would become Windows-only, could not be tested on Linux/macOS, and could not be reused by Avalonia.
- **Analogy:** a car engine (Core) does not depend on the dashboard (UI). You can put the same engine in a different car.

### Lesson 4 — Warnings as errors and analyzers

- **Analyzers** are rules that read your code during the build and point out problems: possible `null` crashes,
  weak cryptography, unused code, naming mistakes. .NET has hundreds built in; `AnalysisLevel=latest-all` turns them all on.
- Normally they produce **warnings**, which people ignore until there are hundreds. `TreatWarningsAsErrors` makes any
  warning **stop the build**, so every problem is fixed the moment it appears.
- **Benefit:** quality is enforced by a tool, not by memory — and it is measurable ("0 warnings").
- **Cost:** sometimes a rule must be explained or turned off on purpose (we did that for test-name underscores, CA1707,
  and wrote down why). You saw the benefit already: the rules found 3 real issues in Microsoft's own WPF template.

### Lesson 5 — NuGet packages and Central Package Management

- A **NuGet package** is a ready-made library someone else wrote (like a downloadable building block).
- Normally each `.csproj` writes the package **and** its version. With many projects, versions drift apart
  (project A on 8.3, project B on 8.4) → confusing bugs.
- **Central Package Management:** versions live in **one** file, `Directory.Packages.props`. Projects only say *which*
  package they use. Updating a version = one line, everywhere at once.
- **Transitive package:** a package you get *through* another project (Wpf gets the MVVM toolkit through Presentation).
- **Analogy:** one shared shopping list for a whole house, instead of every room buying its own different-brand light bulbs.

### Lesson 6 — Why the window is created in `OnStartup` (and not by `StartupUri`)

- The WPF template opened the window with `StartupUri="MainWindow.xaml"` — WPF creates the window *invisibly*, by name.
- Problems: (1) the analyzer could not see the window being used (CA1812); (2) we cannot pass anything into the window,
  but soon the window must receive its **ViewModel** (its screen logic).
- So `App.OnStartup` now creates it in code: `MainWindow mainWindow = new(); mainWindow.Show();`
- This one place where objects are created and connected is called the **composition root** (ADR-09).
  In Stage 3 it will become: create Core services → create the ViewModel with them → create the window with the ViewModel.
- **Analogy:** instead of a robot assembling the car hidden in a closed room, we assemble it on an open workbench where we
  can choose and see every part that goes in.

### Lesson 7 — Git basics used here: `.gitignore`, `.gitattributes`, commits

- **Git** stores the history of the project: every **commit** is a saved snapshot with a message saying *what* and *why*.
- **`.gitignore`** = files Git must never store: build output (`bin/`, `obj/`, re-created every build), Visual Studio's
  private cache (`.vs/`), personal settings. Storing them bloats the repo and causes useless conflicts.
- **`.gitattributes`** = how Git treats file *contents*. Text files can get their line endings converted
  (Windows uses CRLF, Linux/macOS use LF). That is fine for code — but an **encrypted file** must never be touched:
  changing even one byte breaks its HMAC seal and the decryption would correctly refuse it. So `*.enc` is marked `binary`.
- **Conventional Commits:** `type(scope): subject`, e.g. `build: add solution structure` — readable history that tools understand.

### Lesson 8 — CI (Continuous Integration)

- **CI** = a computer at GitHub that, on every push, downloads the code and runs: format check → build → tests.
- If anything fails, you see a red ❌ on GitHub; green ✅ means the code builds and passes on a **clean machine**,
  not only on your PC.
- Our CI has 3 machines: **Windows** builds everything (including WPF), **Ubuntu and macOS** build Core + Presentation + tests.
  They cannot build the WPF project because WPF exists only on Windows — but they prove the encryption logic works on
  Linux and macOS from day one (Phase 5).
- **Dependabot** is a GitHub robot that checks weekly for newer package versions and opens a pull request for each.

---

### Checkpoint 0 — self-check (answer in your own words, open book)

1. What does `global.json` do, and why did we need it on this machine specifically?
2. What is the difference between `net10.0` and `net10.0-windows`? Why is Core `net10.0`?
3. Why does Core not reference WPF? What would break if it did?
4. What does `TreatWarningsAsErrors` buy us? What is the cost?
5. Why are package versions in `Directory.Packages.props` instead of each `.csproj`?
6. Why did we remove `StartupUri` and create the window in `OnStartup`?
7. Why must `*.enc` files be marked `binary` in `.gitattributes`?
8. What does the CI do on every push, and why do Ubuntu and macOS only build part of the solution?

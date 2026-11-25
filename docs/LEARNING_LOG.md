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

---

## Stage 1 — Core library

Stage 1 builds `SecureFile.Core`: everything the app *does*, with no screen. It is built in 8 small steps
(PROJECT_PLAN §7). Every step: **lesson → code → walkthrough → self-check.**

## Step 1.1 — File details and file size text (requirement R2) · 2026-11-24

### Lesson 1.1 — the concepts used in this step

**1. `record` — a type for holding data.**
A `class` is a general type: it has behaviour and state that can change. A `record` is a short way to write a type
whose job is to *hold values*. The compiler writes for us: a constructor, read-only properties, equality by value
(two `FileDetails` with the same values are equal), and a readable `ToString()`.
*Analogy:* a printed receipt — once printed, its contents don't change, and two receipts with the same lines are "the same receipt".
*Why here:* file details are pure data shown on screen; they must not be changed by accident.

**2. `sealed` — nobody may inherit from this class.**
States clearly that the class is complete as it is. It prevents accidental changes of behaviour through inheritance
and lets .NET call its methods slightly faster. The analyzer (CA1852) requires it when nothing inherits.

**3. Interface — a contract without code.**
`IFileDetailsReader` says *"there is a method `Read(path)` that returns `FileDetails`"* but not *how*.
`FileDetailsReader` is the real implementation that reads the disk.
*Why:* in Stage 2 the ViewModel will depend on the **interface**. Its tests can then pass a fake reader that returns
prepared details instantly, without real files. *Analogy:* a wall socket (interface) — any device with the right plug
works; you can test the lamp with a battery pack instead of the power grid.

**4. `static` class — functions without an object.**
`FileSizeFormatter.Format(...)` needs no stored state: same input → same output. A `static` class cannot be
created with `new`; you call its methods directly. *Why not an interface here?* There is nothing to fake —
formatting a number is pure calculation and is fast to test directly.

**5. Exceptions and guard clauses.**
A *guard clause* checks the inputs at the start of a method and stops with a clear exception if they are wrong:
- `ArgumentException.ThrowIfNullOrWhiteSpace(filePath)` → `ArgumentNullException` for `null`, `ArgumentException` for `""` or `"   "`.
- `ArgumentOutOfRangeException.ThrowIfNegative(size)` → a size cannot be negative.
- `FileNotFoundException` → the path does not lead to a file.
*Why:* fail **early and clearly** at the place of the mistake, instead of a confusing error somewhere later.
These are the .NET built-in "throw helpers": one line each, standard messages.

**6. `FileInfo` — .NET's view of one file.**
`new FileInfo(path)` gives `Name`, `Extension`, `Length` (size in bytes), `FullName` (full path), `LastWriteTimeUtc`
and `Exists`. Note: `Exists` is `false` both for a missing file **and** for a folder, so one check covers both.

**7. UTC time.**
UTC (Coordinated Universal Time) is the world reference time with no time zone or daylight-saving changes.
We store UTC and convert to local time only when showing it. *Why:* a value stored in local time becomes ambiguous
when the clock changes or the file moves to another time zone.

**8. Binary units and culture.**
- 1 KB = **1,024** bytes here (binary, 2¹⁰), as Windows File Explorer shows. (macOS and disk makers use 1,000.)
- **Culture** = the number and date rules of a language/region. English writes `1.5`, German `1,5`.
  `IFormatProvider` is the parameter that carries these rules. The UI will pass the user's culture; tests pass
  `CultureInfo.InvariantCulture` (a fixed, region-neutral culture) so results are the same on every machine.
- `string.Create(formatProvider, $"...")` builds the text using exactly that culture (analyzer CA1305 requires
  stating the culture whenever numbers become text).

**9. Unit tests with xUnit.**
- `[Fact]` = one test with fixed data. `[Theory]` + `[InlineData(...)]` = the same test run once per data row.
- **Arrange – Act – Assert:** prepare the input, call the method once, check the result. Blank lines separate the three parts.
- **Test names** say *Method_Situation_ExpectedResult*, e.g. `Read_MissingFile_ThrowsFileNotFoundException`.
- **Property-based test** (CsCheck): instead of a few examples, generate thousands of random inputs and check a rule
  that must *always* hold ("the number shown is always below 1024"). It finds edge cases a human would not think of.
- **Temporary folder per test:** xUnit creates a new test-class object for every test; the constructor makes a fresh
  folder and `Dispose()` deletes it. Tests never see each other's files.
- **Code coverage** = the share of code lines/branches that the tests executed. Step 1.1: **100 %**.

### Walkthrough — `src/SecureFile.Core/Files/`

**`FileDetails.cs`**

| Code | Meaning |
|---|---|
| `namespace SecureFile.Core.Files;` | File-scoped namespace: everything in this file belongs to `SecureFile.Core.Files` (the "file" features of Core). Folder name = namespace. |
| `/// <param name="FullPath">…` | XML docs for each value of the record (shown in IntelliSense). |
| `public sealed record FileDetails(string FullPath, string Name, string Extension, long SizeInBytes, DateTime LastModifiedUtc);` | A **positional record**: the list in brackets creates five read-only properties and a constructor in one line. |
| `long SizeInBytes` | `long` (64-bit) not `int` (32-bit): `int` stops at ~2 GB; files can be bigger. |
| `DateTime LastModifiedUtc` | The name says the time is UTC, so nobody mixes it with local time. |

**`IFileDetailsReader.cs`**

| Code | Meaning |
|---|---|
| `public interface IFileDetailsReader` | The contract. Name starts with `I` (naming rule). |
| `FileDetails Read(string filePath);` | One method. No `public` keyword: interface members are always public. |
| `<exception cref="...">` docs | Tell the caller exactly which errors to expect and when — part of the contract. |

**`FileDetailsReader.cs`**

| Line | Meaning |
|---|---|
| `public sealed class FileDetailsReader : IFileDetailsReader` | The real implementation; `: IFileDetailsReader` = "fulfils that contract". |
| `/// <inheritdoc/>` | Reuse the interface's documentation instead of copying it. |
| `ArgumentException.ThrowIfNullOrWhiteSpace(filePath);` | Guard clause: reject `null`, `""`, `"   "`. |
| `FileInfo fileInfo = new(filePath);` | Create .NET's description of the file (type written out, then target-typed `new()`). |
| `if (!fileInfo.Exists) { throw new FileNotFoundException("...", filePath); }` | Missing file **or** folder → clear error that also carries the path. |
| `return new FileDetails(FullPath: fileInfo.FullName, ...);` | Build the record. **Named arguments** (`FullPath:`) make it impossible to mix up the order of the five values. |

**`FileSizeFormatter.cs`**

| Line | Meaning |
|---|---|
| `public static class FileSizeFormatter` | Pure calculation, no state → static. |
| `private const int BytesPerUnitStep = 1024;` | Named constant instead of the "magic number" 1024. |
| `private const int MaximumDecimalPlaces = 2;` | At most two decimals shown. |
| `private static readonly string[] UnitNames = ["bytes", "KB", ...];` | The unit labels; `[...]` is a C# 12 collection expression. `static readonly` = created once, never replaced. |
| `ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);` | Guard: negative sizes are impossible. |
| `ArgumentNullException.ThrowIfNull(formatProvider);` | Guard: the culture must be given. |
| `if (sizeInBytes == 1) { return "1 byte"; }` | Grammar: "1 byte", not "1 bytes". |
| `double size = sizeInBytes; int unitIndex = 0;` | Work with a decimal number; start at the "bytes" unit (index 0). |
| `while (unitIndex < UnitNames.Length - 1 && Math.Round(size, 2, AwayFromZero) >= 1024)` | *(Replaced in the revision below by `ScaleToLargestFittingUnit`; same logic.)* Keep moving to the next unit while the number **as it would be shown** is still ≥ 1024 — and stop at the last unit (EB). Rounding first avoids the ugly result "1024 KB" for 1,048,575 bytes. `AwayFromZero` is the same rounding the text formatting uses, so the check and the display always agree. |
| `size /= BytesPerUnitStep; unitIndex++;` | Divide by 1024 and step to the next unit name. |
| `return string.Create(formatProvider, $"{size:0.##} {UnitNames[unitIndex]}");` | Build the text with the given culture. Format `0.##`: at most two decimals, no trailing zeros (`1.5`, not `1.50`). |

### Walkthrough — the tests (`tests/SecureFile.Core.Tests/Files/`)

| Test | What it proves |
|---|---|
| `Format_KnownSizes_ReturnsExpectedText` (12 rows) | 0 B, 1 byte, 1023 bytes, exact KB/MB/GB, 1.5 KB/MB, the 1,048,575 rounding edge, > 4 GB, and the largest possible `long` (8 EB). |
| `Format_GermanCulture_UsesCommaAsDecimalSeparator` | Culture is respected (`1,5 KB`). |
| `Format_NegativeSize_…` / `Format_NullFormatProvider_…` | Guard clauses work. |
| `Format_AnyValidSize_ShowsNumberBelow1024WithKnownUnit` | Property test over random sizes from 0 to `long.MaxValue`. |
| `Read_ExistingFile_ReturnsNameExtensionAndSize` | The R2 values are correct. |
| `Read_EmptyFile_ReturnsZeroSize` | 0-byte files work. |
| `Read_VariousFileNames_ReturnsExpectedExtension` (6 rows) | No extension, `.tar.gz` → `.gz`, `.bashrc`, upper-case `.JPG`, spaces/brackets, **Arabic name**. |
| `Read_PathLongerThan260Characters_ReturnsDetails` | Long Windows paths work. |
| `Read_ExistingFile_ReturnsLastModifiedTimeInUtc` | The time is read in UTC. |
| `Read_MissingFile_…` / `Read_FolderInsteadOfFile_…` | Clear `FileNotFoundException`, including for folders. |
| `Read_NullPath_…` / `Read_EmptyOrWhiteSpacePath_…` | Guard clauses work. |

`null!` in a test: the `!` tells the nullable checker "I am passing null on purpose" — needed to test the guard.

### Two rule adjustments made in this step (and why)

1. **Interface members** no longer need `public` written (`for_non_interface_members`): they are public by definition.
2. **`static readonly` fields** use `PascalCase` like constants (e.g. `UnitNames`), not `_camelCase`.
   `dotnet format` caught this — which is why CI runs it on every push.

Also: the "zero tests ran" exception was removed from Core.Tests (it now has tests) and kept only on the three
still-empty test projects, each with a note to remove it when their first test arrives.

### Results

| Check | Result |
|---|---|
| Build | 0 warnings, 0 errors |
| Tests | 31 passed, 0 failed |
| Core coverage | 100 % lines, 100 % branches (8/8), 100 % methods |
| Format | clean |

### Self-check 1.1 (open book)

1. Why is `FileDetails` a `record` and not a normal `class`?
2. Why is `SizeInBytes` a `long` and not an `int`?
3. Why does the reader have an interface, while the formatter is a static class without one?
4. What does `FileInfo.Exists` return for a folder, and how does our code use that?
5. Why does the loop round the number *before* comparing it with 1024? Give the example.
6. Why do the tests pass `CultureInfo.InvariantCulture`, while the app will pass the user's culture?
7. What is the difference between `[Fact]` and `[Theory]`?
8. What does the property-based test check, and why is that stronger than examples alone?

### Step 1.1 — revision: exception handling and the quality gate (2026-11-24)

You asked two things: *"are all possible errors handled so the app never crashes?"* and *"verify every requirement
before any commit or push."* This revision answers both.

#### Lesson — exceptions in .NET (from Microsoft Learn)

- An **exception** is .NET's way of saying "this operation could not be completed". If nobody catches it, it travels
  up the call chain; if it reaches the top, the app closes.
- **Catch only what you can handle.** Microsoft's first rule: *if your code can't recover, don't catch* — let a
  higher level (that can show a message or retry) handle it. Catching everything hides bugs.
- **Throw early with clear types** (guard clauses), **document** what you throw, and **clean up** with `using`/`finally`
  so a failure leaves no half-finished work.
- **Exception families:** `FileNotFoundException`, `DirectoryNotFoundException` and `PathTooLongException` all *derive*
  from `IOException`. One `catch (IOException)` therefore handles every file-system problem.
- **Global safety nets (WPF):** `Application.DispatcherUnhandledException` (UI thread — can keep the app alive),
  `AppDomain.CurrentDomain.UnhandledException` (other threads — can only log), `TaskScheduler.UnobservedTaskException`
  (forgotten tasks). **WinForms** uses `Application.ThreadException` + `SetUnhandledExceptionMode` instead of the first one.
- Our design (**ADR-13**, PROJECT_PLAN §4): **Core throws** precise exceptions → **ViewModel catches the expected ones**
  per button and shows a specific message → **global safety nets** log and show a friendly message for anything
  unexpected. So: never crash, never a stack trace on screen, never a silent failure.

**What Step 1.1 can throw — checked by experiment, not only by reading docs:**

| Input | Exception | Note |
|---|---|---|
| `null` | `ArgumentNullException` (ParamName `filePath`) | our guard |
| `""`, `"   "` | `ArgumentException` (ParamName `filePath`) | our guard — needed because `"   "` is a valid name on Linux/macOS |
| null character in path | `ArgumentException` | thrown by .NET |
| missing file, folder, invalid characters, missing drive, unreachable server, access denied | `FileNotFoundException` | `FileInfo.Exists` reports all of these as "does not exist" |
| path far beyond the OS limit | `PathTooLongException` (Windows) / `FileNotFoundException` (Linux/macOS) | both are `IOException` |
| file deleted between the check and reading the size | `FileNotFoundException` / `IOException` | rare race; documented |

The interface documentation (`IFileDetailsReader`) now lists all of these, and new tests cover them.
Today nothing crashes the app because the app does not yet *call* Core; the ViewModel handling (step 2.6) and the
global safety nets (step 3.1) are scheduled in the plan.

#### Lesson — the quality gate

A **quality gate** is a list of checks that must *all* pass before work moves forward. Ours (PROJECT_PLAN §1.7) has
15 items (G1–G15) covering requirements, security, performance, modularity, code quality, documentation and Git.

- **Automated:** `pwsh -File scripts/verify.ps1` runs format, build, tests, coverage limits, vulnerable packages,
  secrets scan, forbidden code patterns (ADR-13 rules) and the AI-attribution check, then prints PASS/FAIL per check.
- **Enforced by Git hooks** (`git config core.hooksPath .githooks`):
  - `commit-msg` rejects AI attribution and messages that are not `type(scope): subject`;
  - `pre-push` runs `verify.ps1` and cancels the push if anything fails.
- **Code metrics in the build:** analyzers **CA1502** (cyclomatic complexity ≤ 10 per method) and **CA1505**
  (Maintainability Index ≥ 60) read `CodeMetricsConfig.txt`.
  - *Cyclomatic complexity* = number of independent paths through a method (each `if`, loop, `&&` adds one).
  - *Maintainability Index* = 0–100 score from code size, complexity and vocabulary; Microsoft rates 20–100 as good.
- **Mutation testing (Stryker.NET):** the tool changes the code on purpose (e.g. `>=` → `>`, `"TB"` → `""`, removes a line)
  and re-runs the tests. If no test fails, that change "survived" → a gap in the tests.
- **A gate must be able to fail:** every new check was proven by injecting a real violation (an over-complex method,
  `catch (Exception) { }`, `new Random()`, an AI attribution line, a badly formatted commit message) and seeing it rejected.

#### What the gate found in Step 1.1 — and the fixes

| Finding | Fix |
|---|---|
| `latest-all` does **not** switch on CA1502/CA1505 (found because the first probe was not flagged) | Enabled them explicitly in `.editorconfig` |
| `FileSizeFormatter.Format` Maintainability Index 61 (< first target 70) | Extracted `ScaleToLargestFittingUnit` and `RoundForDisplay` — each step now has a name |
| A clear 10-line method still scored 66–69: 70 applied to *every method* is stricter than intended | Limit set to **60** (3× Microsoft's "good" line of 20; tangled code at 51 still fails). Recorded in the plan. |
| A test's `[InlineData]` table counted as "unmaintainable" | CA1505 off for tests (data tables ≠ logic); CA1502 still on |
| Mutation: removing our guard clause went unnoticed (FileInfo throws a similar error itself) | Tests now assert `ParamName == "filePath"` |
| Mutation: emptying the exception message went unnoticed | Test asserts the message is not empty |
| Mutation: changing `"TB"` went unnoticed (random sizes almost never land in TB) | Added exact 1 TB and 1 PB examples |

**Walkthrough of the refactored `FileSizeFormatter`:**

| Code | Meaning |
|---|---|
| `(double size, int unitIndex) = ScaleToLargestFittingUnit(sizeInBytes);` | Call the helper and **deconstruct** its two results into two variables in one line. |
| `private static (double Size, int UnitIndex) ScaleToLargestFittingUnit(long sizeInBytes)` | Returns a **tuple**: two values at once, each with a name. `private` = only this class uses it. |
| `int largestUnitIndex = UnitNames.Length - 1;` | Named value for "the index of EB", instead of an expression inside the loop condition. |
| `while (unitIndex < largestUnitIndex && RoundForDisplay(size) >= BytesPerUnitStep)` | Same logic as before, now readable as a sentence. |
| `private static double RoundForDisplay(double value)` | One job: round exactly like the `0.##` text format, so the check and the display always agree. |

**New architecture tests** (`tests/SecureFile.Architecture.Tests/LayerDependencyTests.cs`):
`Core_DoesNotDependOnPresentationOrAnyUserInterface`, `Presentation_DoesNotDependOnAnyUserInterfaceFramework`,
`Core_ConcreteClasses_AreSealed`. They use NetArchTest to read the compiled code and check the rules; the alias
`using ArchitectureTestResult = NetArchTest.Rules.TestResult;` avoids a name clash with xUnit's own `TestResult`.

#### Quality gate — Step 1.1 results

| # | Check | Result |
|---|---|---|
| G1 | Requirement R2 (name, size, extension) | ✅ implemented and tested |
| G2 | Format | ✅ clean |
| G3 | Build, all analyzers incl. CA53xx | ✅ 0 warnings |
| G4 | Tests | ✅ 40 passed, 0 failed (normal, edge, invalid, property-based) |
| G5 | Core coverage | ✅ 100 % lines, 100 % branches |
| G6 | Vulnerable packages | ✅ none |
| G7 | Secrets | ✅ none |
| G8 | Forbidden patterns | ✅ clean (7 rules) |
| G9 | Exceptions documented, no secrets in messages | ✅ all 5 exception types documented |
| G10 | Architecture | ✅ 3 tests pass |
| G11 | Metrics | ✅ complexity ≤ 10, Maintainability Index ≥ 60 (build-enforced) |
| G12 | Mutation score | ✅ **100 %** (26 of 26 mutants killed; first run 88.5 % → gaps fixed) |
| G13 | Performance | ✅ `Format` 0.36 µs/call, allocates only the result (47 B); `Read` p50 26.5 µs, p95 28.1 µs (i7-8750H, HDD) — far below one 16 ms frame |
| G14 | Docs | ✅ XML docs (build-enforced), changelog, this log, plan |
| G15 | Git | ✅ hooks active; Conventional Commits; no AI attribution |

#### Self-check — revision (open book)

1. Why should Core *not* catch `Exception`? Where are unexpected exceptions handled instead?
2. Why does one `catch (IOException)` cover `FileNotFoundException` and `PathTooLongException`?
3. Which WPF event can keep the app alive after an error, and which can only log?
4. Why did we keep our own guard clause although `FileInfo` throws for empty paths too?
5. What is mutation testing, and what did it reveal that 100 % coverage did not?
6. Why is a gate check useless unless you have seen it fail once?

### Step 1.1 — Review Board report (2026-11-24)

Three independent reviewer agents read the code fresh and only reported (PROJECT_PLAN §1.8):
**(A)** Architect + Performance/Algorithms engineer, **(B)** Security engineer + QA engineer + Critic,
**(C)** Senior PM/Product Owner + End user + Team Lead. 48 findings in total. Every one was fixed or answered.

#### Lesson — what a code review board is for

One author reviewing their own work keeps the same blind spots. Reviewers with *different jobs* ask different
questions: the architect asks "will this design survive the next steps?", the performance engineer "what is the Big-O
and what does it allocate?", the security engineer "what can an attacker control?", QA "what case is missing?", the
critic "what is the weakest part?", the PM "does this serve the requirement and the deadline?", the end user "would
I understand this?", and the Team Lead "can you defend this?". The result is a list of findings with a severity;
the author fixes each one or writes down why not. That record is what makes the work *defensible*.

#### High findings — all fixed

| ID | Finding | Fix |
|---|---|---|
| A1 | The "no UI framework" architecture rule matched the namespace prefix `System.Windows`, which would wrongly fail the first ViewModel that uses `ICommand` (`System.Windows.Input` lives in the cross-platform System.ObjectModel assembly). It only passed because Presentation is still empty. | Rules now check the referenced **assemblies** (PresentationFramework, WindowsBase, Avalonia…). Added "Core has types" so a rule can't pass on nothing. |
| R1 | CI never ran `verify.ps1`; hooks can be bypassed, so "a failing push is impossible" was false. | The Windows CI job now runs the full gate; wording corrected: hooks are a fast local check, **CI is the real gate**. |
| R2 | The empty-catch check could never fire: `git grep` reads one line, but our brace style puts `{` and `}` on separate lines. | Each C# file is now read whole (multi-line patterns) with comments removed first. Proven with a 3-line empty catch. |
| F1–F3 | The plan's own numbers add up to ~5 coding days, plus process time; nothing works end-to-end until Stage 3. | Honest re-baseline (8–10 days, you chose quality over speed); vertical slice proposed as the first milestone after Step 1.7 (your decision). |

#### Medium findings — fixed

| ID | Finding | Fix |
|---|---|---|
| A2 | Complexity (Big-O) not stated as §1.9 requires | `<remarks>` "Complexity:" on `FileDetailsReader` and `FileSizeFormatter` |
| A3 | `Read` can block for seconds on an unavailable network drive | Documented: UI must call it off the UI thread (`Task.Run`); added to UX spec §11.1 |
| A5 / R6 | A symbolic link showed **0 bytes** on Windows (the link, not the file it points to) | `ResolveFinalTarget`: size and time come from the final target; the name stays the one the user chose; broken links → `FileNotFoundException`. Tests run on Linux/macOS CI and on Windows with Developer Mode |
| R6 | Devices/FIFOs on Linux (`/dev/zero`) pass `Exists` with size 0 → later encryption could read forever | Decision: the encryption step (1.5) opens the file and rejects non-seekable streams — the right boundary, since only reading the content is affected |
| R3 / R4 / R13 / R14 | `verify.ps1` could pass silently: `git grep` errors hidden, missing branch-coverage value defaulted to 100, English-text parsing for build warnings and vulnerable packages | git grep exit codes separated (0 found / 1 clean / other = FAIL); missing coverage = FAIL; build judged by exit code; vulnerable packages read from JSON |
| R5 | Secret patterns thin; only the working tree scanned | More patterns (GitHub fine-grained tokens, Slack, Azure keys, `sk-` keys, passwords); **gitleaks** CI job scans the whole history |
| R7 | The property test drew sizes uniformly, so ~99.9 % were PB/EB; and it was weak (a formatter that always says "0 bytes" passed) | Sizes now drawn per magnitude (bit length first); the shown text must convert back to the original size within rounding; 10,000 samples |
| F9 | Planned messages showed the raw .NET message (`{reason}`), said "Windows denied access" in a cross-platform layer, and had no disk-full message | Fixed plain messages; "access was denied"; disk-full and file-in-use messages; details only in the log; file name only, never the full path |
| F10 | Users could think the original is protected after encryption | Success text: "Encrypted copy saved. Your original file was not changed." |
| F11 | A copied generated password stays in clipboard history | Generator is now *Could*; if built: "save it now" warning and clipboard cleared after 30 s |
| F14 / F15 / F17 | Over-claims: "failing push impossible", CodeQL "free" on a private repo, "tested on 3 OS" | Reworded honestly in §1.7, §8.1, §1.9 |
| F16 | Decrypt writes not-yet-verified plaintext to a temp file; left behind after a crash | Trade-off documented in ADR-07 with mitigations (hidden recognisable temp name, deletion in `finally`); two-pass kept as fallback |
| TL | "Where does the password live in memory?" had no answer | Answer added to §5.2 (string limits, no `SecureString`, bytes wiped, honest limit) |
| TL | Iteration "allowed range" had no numbers; OWASP figure unverified | 100,000–10,000,000 on read (too weak / denial of service); OWASP 600,000 verified on the cheat sheet itself |
| TL | Is "keep running after an unexpected error" honest? | ADR-13: data stays consistent (atomic output); ViewModel reset; second error or process-level failure → Close only |
| F4 / F5 | Too much scope for the time; no references file yet | *Could* items marked and time-boxed; usability test reduced to 2–3 people; `docs/REFERENCES.md` created |

#### Low findings and notes — fixed or recorded

| ID | Finding | Decision |
|---|---|---|
| A4 / R12 | Docs promised exceptions that cannot happen (`Read` never opens the file; size is cached after `Exists`) | Docs now say the result is a **snapshot**; speculative "changed while reading" removed; `PathTooLongException` kept (observed) |
| A6 | Wrong comment ("static classes count as sealed" — they are *excluded*) and a magic string | Comment fixed; magic string replaced in Stage 2.2 when Presentation gets its first type |
| A7 | Bonus plans trimming/Native AOT but analyzers are off | `<IsAotCompatible>true</IsAotCompatible>` on Core and Presentation |
| A8 | "Matches Explorer" over-stated; unit words English-only | Remarks reworded (Explorer uses 3 significant digits); English units = recorded decision until translation |
| F12 | `1023 bytes` without thousands separator | `1,023 bytes` (culture-aware `N0`); exact bytes in the UI tooltip |
| F13 | `.enc` files have no icon/hint | Hint text in UX spec §11.1 |
| R8 / R9 / R10 / R11 | Missing tests: UTC kind, 1.5 GB, the exact rounding boundary, relative path, locked/read-only/hidden files; misleading comment on an OS-dependent case | All added; comment corrected |
| R15 | Pattern scan flagged comments; `throw\s+\w+;` blocked legitimate code | Comments stripped before matching; rule dropped (CA2200 in the build covers `throw ex;`); `ReadAllLines` and `Random r = new()` added |
| R16 | Actions pinned by tag; checkout kept the token on disk | Actions pinned to commit SHAs (Dependabot updates them); `persist-credentials: false` |
| R17 | Hook limited only the subject to 72, rejected `fixup!` commits | Whole line ≤ 72; `fixup!`/`squash!`/`amend!` allowed |
| R18 | Exception `FileName` holds the full path (may include the user name) | `ErrorMessageProvider` shows the file name only (ADR-13) |
| R19 | Nothing enforced ADR-12 (no network) | Architecture test bans network assemblies in Core and Presentation — proven by injecting `HttpClient` (test failed as expected) |
| Mutation | The word "bytes" was written twice (the table entry became dead data) | DRY fix: the bytes branch uses `UnitNames[0]` |
| Screenshot | `CodeMetricsConfig.txt` appeared inside every project in Solution Explorer | `Visible="false"` on the shared item |
| F8 | Plan is long and repeats ADRs | Kept: the plan is the single source the presentation and final guide are built from; ADR files are added as each decision is implemented |

#### Your question — "interfaces and services in the same folder?"

Answered in **ADR-14**: organise **by feature** (`Files/`, `Cryptography/`), interface next to its implementation.
SOLID's Interface Segregation is about the *size* of an interface and Dependency Inversion about depending on the
interface *type* — neither is about folders. Grouping by feature gives high cohesion (one change = one folder).
A separate abstractions project becomes right only when another assembly needs the contracts without the code.

#### Complexity summary (G17)

| Operation | Time | Space |
|---|---|---|
| `FileDetailsReader.Read(path)` | O(p) for the path + 1 metadata request (2 for a symbolic link); independent of file size | O(p) |
| `FileSizeFormatter.Format(size, culture)` | O(1) — at most 6 divisions | O(1) — only the result text |

#### Quality gate — Step 1.1 after the Review Board (final)

Windows **Developer Mode** was turned on so the symbolic-link tests run locally too (Windows only lets ordinary
users create symbolic links in Developer Mode; GitHub's Windows, Linux and macOS runners allow it already).

| # | Check | Result |
|---|---|---|
| G1 | Requirement R2 | ✅ |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings |
| G4 | Tests | ✅ 54 passed, 0 failed, 0 skipped |
| G5 | Core coverage | ✅ 100 % lines, 100 % branches |
| G6–G8 | Vulnerable packages, secrets, forbidden patterns | ✅ none / none / clean |
| G9 | Exceptions documented (ADR-13) | ✅ incl. links and snapshot semantics |
| G10 | Architecture (assembly-based UI and network rules) | ✅ 5 tests |
| G11 | Complexity ≤ 10, Maintainability Index ≥ 60 | ✅ build-enforced |
| G12 | Mutation score | ✅ **100 %** (34/34). The last survivor (`returnFinalTarget: true` → `false`) was killed by a new link-chain test |
| G13 | Performance | ✅ unchanged; one extra metadata request only for links |
| G14 | Docs | ✅ |
| G15 | Git | ✅ hooks + CI |
| G16 | System design (ADR-14, SOLID review) | ✅ Review Board |
| G17 | Complexity stated | ✅ `<remarks>` on both public types |
| G18 | Review Board | ✅ 48 findings: all fixed or recorded |

#### Self-check — Review Board (open book)

1. Why did checking the *namespace* `System.Windows` give a wrong rule, and why are *assembly* references better?
2. Why is CI the real quality gate while Git hooks are only a fast local check?
3. Why must a symbolic link's size come from its final target — and which test proves "final"?
4. Why do error messages show fixed text instead of the exception's own message?
5. What is the trade-off in decrypting to a temp file in one pass, and how is the risk reduced?
6. Why is "organise by feature" compatible with Interface Segregation and Dependency Inversion?

## Step 1.2 — The encrypted file header (requirements R3, R4, D2) · 2026-11-24

**Decision first:** you chose **"slice first"**. After Step 1.7 we build the thinnest app that really encrypts and
decrypts a file end to end, before Step 1.8 and the UI polish (PROJECT_PLAN §7).

### Lesson 1.2 — the concepts used in this step

**1. What a file header is, and why we need one.**
An encrypted file is unreadable bytes. To decrypt it later, the app must know *how* it was made: which method, how
many password rounds, and the random values used. These go in a small, fixed block at the start of the file: the
**header**. Ours is 41 bytes. *Analogy:* the label on a medicine bottle. The contents are useless if you don't know
what they are and how to use them.

**2. The signature (also called a "magic number").**
The first 4 bytes are always the letters `SFEN` ("SecureFile ENcrypted"). Many formats do this: PDF files start with
`%PDF` and ZIP files with `PK`. *Why:* in one fast check, without a password, the app can tell "this is our file"
from "this is a photo". The UI will use it to suggest Decrypt or Encrypt (Step 2.4).

**3. The version byte.**
Byte 5 is the format version (`1`). If a future version changes the layout (for example to AES-GCM), it writes `2`.
This app then says *"made by a newer version"* instead of producing garbage. *Why it matters:* without a version, a
format can never change without breaking every old file.

**4. Bytes, numbers and byte order (endianness).**
A file holds bytes (0–255). The iteration count 600,000 needs 4 bytes. The order of those 4 bytes is called
**endianness**. *Little-endian* puts the smallest part first: 600,000 = `0x000927C0` is stored as `C0 27 09 00`.
We fix the order in the format and use `BinaryPrimitives.WriteInt32LittleEndian`, which writes the same bytes on
every computer.
- `BitConverter.GetBytes` uses the computer's own order. That is little-endian on today's PCs and Macs, but it is
  not guaranteed by the format, and it allocates a new array every call. ❌
- `BinaryWriter` needs a stream and is meant for writing many values to a stream. That is too heavy for 41 fixed bytes. ❌
- `BinaryPrimitives` gives the exact byte order, works on a span, and allocates nothing. ✅

**5. `Span<T>`, `ReadOnlySpan<T>`, `stackalloc`, `u8` and `ReadOnlyMemory<T>`.**
- A **span** is a *window* onto existing memory, such as an array or part of one. Slicing it (`source[5..]` or
  `source.Slice(9, 16)`) creates a new window onto the same bytes. Nothing is copied.
- `ReadOnlySpan<byte>` is the same window, but read-only.
- `stackalloc byte[16]` reserves 16 bytes on the *stack* (fast, temporary memory that is freed automatically when
  the method ends). The garbage collector never sees it.
- `"SFEN"u8` is a C# string written directly as UTF-8 bytes, stored inside the program. Reading it allocates nothing.
- A span cannot be kept in a field or across an `await`. To keep bytes in an object, we store a private array and
  hand out a **`ReadOnlyMemory<byte>`**: a read-only handle that *can* be stored and used in async code (needed in
  Steps 1.5–1.6).

**6. Salt and IV: random, unique, but not secret.**
- **Salt** (16 bytes): mixed into the password-to-key function. The same password gives a *different* key for every
  file, so an attacker cannot use one pre-computed table for everyone.
- **IV** (initialisation vector, 16 bytes = one AES block): the random starting block for CBC mode. The same file
  encrypted twice gives different output, so nobody can see that two files are the same.
- Both are stored openly in the header. That is safe and standard: security depends only on the password and the
  keys.
- They come from **`RandomNumberGenerator`**, the operating system's cryptographic random generator. `System.Random`
  is predictable (its output can be guessed) and is banned by our quality gate (G8) and by analyzer CA5394.

**7. Untrusted input: "validate at the boundary".**
When decrypting, the header comes from a file that anyone could have edited. So `Read` checks every value before
using it:
- **Iteration count range 100,000 to 10,000,000.** *Too high:* a crafted file with 2 billion rounds would freeze
  the app for hours before the password could even be checked. This is a denial-of-service attack, and the upper
  limit is the real protection. *Too low:* this app never writes fewer than 600,000, so a low value means damage or
  another program wrote the file. Refusing it is a sanity check (defence in depth). It is **not** what protects
  integrity: a tampered value would also fail the HMAC check (corrected after the Review Board, finding R3).
- **The order of checks gives the most useful answer:** first "is it our file?" (signature), then "can this version
  read it?" (version), then "are the values possible?" (length and range).
- **Fail closed:** anything unexpected is rejected. Nothing is "guessed" or repaired.
- *Is it safe to check before the password/HMAC check?* Yes. These checks look only at public, non-secret values,
  and they only *reject*. A file that passes still has to pass the HMAC check (Step 1.6) before any output is kept.

**8. Custom exceptions (Microsoft guidance) and the `Reason` value.**
- Microsoft: create your own exception type **only when callers need to react differently**. Here, the UI must show
  "this is not an encrypted file" instead of a generic disk error. So we create `InvalidEncryptedFileException`.
- **The three standard constructors** (no arguments, a message, a message plus the inner exception) are expected
  of every exception type. Analyzer CA1032 enforces them.
- **One exception type with a `Reason` value, not three types:** the cause is the same ("this file can't be
  accepted"), and only the *message* differs. One type plus an enum is simpler for the caller (one `catch`, then a
  `switch`) than three types.
- **`SecureFileException` is an `abstract` base:** it lets the command-line bonus catch "any SecureFile error" in
  one place. It is abstract because "a SecureFile error" on its own describes nothing.
- Both were planned for Step 1.7. They moved here because the header is their first user.

**9. Immutability and the defensive copy.**
The constructor *copies* the salt and IV (`salt.ToArray()`). If it only kept the caller's array, the caller could
change it later. The bytes written to the file, the bytes in the HMAC, and the bytes used for the key could then
differ: a hard-to-find bug. Copying 32 bytes is very cheap compared to the safety it buys.

**10. `public` vs `internal`.**
`internal` = visible only inside `SecureFile.Core`. The header is internal because only the encryption service
(also in Core) uses it. The UI never needs it. A smaller public API means fewer things that can be misused and
fewer things we must keep stable. The tests can still see internal types through `InternalsVisibleTo` (set up in
Stage 0).

**11. Three test techniques new in this step.**
- **Golden bytes:** write a header with known values and compare it to an exact, hand-checked hex string. This
  proves the byte order and the positions, and it fails if anyone changes the format by accident.
- **Boundary values:** test exactly at each limit and one step past it (99,999 / 100,000 / 10,000,000 / 10,000,001).
  Most bugs live at the edges (`>` written instead of `>=`).
- **Fuzzing:** feed thousands of random byte arrays to `Read` and assert that it *only* ever returns a header or
  throws our one documented exception. It must never crash with `IndexOutOfRangeException`. This is the
  "impossible input" test that Phase 2 asks for.

### Design review before coding (§1.10)

| Principle | Answer for this step |
|---|---|
| Single responsibility | `EncryptedFileFormat` = *what the layout is* (constants and length rules). `EncryptedFileHeader` = *one header's values*, plus turning them into bytes and back. The exception = *how a rejection is reported*. |
| Open/closed | A v2 format gets a new version number and its own reader. Version 1 code is not edited. |
| DRY | Every size and offset is written once. Offsets are *calculated* from the previous field (`SaltOffset = IterationsOffset + sizeof(int)`), so they cannot disagree. |
| KISS / YAGNI | No interface for the header: there is nothing to swap or fake. Pure byte work is tested directly. No `GetEncryptedLength` yet: Step 1.5 adds it when it needs it. |
| Encapsulation | Private arrays exposed only as `ReadOnlyMemory<byte>`. The type is `internal`. |
| Fail fast / secure by design | Guard clauses in the constructor; `Read` validates every value from the file. |
| Algorithms and data structures | Fixed 41 bytes, so every operation is O(1) time and space. Spans avoid copies; `stackalloc` avoids heap memory. |
| Pattern | **Factory method** (`CreateNew`, `Read`): each named way of creating a header has its own entry point and rules. |

### Alternatives we compared

| Choice | Alternative | Why ours |
|---|---|---|
| Binary header, fixed 41 bytes | JSON or text header | Binary is exact, compact and quick to validate. Text needs a parser, which means more code, more ways to fail and more attack surface. |
| `BinaryPrimitives` (little-endian) | `BitConverter`, `BinaryWriter` | Fixed byte order on every OS and no allocation (see concept 4). |
| `sealed class` with private arrays | `record`, `struct` | A `record` compares arrays by *reference*, so its equality would mislead. A `struct` holding arrays is copied by value but shares the arrays, which is confusing. A class with private copies is simple and safe. |
| One exception + `Reason` enum | Three exception types; returning `bool` or `null` | One `catch` covers it, and the reason drives the message. Returning `null` loses *why*. "Is it our file?" without an exception is still available: `HasSignature`. |
| Rejecting version 0 as *Damaged* | Treating every other version as "unsupported" | Version 0 was never written by any app. Telling the user to "update the app" would be wrong advice. |

### Walkthrough — `src/SecureFile.Core/`

**`SecureFileException.cs`**

| Code | Meaning |
|---|---|
| `public abstract class SecureFileException : Exception` | The base of our own errors. `abstract` = cannot be created on its own, only through a concrete error type. |
| `protected SecureFileException(...)` (3×) | The three standard constructors. `protected` because only derived types call them. |

**`Cryptography/InvalidEncryptedFileReason.cs`**

| Code | Meaning |
|---|---|
| `NotEncryptedFile = 0` | No signature. It is also the default when no reason is given (an enum's default is always its `0` value). |
| `UnsupportedVersion = 1` | Made by a newer app. The user should update. |
| `Damaged = 2` | Our file, but impossible values or cut short. |

**`Cryptography/InvalidEncryptedFileException.cs`**

| Code | Meaning |
|---|---|
| `public sealed class InvalidEncryptedFileException : SecureFileException` | The concrete error. `sealed`, like all Core classes. |
| Three standard constructors | Required by convention and CA1032. They leave `Reason` at its default (`NotEncryptedFile`). |
| `(string message, InvalidEncryptedFileReason reason)` | The constructor our code uses. |
| `if (!Enum.IsDefined(reason)) throw …` | An enum can hold *any* number, for example `(InvalidEncryptedFileReason)99`. This guard stops a value no message exists for. |
| `public InvalidEncryptedFileReason Reason { get; }` | Read-only: set once in the constructor. |

**`Cryptography/EncryptedFileFormat.cs`**

| Code | Meaning |
|---|---|
| `public const string FileExtension = ".enc";` | Public: the UI suggests `report.pdf.enc`. |
| `internal const int VersionOffset = SignatureLength;` … | Each position is built from the one before it, so the layout reads like the table in §5.1. |
| `internal const int HeaderLength = IvOffset + IvLength;` | = 25 + 16 = **41**. |
| `internal const int MinimumFileLength = HeaderLength + AesBlockLength + AuthenticationTagLength;` | = 41 + 16 + 32 = **89**. An empty file still produces one 16-byte block, because PKCS7 padding always adds at least 1 byte. |
| `internal static ReadOnlySpan<byte> Signature => "SFEN"u8;` | The 4 signature bytes, with no allocation. |
| `HasSignature(...)`: `fileStart.StartsWith(Signature)` | Byte-by-byte comparison of the start. Returns `false` for data shorter than 4 bytes. It never throws. |
| `iterations is >= MinimumIterations and <= MaximumIterations` | C# *pattern*: "between the two limits, both included". |
| `GetCiphertextLength`: `encryptedFileLength - HeaderLength - AuthenticationTagLength` | What is left is the ciphertext. It must be at least one block and a whole number of 16-byte blocks. Otherwise the file was cut or extended. |
| `long` everywhere for lengths | Files can be larger than 2 GB. `int` would overflow. |

**`Cryptography/EncryptedFileHeader.cs`**

| Code | Meaning |
|---|---|
| `internal sealed class EncryptedFileHeader` | Only Core uses it. |
| `private readonly byte[] _salt; _iv;` | Our private copies. `readonly` = the field always points to the same array. |
| `ArgumentOutOfRangeException.ThrowIfLessThan(iterations, …MinimumIterations)` / `ThrowIfGreaterThan` | .NET throw helpers. The parameter name `iterations` is filled in automatically. |
| `RequireLength(salt, SaltLength, nameof(salt))` | A small helper used twice (salt, IV), so the rule is written once. |
| `_salt = salt.ToArray();` | The defensive copy (concept 9). |
| `public ReadOnlyMemory<byte> Salt => _salt;` | Read-only view of the copy. |
| `CreateNew`: `stackalloc` + `RandomNumberGenerator.Fill` | Fresh cryptographic random salt and IV, in temporary stack memory. Then the constructor copies them and checks the iterations. |
| `Read`: `if (!HasSignature(source)) throw … NotEncryptedFile` | Check 1: is it ours? |
| `if (source.Length > VersionOffset) ThrowIfVersionIsNotCurrent(source[VersionOffset]);` | Check 2: the version, judged as soon as the byte exists. Even 5 bytes of a newer file give "newer version". |
| `if (source.Length < HeaderLength) throw … Damaged` | Check 3: the whole header must be there. |
| `BinaryPrimitives.ReadInt32LittleEndian(source[IterationsOffset..])` | Read 4 bytes as a number, smallest byte first. |
| `if (!IsSupportedIterationCount(iterations)) throw … Damaged` | Check 4: refuse weak or denial-of-service counts. |
| `return new EncryptedFileHeader(iterations, source.Slice(SaltOffset, SaltLength), …)` | Spans point into the input, and the constructor copies them. Validation was already done, so the constructor's guards cannot fail here. |
| `WriteTo(Span<byte> destination)` | Checks there is room, then copies: signature, version, iterations (little-endian), salt, IV. It writes *into the caller's buffer* (no allocation). |
| `ThrowIfVersionIsNotCurrent` | `> 1` → newer app (`UnsupportedVersion`). `< 1` (0) → never existed (`Damaged`). |

### Walkthrough — the tests (`tests/SecureFile.Core.Tests/Cryptography/`)

| Test | What it proves |
|---|---|
| `Layout_Version1_MatchesTheDocumentedFormat` / `Iterations_…` / `Signature_…` | Every format value is pinned. Changing one breaks old files, so it must fail a test. |
| `HasSignature_VariousStarts_…` (7 rows) | Exact bytes at the very start: case-sensitive, never throws on short or empty data. |
| `IsSupportedIterationCount_Boundaries_…` (9 rows) | Both limits and one past each, plus 0, -1, `int.MinValue`, `int.MaxValue`. |
| `GetCiphertextLength_…` (11 rows + negative) | 89 → 16, two blocks, 1 MiB, 64 GiB (no `int` overflow). 0, 41, 73, 88, 90, 104 and `long.MaxValue` are rejected as *Damaged*. |
| `WriteTo_KnownValues_ProducesExactlyTheDocumentedBytes` | **Golden bytes**: the exact 41-byte hex, including `C0 27 09 00` for 600,000. |
| `Read_KnownBytes_…` / `Read_DataLongerThanHeader_…` | Reading the golden bytes gives back the values. Extra bytes after the header are ignored. |
| `WriteTo_LargerDestination_…` / `WriteTo_DestinationTooShort_…` | Writes exactly 41 bytes. A short buffer is refused *before* anything is written. |
| `CreateNew_TwoHeaders_…` | Salt and IV are random, filled, different each time, and different from each other. |
| `Constructor_…` (limits, wrong salt or IV length, caller changes array) | Guard clauses and the defensive copy. |
| `Read_NoSignature_…` (5) / `Read_NewerVersion_…` (2 + short data) / `Read_VersionZero_…` / `Read_HeaderCutShort_…` (4) / `Read_IterationsOutOfRange_…` (6) / `Read_IterationsAtLimits_…` | Every rejection reason, with the right `Reason` and a non-empty log message. |
| `WriteThenRead_AnyValidValues_…` | Property test (2,000 random cases): writing then reading loses nothing. |
| `Read_RandomBytes_…` / `Read_SignatureFollowedByRandomBytes_…` | **Fuzzing** (5,000 cases each): `Read` never fails in an undocumented way. |
| `InvalidEncryptedFileExceptionTests` (7) | The standard constructors, the reason, the base type, and the guard against undefined reasons. |
| `ReadThenWrite_AnyValidHeaderBytes_…` (added after review) | Property test (2,000 cases): reading valid bytes and writing them again gives **the same bytes**. The format is canonical, so the bytes the HMAC covers are exactly the header we use. |

### Step 1.2 — Review Board report (2026-11-24)

One reviewer agent covered all eight roles (§1.8). It read the code fresh, ran the tests, and mutated the code by
hand (operators flipped, guards removed, IV fill removed). Every one of those hand mutations was caught by an
existing test. **No High findings.**

| ID | Role | Severity | Finding | Decision |
|---|---|---|---|---|
| R1 | Architect / QA | Medium | A trap for Step 1.6. If the decrypt service reads the header with `ReadExactly(41)`, a 3-byte text file gives a *disk error* (`EndOfStreamException`) instead of "not an encrypted file". Calling `GetCiphertextLength` first would also call a short `.txt` file "damaged". | **Fixed:** `Read`'s docs say how to call it (`ReadAtLeast(…, throwOnEndOfStream: false)`, and before `GetCiphertextLength`). The Step 1.6 row lists the two tests that prove it. |
| R2 | PM / End user | Medium | The plan still showed one message for `InvalidEncryptedFileException`, and Step 1.7 still said it would create these types. | **Fixed:** ADR-13 has one message per `Reason`. The Step 1.7 row says what moved to Step 1.2. |
| R3 | Security / Critic | Low | The reason given for the *minimum* iterations was overstated. A lowered count in a tampered file only produces a wrong key, and the HMAC then fails. | **Fixed:** the docs, §5.1 and lesson concept 7 now say "sanity check, defence in depth". The upper limit is the real protection (against denial of service). |
| R4 | QA | Low | The tests proved write → read, but not read → write gives the same bytes. The HMAC design relies on that. | **Fixed:** new property test `ReadThenWrite_AnyValidHeaderBytes_GivesBackTheSameBytes`. |
| R5 | QA | Low | The test docs cited catalog T-02 (round-trip sizes); the right IDs are T-06 to T-08. | **Fixed.** |
| R6 | Critic | Low | The standard constructors leave `Reason` as `NotEncryptedFile` (the enum's `0` value). | **Kept:** CA1032 requires those constructors and CA1008 requires a `0` value. Our code always passes a reason. An extra `Unknown` value would be dead code. |
| R7 | Critic | Low | `Enum.IsDefined` in an exception constructor is close to ceremony, because every caller passes a constant. | **Kept:** it costs one line and is tested. It stops a future `(Reason)99` from reaching the message mapper with no message. |
| R8 | Architect | Note | `HasSignature` takes bytes, so the UI would have to read the file itself. | **Recorded** in the Step 2.4 row: a Core method takes the path, so the ViewModel never reads file bytes. |
| R9–R13 | Several | Note | `ReadOnlyMemory` is right for an internal type. Everything is O(1). Checking the header before the HMAC is standard and unavoidable (the HMAC key needs the salt and iterations). The exception design follows Microsoft guidance. Version 0 → *Damaged* is sensible. | No change. These are good presentation material. |

**Team Lead questions the reviewer would ask (with short answers):**
1. *"You parse untrusted bytes before checking the MAC?"* There is no other way: the MAC key needs the salt and
   iterations from that header. The parse is fixed-size and checks every length. It is fuzzed with 10,000 random
   inputs, and only safe ranges are accepted. Anything tampered but in range is then rejected by the HMAC, which
   covers all 41 header bytes. age, LUKS and WinZip-AES work the same way.
2. *"Why cap iterations at 10 million?"* The key derivation runs before we know whether the file is genuine. Without
   a cap, a crafted file could freeze the app for hours. 10M is about 17× our default: room to grow, but still only
   seconds.
3. *"Why a `Reason` instead of three exception classes?"* Microsoft: a new type only when the caller *handles* it
   differently. Here the handling is the same (stop, write nothing) and only the message differs. .NET does the
   same with `SocketException.SocketErrorCode`.
4. *"How do you know old files will still open?"* The golden-bytes and layout tests pin every byte, and
   `BinaryPrimitives` writes the same bytes on every OS. A new layout gets a new version byte.

#### Complexity summary (G17)

| Operation | Time | Space |
|---|---|---|
| `EncryptedFileFormat.HasSignature` / `IsSupportedIterationCount` / `GetCiphertextLength` | O(1) | O(1), no allocation |
| `EncryptedFileHeader.CreateNew` | O(1): 32 random bytes | O(1): 32 stack bytes + the header (2 × 16-byte arrays) |
| `EncryptedFileHeader.Read` | O(1): fixed 41 bytes, no copies except the two 16-byte arrays kept | O(1) |
| `EncryptedFileHeader.WriteTo` | O(1): 41 bytes into the caller's buffer | O(1), no allocation |

#### Quality gate — Step 1.2

| # | Check | Result |
|---|---|---|
| G1 | Step 1.2 "Done when": round-trip, wrong magic, unknown version, iterations out of range, file too short | ✅ all tested, plus golden bytes and fuzzing |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings |
| G4 | Tests | ✅ 134 passed (80 new), 0 failed, 0 skipped |
| G5 | Core coverage | ✅ 100 % lines, 100 % branches |
| G6–G8 | Vulnerable packages, secrets, forbidden patterns | ✅ none / none / clean |
| G9 | Exceptions documented, no secrets in messages (ADR-13) | ✅ every `throw` has an `<exception>` entry; messages hold only format facts |
| G10 | Architecture | ✅ new classes are sealed, or the abstract base; no new references |
| G11 | Complexity ≤ 10, Maintainability Index ≥ 60 | ✅ build-enforced |
| G12 | Mutation score | ✅ **100 %** (64/64). The first run gave 95.3 %. 3 survivors replaced an argument exception's message with `""`. They survived because .NET adds *"(Parameter 'x')"*, so a blank message still looked non-empty. The tests now check that each message states the rule ("41 bytes", "exactly 16 bytes", "defined value"). |
| G13 | Performance | ✅ fixed-size work, no allocation in `WriteTo` and the checks |
| G14 | Docs | ✅ XML docs, CHANGELOG, plan status, this lesson |
| G15 | Git | ✅ hooks + CI |
| G16 | System design | ✅ Review Board (Architect) |
| G17 | Complexity stated | ✅ `<remarks>` on every type, and the table above |
| G18 | Review Board | ✅ 13 findings: 7 fixed or recorded, 6 notes |

### Self-check 1.2 (open book)

1. What is the signature (`SFEN`) for? Name two other file formats that use one.
2. Why is the iteration count stored *little-endian*, and why `BinaryPrimitives` instead of `BitConverter`?
3. The salt and IV are stored in plain view. Why is that safe? What would go wrong if the IV were always the same?
4. Why is there an *upper* limit on the iteration count? Which attack does it stop?
5. Why does `Read` check the version **before** the header length? Give the example.
6. Why does the constructor copy the salt and IV instead of keeping the caller's arrays?
7. Why one exception type with a `Reason`, and not three exception types?
8. What does the fuzz test prove that the example tests cannot?
9. Why did three mutants survive the first Stryker run, and how did the tests change?

## Step 1.3 — Key derivation: from a password to two keys (requirements R3, R4) · 2026-11-24

### Lesson 1.3 — the concepts used in this step

**1. Why a password cannot be the key.**
AES-256 needs a key of exactly 32 random bytes (256 bits). A password is short, typed by a human, and easy to
guess. A **key derivation function (KDF)** turns the password into a key. It does three things:
1. It makes the length right.
2. It mixes in the **salt**, so the same password gives a different key in every file.
3. It is deliberately **slow**, so every guess an attacker makes costs real time.

**2. PBKDF2: slow on purpose.**
PBKDF2 ("Password-Based Key Derivation Function 2", RFC 8018) runs HMAC-SHA256 over and over, 600,000 times here,
feeding each result into the next round.
- **For us:** about **0.46 s once per file** (measured on this i7-8750H: 600k rounds ≈ 0.46 s median, 100k ≈ 67 ms).
- **For an attacker:** 0.46 s *for every password they try*. 600,000 rounds means 600,000 times more work per guess
  than a single hash.
- Why not just `SHA256(password)`? A graphics card computes billions of SHA-256 hashes per second, so a weak
  password would fall in seconds.
- Why not Argon2 (the modern winner)? It is not built into .NET, and the task requires the built-in crypto APIs.
  PBKDF2-HMAC-SHA256 at 600,000 rounds is OWASP's recommendation for when Argon2 is not available.
- The 10,000,000 cap from Step 1.2: on this machine that is about **7 s**, the longest a crafted file can make us wait.

**3. HKDF: one master key, two independent keys.**
We need **two** keys: one for AES (to hide the data) and one for HMAC (to detect changes). Using one key for two
jobs is a classic mistake. HKDF (RFC 5869) has two parts:
- **Extract:** turns uneven input into a uniformly random key. We skip it, because PBKDF2's output already *is* a
  uniformly random 32-byte key. RFC 5869 §3.3 allows skipping Extract in this case.
- **Expand:** turns that key plus a **label** into as many key bytes as needed. Different label → unrelated key.
  Our labels are `"SecureFile v1 encryption"` and `"SecureFile v1 authentication"`. This is called **domain
  separation**: each key is tied to one purpose and to format v1.
- **Why not ask PBKDF2 for 64 bytes and cut them in half?** PBKDF2 computes each 32-byte block *separately*, with
  all 600,000 rounds each. We would pay 2 × 0.46 s. An attacker only needs **one** block to test a guess (the HMAC
  key alone tells them whether the tag matches), so they would still pay only once. Double cost for us, no extra
  safety. With HKDF, the slow part runs once and the split costs microseconds.

**4. Unicode normalisation (NFC): the "same" password as different bytes.**
The letter "é" can be stored in two ways:
- one character, U+00E9 (typical on Windows);
- "e" plus a combining accent, U+0065 U+0301 (common on macOS).

They look identical on screen but are different bytes, so they would give **different keys**. A file encrypted on a
Mac would then refuse the "correct" password on Windows. **NFC** ("Normalization Form C", for "composed") rewrites
both into the same form before hashing.
- **NFC, not NFKC:** NFKC also merges *look-alikes*. The ligature "ﬁ" becomes "fi" and "²" becomes "2", so different
  passwords would become equal. We keep those as typed.
- **Nothing else is changed.** Case matters and spaces are kept (never trimmed): the password is exactly what was
  typed, apart from the accent form.

**5. UTF-8, strictly.**
After normalising, the characters become bytes with UTF-8 (1–4 bytes per character; Arabic uses 2, an emoji 4).
We use a **strict** encoder that throws on invalid text, such as half of an emoji (a "lone surrogate"). The default
encoder would silently replace it with "�", so two *different* broken passwords would give the *same* key.
*What mutation testing taught us here:* the NFC step already rejects invalid text, so today the strict encoder never
sees any. It stays as a second guard (defence in depth), and the error is re-reported with *our* parameter name,
`password`, instead of the runtime's internal one.

**6. Memory hygiene: keeping secrets in memory for as short a time as possible.**
- `CryptographicOperations.ZeroMemory` overwrites a buffer with zeros. Unlike a normal loop, the compiler cannot
  remove it as "useless".
- **`finally`** makes the wipe run even if something throws.
- **Pinned arrays** (`GC.AllocateUninitializedArray<byte>(n, pinned: true)`): the garbage collector moves normal
  arrays around in memory to compact the heap, and each move can leave an old copy behind that we cannot wipe.
  A pinned array never moves.
- **`stackalloc`** for the master key: stack memory is never moved and is wiped in `finally`.
- **Honest limit:** the password arrives as a .NET `string`, which cannot be wiped (PROJECT_PLAN §5.2). We make no
  *extra* copies we cannot wipe, but a memory dump taken during the operation could still find it.

**7. `IDisposable` and `using`: a visible lifetime for the keys.**
`FileKeys` holds the two keys. Its `Dispose()` wipes them, and `using FileKeys keys = …;` calls `Dispose()`
automatically at the end of the block, even after an exception. After disposal, reading a key throws
`ObjectDisposedException`, so a wiped (all-zero) key can never be used by mistake. The keys are handed out as
`ReadOnlySpan<byte>`: the caller can give them to `Aes.SetKey(...)` and `IncrementalHash.CreateHMAC(...)` (both
accept spans in .NET 10) but cannot keep or change them.

**8. Known-answer tests and an independent oracle.**
- **Known-answer test (KAT):** feed in a published input and compare with the published output. RFC 7914 §11 gives
  two PBKDF2-HMAC-SHA256 vectors; RFC 5869 Appendix A gives HKDF vectors. If we used SHA-1 by mistake, or swapped
  two arguments, the KAT would fail.
- **Independent oracle:** for the whole chain (normalise → UTF-8 → PBKDF2 → HKDF), no published vector exists, so the
  expected keys were computed with a **different implementation** (Python's `hashlib`, `hmac` and `unicodedata`).
  Comparing our code with itself would prove nothing; comparing with an unrelated implementation does.

### Design review before coding (§1.10)

| Principle | Answer for this step |
|---|---|
| Single responsibility | `KeyDerivation` = password → keys. `FileKeys` = holds two keys and wipes them. Neither reads files or encrypts. |
| KISS / YAGNI | A `static` class, not an interface: there is exactly one correct key derivation for format v1 and nothing to swap. A v2 format can add its own. Tests run the real (fast enough) code. |
| DRY | `KeyLength` is written once. Each algorithm call is in one small method (`DeriveMasterKey`, `ExpandKey`), and those exact methods are checked by the published vectors. |
| Encapsulation | Keys only as read-only spans; unusable after `Dispose`. Both types are `internal`. |
| Fail fast | Empty password, missing header and invalid Unicode are rejected before any slow work. |
| Secure by design | Salt from the header, OWASP iteration count, domain-separated keys, strict encoding, wiping in `finally`, no secrets in exception messages. |
| Algorithms | O(i) time for i rounds: the cost is *intended*. Only one slow PBKDF2 run per file (HKDF instead of 64-byte PBKDF2). O(p) temporary memory for the password bytes, O(1) for the keys. |
| Pattern | **Dispose pattern** (deterministic cleanup of a resource, here secret memory). |

### Alternatives we compared

| Choice | Alternative | Why ours |
|---|---|---|
| PBKDF2-HMAC-SHA256, 600k | Argon2id, scrypt | Built into .NET and required by the task; OWASP's PBKDF2 setting. Argon2 noted as a v2 option. |
| PBKDF2 → HKDF-Expand | PBKDF2 with 64 bytes; one key for both jobs | Half the waiting time with the same safety; never reuse one key for two purposes. |
| NFC | No normalisation; NFKC | Same password on every OS; NFKC would merge different passwords. |
| Strict UTF-8 | `Encoding.UTF8` | No silent "�" replacement. |
| Pinned arrays + `ZeroMemory` | `SecureString` | Microsoft no longer recommends `SecureString` for new code, and it must become plain bytes to be used anyway. |
| `FileKeys : IDisposable` | Returning a `byte[]` | The lifetime and the wipe are visible and automatic with `using`. |

### Walkthrough — `src/SecureFile.Core/Cryptography/`

**`KeyDerivation.cs`**

| Code | Meaning |
|---|---|
| `internal static class KeyDerivation` | Pure computation with no state, used only inside Core. |
| `internal const int KeyLength = 32;` | 256 bits: AES-256 and HMAC-SHA256 key size. |
| `StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)` | No byte-order mark at the start; throw on invalid text. Created once (`static readonly`). |
| `EncryptionKeyLabel => "SecureFile v1 encryption"u8` | The HKDF label, as bytes with no allocation. |
| `DeriveKeys(ReadOnlySpan<char> password, EncryptedFileHeader header)` | A `string` converts to `ReadOnlySpan<char>` automatically, so the ViewModel can pass `PasswordBox.Password`. The header gives the salt and the iteration count. |
| `if (password.IsEmpty) throw …` / `ThrowIfNull(header)` | Guard clauses, before any slow work. |
| `byte[] passwordBytes = PasswordToBytes(password);` | Normalised, UTF-8, pinned. |
| `Span<byte> masterKey = stackalloc byte[KeyLength];` | The master key lives only on the stack. |
| `try { DeriveMasterKey(…); return SplitMasterKey(masterKey); } finally { ZeroMemory(…) ×2 }` | Slow step, then fast split; both temporaries are wiped whatever happens. |
| `SplitMasterKey` | Two pinned 32-byte arrays, filled by HKDF-Expand with the two labels, handed to `FileKeys`. (It is its own method because the maintainability gate, CA1505, scored `DeriveKeys` at 58 with it inline, below our limit of 60.) |
| `PasswordToBytes`: `try { return NormalizeAndEncode(password); } catch (ArgumentException e) { throw new ArgumentException("…invalid Unicode…", nameof(password), e); }` | A *specific* catch that only re-labels. The runtime names its own parameter (`source`); we report ours (`password`) and keep the original as `InnerException` for the log. |
| `NormalizeAndEncode`: `password.GetNormalizedLength(FormC)` | How many characters the NFC form needs, so the buffer has exactly that size. |
| `if (!password.TryNormalize(normalized, out int charsWritten, FormC)) throw new CryptographicException(…)` | NFC straight into our pinned buffer (a .NET 10 span API). The `string` version would create another copy we could not wipe. The check "cannot fail", but if it ever did, going on would encrypt with an **empty** password. So it throws (fail closed). The first version used `Debug.Assert`, which Release builds remove (Review Board R1). |
| `StrictUtf8.GetByteCount` / `GetBytes` | Size, then encode into a pinned byte array. |
| `finally { ZeroMemory(MemoryMarshal.AsBytes(normalized.AsSpan())); }` | Wipe the normalised characters. `AsBytes` views the `char` array as bytes, because `ZeroMemory` works on bytes. |
| `AllocatePinned<T>(int length) where T : unmanaged` | The one place that creates every pinned array (DRY). `unmanaged` = plain value types such as `byte` and `char`. |
| `// Stryker disable once … : reason` | Tells the mutation tester to skip a line no test can observe (wiping, pinning) or that can never run. **Each one has a written reason** and is checked by review (G9). |
| `DeriveMasterKey` → `Rfc2898DeriveBytes.Pbkdf2(…, HashAlgorithmName.SHA256)` | The one-call static PBKDF2 API (no object to dispose). |
| `ExpandKey` → `HKDF.Expand(HashAlgorithmName.SHA256, masterKey, destination, label)` | RFC 5869 Expand. |

**`FileKeys.cs`**

| Code | Meaning |
|---|---|
| `internal sealed class FileKeys : IDisposable` | Holds two keys; wipes them on `Dispose`. |
| Constructor `RequireKeyLength(…)` | Both keys must exist and be exactly 32 bytes. The object takes **ownership**: it will wipe these arrays. |
| `EncryptionKey { get { ObjectDisposedException.ThrowIf(_disposed, this); return _encryptionKey; } }` | Read-only view; refused after wiping. |
| `Dispose()`: `ZeroMemory` ×2, `_disposed = true` | Wipe; safe to call twice. |

### Walkthrough — the tests

| Test | What it proves |
|---|---|
| `DeriveMasterKey_Rfc7914Vectors_…` (2) | Our PBKDF2 call matches the RFC's published outputs (also confirmed with Python before writing them in). |
| `ExpandKey_Rfc5869Vectors_…` (2) | Our HKDF-Expand call matches RFC 5869 test cases 1 and 3. |
| `DeriveKeys_KnownInputs_MatchIndependentlyComputedKeys` (2) | The whole chain, including Arabic, an accent and an emoji, matches Python's independent result. |
| `DeriveKeys_SamePasswordTypedDifferently_…` | "café" typed as é or as e + accent gives the same keys (NFC). |
| `DeriveKeys_AnyInput_GivesTwoDifferentKeys` | The labels really separate the two keys. |
| `DeriveKeys_SlightlyDifferentPassword_…` (3) | One character less, a capital letter or a trailing space gives a different key. |
| `DeriveKeys_DifferentSaltOrIterations_…` / `DeriveKeys_IvIsNotPartOfTheKey_…` | The key depends on salt and iterations, and *not* on the IV. |
| `DeriveKeys_ShortPassword_…` / `DeriveKeys_VeryLongPassword_…` | One space, one letter and 10,000 characters are all accepted (strength is Step 1.8's job). |
| Empty password, `null` header, lone surrogate | Rejected with the documented exceptions. The lone-surrogate error names `password` and keeps the runtime's error inside. |
| `PasswordToBytes_VariousText_…` (8) | Exact bytes for ASCII, é in both forms, Å (Ångström sign), the "ﬁ" ligature (NFC keeps it), Arabic, an emoji, and spaces. |
| `FileKeysTests` (7) | Keys are readable before disposal, and **both arrays are all zeros after `Dispose`**. A span read *before* `Dispose` shows zeros afterwards: a span is a window, not a copy. Access after disposal throws; double `Dispose` is safe; a wrong length or `null` is rejected. |

### Step 1.3 — Review Board report (2026-11-24)

One reviewer agent covered all eight roles. It ran the tests and probed the .NET 10 behaviour in a throwaway
program: a lone surrogate fails inside normalisation with parameter `source`, and a 50-million-character password
costs about 88 ms to convert. **No High findings.**

| ID | Role | Severity | Finding | Decision |
|---|---|---|---|---|
| R1 | Critic / Security | Medium | `Debug.Assert` is removed in Release. If normalisation ever failed, the file would be encrypted with an **empty password**, silently: fail-open where it must fail closed. | **Fixed:** a real check that throws `CryptographicException`. |
| R2 | QA / Security | Medium | The lone-surrogate test passed because of *normalisation*, not strict UTF-8 (strict mode was dead code for that input, and Stryker proved it). The error named the runtime's parameter `source`. | **Fixed:** the error is re-reported with `password` and keeps the original inside; the test checks both. Strict UTF-8 is kept as a documented second guard. |
| R3 | QA | Medium | The first Stryker run scored **65.6 %**. The survivors were the wipes of temporaries, the `pinned` flags, the `Debug.Assert`, and the encoder flags. None of these can be observed by a black-box test. | **Fixed honestly:** no test-only "seam" (over-engineering). Each such line has a `// Stryker disable once … : reason` comment: 11 mutants in total, all listed in the gate table. One `AllocatePinned` helper replaced four `pinned: true` flags. Score: **100 %**. |
| R4 | Security | Low | "No stray copies are left behind" overstated it: copies inside the runtime, the OS crypto library and the caller's string are outside our control. | **Fixed:** the class docs say exactly what is and is not wiped. |
| R5 | Security / Perf | Low | No password length limit. A 100 MB password costs ~0.5 GB of short-lived memory (not CPU: PBKDF2 hashes a long password once). | **Recorded** for Step 1.8 and the UI (`PasswordBox.MaxLength`, e.g. 1,024). Core stays simple; the user is local. |
| R6 | Security | Note | Skipping HKDF-Extract is sound (RFC 5869 §3.3, NIST SP 800-132 §5.4). | **Added** the references to `ExpandKey`'s docs. |
| R7 | Security | Note | NFC trade-off: full-width "Ａ" ≠ "A". | **Documented** in `PasswordToBytes`. |
| R8 | Architect | Low | If the second `ExpandKey` threw, the first key would not be wiped. | **Accepted:** `HKDF.Expand` only throws for invalid lengths, and ours are constants. A try/catch for an impossible case would be ceremony. |
| R9 | Architect | Note | A static class plus disposable `FileKeys` is the right shape. In 1.5/1.6 call `await Task.Run(() => KeyDerivation.DeriveKeys(password, header), ct)` with the `string` (a span cannot be captured in a lambda). | **Recorded** for Step 1.5. |
| R10 | QA | Low | Add a test that documents "a span read before `Dispose` shows zeros after". | **Added.** |
| R11 | QA | Low | The test source held *raw* combining characters. An editor that normalises on save would silently turn two test rows into duplicates. | **Fixed:** every non-ASCII character inside test strings is now a `\u` escape. |
| R12 | Perf | Note | Cost is PBKDF2; everything else is microseconds. | No change. |
| R13 | End user | Note | Caps Lock, keyboard layout and a trailing space copied from a password manager all give "wrong password". | **Recorded** for the UI (§11 already has a Caps Lock warning): the wrong-password message will add "check Caps Lock, keyboard layout and spaces". Never trim silently. |
| R14 | PM | Note | Acceptance met and exceeded. | — |

**Team Lead questions (short answers):**
1. *"Why not 64 bytes from PBKDF2?"* Each 32-byte block is a full 600k run. The attacker needs only one block to test
   a guess, so we would pay double for nothing.
2. *"Why skip HKDF-Extract?"* Extract makes uneven input uniform. PBKDF2's output already is uniform, and RFC 5869
   §3.3 allows going straight to Expand.
3. *"Is 600k enough? Why not Argon2?"* It is OWASP's setting for PBKDF2-SHA256 (≈0.46 s here). Argon2 is not built
   into .NET, and the task requires built-in crypto. The count is stored in each file's header, so it can be raised
   later without breaking old files.
4. *"Does zeroing protect the password?"* Partly, and we say so. Our own copies are pinned and wiped. The `string`
   and the crypto library's internals are not ours to wipe. It shortens exposure; it does not stop an attacker who
   can already read our memory.
5. *"How do you test that something was zeroed?"* For `FileKeys`: directly. For temporaries inside a method: we
   can't without a test-only seam, so those lines are excluded from mutation testing *with a written reason* and
   checked in review.

#### Complexity summary (G17)

| Operation | Time | Space |
|---|---|---|
| `KeyDerivation.DeriveKeys` | O(i + p): i PBKDF2 rounds (≈0.46 s at 600k on this machine) + p password characters | O(p) temporary (wiped), O(1) result |
| `KeyDerivation.PasswordToBytes` | O(p) | O(p) |
| `FileKeys` (access, `Dispose`) | O(1) | O(1) |

#### Quality gate — Step 1.3

| # | Check | Result |
|---|---|---|
| G1 | "Done when": RFC 7914 §11 and RFC 5869 test case 1 pass | ✅ plus RFC 5869 test case 3 and two independent golden vectors |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings. CA1505 caught `DeriveKeys` at MI 58 and it was split. |
| G4 | Tests | ✅ 170 passed (36 new), 0 failed, 0 skipped |
| G5 | Core coverage | ✅ 99.4 % lines, 97.8 % branches (limits 90 / 85). The only code never run is the fail-closed `throw` after `TryNormalize` (1 line, 1 branch): it exists for a case that cannot happen, so no test can reach it. We report the real number instead of hiding it. |
| G6–G8 | Vulnerable packages, secrets, forbidden patterns | ✅ none / none / clean. The one `catch` is specific (`ArgumentException`) and rethrows. |
| G9 | Exceptions and secrets | ✅ all documented; messages never contain the password; every temporary is wiped in `finally` (reviewed line by line) |
| G10 | Architecture | ✅ both new types sealed/static and internal |
| G11 | Complexity ≤ 10, MI ≥ 60 | ✅ after the split |
| G12 | Mutation score | ✅ **100 %** of testable mutants. 11 mutants are excluded, each with a reason: 5 wipes, 1 pinning flag, 3 on the unreachable fail-closed throw, 2 equivalent encoder flags. |
| G13 | Performance | ✅ measured: PBKDF2 600k ≈ 0.46 s median (i7-8750H); one PBKDF2 run per file |
| G14–G15 | Docs, Git | ✅ |
| G16–G18 | Design, complexity, Review Board | ✅ 14 findings: 11 fixed or recorded, 3 notes |

### Self-check 1.3 (open book)

1. Why can't the password itself be the AES key? Name the three jobs of a KDF.
2. What does 600,000 iterations cost the user, and what does it cost an attacker?
3. Why do we run PBKDF2 once and then HKDF, instead of asking PBKDF2 for 64 bytes?
4. What are the two labels for? What would go wrong if one key did both jobs?
5. Give an example where the same visible password is two different byte sequences. How does NFC fix it, and why not NFKC?
6. Why are the temporary arrays *pinned*? What can we not wipe, and why?
7. Why did the first version's `Debug.Assert` become a real `throw`?
8. What is a known-answer test, and why were the golden keys computed with Python and not with our own code?
9. Why are some lines excluded from mutation testing, and how do we keep that honest?

## Step 1.4 — AES-256-CBC, checked against NIST (requirement R3) · 2026-11-24

### Lesson 1.4 — the concepts used in this step

**1. AES is a block cipher.**
AES (the Advanced Encryption Standard, FIPS 197) takes exactly **16 bytes** (one *block*) and a key, and turns them
into 16 bytes that look random. With the key, the block can be turned back. *Analogy:* a lockbox that holds exactly
16 letters. The key size picks the strength: 16 bytes = AES-128, 24 = AES-192, **32 = AES-256** (ours). .NET
picks the size *from the key length*, so a 16-byte key by mistake would silently give AES-128. That is why our
factory refuses any key that is not exactly 32 bytes.

**2. A mode of operation: how to encrypt more than 16 bytes.**
Files are longer than one block, so a *mode* says how the blocks are chained.
- **ECB** (each block alone) is broken: identical blocks give identical output, so patterns stay visible (the
  famous "ECB penguin" picture is still recognisable after encryption). The analyzer forbids it (CA5358).
- **CBC** (Cipher Block Chaining, the mode the task requires): before encrypting a block, XOR it with the
  *previous ciphertext block*. The first block is XORed with the **IV**. Every output block then depends on all the
  data before it, so repeated data does not repeat in the output.

**3. Why the IV must be random for every file.**
With the same key and the same IV, the same file gives exactly the same bytes, so an observer could tell that two
encrypted files are equal (test `Encrypt_SameDataWithDifferentIv_…`). A fresh random IV per file (from the header,
Step 1.2) prevents that. The IV is not secret.

**4. PKCS7 padding: filling the last block.**
AES works on whole 16-byte blocks, so the last block is filled up. PKCS7 adds *n* bytes, each holding the value *n*:
1 byte missing → `01`; 15 missing → fifteen `0F`. If the data already fills the last block, a **whole extra block**
of sixteen `10` (16) is added. Otherwise the decryptor could not tell padding from real data that happens to end in
`01`. So the encrypted size is always `(⌊size / 16⌋ + 1) × 16`, and an empty file becomes one block. That is why
the smallest encrypted file is 89 bytes (Step 1.2). Test `Decrypt_EmptyFileCiphertextWithoutRemovingPadding_…`
shows the sixteen `10` bytes.

**5. What CBC does *not* do, and why we need the HMAC.**
CBC hides the data but does **not** detect changes:
- **Tampering:** flipping one bit in a ciphertext block garbles that block and flips *the same bit* in the next
  plaintext block, with no error at all. An attacker can make controlled changes (test
  `Decrypt_CiphertextWithOneBitChanged_…`).
- **Wrong password:** a wrong key usually breaks the padding (an exception), but about 1 time in 256 the padding
  looks valid and garbage comes out with no error.
- **Padding oracle:** if an app tells an attacker "bad padding" versus "other error", the attacker can decrypt a
  file byte by byte, just by asking. This attack is well known and practical.

The fix is **encrypt-then-MAC** (ADR-04): the HMAC tag over header and ciphertext is checked **before** decrypting
anything that is kept, and every failure gives the same message ("wrong password, or the file was changed"). The
attacker learns nothing, and no unverified data reaches the user.

**6. Known-answer tests against NIST, and three independent sources.**
NIST SP 800-38A Appendix F publishes AES-256-CBC example vectors (F.2.5 encrypt, F.2.6 decrypt): key, IV, four
plaintext blocks and the expected ciphertext. Our configured cipher must reproduce them exactly.
- The NIST vector has no padding (exactly four blocks), so the test switches padding off for that one check.
- For the *padded* cases (0, 1, 15, 16, 17 bytes), NIST has no vector, so the expected bytes were computed with
  **OpenSSL** (`openssl enc -aes-256-cbc`), a completely separate AES implementation. .NET on Windows uses Windows
  CNG; OpenSSL is a different code base.
- OpenSSL also reproduced the NIST vector, and the NIST values were read from the NIST PDF itself. Three sources agree.

**7. A security analyzer at work (CA5401).**
The build refused the tests at first: CA5401 warns when encryption uses a fixed IV ("could be repeatable"). It is
right in general, but a known-answer test *must* use the published IV. So the warning is suppressed **only for this
test class**, with a written justification. The app itself takes the IV from the random header. In production, CA5401 fires even on random IVs (it cannot see where they come from), so `CreateEncryptor` carries one suppression with the reason written next to it.

**8. `var`: when the type is already visible.**
`var aes = Aes.Create();`. The project's style (IDE0007) asks for `var` when the type is obvious from the right-hand
side, and explicit types otherwise. The build enforced it.

### Design review before coding (§1.10)

| Principle | Answer for this step |
|---|---|
| Single responsibility / DRY | One place configures AES (`AesCbc.CreateEncryptor` / `CreateDecryptor`). Steps 1.5 and 1.6 both use it, so encrypt and decrypt cannot drift apart. |
| YAGNI | Only a factory. The streaming loop comes in Step 1.5, when it is needed. The factory exists now so the NIST test checks *our* configuration, not a copy of it. |
| Fail fast / secure by design | The key length is checked *before* the cipher is created: no silent AES-128, and nothing to clean up. |
| Encapsulation | `internal`: only Core uses it. The caller disposes the cipher, which clears its own key copy. |
| Algorithms | AES-CBC is O(n) for n bytes. Encryption cannot run in parallel (each block needs the previous one); decryption could, but files are I/O-bound anyway. The OS uses the processor's AES instructions. |
| Pattern | **Factory method**: hides *how* the cipher is configured behind one name. |

### Walkthrough — `src/SecureFile.Core/Cryptography/AesCbc.cs` (final version, after the Review Board)

| Code | Meaning |
|---|---|
| `internal static class AesCbc` | A factory with no state, used only inside Core. |
| `CreateEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)` / `CreateDecryptor(…)` | Return a ready **`ICryptoTransform`** for one file. Key *and* IV are required together, so the file's IV cannot be forgotten. |
| `using Aes aes = CreateConfigured(key, iv); return aes.CreateEncryptor();` | The `Aes` object is only a builder. It is disposed at once (clearing its key copy), and the transform keeps working on its own (verified by the Review Board). |
| `[SuppressMessage("Security", "CA5401", Justification = …)]` on `CreateEncryptor` | CA5401 fires even for IVs from `RandomNumberGenerator` (verified): the analyzer cannot follow where the IV comes from. One method-level suppression, with the reason written in the code. A test proves that two encryptions of the same data differ. |
| `RequireLength(key, KeyDerivation.KeyLength, …)` / `RequireLength(iv, EncryptedFileFormat.IvLength, …)` | Both lengths are checked *before* anything is created. One constant per fact: the key length comes from `KeyDerivation`, the IV length from the format. |
| `var aes = Aes.Create(); aes.SetKey(key); aes.IV = iv.ToArray();` | The OS implementation (Windows CNG, OpenSSL, Apple). The key is copied from a span; the IV is not secret, so an ordinary copy is fine. |
| `aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;` | Already the .NET defaults, set explicitly so the code *states* the format's rule. |

### Walkthrough — the tests (`AesCbcTests`)

| Test | What it proves |
|---|---|
| `CreateEncryptor_NistF25Vector_…` | Our encryptor writes the four NIST F.2.5 blocks, then one PKCS7 block (computed by OpenSSL). |
| `CreateDecryptor_NistF26Vector_…` | Our decryptor gives back the NIST F.2.6 plaintext exactly. |
| `EncryptThenDecrypt_VariousLengths_…` (0, 1, 15, 16, 17 bytes) | Matches OpenSSL byte for byte; size follows the §5.1 formula; round-trips. |
| `Ciphertext_OfEmptyFile_HoldsSixteenPaddingBytesOfValue16` | PKCS7 made visible (with plain .NET AES, since the app never turns padding off). |
| `CreateEncryptor_SameDataWithDifferentIv_…` | Same IV → same bytes; different IV → different bytes. This is why every file has a random IV. |
| `Decrypt_CiphertextWithOneBitChanged_…` | CBC lets a bit flip through silently. This is why the HMAC exists. |
| `Decrypt_WithThisWrongKey_ThrowsBecauseThePaddingIsBroken` | Deterministic for this key. The comment explains why a *random* wrong key slips through about 1 time in 256. |
| `Decryptor_TransformBlock_HoldsBackTheLastBlockUntilFinalBlock` | The behaviour Step 1.6 relies on: the last block is released only by the final call, *after* the HMAC check. |
| `Create_KeyNot32Bytes_…` (0, 16, 24, 31, 33) / `Create_IvNot16Bytes_…` (0, 15, 17) | Both factory methods refuse wrong lengths, including the sizes that would silently be AES-128 or AES-192. |

**Mutation testing note:** the first version had `// Stryker disable` comments on the `Mode` and `Padding` lines
"just in case". A re-run showed Stryker creates **no** mutants for those lines, so the comments were noise and
were removed. The rule: exclude only a mutant that *actually* survives, with a reason.

### Step 1.4 — Review Board report (2026-11-24)

The reviewer ran throwaway probe programs to check how .NET actually behaves:
- CA5401 also fires on random IVs.
- `EncryptCbc` ignores the `Mode` and `Padding` settings.
- A transform keeps working after its `Aes` object is disposed.
- A decrypting `CryptoStream` with a broken last block **throws from `Dispose`**.
- 788 of 200,000 random blocks decrypt with valid PKCS7 padding, which matches the expected 1 in 256.

| ID | Role | Severity | Finding | Decision |
|---|---|---|---|---|
| R1 | Security / Architect | **High (for the Step 1.6 design)** | With the planned `CryptoStream` for decryption, disposing it after an HMAC mismatch runs the final-block step and throws a *padding* error (verified). That replaces the planned "wrong password" path, and it processes the padding before the verdict. | **Recorded** in ADR-06, §5.4 and the Step 1.6 row: decryption uses our own `TransformBlock` loop, and `TransformFinalBlock` only runs **after** `FixedTimeEquals` succeeds. A new test pins the "last block is held back" behaviour this relies on. |
| R2 | Architect | Medium | Returning a mutable `Aes` with its own random IV made "forgot to set the IV" possible, which would produce files that can never be decrypted. | **Fixed:** `CreateEncryptor(key, iv)` / `CreateDecryptor(key, iv)` return ready transforms; both lengths are checked. |
| R3 | Security / Build | Medium | CA5401 will fire on production code (verified, even with random IVs). | **Fixed:** one method-level suppression with a written reason, backed by the "different IV → different output" test. |
| R4 | QA | Medium | The wrong-key test accepted "exception **or** garbage", but its inputs always throw, so a regression returning garbage would still pass. | **Fixed:** `Assert.Throws<CryptographicException>`; the 1-in-256 explanation stays as a comment. |
| R5 | QA | Low | No test that chunked `TransformBlock` calls equal one call. | **Recorded** for Steps 1.5 and 1.6, where the loop lives. |
| R6 | Critic | Low | The exclusion named the `Assignment` mutator, which does not apply to `=`. | **Moot:** the comments were removed (Stryker makes no mutants there). |
| R7 | Critic | Low | Two `KeyLength` constants kept in step only by a test. | **Fixed:** `AesCbc` uses `KeyDerivation.KeyLength`, so there is one source. |
| R8 | Security | Low | The `Aes` object's key copy is not pinned; the OS library holds its own. | **Documented** in the class remarks. The `Aes` object now lives only for one line. |
| R9–R12 | Several | Note / Good | Negligible cost; vectors from outside our code; the factory is justified; small, traceable step. | No change. |

**Recommendation recorded for Steps 1.5/1.6:** use our own loop over a 1 MiB `ArrayPool` buffer with
`TransformBlock`, and do **not** use the one-shot `EncryptCbc` per chunk (chaining by hand is new risk). Decrypt:
HMAC each chunk → `TransformBlock` → write to temp file → progress → cancel check; after the loop, `FixedTimeEquals`;
only on a match, `TransformFinalBlock` → flush → move. Encrypt uses the same loop shape for symmetry.

#### Quality gate — Step 1.4

| # | Check | Result |
|---|---|---|
| G1 | "Done when": NIST SP 800-38A F.2.5/F.2.6 pass | ✅ through the production factory, plus OpenSSL-checked padding cases |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings (IDE0007 and CA5401 handled, see above) |
| G4 | Tests | ✅ 190 passed (20 new), 0 failed, 0 skipped |
| G5 | Core coverage | ✅ 99.4 % lines, 97.9 % branches (the only uncovered code is still the fail-closed line from Step 1.3) |
| G6–G8 | Packages, secrets, patterns | ✅ |
| G9 | Exceptions documented; no secrets in messages | ✅ |
| G10–G11 | Architecture, complexity, MI | ✅ |
| G12 | Mutation score | ✅ **100 %**, with no exclusions in `AesCbc` |
| G13 | Performance | ✅ O(1) creation; AES uses the processor's AES instructions |
| G14–G18 | Docs, Git, design, complexity, Review Board | ✅ 12 findings: 1 High recorded for Step 1.6 (with a test), 3 Medium fixed, the rest fixed or recorded |

### Self-check 1.4 (open book)

1. Why is ECB unsafe, and what does CBC add?
2. What does PKCS7 add to a 16-byte file, and why a whole block?
3. Show with the bit-flip test why CBC alone is not enough. What does the HMAC add?
4. What is a padding oracle, and how does our design make sure there is none?
5. Why does the factory take the key *and* the IV together, instead of returning an `Aes` object?
6. Why are 16- and 24-byte keys rejected, when AES accepts them?
7. CA5401 fired twice. Why is it suppressed in the tests and in `CreateEncryptor`, and why is that honest?
8. Why must Step 1.6 call `TransformFinalBlock` only after the HMAC check, and not use a decrypting `CryptoStream`?

---

## Step 1.5 — Encrypting a file: the streaming loop (requirements R3, D2–D5) · 2026-11-24

### Lesson 1.5 — the concepts used in this step

**1. Streaming: never hold the whole file.**
A file can be 10 GB, but our memory use must not grow with it (§1.9: O(n) time, O(1) memory). So we read a
**chunk** (1 MiB), encrypt it, write it, and reuse the same buffer for the next chunk. *Analogy:* emptying a bath
with a bucket — the bucket's size, not the bath's, decides how much you carry at once. Why 1 MiB? Big enough that
the cost of each read/write call is tiny compared with the data moved; small enough to be irrelevant for memory.
Smaller buffers (4–80 KB) mean many more calls; much bigger ones bring no measurable speed-up.

**2. `ArrayPool<byte>`: borrow a buffer instead of buying one.**
`ArrayPool<byte>.Shared.Rent(size)` lends an array that already exists, and `Return` gives it back. A new 1 MiB array
on every encryption would land on the *Large Object Heap*, which the garbage collector cleans rarely and slowly.
We rent **one 2 MiB buffer**: the first half receives the plaintext chunk, the second half the ciphertext
(`TransformBlock` takes an input and an output offset). Two rules: `Rent` may return a *bigger* array than asked for,
so we always use our own `ChunkLength`, never `buffer.Length`; and the buffer is returned with `clearArray: true`, so
the next borrower never sees our user's data. Why not encrypt in place (same half for input and output)? The
`ICryptoTransform` documentation does not promise that overlapping input and output work, so we do not rely on it.

**3. `ICryptoTransform`: AES in pieces.**
`AesCbc.CreateEncryptor(key, iv)` (Step 1.4) returns an object with two methods:
- `TransformBlock(input, …, output, …)` encrypts **whole 16-byte blocks** and remembers the last ciphertext block,
  so the next call continues the CBC chain exactly where the previous one stopped.
- `TransformFinalBlock(input, …)` encrypts the **rest** (0–15 bytes) and adds the PKCS7 padding.
So "1 MiB, 1 MiB, …, then the last bit" gives byte-for-byte the same result as encrypting the whole file in one call
(test *chunked == single call*, Review Board 1.4 R5). We do not use `CryptoStream`: it hides when the last block is
processed, and for decryption that is exactly what we must control (ADR-06). We do not call the one-shot
`EncryptCbc` per chunk: we would have to carry the IV chain and avoid padding by hand — new risk for no gain.

**4. `IncrementalHash`: an HMAC fed in pieces.**
HMAC-SHA256 with the authentication key gives a 32-byte **tag**: a fingerprint only someone with the key can make.
`IncrementalHash.CreateHMAC(SHA256, key)` lets us add data piece by piece (`AppendData`) and get the tag at the end
(`GetHashAndReset`), so the HMAC streams like the encryption. It covers **the header and the ciphertext**
(encrypt-then-MAC, ADR-04): if anyone changes the salt, IV, iteration count or any ciphertext byte, the tag no longer
matches. The tag is written as the last 32 bytes.

**5. `async`/`await` and `Task.Run`: keep the window responsive.**
- *I/O* (reading/writing the disk) uses `ReadAtLeastAsync` / `WriteAsync`: while the disk works, no thread waits.
- *CPU work* (PBKDF2, ≈0.5 s of pure computing) cannot be made "async" — it needs a thread. `Task.Run` moves it to a
  thread-pool thread, so the UI thread keeps drawing. Without it, the window would freeze for half a second.
- `ConfigureAwait(false)` (library rule, CA2007): "I don't need to come back to the UI thread after this await", so
  Core never blocks or deadlocks on the UI thread.

**6. `CancellationToken`: polite stopping.**
The UI's Cancel button sets a flag; our loop asks `ThrowIfCancellationRequested()` before each chunk and throws
`OperationCanceledException`. Nothing is killed from outside — the loop stops itself at a safe point, so `finally`
blocks run and clean up. PBKDF2 itself cannot be interrupted, so we check the token right after it.

**7. `IProgress<int>`: telling the UI how far we are.**
The service reports a **whole** percentage (0–100), and only when it goes up (`PercentageProgress`): at most 101
reports for any file. One report per chunk would be ~10,000 for a 10 GB file, each one a message to the UI thread,
for changes a progress bar cannot even draw (Review Board R3). In the app, `new Progress<int>(…)` captures the UI
thread and runs the callback there, so the progress bar can be updated safely. 100 % means "all data encrypted"; the
task finishing means "the file is in place" — on a slow USB stick the flush and move can take a moment, so the
ViewModel shows "Finishing…" in between. (Tests use a tiny synchronous `IProgress` so the order of reports is exact;
`Progress<T>` posts them asynchronously.)

**8. Temp file + atomic move: the result appears whole or not at all (ADR-07).**
We write into a hidden-named temp file **in the destination folder** (`.~report.pdf.enc.<random>.securefile-tmp`),
flush it to the disk (`Flush(flushToDisk: true)`, so a power cut cannot leave a renamed-but-empty file), and only then
`File.Move` it to the chosen name. A move inside one folder is a rename: atomic. A move across drives would be a
copy, not atomic — that is why the temp file is next to the output, not in `%TEMP%`. On any error or cancel, `finally`
deletes the temp file. The input file is only read, never changed.

**9. Validate first, then go async (ADR-13).**
`EncryptAsync` is *not* `async`: it checks the arguments immediately and throws at the call, then hands over to a
private `async` method. Otherwise a bad argument would only appear when the task is awaited — later and further from
the bug.

**10. A test seam: a delegate instead of an interface.**
The header (salt + IV) must be random in the app, but a golden-output test needs a fixed one, and tests want 100,000
iterations instead of 600,000 (6× faster). An `internal` constructor takes a `Func<EncryptedFileHeader>` ("how to
make a header"). The public constructor passes the real one. An `IHeaderFactory` interface would be more ceremony for
the same thing (YAGNI).

### Design review before coding (§1.10)

| Principle | Answer for this step |
|---|---|
| Single responsibility | `FileEncryptionService` does the crypto; `AtomicFileWriter` (in `Files/`) makes a file appear whole or not at all; `PercentageProgress` turns bytes into percents. Step 1.6 reuses the last two. |
| Dependency inversion / Strategy | The ViewModel will depend on `IFileEncryptionService`; a format v2 (AES-GCM) would be a new class behind it. |
| Fail fast | Arguments are checked before the task starts; the input and the temp file are opened **before** the 0.5 s key derivation, so a missing file or a read-only folder is reported at once. |
| YAGNI / KISS | A `Func<EncryptedFileHeader>` test seam instead of an `IHeaderFactory`; no options record; no free-space pre-check (see R-Q1 below). |
| Algorithms | O(n) time, O(1) memory: one 2 MiB pooled buffer, whatever the file size. AES-CBC encryption is sequential by nature (each block needs the previous one). |
| Secure by design | Encrypt-then-MAC over header + ciphertext; plaintext buffer cleared when returned; keys wiped by `using`; messages never contain paths, passwords or keys. |

The design was reviewed by an independent reviewer **before** coding (findings F1–F14). Applied: input and temp file
opened before key derivation (F6), the count returned by `TransformBlock` is used for the HMAC and the write (F3),
no cancel check after the move (F8), long names shortened in the temp name (F11), §5.3 and ADR-07/ADR-13 updated
(F2, F5). F1 (temp stream disposed before the delete) was already true: the stream's `await using` is inside the `try`.

### Walkthrough — `src/SecureFile.Core/Cryptography/FileEncryptionService.cs`

| Code | Meaning |
|---|---|
| `public sealed class FileEncryptionService : IFileEncryptionService` | The app's encryption service. `sealed`: nothing inherits from it (composition over inheritance). |
| `internal const int ChunkLength = 1024 * 1024;` | 1 MiB per round, a multiple of 16, so every full chunk is whole AES blocks. |
| `private readonly Func<EncryptedFileHeader> _createHeader;` | "How to make a header". The public constructor passes `CreateNew(600_000)`; tests pass 100,000 iterations or a fixed salt/IV. |
| `EncryptAsync(…)` — not `async` | Checks the arguments at once (`ThrowIfNullOrWhiteSpace`, `ThrowIfNullOrEmpty`), turns both paths into full paths, calls `ThrowIfOutputPathIsUnusable`, then returns the task of `EncryptCoreAsync`. A bad argument throws at the call (ADR-13). |
| `ThrowIfOutputPathIsUnusable` | Refuses: no file name, a character a file name cannot have (`:` on Windows would address a hidden NTFS stream), an existing folder, the input itself (ignoring case), and an existing file when overwriting was not confirmed. The final move is the real guard; this saves the user the wait. |
| `new FileStream(…, FileShare.Read, Asynchronous \| SequentialScan, BufferSize = 0)` | Read-only; others may read but nobody may change the file meanwhile; true async I/O; a hint that we read front to back; no second buffer, because we have our own. |
| `await using (input.ConfigureAwait(false))` | Closes the input at the end, also on errors, without returning to the UI thread. |
| `AtomicFileWriter.WriteAsync(outputFilePath, overwriteExisting, output => WriteEncryptedFileAsync(…))` | The writer creates the temp file and hands us its stream; we fill it; the writer flushes and moves it, or deletes it on failure. |
| `using FileKeys keys = await Task.Run(() => KeyDerivation.DeriveKeys(password, header), ct)` | PBKDF2 on a thread-pool thread. The string is passed because a lambda cannot capture a span. `using` wipes the keys at the end. |
| `AesCbc.CreateEncryptor(keys.EncryptionKey, header.Iv.Span)` / `IncrementalHash.CreateHMAC(SHA256, keys.AuthenticationKey)` | The two tools for this file, both disposed by `using`. |
| `header.WriteTo(headerBytes); AuthenticateAndWriteAsync(…)` | The 41 header bytes go into the HMAC **and** the file: changing the salt, IV or iterations breaks the tag. |
| `new PercentageProgress(progress, input.Length)` | Progress is measured against the length at the start. |
| `output.WriteAsync(hmac.GetHashAndReset(), ct)` | The 32-byte tag, last. The token reaches this write too, so a cancel after the last chunk still leaves no file. |
| `EncryptContentAsync`: `ArrayPool<byte>.Shared.Rent(2 * ChunkLength)` | One buffer: plaintext in the first half, ciphertext in the second. |
| `ReadAtLeastAsync(buffer[..ChunkLength], ChunkLength, throwOnEndOfStream: false, ct)` | Keeps reading until 1 MiB is in or the file ends. A plain `ReadAsync` may return less on a network drive, and treating that as the end would give a **cut-short file with a valid tag** — the High finding F1, now pinned by a test. |
| `while (bytesRead == ChunkLength)` | A shorter read is the end. A file of exactly 1 MiB takes a second round with 0 bytes, which writes a whole padding block. |
| `finally { Return(buffer, clearArray: true); }` | The buffer goes back to the pool wiped. |
| `EncryptOneChunkAsync`: `wholeBlockLength = length - length % 16` | The part that is whole blocks. |
| `if (wholeBlockLength > 0)` | `TransformBlock` throws on 0 bytes (found by the empty-file test). |
| `TransformBlock(buffer, 0, wholeBlockLength, buffer, ChunkLength)` | Encrypt into the second half; the encryptor remembers the last block, so the CBC chain continues into the next chunk. |
| `if (length < ChunkLength) TransformFinalBlock(buffer, wholeBlockLength, rest)` | Only for the last chunk: the 0–15 remaining bytes plus PKCS7 padding → 16 bytes. |
| `AuthenticateAndWriteAsync` | `hmac.AppendData` then `WriteAsync`: no byte reaches the file without entering the tag. |

### Walkthrough — `Files/AtomicFileWriter.cs` and `Cryptography/PercentageProgress.cs`

| Code | Meaning |
|---|---|
| `CreateTempFilePath`: `.~<name, max 100 chars>.<8 random hex>.securefile-tmp` in the output's folder | Same folder → the move is a rename (atomic). The random part lets two operations run at once and means a leftover never blocks the next try. Names are cut at 100 characters, never between the two halves of an emoji. A path without a folder (a drive root) is refused. |
| `CreateTempFile`: `FileMode.CreateNew`, `FileShare.None` | Never opens an existing file — so a foreign file with the same name is never overwritten or deleted. It is created **before** the `try` for the same reason. |
| `await using (tempFile…) { await writeContent(tempFile); tempFile.Flush(flushToDisk: true); }` | The stream is closed before the move (Windows cannot move an open file) and before the delete. `Flush(true)` forces the data onto the disk; it has no async version, hence the documented CA1849 suppression. |
| `File.Move(temp, output, overwriteExisting); isMoved = true;` | The result appears in one step. |
| `finally { if (!isMoved) _ = TryDeleteFile(temp); }` | Cleanup on every failure or cancel. "Try": a virus scanner may hold the file for a moment, and a failed delete must not hide the real error. |
| `PercentageProgress.Advance(byteCount)` | Adds the bytes, computes the whole percent (`0` length = 100 %, capped at 100) and reports only when it rose: at most 101 reports. |

### Walkthrough — the tests

| Test | What it proves |
|---|---|
| `EncryptAsync_FixedSaltAndIv_MatchesIndependentlyComputedGoldenFile` | 121 bytes computed with Python (`hashlib`, `hmac`) and OpenSSL 3.2.1 — outside .NET — match ours exactly: format, key derivation, AES, HMAC, all at once. |
| `…_ChunkedFileEqualsOneCallReference` (0, 1, 15, 16, 17, 1 MiB−1, 1 MiB, 1 MiB+1, 2 MiB+17) | Our loop equals "everything in one call" at every block and chunk boundary (Review Board 1.4, R5). |
| `…_FileLengthMatchesFormatFormula` | `41 + (⌊n/16⌋+1)·16 + 32` (§5.1). |
| `…_RoundTripsWithReferenceDecryption` | A minimal reader (check tag with `FixedTimeEquals`, then decrypt) gets the original back. Step 1.6 replaces it with the real `DecryptAsync`. |
| `WriteEncryptedFileAsync_InputThatReturnsFewBytesPerRead_…` | A stream that returns ≤ 7,777 bytes per read still gives the reference file (F1). |
| `…_SameInputTwice_ProducesDifferentFiles` | Fresh salt and IV per file. |
| `…_Progress_RisesAfterEveryChunkAndEndsAt100` | Exact sequences: `[100]`, `[100]`, `[100]` for exactly 1 MiB, `[40, 80, 100]` for 2.5 MiB. |
| Cancel: already cancelled / during key derivation / after the first chunk / after the last chunk | Always `OperationCanceledException`, and the folder holds only the input. |
| `…_OutputAppearsDuringEncryption_…` | The move refuses to overwrite a file that appeared meanwhile; that file is kept, the temp file is gone. |
| Invalid arguments (null, empty, same file in other case, folder, NTFS stream, existing output) | Thrown **at the call** (`Assert.Throws`, not `ThrowsAsync`), with the expected parameter name and message. |
| `AtomicFileWriterTests`, `PercentageProgressTests` | Temp name format, randomness, truncation (also emoji-safe), root path, content-writer failure, overwrite rules, `TryDeleteFile` on success, IOException and (Windows) read-only. |

### Step 1.5 — Review Board report (2026-11-24)

Three independent reviewer agents on the strongest model: a design reviewer before coding (above) and two code
reviewers after the tests were green — (A) Security + QA + Critic, who wrote probe programs; (B) Architect +
Performance + PM + End user + Team Lead. Findings marked **P** were confirmed with a probe.

| ID | Role | Severity | Finding | Decision |
|---|---|---|---|---|
| A-F1 | QA | **High** (test gap) | Replacing `ReadAtLeastAsync` by `ReadAsync` would pass every test (local disks always return full reads), but on a network drive a short read would end the file early — with a **valid tag**. | **Fixed:** `WriteEncryptedFileAsync` is `internal` and takes any `Stream`; a test feeds a stream returning ≤ 7,777 bytes per read and compares with the reference. |
| A-F2 | QA | Medium | The final "last chance" cancel check and the per-read cancel check were redundant: the token already reaches every read and write (**P**). Stryker agreed (survivors). | **Fixed:** both lines removed; tests prove cancel after the first chunk, after the last chunk and during key derivation (B-R12). |
| A-F3 / B-R8 | Critic / Architect | Medium | An existing **folder** as output passed the checks; the error came only after the whole encryption, with a misleading message (**P**). | **Fixed:** `Directory.Exists` → `ArgumentException` up front, tested. |
| A-F4 | Security | Medium | `out.enc:s` (an NTFS alternate data stream) passed every check and left an empty `.~out.enc` file behind (**P**). | **Fixed:** names with characters a file name cannot have are refused up front; Windows-only test. |
| A-F5 | Critic | Medium | A device or pipe as input throws `NotSupportedException` at `Length` (**P**); a folder as input gives `UnauthorizedAccessException`. Neither was documented. | **Documented** in the interface (ADR-13). The file dialog only offers regular files, so no extra guard (YAGNI). |
| B-R3 | Performance / End user | Medium | One progress report per chunk = ~10,000 UI messages for 10 GB, ~1,000/s on a fast disk (§8.2 says ≤ 10 updates/s). | **Fixed:** `PercentageProgress` reports whole percents only when they rise (≤ 101 reports); `IProgress<int>`. |
| B-R1 / B-R2 | Architect / PM | Medium | ADR-07 promised a hidden temp file; §5.3 still described `CryptoStream`. | **Fixed** (already after the design review): §5.3 rewritten; ADR-07 says the hidden attribute is for the decrypt temp file (Step 1.6). |
| B-R4 | Team Lead | Medium | "Output exists" is a plain `IOException`, like "disk full". | **Decision:** the ViewModel checks and asks *before* calling (it must ask the user anyway); Core's check is the safety net. Row added to the ADR-13 table. A custom exception would be ceremony for a case the UI prevents. |
| A-F9 | Critic | Low | Cutting the name at 100 characters could split an emoji (**P**). | **Fixed** + test. |
| A-F10 / B-R6 | Critic / Team Lead | Low / Note | `EncryptChunkAsync` vs `EncryptChunksAsync` differ by one letter; `WriteAuthenticatedAsync` is ambiguous; helpers are fragmented. | **Fixed:** `EncryptContentAsync`, `EncryptOneChunkAsync`, `AuthenticateAndWriteAsync`; one-line helpers folded back in. |
| A-F8 | QA | Low | "The last report is 100" is not guaranteed when a file is cut short on Linux while we read it. | **Kept**, wording: "100 means all data is encrypted"; the task completing is the real signal. |
| B-R5 | Performance | Low | The per-chunk `async` method allocates a small state machine per chunk (~2 MB per 10 GB). | **Recorded:** §8.2 now says "no per-chunk *buffer* allocations"; measured in Stage 5 before any pooling trick. |
| B-R7 | End user | Low | 100 % can show for a moment before the flush and move finish. | **Recorded** for the ViewModel ("Finishing…"), in the lesson and the interface docs. |
| A-F6 | Critic | Low | Opening the files runs on the caller's thread before the first real `await`. | **Accepted:** milliseconds on local disks; measured in Stage 5 on a network drive. |
| A-F7 / B-R11 | Security / Perf | Low / Note | The data is flushed, but the rename itself is not made durable. | **Recorded** in ADR-07 ("Accepted limits"). |
| B-R9 | API | Low | `bool overwriteExisting` is a "boolean trap". | **Kept;** callers use the named argument (as the tests do). |
| B-Q1 | Team Lead | Question | "10 GB to a nearly full USB stick — do we learn it after 5 minutes?" | **Decision:** no free-space pre-check: it is racy and network drives report it unreliably. Disk full gives an `IOException` (ADR-13 message), and the temp file is deleted. A pre-check is a Stage 8 suggestion. |
| B-Q2 | Team Lead | Question | "Who cleans up temp files left by a crash?" | Recorded in ADR-07 "Accepted limits"; start-up scan is a Stage 8 suggestion. |
| Others | All | Note | Buffer cleared, no FileStream buffer, keys disposed, no overlap in `TransformBlock`, no secrets in messages, `TransformBlock(…,0,…)` throws (**P**); the seam, the 2 MiB split buffer and `AtomicFileWriter`'s shape for Step 1.6 judged right. | No change. |

**Mutation testing.** The first run scored 69 % (26 survivors). What they taught:
- 14 were `ConfigureAwait(false → true)`: only observable with a UI synchronization context. `stryker-config.json`
  now ignores `ConfigureAwait` arguments (`ignore-methods`), with this reason.
- 2 showed redundant code (the two extra cancel checks) → removed.
- 4 were unasserted messages → tests now assert them.
- The rest cannot be observed by any test and are excluded line by line with the reason written next to them:
  clearing/returning the pooled buffer, `Flush(flushToDisk: true)` (needs a power cut), the `isMoved` flag (deleting a
  no-longer-existing temp path is a no-op), the FileStream speed options, the 100 % cap (Windows prevents a growing
  file), and an equivalent `>`/`>=` mutant at exactly 100 characters.
- One `Timeout` (the loop condition inverted → endless loop) counts as detected.

#### Quality gate — Step 1.5

| # | Check | Result |
|---|---|---|
| G1 | "Done when": round trip at 0, 1, 15, 16, 17 bytes, 1 MiB ± 1; different output each time; keys via `Task.Run` | ✅ plus golden file, chunked = one-call reference, short-read stream, length formula |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings (CA1505 forced smaller methods; CA1849 suppressed once with a reason) |
| G4 | Tests | ✅ 266 passed (76 new), 0 failed, 0 skipped on Windows (3 Windows-only cases skip on Linux/macOS by design) |
| G5 | Core coverage | ✅ 99.6 % lines, 98.7 % branches |
| G6–G8 | Packages, secrets, forbidden patterns | ✅ |
| G9 | Exceptions documented (ADR-13); no secrets or paths in messages; cleanup in `using`/`finally` | ✅ interface lists every exception, incl. `NotSupportedException` (device input) found by the Review Board |
| G10–G11 | Architecture, complexity ≤ 10, MI ≥ 60 | ✅ |
| G12 | Mutation score | ✅ **100 %** (66 killed, 1 timeout); exclusions listed above, each with its reason; `ConfigureAwait` ignored via `stryker-config.json` |
| G13 | Performance | ✅ O(n) time, O(1) memory (one 2 MiB pooled buffer); ≤ 101 progress reports; throughput measured in Stage 5 |
| G14 | Docs | ✅ XML docs, lesson, walkthrough, §5.3/ADR-07/ADR-13/§8.2 updated, CHANGELOG, plan row and progress log |
| G15 | Git | ✅ Conventional Commit, no AI attribution |
| G16–G18 | Design, complexity, Review Board | ✅ design review + 2 code reviewers; 1 High and all Medium findings fixed or answered with a recorded decision |

### Self-check 1.5 (open book)

1. Why do we read 1 MiB at a time instead of the whole file? What would `File.ReadAllBytes` do with a 5 GB file?
2. `TransformBlock` vs `TransformFinalBlock`: which one adds the padding, and why does a file of exactly 1 MiB need a second, empty round?
3. Why `ReadAtLeastAsync` and not `ReadAsync`? What exactly would go wrong on a network drive — and why would the tag *not* catch it?
4. Why does the HMAC cover the header too? What could an attacker do if only the ciphertext were covered?
5. Why `Task.Run` for PBKDF2 but plain `await` for the disk? What does `ConfigureAwait(false)` change?
6. Why is `EncryptAsync` itself not `async`?
7. Why is the temp file in the destination folder, created with `CreateNew`, and created *before* the `try`?
8. What does `Flush(flushToDisk: true)` protect against that `FlushAsync` does not? What is still not protected (ADR-07 "Accepted limits")?
9. Why does the service report whole percents only when they rise? What does the user see between 100 % and "done"?
10. Which Stryker survivors were excluded, and why is each exclusion honest?

## Step 1.6 — Decrypting a file: verify, then commit (requirements R4, D2–D5) · 2026-11-24

### Lesson 1.6 — the concepts used in this step (short)

**1. Verify-then-commit.** Decryption reads the file **once**: each ciphertext chunk goes into the HMAC *and* through
the decryptor, and the plaintext goes to a temp file. Only after the last byte is the stored tag compared with the
computed one. Match → finish the last block, move the temp file into place. Mismatch → delete the temp file, throw.
*Analogy:* a cashier counts the notes into a drawer that is still locked; the drawer opens only if the total matches.
Alternative: two passes (check the tag first, then decrypt) — no unverified plaintext on disk, but twice the reading
(ADR-07 keeps it as the fallback).

**2. Why the last block waits.** The AES decryptor always **holds back the last block** in `TransformBlock`; only
`TransformFinalBlock` decrypts it and checks/removes the PKCS7 padding. We call it only after the tag matched, so the
padding of an unverified file is never examined — that is what closes the door on *padding-oracle* attacks, where an
attacker learns plaintext from "bad padding" vs "bad tag" answers. (A decrypting `CryptoStream` would run the final
block on dispose, before our verdict — ADR-06.)

**3. Constant-time comparison.** `CryptographicOperations.FixedTimeEquals(a, b)` always looks at all 32 bytes. A normal
`SequenceEqual` stops at the first difference, so its timing would tell an attacker how many leading tag bytes were
right.

**4. Check the cheap things first.** Before any temp file or the 0.5 s key derivation: read the first 41 bytes with
`ReadAtLeastAsync(41, throwOnEndOfStream: false)` and pass **only the bytes read** to `EncryptedFileHeader.Read`
(a 3-byte text file → "not an encrypted file", not a disk error), then `GetCiphertextLength(file length)` (an 80-byte
`SFEN…` file → "damaged"). Then the ciphertext length is known, so the loop uses `ReadExactlyAsync`: short reads are
waited out, and a file that shrank meanwhile throws `EndOfStreamException`.

**5. One error for "wrong password" and "changed file".** Both just make the tag differ; the HMAC cannot tell them
apart, so `AuthenticationFailedException` (Step 1.7, pulled forward) says "wrong password, or the file was changed or
damaged". Guessing one would mislead the user.

**6. The hidden temp file.** The decrypt temp file holds *unverified plaintext*, so on Windows it gets the hidden
attribute while it is written (set and cleared through the open handle). The mark is removed before the move, because
a move keeps attributes and the user's result must not be hidden. Encryption does not hide (ciphertext only).

### Walkthrough — `FileEncryptionService.DecryptAsync`

| Code | Meaning |
|---|---|
| `DecryptAsync` → `GetCheckedFullPaths(…)` | Not `async`: null/empty arguments, folder or same-file output, existing output without overwrite all throw **at the call** (shared with `EncryptAsync`, DRY). |
| `OpenInputFile` | `FileShare.Read`, async, sequential, no FileStream buffer — shared with encryption. |
| `ReadHeaderAsync`: `ReadAtLeastAsync(header, 41, throwOnEndOfStream: false)` → `EncryptedFileHeader.Read(bytes[..read])` | Lesson point 4: "not ours" before "damaged". |
| `EncryptedFileFormat.GetCiphertextLength(input.Length)` | Rejects lengths < 89 or not whole blocks, still before any output exists. |
| `AtomicFileWriter.WriteAsync(…, isSensitive: true, …)` | Temp file in the output folder, hidden on Windows, deleted on any failure, moved only on success. |
| `Task.Run(() => KeyDerivation.DeriveKeys(password, header), ct)` | PBKDF2 off the UI thread (as in encryption). |
| `header.WriteTo(headerBytes); hmac.AppendData(headerBytes)` | `Read` only accepts values that `WriteTo` writes back unchanged, so these are exactly the 41 bytes of the file. |
| `DecryptContentAsync`: `ReadExactlyAsync` → `hmac.AppendData` → `TransformBlock` → `WriteAsync` → `progress.Advance` | One 2 MiB pooled buffer (ciphertext half / plaintext half), cleared on return. HMAC over the *ciphertext* (encrypt-then-MAC). |
| `VerifyTagAsync`: `ReadExactlyAsync(32)` → `FixedTimeEquals` → `AuthenticationFailedException` | Lesson points 3 and 5. |
| `WriteLastBlockAsync`: `TransformFinalBlock([], 0, 0)` | Only after the tag matched; the returned array is wiped after writing. An invalid padding *with* a valid tag (only another program knowing the password could make that) throws `CryptographicException` — documented, tested, nothing left behind. |
| `AtomicFileWriter.FillAsync` / `SetHidden` / `TryEmpty` | Hide through the open handle (`File.SetAttributes(SafeFileHandle, …)`, no second path lookup), write, flush to disk, un-hide, then close and move. On any failure the file is first cut to 0 bytes through the still-open handle, so even a failed delete leaves no plaintext. |
| `CreateTempFile(…, isSensitive)` | On Linux/macOS a sensitive temp file is created with mode 0600 (owner only); the decrypted result keeps it. |

### Walkthrough — the tests (`FileDecryptionTests`, 42 cases)

| Test | What it proves |
|---|---|
| Round trip at 0, 1, 15, 16, 17, 1 MiB − 1, 1 MiB, 1 MiB + 1, 2 MiB + 17 | `DecryptAsync(EncryptAsync(x)) == x` at every block and chunk boundary; only the output is left, and it is not hidden. |
| `WriteDecryptedFileAsync_InputThatReturnsFewBytesPerRead_…` | Header and ciphertext read correctly from a stream returning ≤ 7,777 bytes per read. |
| Wrong password (also with overwrite allowed) | `AuthenticationFailedException`; no output, no temp — and an existing output file is **kept**. |
| One flipped bit in signature, version, iterations (in range / negative), salt, IV, first, middle and last ciphertext block, first and last tag byte (T-06) | Header damage → `InvalidEncryptedFileException`; everything else → `AuthenticationFailedException`; nothing left. |
| Bit flipped in a 3-chunk file | The whole file was streamed (progress hit 100) before the verdict, and the plaintext temp file is still deleted. |
| Cut by 1 / 16 / 32 bytes, extended by 16 | Not whole blocks → `Damaged`; otherwise tag mismatch. Nothing left. |
| 3-byte text file / 80-byte `SFEN` file | `NotEncryptedFile` / `Damaged`, before any temp file. |
| Valid tag, invalid padding | `CryptographicException`, nothing left. |
| Input shorter than its checked length | `EndOfStreamException` — a file that shrinks while read is never cut silently. |
| Cancel: already cancelled / during PBKDF2 / after the first of four chunks / after the only chunk | `OperationCanceledException`, nothing left. |
| Progress for 2.5 MiB | `[39, 79, 100]` (ciphertext is 16 bytes longer than the plaintext). |
| Temp file hidden while writing (Windows) | The decrypt temp file really has the hidden attribute. |
| Invalid arguments, existing output, missing input | Thrown at the call / `FileNotFoundException`, nothing created. |
| `AtomicFileWriterTests` (sensitive on/off, 0600, emptied when the delete fails), `AuthenticationFailedExceptionTests` | Hidden only when asked (Windows), result never hidden; owner-only on Linux/macOS (runs in CI); a read-only leftover is 0 bytes; the three standard constructors. |

### Step 1.6 — review (lean mode: one reviewer, Security + QA + Critic)

| ID | Severity | Finding | Decision |
|---|---|---|---|
| M1 | Medium | After a failed decrypt the temp file is mostly real plaintext; if the delete fails (virus scanner), it stays — and hidden. | **Fixed:** on any failure the file is emptied through its open handle before the delete; Windows test with a read-only leftover. |
| M2 | Medium | On Linux/macOS the temp file was created 0644 (readable by other local users); `FileShare.None` is only advisory there. | **Fixed:** sensitive temp files are created 0600; the decrypted result keeps it (private to the user). Test runs in CI on Linux/macOS. ADR-07 updated. |
| L3 | Low | A crafted file with 10,000,000 iterations makes Cancel wait ~8 s (PBKDF2 cannot be interrupted). | **Documented** on the interface. `WaitAsync(ct)` would return early but leave the KDF running and need extra key-disposal code — not worth it for a crafted file. |
| L4 | Low | The header is re-serialised for the MAC; a future v2 `WriteTo` could differ. | **Kept:** a v2 format is a new class behind `IFileEncryptionService` (Strategy, ADR-14), and v1 `Read` accepts only bytes `WriteTo` reproduces. Revisit if v2 ever shares this class. |
| L5 | Low | The docs said a lone-surrogate password throws "at the call"; it throws when the task is awaited. | **Fixed** in the interface docs (both methods). |
| L6 | Low | Hiding is not best effort; ADR-07 said "created with" the attribute. | **ADR wording fixed.** Hiding stays strict (fail closed): NTFS, FAT/exFAT and SMB all support it. |
| T | Low | No test for a file that shrinks while read. | **Added** (`EndOfStreamException`). |

Confirmed fine by the reviewer: check order (header → length → temp → KDF), HMAC over exactly the decrypted bytes, `FixedTimeEquals` before `TransformFinalBlock` (no padding oracle), buffer sizes, wiping, and that `GetFiles()` also lists hidden leftovers (so "nothing left" asserts are real).

#### Quality gate — Step 1.6

| # | Check | Result |
|---|---|---|
| G1 | "Done when": wrong password and every tamper/truncation case fail and leave no output; header read before length; 3-byte → `NotEncryptedFile`, 80-byte `SFEN` → `Damaged`; `TransformFinalBlock` only after `FixedTimeEquals`; chunked = original | ✅ |
| G2–G3 | Format, build with all analyzers | ✅ 0 warnings (CA1505 split two methods; CA2000 kept `await using` in `WriteAsync`) |
| G4 | Tests | ✅ 316 passed (50 new), 0 failed, 1 skipped on Windows (the Linux/macOS 0600 test) |
| G5 | Core coverage | ✅ 98.9 % lines, 98.0 % branches |
| G6–G8 | Packages, secrets, forbidden patterns | ✅ |
| G9 | Exceptions documented (ADR-13) | ✅ `DecryptAsync` lists every exception, incl. `AuthenticationFailedException`, `CryptographicException` (valid tag, bad padding) and `EndOfStreamException` |
| G12 | Mutation score (one pass, the three changed files) | ✅ **96.67 %** — `FileEncryptionService` 100 %. Two survivors in `AtomicFileWriter`, both dealt with without a re-run (lean mode): `lowercase: true → false` survived because that run drew an all-digit random part (about 2 % per draw; a manual mutation fails the test), so a new test checks 50 draws; `TryEmpty`'s `return true → false` is equivalent (the result is discarded), so it is excluded with that reason. The Unix-mode line is excluded because only Linux/macOS CI can observe it. |
| G13 | Performance | ✅ O(n) time, O(1) memory (one 2 MiB pooled buffer), one read of the file |
| G14–G15 | Docs, git | ✅ |

### Self-check 1.6 (open book)

1. Why may the decryptor not run `TransformFinalBlock` before the tag is checked? What is a padding oracle?
2. Why `FixedTimeEquals` and not `SequenceEqual`?
3. Why is the header read with `throwOnEndOfStream: false`, and why before `GetCiphertextLength`?
4. Why one pass to a temp file instead of two passes? What is the price, and how do the hidden attribute, 0600 and "empty on failure" reduce it?
5. Why can't the app tell "wrong password" from "changed file"?
6. Why is the hidden attribute removed before the move?

---

## Vertical slice (Steps 2.1–2.6, 3.1) — 2026-11-24

**Built.** A working app end to end: `ErrorMessageProvider` (ADR-13 Layer 2 table, IOException split by HResult),
`MainViewModel` with specific catches, `ResetAfterUnexpectedError`, a plain WPF window, `FileErrorLog`
(`%LOCALAPPDATA%\SecureFile\logs\securefile.log`, lock, rotation at 1 MB, never throws), `WpfFileDialogService`, and the
Layer 3 safety nets in `App.OnStartup` (composition root). First integration test: ViewModel + real Core, encrypt then
decrypt, bytes equal.

**Decisions.** CryptographicException after a valid tag is wrapped as `InvalidEncryptedFileException(Damaged)`;
`EndOfStreamException` stays an `IOException`; a lone-surrogate password is refused (Core throws; the ViewModel checks
first and shows a message); the slice uses `IFileDialogService` + `IErrorLog` only; status text instead of
`IMessageService`; the Save dialog's overwrite prompt means `overwriteExisting: true`; the ViewModel pre-checks
input == output; `RunGuardedAsync` is the one place with the catches (never `Exception`/`ArgumentException`).

**Review (Sonnet, all §1.8 roles).**

| Finding | Severity | Action |
|---|---|---|
| Lone-surrogate password reached Layer 3 | High | Fixed: ViewModel check + test |
| Log only caught IO/Unauthorized | Medium | Fixed: also NotSupported/Argument |
| Log unbounded | Medium | Fixed: rotate at 1 MB; path-in-log noted in docs |
| Handlers registered after `base.OnStartup` | Medium | Fixed: moved first |
| Reset could re-enable buttons during a running operation | Medium | Fixed: no-op while Working; `finally` resets after an unexpected exception |
| No Cancel path (OperationCanceledException unused) | Medium | Deferred to Step 3.5 (Cancel) |
| PasswordBox without MaxLength | Medium | Fixed: 1024 |
| Errno values on Unix, `IsSameFile` case rule, progress bar at 0 during Browse, FileErrorLog tests, DateTime vs Stopwatch | Low | Deferred (Avalonia stage / 3.2 / 3.5) |

**Manual smoke (UI Automation, Debug build).** Browse shows "note.txt (.txt) · 8.79 KB": pass. Encrypt writes
`note.txt.enc`, message says the original is unchanged: pass. Save cancel changes nothing: pass. Wrong password shows
"Wrong password, or the file was changed or damaged." and writes no output: pass. Correct decrypt: bytes equal: pass.
Ctrl+Shift+F12: friendly dialog, app keeps running, exception line in the log: pass.

**Gate.**

| Check | Result |
|---|---|
| Build Release (warnings = errors) | pass |
| Presentation tests | pass |
| Integration test | pass |
| `verify.ps1` | run by pre-push (see CI) |

**Questions to be able to answer.**
1. Why does the ViewModel catch only specific exception types and never `Exception`?
2. Why are the user messages fixed text instead of `exception.Message`?
3. Why are the safety nets registered before `base.OnStartup`, and when must the app close instead of continue?
4. Why is `overwriteExisting: true` safe in the ViewModel?
5. Why can `ResetAfterUnexpectedError` not simply set the state when an operation is Working?

---

## Stage 3 — WPF window (Steps 3.1–3.7) — 2026-11-24

**Built.** Composition root and safety nets (3.1, in PR #1); layout following the UX flow (3.2); Fluent theme that follows
Windows light/dark (3.3); drop a file, Ctrl+O, Enter, F1 (3.4); progress with speed and time left, Cancel (Esc), Open
folder, locked inputs (3.5); padlock icon and About with the assembly version (3.6); FlaUI UI tests (3.7).

**Decisions.** Speed and time left are computed in the ViewModel from percentage + file size + clock, so Core's API
stays a plain `IProgress<int>`. `IFolderOpener` is its own small interface and returns `bool` (a file manager that will
not start deserves a hint, not an error dialog). `Application.ThemeMode="System"` (experimental WPF0001, allowed on
purpose) instead of hand-made colours. UI tests are skipped unless `SECUREFILE_UI_TESTS=1`: they take over the keyboard
and need an unlocked desktop, so they are a local/manual check, not part of the gate or CI.

**Review (Sonnet, §1.8 roles, Stage 3 diff).**

| Finding | Severity | Action |
|---|---|---|
| UI tests typed into the main window if a dialog never opened | High | Fixed: assert the dialog exists, bring it to front, then type |
| Typing was global input with no focus check | High | Mitigated: `SetForeground` + readiness wait (a UIA Value-pattern set was not used: the dialog's combo box does not support it) |
| Dispose could throw or leave the app running | Medium | Fixed: kill if it will not close, best-effort folder delete |
| Runner parallelism could break shared-keyboard tests | Medium | Fixed: `parallelizeTestCollections: false` |
| `FindExe` gave a raw error, tests order-dependent | Medium | Fixed: clear message; password re-entered before decrypt |
| UI tests never run in gate/CI | Medium | Accepted and documented (see above) |
| F1 could be swallowed by a focused control | Low | Fixed: `PreviewKeyDown` |
| About dialog had no test | Low | Fixed: new F1/About test |
| Explorer argument string, XML docs on test helpers, About fixed width at large text scale | Low | Explorer remark added; others deferred |

**Results.** Gate: build (warnings = errors) and 389 tests pass. UI tests: 4 pass locally (encrypt/decrypt round trip,
wrong password, accessible names, About). Accessibility Insights FastPass, automated checks on the SecureFile window:
**0 failures**. Not verified by hand: FastPass Tab Stops, Narrator, dark mode, 150%/200% scaling, a real drag-and-drop
from Explorer, the Open folder click. Known flaky test (cleanup race, name saved in the project notes):
`FileDecryptionTests.DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing(length: 3145728)` failed once in
`Dispose()` because a temp file was still open.

**Questions to be able to answer.**
1. Why compute speed and time left in the ViewModel instead of changing Core's progress API?
2. Why does Cancel leave no output file? (Atomic write, ADR-07: temp file + move.)
3. Why are the UI tests skipped by default, and what would it take to run them in CI?
4. Why is `ResetAfterUnexpectedError` a no-op while an operation is running, yet the `finally` in `RunGuardedAsync` resets?
5. What does "0 failures" in Accessibility Insights automated checks prove, and what does it not prove?

## Stage 4 — testing

### Step 4.2 — large-file runs (T-27), 2026-11-24

Run with `scripts/large-file-check.ps1` (work folder on D:, deleted afterwards; the folder was empty at the end).

| Size | Encrypt | Decrypt | Peak working set | Result |
|---|---|---|---|---|
| 1 GiB (1024 MB) | 10.6 s (96 MB/s) | 10.3 s (100 MB/s) | 46 MB | passed, hash equal, limit 400 MB |
| 4.5 GiB (4608 MB) | not measured | not measured | not measured | **blocked** (see below) |

**Finding: the 4.5 GiB run was blocked by antivirus, not by the app.** Kaspersky Premium's System Watcher flagged
`SecureFile.Integration.Tests.exe` as `PDM:Trojan.Win32.Generic` about 35 s into the run, terminated the process
(a behavioral heuristic: a program writing gigabytes of random data and then encrypting it looks like ransomware) and
deleted the exe the first time. Two attempts gave the same result; both died while the 4.5 GiB file was being written
(555 MB written in the first). The memory assertion never ran for this size, so nothing is known about the app's
behavior above 4 GB yet. We could not add an exclusion, and we did not change the antivirus settings.

**To do:** run the 4.5 GiB case on a machine or CI runner without this heuristic (or after the user excludes the test
output folder), then fill in the row above.

**Questions to be able to answer.**
1. Why is the peak working set a valid proof that the file is streamed? (A loading version needs at least the file size.)
2. Why is a 4.5 GiB file used and not 4 GiB? (More than 2^32 bytes, so any 32-bit size or offset breaks.)
3. Why do we not raise the memory limit or skip the case silently when it cannot run?

---

## Stage 5 — Quality: measure, harden, burn down (2026-11-24)

**What was built.** A BenchmarkDotNet project (`benchmarks/SecureFile.Benchmarks`) and three scripts
(`run-benchmarks.ps1`, `measure-startup.ps1`, `measure-memory.ps1`); `docs/QUALITY_REPORT.md` with every number and its
verdict; `docs/THREAT_MODEL.md` (STRIDE, 20 threats); a progress throttle (10 updates per second); a CLI fix; look-alike
reading in the strength check; and 13 new tests that took the Core mutation score from 95.34 % to 100 %.

**Key decisions.**
- *Report misses, do not hide them.* Three targets were not met (throughput vs copy, warm start, cold start) and are marked
  "Not met" with the reason; a limit was never raised.
- *Compare with the right baseline.* A plain `CopyTo` finishes in RAM (1.9 GB/s); the fair baseline is a copy that is
  forced to disk like our atomic writer. Encrypt costs CPU time plus disk time because the two do not overlap (backlog C13).
- *Do not touch the crash-safe path late.* The overlap fix needs its own design and tests, so it is recorded, not rushed.
- *Disable a mutant only with a written reason.* 6 new `// Stryker disable` comments; the headline score says how many
  mutants are excluded.
- *The threat model is checked by a second pair of eyes.* The reviewer found that the table claimed "verified before any
  plaintext is written", which the code does not do (verify-then-commit); the document was wrong, not the code.

**Review table (security review, then product-owner/tester review).**

| Review | Finding | Result |
|---|---|---|
| Security R1 | Threat table claimed the tag is checked before plaintext is written | Doc fixed (verify-then-commit) |
| Security R2 | Same-file check is path-text only; on Linux/macOS a symlink alias can replace the input | Recorded C14 |
| Security R3 | CLI crashed on `--output <folder>` | Fixed, test |
| Security R4-R8 | Wrong temp-name text, ACL on Windows temp file, PowerShell 5.1 `?` password, log size, replay | Fixed or recorded C15-C17 |
| Second review R1 | A failed drop kept the old file beside an unnamed error | Fixed: message names the file |
| Second review R2, R3 | Cancel after success; trailing separator in `--output` | Fixed |
| Second review R4 | Throttle boundary untestable (`Stopwatch`) | Recorded C18 |

**Gate table.**

| Gate | Result |
|---|---|
| Build, warnings as errors | 0 warnings, 0 errors |
| Tests | 604 total, 596 passed, 8 skipped (large files, UI tests that need a desktop), 0 failed |
| Core coverage | 98.7 % lines, 98.6 % branches |
| Presentation coverage | 95.8 % lines |
| Mutation score (Core) | 100 % of 379 tested mutants (207 disabled with reasons) |
| Vulnerable packages | 0 |
| Throughput / warm start / cold start | not met, explained |

**Self-check questions.**
1. Why is a plain file copy a misleading baseline for encryption speed, and what is a fairer one?
2. Why does memory stay at about 28 MB for 1 MB and 1 GiB files alike?
3. What does a mutation score of 100 % not prove, and why must a disabled mutant carry a reason?
4. Why can a wrong password and a damaged file give the same error without weakening the design? (Hint: padding oracle.)
5. Why was the threat table's "verified before any plaintext is written" wrong, and what is verify-then-commit?

---

## Stage 6: Publishing (2026-11-25)

What was built: the publish scripts now build `win-arm64` next to `win-x64` (WPF and CLI), a Windows Sandbox config and click list,
ADR-15 (installer and signing, **proposed**), a user-facing README, and a release-candidate procedure in `docs/RELEASING.md`.

Why these choices:
- **win-arm64 is cross-compiled**: .NET can target another CPU from an x64 PC because a self-contained build only needs the
  matching runtime pack. Building proves it compiles and links (the exe header says ARM64, machine type 0xAA64); it does **not**
  prove it runs. The docs say "built, never run" everywhere.
- **No installer (proposed)**: a portable zip needs no admin rights and leaves nothing behind. An installer is also flagged by
  SmartScreen unless signed, so it adds work without removing the warning. Signing costs money and identity checks: the user decides.
- **Sandbox**: a throw-away clean Windows is the closest thing to a friend's PC. It is not enabled here (admin and reboot), so the
  click list in `docs/SANDBOX_TEST.md` is a user task.
- **CLI archive names** now start `securefile-cli-` because GitHub compares asset names without case, and `SecureFile-...zip`
  would collide with `securefile-...zip`.
- **`asInvoker`** in the WPF manifest states that the app never asks for administrator rights (half of backlog C16).

Waiting for the user: run the Sandbox test, decide ADR-15, supply screenshots (`docs/SCREENSHOTS_NEEDED.md`), nothing published.
Gotcha: `powershell -File script.ps1 -Runtimes a,b` passes `a,b` as ONE string; use one runtime, or call it from inside PowerShell.

## Stage 7: Avalonia windows for macOS and Linux (2026-11-25)

- **Why the dialog interfaces became async (ADR-16):** Avalonia's `StorageProvider` and `Launcher` only return tasks (on
  Linux another process, the desktop portal, draws the dialog). Blocking on them freezes the UI thread they need; a nested
  message loop invites re-entrancy bugs. WPF just returns `Task.FromResult`, so its behaviour did not change.
- **One ViewModel, two windows:** the Avalonia window binds the same `MainViewModel`; only three small services differ. In
  Avalonia the password box is a normal `TextBox` with `PasswordChar`/`RevealPassword`, so the password is a plain two-way
  binding (WPF needed code-behind because `PasswordBox.Password` is not bindable).
- **A test found a real gap:** Avalonia blocks Copy in a hidden password box but allows it once the text is revealed; the
  window's `CopyingToClipboard`/`CuttingToClipboard` handlers are what keep it off the clipboard, and the headless test proves it.
- **Headless UI tests:** `Avalonia.Headless.XUnit` is built for xUnit v3 3.x and fails discovery under 4.x
  (`MissingMethodException`); `HeadlessUnitTestSession` from `Avalonia.Headless` is framework-neutral and works.
- **Cross-OS golden files:** each OS encrypts the same computed 3 MB sample with the CLI in CI, every OS decrypts all of
  them. Nothing large is committed; the password goes through stdin and is non-ASCII on purpose.
- **Windows `tar.exe` stores 0644,** so programs packed on Windows need `chmod +x`. `System.Formats.Tar` lets the script set
  0755 itself. The SDK adds an ad-hoc code signature to `osx-arm64` even on Windows (Apple silicon requires one).
- **Size:** WPF 133.6 MB vs Avalonia trimmed 38.3 MB on win-x64; SkiaSharp also copies about 100 MB of native `.pdb` files
  on Windows, which the script deletes. Native AOT needs the platform's native linker, so it was not measured.

Waiting for the user: try the macOS and Linux archives on real systems (no Mac, no WSL distribution here), Native AOT size.
Gotcha: inside `namespace SecureFile.Avalonia`, `Avalonia.Media.X` resolves to `SecureFile.Avalonia.Media`; use `global::`.

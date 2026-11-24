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

# SecureFile — Master Plan

> **What this file is:** the single plan we follow, step by step, to build the
> *Secure File Encryption & Decryption Desktop Application* (RD Technical Assessment – C#).
> Every step says **what** we do, **why**, **what else we could have done and why we did not**,
> **how we prove it works**, and **what you must be able to explain** before we tick it.
>
> **Status legend:** ⬜ not started · 🟨 in progress · ✅ done and understood · ⏸ deferred
>
> **Last updated:** 2026-11-24

---

## Table of contents

1. [The learning rule: understand every line](#1-the-learning-rule-understand-every-line)
2. [The task: requirements and how we are graded](#2-the-task-requirements-and-how-we-are-graded)
3. [The six quality phases and where each one happens](#3-the-six-quality-phases-and-where-each-one-happens)
4. [Key decisions (with alternatives)](#4-key-decisions-with-alternatives)
5. [Crypto design: the encrypted file format and the two flows](#5-crypto-design-the-encrypted-file-format-and-the-two-flows)
6. [Solution structure](#6-solution-structure)
7. [Step-by-step roadmap (Stages 0–9)](#7-step-by-step-roadmap-stages-09)
8. [Phase 1 — Measurements and targets](#8-phase-1--measurements-and-targets)
9. [Phase 2 — Test catalog](#9-phase-2--test-catalog)
10. [Phase 3 — Delivery to the end user](#10-phase-3--delivery-to-the-end-user)
11. [Phase 4 — UI/UX specification](#11-phase-4--uiux-specification)
12. [Phase 5 — Cross-platform (bonus track)](#12-phase-5--cross-platform-bonus-track)
13. [Phase 6 — Stakeholder suggestions](#13-phase-6--stakeholder-suggestions)
14. [Final deliverables: explanation guide and Team Lead presentation](#14-final-deliverables-explanation-guide-and-team-lead-presentation)
15. [Requirements traceability](#15-requirements-traceability)
16. [Risks](#16-risks)
17. [Glossary (plain language)](#17-glossary-plain-language)
18. [References](#18-references)
19. [Progress log](#19-progress-log)

---

## 1. The learning rule: understand every line

AI is allowed as an assistant, but **you must understand and be able to defend every word**.
So the code is written to be *read*, and every step follows the same loop.

### 1.1 The loop for every step

| # | What happens | Output |
|---|---|---|
| 1 | **Lesson first.** Before any code, a beginner-friendly lesson on every concept the step uses: plain definition, an everyday analogy, why we need it, and the alternatives. | "Lesson" section in `docs/LEARNING_LOG.md` + summary in chat |
| 2 | **Small code.** One step = one small, focused piece (aim ≤ ~150 lines incl. tests). | Code + tests |
| 3 | **Walkthrough.** Line-by-line explanation of the new code: every type, method, variable name, and why it is there. | Entry in `docs/LEARNING_LOG.md` |
| 4 | **Review Board (§1.8).** Independent reviewers (Architect, Performance, Security, QA, Critic, PM, End user, Team Lead) review the step; every finding is fixed or answered. | Review report in `docs/LEARNING_LOG.md` |
| 5 | **Self-check (not an exam).** Open-book questions *after* the lesson; you answer in your own words, and any unclear answer just shows which part to explain again. | ✅ when the concepts are clear to you |
| 6 | **Green.** Tests pass, analyzers clean. | Commit |

### 1.2 Code-readability rules (so every line is understandable)

| Rule | Why |
|---|---|
| Full-word names: `inputFilePath`, `authenticationKey`, `iterationCount`. Only standard crypto abbreviations (`Iv`, `Hmac`, `Aes`), each explained in the glossary. | Names explain themselves; nothing to decode. |
| One class per file; file name = class name. | Easy to find things. |
| Methods short (≈ ≤ 30 lines) and do one thing. | Each method can be explained in one sentence. |
| `var` only when the type is obvious on the same line (`var buffer = new byte[16];`). Otherwise write the type. | You always see the type you are working with. |
| No "clever" code: no long LINQ chains, no nested ternaries, no reflection tricks. | Clever code is hard to defend in a review. |
| Every public member has an XML doc comment (`/// <summary>`). Non-obvious lines get a **why** comment, not a *what* comment. | Graded criterion "Code and Comments Quality". |
| Constants instead of magic numbers (`SaltSizeInBytes = 16`, not `16`). | Every number has a name and a reason. |
| Source-generated code (CommunityToolkit.Mvvm) is opened and explained once. In VS: *Solution Explorer → Dependencies → Analyzers → CommunityToolkit.Mvvm.SourceGenerators*. | No hidden "magic". |
| `async void` only for UI event handlers; library code uses `ConfigureAwait(false)`. | Standard async rules; avoids deadlocks and lost exceptions. |

### 1.3 Naming conventions (C# standard)

| Kind | Style | Example |
|---|---|---|
| Class, record, method, property, constant | `PascalCase` | `FileEncryptionService`, `EncryptAsync`, `SaltSizeInBytes` |
| Interface | `I` + PascalCase | `IFileDialogService` |
| Local variable, parameter | `camelCase` | `outputFilePath` |
| Private field | `_camelCase` | `_fileDialogService` |
| Async method | ends with `Async` | `DecryptAsync` |
| Boolean | reads as a question | `IsBusy`, `CanEncrypt`, `HasSelectedFile` |

An **identifier dictionary** (every class/method/variable name and what it means) is built in
`docs/LEARNING_LOG.md` as we go and copied into the final guide.

### 1.4 XML documentation standard (every service, feature and change)

Every public type and member is documented, and the build fails if one is missing (`GenerateDocumentationFile` + CS1591 as an error).

| Tag | Used for | Example |
|---|---|---|
| `<summary>` | One sentence: what it does | `Encrypts a file with AES-256-CBC and seals it with HMAC-SHA256.` |
| `<param>` | Each parameter: meaning and allowed values | `<param name="inputFilePath">Full path of the file to encrypt. Must exist.</param>` |
| `<returns>` | What comes back | `A task that completes when the output file is written.` |
| `<exception>` | Each exception the caller should handle, and when | `<exception cref="AuthenticationFailedException">Wrong password or damaged file.</exception>` |
| `<remarks>` | **Why** it works this way, alternatives rejected, links to the ADR | `See ADR-07: temp file + atomic move.` |
| `<example>` | Short usage for the main services | A 3-line call example |

Interfaces carry the full documentation; implementations use `<inheritdoc/>` so the text is written once.
Every change also updates `CHANGELOG.md` (Keep a Changelog format).

### 1.5 Git standard

| Item | Rule | Example |
|---|---|---|
| Commit format | **Conventional Commits**: `type(scope): subject` | `feat(core): add streaming AES-256-CBC encryption` |
| Types | `feat`, `fix`, `test`, `docs`, `refactor`, `perf`, `build`, `ci`, `chore`, `style` | `test(core): add NIST SP 800-38A CBC vectors` |
| Scopes | `core`, `presentation`, `wpf`, `avalonia`, `cli`, `tests`, `docs`, `build` | — |
| Subject | Imperative ("add", not "added"), ≤ 72 characters, no period | — |
| Body | **Why** the change was made, and anything a reviewer should know | — |
| Size | One logical change per commit; tests in the same commit as the code they test | — |
| Branches | `main` always builds; work on `feature/<short-name>`, merged after CI is green | `feature/core-encryption` |
| Tags | Semantic versioning: `v1.0.0` for the submitted build | — |
| Attribution | **No AI attribution lines** in commits, PR descriptions or any document | — |

### 1.6 Swagger / OpenAPI

Swagger documents **web APIs** (HTTP endpoints). This is a desktop app with no web API and no network
calls (ADR-12), so there is nothing for Swagger to describe; adding it would add code without value and
would be a fair question from the Team Lead. The equivalent here is the XML documentation (§1.4),
which Visual Studio shows in IntelliSense and which can be turned into a browsable API reference
site with **DocFX** if wanted. If a REST API is ever added (for example a v2 server version), Swagger/OpenAPI
becomes required for it.

### 1.7 Quality gate — checked before EVERY commit and push (mandatory)

No step is committed, pushed or marked done until **every** item below is checked. Automated items run with one
command. Locally, the `pre-push` Git hook runs it and blocks a failing push by default (hooks must be enabled once
per clone and can be bypassed with `--no-verify`); **the real gate is CI**, which runs the same script on every push
and pull request and cannot be skipped:

```powershell
pwsh -File scripts/verify.ps1
```

| # | Area | Check | How it is verified | Automated |
|---|---|---|---|---|
| G1 | Requirements | The step's "Done when" is met; traceability (§15) still true | Review against §7 and §15 | Manual |
| G2 | Code quality | Formatting matches `.editorconfig` | `dotnet format --verify-no-changes` | ✅ verify.ps1 |
| G3 | Code quality / security | Build with **all analyzers** (incl. CA53xx crypto rules): 0 warnings | `dotnet build -c Release` | ✅ verify.ps1 |
| G4 | Tests | All tests pass; new behaviour has tests for normal, edge and invalid cases | `dotnet test` + review of §9 catalog | ✅ + manual review |
| G5 | Code quality | Core coverage ≥ 90 % lines, ≥ 85 % branches | Microsoft Code Coverage + ReportGenerator | ✅ verify.ps1 |
| G6 | Security | 0 vulnerable NuGet packages (incl. indirect) | `dotnet list package --vulnerable --include-transitive` | ✅ verify.ps1 |
| G7 | Security | No secrets, private keys or tokens in tracked files | Pattern scan (`git grep`) | ✅ verify.ps1 |
| G8 | Exceptions / security | No `catch (Exception)` in Core/Presentation, no empty `catch`, no `throw ex;`, no `async void` in libraries, no `System.Random`, no whole-file reads in Core, no console output | Pattern scan | ✅ verify.ps1 |
| G9 | Exceptions | Every thrown exception is documented (`<exception>`); messages contain no secrets; cleanup in `using`/`finally` (ADR-13) | Review | Manual |
| G10 | Modularity | Layer rule holds (Core → no UI; Presentation → no UI framework); Core classes sealed | Architecture tests | ✅ (tests) |
| G11 | Modularity / quality | Cyclomatic complexity ≤ 10 per method; Maintainability Index ≥ 60 per type **and** method | Built-in analyzers CA1502/CA1505 with `CodeMetricsConfig.txt` (same numbers as VS *Calculate Code Metrics*) | ✅ build |
| G12 | Test strength | Mutation score ≥ 80 % on Core (target 100 %), measured in the final hardening phase. A line may be excluded only with `// Stryker disable once <mutator> : reason` when no test can observe it (wiping memory, pinning) or it can never run; every exclusion is listed in the step's gate table | `dotnet stryker` | Manual (final hardening) |
| G13 | Performance | No whole-file loading; no needless allocations in loops; measured when the step is performance-relevant | Review + quick measurement / BenchmarkDotNet (Stage 5) | Manual |
| G14 | Documentation | XML docs on all public members (build-enforced); CHANGELOG; LEARNING_LOG lesson + walkthrough; plan status | Build (CS1591) + review | Partly |
| G15 | Git | Conventional Commit message; **no AI attribution**; one logical change per commit | `commit-msg` hook + verify.ps1 | ✅ hooks |
| G16 | System design / patterns | The design fits §1.9 (responsibilities, dependencies, patterns chosen *for a reason*, no over-engineering) | Review Board: Architect | Manual |
| G17 | Algorithms & data structures | Time and space complexity of every public operation stated and justified; no avoidable work; best-fit data structure | Review Board: Performance engineer + `<remarks>` "Complexity:" line | Manual |
| G18 | Independent review | Review Board (§1.8) ran; every finding is fixed or answered with a recorded decision | Review Board report in `LEARNING_LOG.md` | Manual |

**One-time setup per clone:** `git config core.hooksPath .githooks` (enables the `commit-msg` and `pre-push` hooks).

The results of the manual items (G1, G9, G11–G13, G16–G18) are written in the **stage's** "Quality gate" table in
`docs/LEARNING_LOG.md`.

#### Cadence — risk-based testing, stage-level reviews (decided 2026-11-24, after Step 1.6)

Per-step reviews, mutation runs and long write-ups cost too much time for the number of stages left. The rule from
now on: **test where bugs are expensive, automate everything that is cheap, and review once per stage.**

| Level | Scope | Testing |
|---|---|---|
| **A — Core** | Crypto, file I/O, error model (Stage 1) | Full tests as built in Steps 1.2–1.6: boundaries, tampering, cancel, no partial output. |
| **B — ViewModels** | State, commands, error messages (Stage 2) | A few tests per ViewModel, written in the same session as the code: main path, state changes, one error case. No mutation testing. |
| **C — UI** | XAML, layout, dialogs (Stage 3+) | No unit tests. One end-to-end integration test (encrypt → decrypt a real file) + a short manual smoke checklist run in the app. |

| Item | When |
|---|---|
| `verify.ps1` (G2–G8, G10, G11, G15), pre-push hook, CI | **Every commit / push** — unchanged |
| G1, G4 review, G9, G13 | Every step, by the author, without a write-up unless a decision is made |
| §1.9 / §1.10 checklists | Followed silently; only real decisions are recorded |
| Reviewer agent (§1.8) — G16–G18 | **Once per stage**, one agent with all roles, on the whole stage's changes |
| Stryker (G12) | **Once, in the final hardening phase** (full Core run, ≥ 80 %) |
| LEARNING_LOG | **One short section per stage**: what was built, key decisions, review table, gate table, ~5 self-check questions |
| Steps per chat | Several steps per chat (e.g. a whole slice), one commit per step, one hand-off prompt (`docs/NEXT_STEP_PROMPT.md`) at the end |

**Final hardening phase** (before the presentation): full Stryker run, one security review, performance measured on
large files, manual end-to-end pass on a clean Windows machine, then the explanation guide and the Team Lead PPT.

### 1.8 Review Board — independent reviewers for every stage

**Goal:** production-ready code that would impress a Team Lead, a senior engineer and an end user. One author
checking their own work misses things, so every step is reviewed by **independent reviewers with different
viewpoints** (run as separate AI reviewer agents that read the code fresh and only report — they never edit).
The author then fixes each finding or records why not.

**When (since 2026-11-24, see §1.7 Cadence):** **one reviewer agent per stage**, covering all roles below, on the
whole stage's changes after its tests are green. Steps 1.1–1.6 had per-step reviews; that level of review returns
only in the final hardening phase.

| Role | Looks at | Key questions |
|---|---|---|
| **Software Architect** | System design, layering, responsibilities, design patterns, extensibility | Is each class doing one job (SRP)? Are dependencies pointing the right way (DIP)? Is each pattern justified — or is it ceremony? Will the next steps (crypto, UI, CLI, Avalonia) fit without rewrites? |
| **Performance & Algorithms Engineer** | Time and space complexity, data structures, allocations, I/O patterns, scalability with file size | What is the Big-O in time and memory? Is there a faster or leaner standard approach? Any hidden allocations, copies, repeated I/O or blocking calls? Does it scale from 0 B to 100 GB? |
| **Security Engineer** | Threats (STRIDE), input validation, crypto use, secrets in memory/logs/messages, file-system attacks | What can a malicious file, path or user do? Is anything sensitive leaked? Is every input validated at the boundary? |
| **QA / Test Engineer** | Test completeness and strength | Which cases are missing (normal, edge, invalid, "impossible", cross-OS)? Would a subtle bug survive the tests? |
| **Critic (devil's advocate)** | Weak points, hidden assumptions, over- and under-engineering, naming | What is the weakest part? What will break first? What would a harsh senior reviewer say in a code review? |
| **Senior Project Manager / Product Owner** | Requirement fit, scope, risk, value, documentation | Does this serve the task's requirements and grading criteria? Is anything gold-plated or missing? Is the story clear for the presentation? |
| **End User (non-technical)** | Behaviour visible to people: wording, units, messages, predictability | Would a normal person understand what they see? Are messages calm, clear and actionable? |
| **Team Lead (presentation audience)** | Defensibility | Which question would I ask, and does the author have a clear answer? |

**Report format** (stored in the step's section of `LEARNING_LOG.md`):

| ID | Role | Severity | Finding | Decision (fix / keep, with reason) |
|---|---|---|---|---|

Severity: **High** (bug, security issue, or requirement miss — must fix), **Medium** (real weakness — fix now unless
there is a recorded reason), **Low** (polish — fix if cheap), **Note** (no change; worth knowing or presenting).

### 1.9 Design, pattern and algorithm standards

| Area | Standard |
|---|---|
| **SOLID** | **S**ingle responsibility per class · **O**pen for extension (new formats/UIs added without editing Core) · **L**iskov (implementations honour their interface contract, incl. exceptions) · **I**nterfaces small and focused · **D**ependencies on abstractions at the boundaries (file system, dialogs, clock) |
| **Patterns — only with a reason** | Use a pattern when it removes a real problem, name it in the XML `<remarks>`, and record the alternative. Planned: **Strategy** (the cipher behind `IFileEncryptionService`, so a v2 AES-GCM format can be added), **MVVM** (UI), **Composition Root / pure DI** (wiring), **Template-free value objects / records** (data), **Guard clauses** (validation), **Result mapping** at the UI boundary (ADR-13). Avoided unless needed: service locator, singletons with state, inheritance hierarchies, generic repositories. |
| **KISS / YAGNI** | The simplest design that meets the requirement and the next known step. No abstraction "for the future" without a planned step that uses it. |
| **Algorithms & data structures** | Every public operation documents **time and space complexity** in a `<remarks>` "Complexity:" line. Streaming algorithms for file data: **O(n) time, O(1) extra memory** (fixed buffer) regardless of file size. Pick the data structure for the access pattern (array for fixed lookups, `Span<T>`/pooled buffers for byte work, no LINQ in hot loops). |
| **Performance hygiene** | No repeated I/O for the same data; no blocking the UI thread; async for I/O; buffers reused (`ArrayPool`); measure before and after optimising (BenchmarkDotNet). |
| **Immutability & state** | Data passed between layers is immutable (records); mutable state only inside the ViewModel and short-lived local variables. |
| **Production readiness** | Deterministic, documented, Core tested on 3 OS (the WPF app is Windows-only by nature), observable (local logs), secure by default, no telemetry, graceful failure (ADR-13), versioned file format. |

### 1.10 Engineering principles — checked BEFORE writing any code (mandatory)

Before each step's code is written, the design is checked against this list (Review Board design review, §1.8).
The answers are written in the step's lesson in `LEARNING_LOG.md`. Each principle is a tool, not a religion: when two
conflict (e.g. DRY vs. clarity, OCP vs. YAGNI), the step records which one wins and why.

| Principle | Question we answer before coding | In this project |
|---|---|---|
| **S — Single Responsibility** | Does each class have exactly one reason to change? | `FileDetailsReader` reads metadata; `FileSizeFormatter` formats; neither does both. |
| **O — Open/Closed** | Can we add behaviour by adding code instead of editing working code? | A future AES-GCM format = a new cipher class behind the same interface (Strategy), not edits to v1. |
| **L — Liskov Substitution** | Can every implementation replace its interface without surprises (same inputs, results, exceptions)? | Fakes in ViewModel tests honour the documented exceptions of `IFileDetailsReader`. |
| **I — Interface Segregation** | Is every interface small and specific to one client need? | One method per interface where possible (`Read`); no "god" service interface. |
| **D — Dependency Inversion** | Do high-level parts depend on abstractions at the boundaries (disk, dialogs, clock), wired in one place? | ViewModels receive interfaces; `App.OnStartup` is the only place that chooses implementations. |
| **DRY** | Is each piece of *knowledge* (a rule, a constant, a format) written once? | Header layout constants in one class; error messages in one `ErrorMessageProvider`. Duplicated *code* that represents different knowledge may stay separate. |
| **KISS** | Is this the simplest design that fully works? | Pure DI instead of a container; static formatter instead of a service. |
| **YAGNI** | Is every abstraction used by a current or *planned* step? | No `IFileSystem` wrapper until a test needs one; no plugin system. |
| **OOP — encapsulation** | Is state private and changed only through meaningful methods? Are invariants protected? | Records are immutable; fields are private and `readonly`. |
| **OOP — abstraction & polymorphism** | Do callers see *what* an object does, not *how*? | Callers use `IFileDetailsReader`, not `FileInfo`. |
| **Composition over inheritance** | Are we combining small objects instead of building class hierarchies? | Core classes are `sealed`; behaviour is combined through constructor parameters. |
| **Separation of concerns / layering** | Is each concern in its layer (Core logic, Presentation state, UI rendering)? | Enforced by architecture tests (G10). |
| **High cohesion, low coupling** | Are related things together and unrelated things apart? | Code is organised **by feature** (`Files/`, `Cryptography/`), interface next to implementation (ADR-14). |
| **Law of Demeter** | Does code talk only to its direct collaborators (no `a.B.C.D()` chains)? | Review item. |
| **Fail fast** | Are invalid inputs rejected at the boundary with a clear exception? | Guard clauses (ADR-13). |
| **Immutability by default** | Can this be immutable? | Records, `readonly` fields, `static readonly` tables. |
| **Design patterns** | Which known pattern solves this, and is it worth its cost? Name it in `<remarks>`. | §1.9 list; no pattern without a reason. |
| **Algorithms & data structures** | What is the time and space Big-O? Is there a better standard algorithm or data structure for this access pattern? | `<remarks>` "Complexity:" line (G17); streaming O(n) time / O(1) memory for file data. |
| **System design qualities** | Scalability (0 B to 100 GB+), reliability (atomic output), security (STRIDE), observability (local log), maintainability, portability (3 OS). | Review Board roles (§1.8). |
| **Secure by design** | What could an attacker control here (path, file content, password), and how is it constrained? | Security Engineer review; threat table in Stage 5. |
| **SDLC traceability** | Which requirement does this serve (§15), which test proves it (§9), where is it documented? | Every step links requirement → code → test → doc. |

**SDLC mapping (how this project follows the software development life cycle):**

| SDLC phase | Where it happens here |
|---|---|
| Requirements | §2 (R1–R6 from the task, D1–D7 derived), §13 stakeholder suggestions |
| Design | §4 ADRs, §5 crypto design, §6 structure, §1.9–1.10 standards, Review Board design review |
| Implementation | §7 steps (small, explained, reviewed) |
| Verification | §1.7 quality gate, §9 test catalog, Review Board code review, CI on 3 OS |
| Release | Stage 6 packaging, Stage 9 deliverables, semantic version tags |
| Maintenance | CHANGELOG, Dependabot, versioned file format, ADRs explaining *why* for future maintainers |

---

## 2. The task: requirements and how we are graded

### 2.1 Requirements from the document

| ID | Requirement (from the document) |
|---|---|
| **R1** | Let the user select **any** file from the local computer. |
| **R2** | Show the selected file's **name, size, and extension** on the main window. |
| **R3** | **Encrypt** the file with **AES, 256-bit key, CBC mode**, using .NET cryptographic APIs. |
| **R4** | **Decrypt** the encrypted file and get back the original file. |
| **R5** | Let the user choose the **output file name and save location** before saving. |
| **R6** | C#, **WinForms or WPF**, built-in .NET cryptography. Estimated duration: **3 days**. |

### 2.2 Derived requirements (the document is silent, but the app cannot be correct without them)

| ID | Derived requirement | Why it is needed |
|---|---|---|
| **D1** | The key comes from a **user password** (PBKDF2). | AES-256 needs a 32-byte key. The document never says where it comes from. Hard-coding or saving it next to the file makes encryption useless. |
| **D2** | **Integrity check** (HMAC-SHA256, encrypt-then-MAC). | CBC alone cannot detect tampering, and a wrong password sometimes gives garbage output instead of an error. |
| **D3** | **Streaming** (constant memory) for any file size. | "Any file" includes 0 bytes and files bigger than RAM. |
| **D4** | **Progress and Cancel.** | Large files take time; the app must never look frozen. |
| **D5** | **No partial or wrong output** on failure (temp file, then atomic move). | A failed decrypt must not leave a broken file that looks real. |
| **D6** | **Clear, human error messages.** | Users are not technical. |
| **D7** | A **versioned file format** with a recognizable header. | Needed to store salt/IV/iterations, detect "this is not our file", and allow future upgrades. |

### 2.3 How we are graded (and the evidence we will show)

| ID | Criterion | Evidence we will bring |
|---|---|---|
| **E1** | Completeness | Requirements checklist (R1–R6, D1–D7) with the test that proves each one. |
| **E2** | Self-learning | References slide + `docs/REFERENCES.md` (Microsoft Learn, NIST, RFCs, OWASP). |
| **E3** | Performance & code size | Benchmark numbers (MB/s, memory flat vs file size, startup time), published size, what we did to improve them. |
| **E4** | Modularity | Layer diagram; Core reused by WPF, Avalonia, CLI, tests; architecture tests. |
| **E5** | Code & comment quality | Analyzer results, code metrics, coverage, mutation score, XML docs. |
| **E6** | UI design | Screens, UX decisions, accessibility check, usability test result (SUS score). |

---

## 3. The six quality phases and where each one happens

The six phases are **quality dimensions**, not a strict order. Each one is built into specific stages.

| Phase | Meaning | Where it happens (stages in §7) | Proof document |
|---|---|---|---|
| **1** Security, performance, modularity, code quality — *measured* | Every aspect has a tool and a numeric target | Stage 0 (tooling), every stage (analyzers), Stage 5 (measure) | `docs/QUALITY_REPORT.md` |
| **2** Test all possible and "impossible" edge cases | Unit, integration, UI, manual, user tests | Stages 1–4 | `docs/TESTPLAN.md` + test results |
| **3** Easy for a non-technical user, minimal setup | Self-contained `.exe`, tested on a clean machine | Stage 6 | `README.md` quick start |
| **4** Senior-level UI/UX | Layout, sizes, flow, accessibility, consistency | Stage 3 (and Stage 7 for Avalonia) | `docs/UX_SPEC.md` + screenshots |
| **5** All operating systems | Windows / macOS / Linux — **bonus track** | Stage 7 | CI matrix + cross-OS tests |
| **6** Stakeholder suggestions (PO, BA, PM) | Valuable additions, including cross-platform as a bonus | Stage 8 | `docs/SUGGESTIONS.md` + slides |

---

## 4. Key decisions (with alternatives)

Each decision is also saved as an **ADR** (Architecture Decision Record) in `docs/adr/`.
An ADR is a one-page note: *context → options → decision → consequences*. Team Leads like them
because they show the reasoning, not only the result.

### ADR-01 — UI framework

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **WinForms** | Simplest; drag-and-drop designer; the template already exists. | Old look; weak data binding/MVVM; harder high-DPI; Windows-only; skills do not transfer to cross-platform. | ❌ |
| **WPF** | Allowed by the document; XAML + data binding + MVVM; Fluent theme (since .NET 9); mature. | Windows-only; cannot be trimmed (smaller size). | ✅ **Required track** |
| **Avalonia** | Same XAML style as WPF → what you learn in WPF transfers; runs on Windows, macOS, Linux; supports trimming and Native AOT. | Not named in the document, so it must be a bonus, not a replacement. | ✅ **Bonus track** |
| .NET MAUI | Microsoft cross-platform UI. | No official Linux desktop support; heavier. | ❌ |

**Decision:** WPF is the main app (meets the document exactly). Avalonia is a bonus that reuses the same
Core and ViewModels, presented under Phase 6. The empty `EncryptionHandling` WinForms template is replaced
(we ask before deleting it).

**Why WPF over WinForms (be ready to say this):** data binding lets us keep *all* logic in a ViewModel
that can be unit-tested without a window, and the same ViewModel is reused by Avalonia. WinForms logic
tends to live in event handlers inside the form, which cannot be reused or tested easily.

### ADR-02 — Where the AES key comes from

| Option | Problem | Verdict |
|---|---|---|
| Hard-coded key in the code | Anyone with the `.exe` can decrypt every file. Flagged by analyzer CA5390. | ❌ |
| Random key saved in a key file | User must manage and protect a second file; lose it = lose data. | ⏸ possible future option |
| Windows DPAPI (`ProtectedData`) | Windows-only; file can only be opened by the same Windows user on the same machine. | ❌ |
| **User password → key derivation (PBKDF2)** | User remembers one secret; file can be opened anywhere with the password. | ✅ |

### ADR-03 — Password-to-key function

| Option | Notes | Verdict |
|---|---|---|
| Plain SHA-256(password) | Fast → attackers can guess billions of passwords per second. Never for passwords. | ❌ |
| Argon2id / scrypt | Strongest modern choices, but **not built into .NET** → third-party package. The document asks for built-in .NET crypto. | ❌ (mention as future) |
| **PBKDF2-HMAC-SHA256** via `Rfc2898DeriveBytes.Pbkdf2` (static method) | Built-in, cross-platform, FIPS-approved, recommended by Microsoft. OWASP figure: **600,000 iterations** for SHA-256. | ✅ |

Notes: the old `Rfc2898DeriveBytes` constructors are **obsolete in .NET 10** (`SYSLIB0060`), so we use the static
`Pbkdf2` method. The iteration count is stored in the file, so it can be raised later without breaking old files.

### ADR-04 — Integrity (CBC is required, but CBC alone is not enough)

| Option | Notes | Verdict |
|---|---|---|
| CBC only | Tampering is not detected; with a wrong password, ~1 in 256 times the padding happens to look valid and you get garbage with no error. | ❌ |
| MAC-then-encrypt | Known to be weaker (padding-oracle style problems). | ❌ |
| AES-GCM (authenticated encryption) | Modern best choice — but the document **requires CBC**. | ⏸ future format v2 |
| **CBC + HMAC-SHA256, encrypt-then-MAC** | Keeps the required CBC and adds tamper/wrong-password detection. Standard, well-studied construction. | ✅ |

### ADR-05 — Two keys from one password

We need **two different keys**: one for AES, one for HMAC (never reuse one key for two jobs).

| Option | Problem | Verdict |
|---|---|---|
| Ask PBKDF2 for 64 bytes and split them | PBKDF2 computes the 600,000 iterations **twice** (once per 32 bytes). We pay double, but an attacker only needs to compute one half to test a password guess → we lose speed with no security gain. | ❌ |
| Run PBKDF2 twice with two salts | Same doubling problem. | ❌ |
| **PBKDF2 → 32-byte master key → `HKDF.Expand` with two labels** | One slow step for the password, then a fast, standard split into independent keys (RFC 5869). | ✅ |

### ADR-06 — How file data flows (performance and memory)

| Option | Notes | Verdict |
|---|---|---|
| Read the whole file into memory (`File.ReadAllBytes`) | Simple, but a 4 GB file needs 4 GB+ of RAM, and >2 GB fails. | ❌ |
| `CryptoStream` + read/write loop with a reused buffer | Constant memory, but for **decryption** disposing the stream after a failed check runs the final block and throws a *padding* error (verified in Review Board 1.4), so padding would be examined before the HMAC verdict. Also hides when the last block is processed. | ❌ (revised 2026-11-24) |
| **Our own loop with a reused 1 MiB `ArrayPool` buffer and `ICryptoTransform.TransformBlock`; `TransformFinalBlock` only at the end — for decryption only after the HMAC tag matched** | Constant memory for any size; every step is visible (read → HMAC → transform → write → progress → cancel); the padding of an unverified file is never touched. The last-block rule is pinned by a test (`Decryptor_TransformBlock_HoldsBackTheLastBlockUntilFinalBlock`). Encryption uses the same loop shape. | ✅ |
| One-shot `EncryptCbc`/`DecryptCbc` per chunk with manual chaining | Chaining IVs and padding by hand is new correctness risk for no measurable gain. | ❌ |

### ADR-07 — Safe output on decrypt

| Option | Notes | Verdict |
|---|---|---|
| Write straight to the final file | If the password is wrong or the file was changed, a broken file is left behind. | ❌ |
| Two passes: verify HMAC first, then decrypt | Safe, but reads the file twice (slower for big files). | ❌ |
| **One pass to a temp file in the destination folder; verify HMAC; then atomic `File.Move`** | Fast (one read) and safe (the final file appears only if everything is valid). Temp file is in the *same folder* because a move across drives is not atomic. | ✅ |

**Trade-off accepted (Review Board Step 1.1, Team Lead question):** during the single pass, *not-yet-verified*
plaintext exists in the temp file. If the app is killed or the power fails before verification, that temp file stays
behind. Mitigations: (1) the temp file has a recognisable hidden name (`.~<name>.securefile-tmp`) so it is obviously
not a real result; (2) it is deleted in `finally` on every handled failure or cancel; (3) the *decrypt* temp file is protected (Step 1.6): on
Windows it gets the *hidden* attribute right after it is created, removed again before the move (a move keeps
attributes); on Linux and macOS it is created with mode 0600 (owner only), which the decrypted result keeps; if
anything fails, it is emptied through its open handle before the delete, so even a failed delete (virus scanner)
leaves no plaintext. The encrypt temp file holds only ciphertext and is not protected this way; (4) the user chose that folder for the decrypted output anyway. The two-pass alternative would
avoid unverified plaintext on disk at the cost of reading the whole file twice; it stays the fallback if a reviewer
judges the risk higher than the speed benefit.

**Accepted limits (Review Board Step 1.5):** the *data* is forced to the disk before the move
(`Flush(flushToDisk: true)`), but the *rename* itself is best effort: .NET has no API to flush the folder entry
(Linux would need an `fsync` of the folder, Windows `MOVEFILE_WRITE_THROUGH`), so after a power cut right at the end
the result may be missing — never half-written. Temp files left by a crash or power cut are not cleaned up
automatically; a start-up scan of recent folders is a Stage 8 suggestion.

### ADR-14 — Code organisation: by feature, interface next to implementation

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| By type: `Interfaces/`, `Services/`, `Models/` folders | Familiar from tutorials | One feature is spread over 3+ folders; low cohesion; every change touches several places | ❌ |
| Separate abstractions assembly (`SecureFile.Abstractions`) | Needed when other assemblies must use only the contracts (plugins, shared packages) | We have no such consumer → YAGNI | ❌ (until needed) |
| **By feature (`Files/`, `Cryptography/`, …), interface next to its implementation** | High cohesion: everything about one feature in one place; namespaces describe the domain | Folder alone does not show what is an abstraction — solved by the `I` prefix convention | ✅ |

SOLID is respected either way: Interface Segregation is about the *size* of an interface, Dependency Inversion about
depending on the interface *type* — neither depends on the folder. The rule: one feature = one folder = one namespace.

### ADR-08 — MVVM helpers

| Option | Notes | Verdict |
|---|---|---|
| Hand-written `INotifyPropertyChanged` and commands | Fully visible, but ~150 lines of repetitive boilerplate. | ❌ (we write one small example to learn how it works) |
| ReactiveUI | Powerful, but steep learning curve. | ❌ |
| **CommunityToolkit.Mvvm** (Microsoft) | Standard, small; source generators remove boilerplate; works in both WPF and Avalonia. We open and explain the generated code once. | ✅ |

### ADR-09 — Wiring objects together (dependency injection)

| Option | Notes | Verdict |
|---|---|---|
| `new` everything inside the ViewModel | Cannot replace dialogs/file system in tests. | ❌ |
| DI container (`Microsoft.Extensions.DependencyInjection`) | Standard in big apps; extra concept to explain. | ⏸ (mention as option) |
| **Pure DI: create objects once in `App.OnStartup` and pass them through constructors** | ~10 readable lines, no hidden behavior, still fully testable. Fits an app with ~6 services. | ✅ |

### ADR-10 — Test framework

| Option | Verdict |
|---|---|
| **xUnit v3** — the most widely used in .NET open source, used by the .NET team itself | ✅ |
| MSTest 4 / NUnit / TUnit — all valid | ❌ (no strong reason to switch) |

### ADR-11 — Delivery format

| Option | Notes | Verdict |
|---|---|---|
| Framework-dependent `.exe` | User must install .NET first → fails "minimal setup". | ❌ |
| **Self-contained, single-file `.exe` (+ ReadyToRun)** | Double-click to run; nothing to install. | ✅ |
| MSIX / Velopack installer | Nicer install/uninstall/update. | ⏸ optional in Stage 6 |

### ADR-12 — Telemetry

No telemetry and no network calls. A file-encryption tool that sends data out is a privacy risk.
We measure performance locally (profilers, benchmarks), not with Datadog-style monitoring.

### ADR-13 — Exception handling: three layers, "never crash, never lie"

Based on Microsoft Learn: *Best practices for exceptions*, `Application.DispatcherUnhandledException` (WPF),
`AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and `Application.ThreadException` (WinForms).
Full record: [adr/0013-exception-handling.md](adr/0013-exception-handling.md).

**Goal:** the app never crashes on any user action, never shows a stack trace, never leaves a half-written file,
and never hides a real failure (no "swallowed" exceptions).

| Option | Problem | Verdict |
|---|---|---|
| `try/catch (Exception)` around everything, show "Error" | Hides bugs, catches things that cannot be recovered, gives the user no useful message. Microsoft: *"if you can't recover, don't catch"*. | ❌ |
| Catch nothing; rely on the default crash dialog | The app closes and the user loses their place. | ❌ |
| Return error codes / `bool` everywhere instead of exceptions | Errors are easy to ignore by mistake; not the .NET convention. | ❌ (except `Try…` methods for *common* cases) |
| **Three layers: Core throws precise exceptions → ViewModel catches expected ones at the user action → global safety nets log and show a friendly message for anything unexpected** | Each layer does one job; expected errors get specific messages; unexpected ones are logged, never silent. | ✅ |

**Layer 1 — Core (throws, rarely catches)**

| Rule (Microsoft guidance) | How we apply it |
|---|---|
| Validate inputs first with throw helpers | `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIfNegative`… |
| Use predefined exception types | `ArgumentException` family, `FileNotFoundException`, `IOException`, `InvalidOperationException`, `OperationCanceledException`. |
| Custom exceptions only when the caller must react differently | `SecureFileException` (base) → `InvalidEncryptedFileException` (not our file / damaged header), `AuthenticationFailedException` (wrong password or tampered). Names end in `Exception`; the three standard constructors. The base and `InvalidEncryptedFileException` (with a `Reason`: not our file / newer version / damaged) were built in Step 1.2; `AuthenticationFailedException` in Step 1.6 (pulled forward from 1.7). |
| If you can't recover, don't catch | Core never catches `Exception`. It catches only to clean up and rethrow with `throw;` (keeps the stack trace). |
| Clean up with `using` / `finally`; restore state | Streams in `using`; key buffers wiped in `finally`; temp file deleted on any failure → **no side effects after an exception** (ADR-07). |
| Document every exception | `<exception cref="…">` on every public member (§1.4). |
| Async: validate arguments before the async part; cancellation = `OperationCanceledException` | Guard clauses run synchronously; `CancellationToken.ThrowIfCancellationRequested()` in loops. |
| Never put secrets in messages | Messages never contain passwords, keys or file contents. |

**Layer 2 — Presentation (handles expected errors at the user action)**

Each command (Browse, Encrypt, Decrypt) has one `try/catch` with **specific** catches, most specific first:

| Exception | User sees (example) |
|---|---|
| `OperationCanceledException` | "Cancelled. No file was written." |
| `AuthenticationFailedException` | "Wrong password, or the file was changed or damaged." |
| `InvalidEncryptedFileException`, `Reason = NotEncryptedFile` | "This isn't a file encrypted by SecureFile. Choose a file that ends in .enc." |
| `InvalidEncryptedFileException`, `Reason = UnsupportedVersion` | "This file was made by a newer version of SecureFile. Update the app to open it." |
| `InvalidEncryptedFileException`, `Reason = Damaged` (also: valid tag but invalid padding, wrapped in Core — Step 1.7) | "This encrypted file is damaged or incomplete, so it can't be decrypted." |
| `NotSupportedException` (the input is a device or pipe, e.g. `CON`) | "This item can't be opened as a file. Choose a regular file." |
| `FileNotFoundException` / `DirectoryNotFoundException` | "The file or folder can't be found or opened." |
| `UnauthorizedAccessException` | "Access to this file or folder was denied. Choose another folder or check its permissions." |
| `PathTooLongException` | "The path is too long. Choose a shorter folder or name." |
| `IOException` — the output file already exists (the ViewModel asks *before* calling, so this is only the safety net) | "A file with this name already exists. Choose another name or confirm replacing it." |
| `IOException` — disk full (Windows HResult `0x80070070`, `ENOSPC` on Linux/macOS) | "There is not enough free space to save the file. Free up space or choose another drive." |
| `IOException` — file in use (sharing violation `0x80070020`) | "The file is being used by another program. Close it and try again." |
| `IOException` (any other, incl. `EndOfStreamException`: the file shrank while it was read) | "The file couldn't be read or written. Check that the drive is still connected and try again." |
| `ArgumentException` (programming error; the only user-reachable case, a password with a lone surrogate, is refused at the call — Step 1.7) | Not caught here → Layer 3. Step 1.8 adds a ViewModel password check. |

The full failure map (test catalog → exception → message → test) is in [adr/0013-exception-handling.md](adr/0013-exception-handling.md#failure-map-step-17).

Messages are **fixed, plain text** — never the raw .NET message (it is technical, English-only, and can contain full
paths with the user name). The technical details go to the log. Messages say *"access was denied"*, not *"Windows…"*,
because the same ViewModels run on macOS and Linux (bonus). When a file name is shown, it is the name only, never the
full path. The mapping lives in one small, unit-tested class (`ErrorMessageProvider`), so every message is consistent
and testable. The ViewModel does **not** catch `Exception` — anything unexpected goes to Layer 3.

**Layer 3 — WPF app (global safety nets, registered first thing in `App.OnStartup`)**

| Event | Catches | What we do |
|---|---|---|
| `Application.DispatcherUnhandledException` | Unhandled exceptions on the **UI thread** | Log details; show a friendly dialog; `e.Handled = true` → the app keeps running. |
| `AppDomain.CurrentDomain.UnhandledException` | Unhandled exceptions on **any other thread** | Log only — Microsoft: it *cannot* prevent the app from closing. |
| `TaskScheduler.UnobservedTaskException` | Faulted tasks nobody awaited | Log and `SetObserved()`. (In .NET Core it no longer crashes the app, but it would hide a bug.) |

**Is it honest to keep running after an unexpected error? (Team Lead question)** Yes, within limits, because every
file operation is atomic (temp file + move, ADR-07): an interrupted operation leaves no half-written result, so the
app's *data* is consistent. After an unexpected UI-thread error the app: logs it, resets the ViewModel to the idle
state (no operation in progress, inputs enabled), and shows *"Something unexpected went wrong. Your files were not
changed. Details were saved to the log."* If a second unexpected error happens within a short time, or the error is
one that signals a broken process (e.g. `OutOfMemoryException`, `InvalidProgramException`), the dialog offers only
**Close** — continuing would not be safe. So the promise is "never crash *on a user action*", not "never close".

**WinForms equivalent (for comparison in the presentation):** `Application.ThreadException` +
`Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)` (set before `Application.Run`)
plays the role of WPF's `DispatcherUnhandledException`; `AppDomain.UnhandledException` is the same in both.

**Logging:** a small local log file (`%LOCALAPPDATA%\SecureFile\logs`) with time, exception type, message and stack
trace — **never** passwords, keys or file contents. No network (ADR-12).

**Rules for our own code:** no `async void` except UI event handlers; no empty `catch { }`; no `throw ex;`
(use `throw;`); no exception used for normal flow (e.g. "is this our file?" is a `TryRead…` check, not an exception).

---

## 5. Crypto design: the encrypted file format and the two flows

### 5.1 File format version 1 (`.enc`)

All numbers are **little-endian** (written with `BinaryPrimitives`, so the file is identical on every OS).

| Offset | Size (bytes) | Field | Meaning |
|---:|---:|---|---|
| 0 | 4 | `Magic` | ASCII `SFEN` — "this is our file" |
| 4 | 1 | `FormatVersion` | `1` = PBKDF2-SHA256 → HKDF → AES-256-CBC/PKCS7 + HMAC-SHA256 |
| 5 | 4 | `Pbkdf2Iterations` | 600,000 when writing (OWASP Password Storage Cheat Sheet: *"PBKDF2-HMAC-SHA256: 600,000 iterations"*, verified 2026-11-24). On read it must be between **100,000** and **10,000,000**: lower values are refused because this app never writes them, so they mean damage or a foreign writer (defence in depth: a tampered value would also fail the HMAC check, so this is a sanity check, not the integrity control), higher values are refused so a crafted file cannot freeze the app for minutes (denial of service). |
| 9 | 16 | `Salt` | random per file; makes the same password give a different key per file |
| 25 | 16 | `Iv` | random per file; the CBC starting block |
| 41 | n | `Ciphertext` | AES-256-CBC output; n is a multiple of 16 and at least 16 |
| 41 + n | 32 | `AuthenticationTag` | HMAC-SHA256 over **bytes 0 … 41+n−1** (header + ciphertext) |

- Header = 41 bytes. Smallest valid file = 41 + 16 + 32 = **89 bytes** (that is an encrypted empty file).
- Encrypted size = `41 + (⌊size / 16⌋ + 1) × 16 + 32`.
- Salt and IV are **not secret**; they only need to be random and unique. The password and keys are secret.
- Original file name is **not** stored in v1. On decrypt, the suggested name is the encrypted name without `.enc`
  (`report.pdf.enc` → `report.pdf`). Storing it encrypted inside the file is a possible v2 feature.

### 5.2 Key derivation

```
password ──NFC normalize──► UTF-8 bytes
        └─► PBKDF2-HMAC-SHA256(salt, 600,000 iterations) ──► masterKey (32 bytes)
                 ├─► HKDF-Expand(label "SecureFile v1 encryption")     ──► encryptionKey (32 bytes = AES-256)
                 └─► HKDF-Expand(label "SecureFile v1 authentication") ──► authenticationKey (32 bytes)
```

- **NFC normalization:** the same visible password can be stored as different bytes (macOS often uses NFD).
  Normalizing makes the password produce the same bytes on every OS.
- All key buffers are wiped with `CryptographicOperations.ZeroMemory` in `finally`.
- **Where does the password live in memory? (Team Lead question)** WPF's `PasswordBox.Password` returns a .NET `string`,
  which is immutable and cannot be wiped; the garbage collector frees it at an unknown time. `SecureString` is **not**
  used: Microsoft no longer recommends it for new code (it does not protect the value on non-Windows systems and the
  text still has to become a normal string or bytes to be used). What we do instead: read the password only when the
  user clicks Encrypt/Decrypt; convert it once to a UTF-8 byte array; wipe that array with `ZeroMemory` right after key
  derivation; clear the password box after the operation; never log, copy or store it. Honest limit: a process memory
  dump taken during the operation could contain the password — protecting against an attacker who can already read
  this process's memory is outside a desktop app's control.

### 5.3 Encrypt flow

1. Validate the arguments **synchronously**, before any work (ADR-13): paths not empty, output names a file and is not
   the input path, password not empty, output does not exist unless the user confirmed overwriting.
2. Open the input (`FileShare.Read`: nobody may change it meanwhile) and generate `salt` and `iv` with
   `RandomNumberGenerator`.
3. Open a **temp file** `.~<name>.<8 random hex>.securefile-tmp` in the destination folder (`FileMode.CreateNew`) —
   before the slow step, so a missing or read-only folder fails at once.
4. Derive `encryptionKey` and `authenticationKey` (§5.2) inside `Task.Run` (≈0.5 s of CPU work, off the UI thread).
5. Write the header; add the header bytes to the HMAC.
6. Loop (our own, ADR-06; one 2 MiB pooled buffer): check cancel → read up to 1 MiB → `encryptor.TransformBlock` on
   the whole 16-byte blocks → add the ciphertext to the HMAC → write → report progress. A short read is the end of
   the file: `TransformFinalBlock` encrypts the last 0–15 bytes plus padding. **No `CryptoStream`.**
7. Write the 32-byte HMAC tag; a last cancel check; flush **to the disk** (`Flush(flushToDisk: true)`).
8. Atomic `File.Move(temp, output, overwrite: <user confirmed>)`.
9. On any error or cancel: delete the temp file (`finally`). The source file is never modified.

### 5.4 Decrypt flow

1. Read and **validate the header**: magic, version, iteration range, file length ≥ 89, ciphertext length multiple of 16.
2. Derive keys from the password and the stored salt/iterations.
3. Open a temp file in the destination folder.
4. Loop over exactly `n` ciphertext bytes (whole 16-byte blocks, 1 MiB pooled buffer): add chunk to the HMAC → `decryptor.TransformBlock` (it always holds back the last block) → write plaintext to the temp file → progress → cancel. **No decrypting `CryptoStream`:** disposing one after a failed check runs the final block and throws a padding error, so padding would be examined before the verdict (Review Board 1.4, R1 — verified).
5. Read the stored tag; compare with the computed tag using `CryptographicOperations.FixedTimeEquals` (constant time).
6. **Mismatch** → delete temp → "Wrong password, or the file was changed or damaged." **Match** → `TransformFinalBlock` (only now is the padding checked and removed) → flush → atomic move.

---

## 6. Solution structure

```
SecureFile/                           (git repository root)
├─ SecureFile.slnx                    all projects (Windows)
├─ SecureFile.CrossPlatform.slnf      projects that build on macOS/Linux (bonus)
├─ global.json                        pins the .NET 10 SDK
├─ Directory.Build.props              shared settings: nullable, analyzers, warnings as errors, XML docs
├─ Directory.Packages.props           one place for all NuGet versions
├─ .editorconfig                      code style rules
├─ README.md · CHANGELOG.md · LICENSE
├─ src/
│  ├─ SecureFile.Core/                net10.0 — crypto, file format, file details. No UI references.
│  ├─ SecureFile.Presentation/        net10.0 — ViewModels + service interfaces. No WPF/Avalonia references.
│  ├─ SecureFile.Wpf/                 net10.0-windows — the required Windows app
│  ├─ SecureFile.Avalonia/            net10.0 — bonus: Windows, macOS, Linux
│  └─ SecureFile.Cli/                 net10.0 — bonus: command line, proves reuse
├─ tests/
│  ├─ SecureFile.Core.Tests/
│  ├─ SecureFile.Presentation.Tests/
│  ├─ SecureFile.Integration.Tests/
│  ├─ SecureFile.Architecture.Tests/
│  └─ SecureFile.Wpf.UiTests/         (FlaUI)
├─ benchmarks/SecureFile.Benchmarks/
├─ docs/  PROJECT_PLAN.md · LEARNING_LOG.md · FORMAT.md · TESTPLAN.md · UX_SPEC.md
│         QUALITY_REPORT.md · SUGGESTIONS.md · REFERENCES.md · FINAL_GUIDE.md · adr/
└─ .github/workflows/ci.yml           build + test on Windows (and Ubuntu/macOS for the bonus)
```

**Dependency rule (checked by architecture tests):**
`Wpf / Avalonia / Cli → Presentation → Core`. Core depends on nothing of ours. Nothing depends on a UI project.

---

## 7. Step-by-step roadmap (Stages 0–9)

Each step: **Goal · Why · Done when · Checkpoint questions** (you answer before ✅).
**Honest re-baseline (Review Board Step 1.1, PM finding):** the stage estimates below add up to about **5 days of
coding** for the required track, and the learning loop, Review Board and full quality gate add roughly as much again.
The task's "3 days" is the assessor's estimate for the coding alone. You chose quality and understanding over speed
("we are not in a rush — production-ready"), so the realistic plan is **about 8–10 working days for the required
track**, with the bonus (Stage 7) after that. To keep the required app safe whatever happens:

- the automated gate runs on every commit; the full Review Board runs per step but in a lighter form for very small steps;
- a working **end-to-end vertical slice** (Core crypto → minimal ViewModel → plain window that encrypts and decrypts)
  is the first milestone after Step 1.7, before any UI polish (**decided 2026-11-24: slice first**). The slice is
  Steps 1.2–1.7 → 2.1–2.3 and 2.6 in their simplest form → 3.1 with a plain window (pick file, password, Encrypt,
  Decrypt, message). Only when it encrypts and decrypts a real file end to end do we continue with Step 1.8, 2.4–2.5
  and the Stage 3 UI polish. *Why:* the riskiest parts (crypto, file I/O, error handling, wiring) are proven together
  early, and a working app exists at every point after that;
- items marked *Could* (§13) are time-boxed and are the first to be cut.

### Stage 0 — Setup (Phase 1 tooling) · ~½ day

| Step | Goal | Why | Done when | Status |
|---|---|---|---|---|
| 0.1 | Install the **.NET 10 SDK** (LTS) and add `global.json`. | This machine has SDK 9 and 11 only. Pinning the SDK makes every build identical (your PC, CI, reviewer). | `dotnet --version` shows 10.0.x inside the repo. | 🟨 done — checkpoint pending |
| 0.2 | `git init`, `.gitignore`, first commit. | History shows your process; safe rollback. | First commit exists. | 🟨 done — checkpoint pending |
| 0.3 | Decide what to do with the old `EncryptionHandling` WinForms template (empty). | We replace it with WPF (ADR-01). **Ask before deleting.** | Archived or removed with your OK. | 🟨 done — old template deleted with your OK — checkpoint pending |
| 0.4 | Create the solution and projects (§6). | Layers keep logic reusable and testable (E4). | `dotnet build` succeeds. | 🟨 done — checkpoint pending |
| 0.5 | `Directory.Build.props`: `Nullable`, `AnalysisLevel=latest-all`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`. `Directory.Packages.props` for versions. | Quality rules apply to every project automatically; security analyzers (CA53xx) block weak crypto. | Build is clean with rules on. | 🟨 done — checkpoint pending |
| 0.6 | `.editorconfig` + `dotnet format --verify-no-changes`. | One consistent style; checked automatically. | Command passes. | 🟨 done — checkpoint pending |
| 0.7 | CI workflow (GitHub Actions) — **ask** whether to create a GitHub repo (public or private). | Every push is built and tested automatically. | CI green. | 🟨 done — private repo Abdelrahman-2222/SecureFile, first CI run green on Windows/Ubuntu/macOS — checkpoint pending |
| 0.8 | Move this plan to `docs/`; create `LEARNING_LOG.md` and `adr/`. | One home for all documents. | Files exist. | 🟨 done — checkpoint pending |

**Checkpoint 0:** What does `global.json` do? What is the difference between `net10.0` and `net10.0-windows`?
Why does Core not reference WPF? What does `TreatWarningsAsErrors` buy us?

### Stage 1 — Core library (R2, R3, R4, D1–D3, D7) · ~1 day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 1.1 | `FileDetails` (name, size in bytes, extension, last modified) + `FileSizeFormatter` (`1.5 MB`). | Unit tests for 0 B, 1023 B, 1 KB, >4 GB, no extension, `.tar.gz`, `.bashrc`. | 🟨 done — quality gate passed (G1–G15, mutation 100 %) — self-check pending |
| 1.2 | `EncryptedFileFormat` constants + `EncryptedFileHeader` (write/read/validate). | Tests: round-trip, wrong magic, unknown version, iterations out of range, file too short. | 🟨 done — quality gate passed (Review Board: 0 High, 2 Medium fixed), mutation 100 % — self-check pending |
| 1.3 | `KeyDerivation`: PBKDF2 → HKDF (§5.2), NFC normalization, key wiping. | Known-answer tests: RFC 7914 §11 (PBKDF2-HMAC-SHA256), RFC 5869 test case 1 (HKDF-SHA256). | 🟨 done — quality gate passed (Review Board: 0 High, 3 Medium fixed), mutation 100 % — self-check pending |
| 1.4 | AES-CBC check against NIST: prove we call `Aes` correctly. | NIST SP 800-38A F.2.5/F.2.6 (CBC-AES256) vectors pass (with `PaddingMode.None` for the vector). | 🟨 done — through the production factory (`AesCbc.CreateEncryptor/CreateDecryptor`), quality gate passed, mutation 100 % — self-check pending |
| 1.5 | `FileEncryptionService.EncryptAsync` (§5.3): streaming, HMAC, progress, cancel, temp + move. | Round-trip tests at 0, 1, 15, 16, 17 bytes, 1 MiB ± 1; same input → different output each time. Keys: `using FileKeys keys = await Task.Run(() => KeyDerivation.DeriveKeys(password, header), ct);` (off the UI thread; pass the `string`, a span cannot be captured; Review Board 1.3, R9). | 🟨 done — golden file (Python + OpenSSL), chunked = one-call reference, short-read stream; `AtomicFileWriter` + `PercentageProgress` for 1.6; quality gate passed (Review Board: 1 High fixed, all Medium fixed or decided), mutation 100 % — self-check pending |
| 1.6 | `FileEncryptionService.DecryptAsync` (§5.4): verify-then-commit. | Wrong password and every tamper/truncation case fail **and leave no output file**. Header read with `ReadAtLeast(buffer, 41, throwOnEndOfStream: false)` and only the bytes read passed to `EncryptedFileHeader.Read`, **before** `GetCiphertextLength` (Review Board 1.2, R1); tests: a 3-byte text file → `NotEncryptedFile`, an 80-byte file starting with `SFEN` → `Damaged`. Decrypt with our own `TransformBlock` loop and call `TransformFinalBlock` only after `FixedTimeEquals` succeeds (§5.4, Review Board 1.4, R1); test that chunked and one-call results are equal (R5). | 🟨 done — round trip at all boundaries, every tamper/truncation/wrong-password/cancel case leaves nothing; hidden decrypt temp file on Windows; `AuthenticationFailedException` pulled forward from 1.7 — self-check pending |
| 1.7 | Error model (ADR-13 Layer 1): `AuthenticationFailedException` with the three standard constructors (`SecureFileException` and `InvalidEncryptedFileException` were pulled forward into Step 1.2). | Every failure in §9 maps to one exception type; tests prove no partial output. | ✅ done — failure map in ADR-13; bad padding after a valid tag wrapped as `InvalidEncryptedFileException(Damaged)`; lone-surrogate password refused at the call; T-22 tests added |
| 1.8 | `PasswordStrengthEvaluator` (simple rules: length + character variety) and `PasswordGenerator` (`RandomNumberGenerator`). | Tests for empty, short, long, Unicode passwords. A maximum length (e.g. 1,024 characters, also `PasswordBox.MaxLength`) so a pasted 100 MB text cannot waste memory (Review Board 1.3, R5). | 🟡 `PasswordStrengthEvaluator` done 2026-11-24 (min 8 characters enforced when encrypting, advisory strength bar, length cap 1,024 via `MaxLength`; no composition rules, see NIST 800-63B); `PasswordGenerator` not built (a "could" item) |

**Checkpoint 1:** What is an IV and why must it be random for CBC? Why a salt? Why 600,000 iterations,
and what does that cost the user? What is PKCS7 padding? Why encrypt-then-MAC and not MAC-then-encrypt?
Why `FixedTimeEquals` instead of `==`? Why HKDF instead of 64 bytes from PBKDF2? Why a temp file in the
*destination* folder? Why does the HMAC also cover the header?

### Stage 2 — Presentation (ViewModels) · ~½ day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 2.1 | Service interfaces: `IFileDialogService`, `IMessageService`, `IShellService` (open folder), `IClipboardService`. | Interfaces documented. | 🟨 slice form — `IFileDialogService` + `IErrorLog` (ADR-13 log); `IShellService` and `IClipboardService` come with the steps that use them (3.5, 1.8); no `IMessageService`: messages are shown in the window |
| 2.2 | `MainViewModel` with clear states: *No file → File selected → Working → Succeeded / Failed*. | State diagram in `LEARNING_LOG.md`. | 🟨 slice form — `MainViewState` enum (diagram in its XML docs and the slice section of LEARNING_LOG); Browse shows name, extension and size |
| 2.3 | Commands: Browse, Encrypt, Decrypt, Cancel, Generate password, Open folder; `CanExecute` rules. | Buttons enable/disable correctly in tests. | 🟨 slice form — Browse, Encrypt, Decrypt with CanExecute (file + password + not busy); Save dialog suggests `.enc` added/removed; Cancel, Generate password, Open folder come in 3.5/1.8 |
| 2.4 | Auto-detect: if the file starts with `SFEN`, suggest Decrypt; otherwise Encrypt. A small Core method takes the path and returns `true`/`false` (built on `EncryptedFileFormat.HasSignature`), so the ViewModel never reads file bytes itself (Review Board 1.2, R8). | Tests for both cases. | ✅ done 2026-11-24 (Step 4.3 finding): `FileDetailsReader` reads the first 4 bytes and sets `FileDetails.IsEncrypted` (via `HasSignature`), so the ViewModel never reads bytes; the window offers Encrypt or Decrypt by content, not name. Core tests for both cases + a renamed file + a locked file; ViewModel tests for the buttons |
| 2.5 | ViewModel tests with fake services (no window needed). | Presentation coverage ≥ 85%. | ⬜ |
| 2.6 | `ErrorMessageProvider` + per-command `try/catch` with specific catches (ADR-13 Layer 2). | Every exception in the ADR-13 table gives its message; unexpected exceptions are **not** caught here. | ✅ |

**Checkpoint 2:** What is MVVM and what lives in each part? What is data binding? What does
`[ObservableProperty]` generate? Why can we test a ViewModel without opening a window?

### Stage 3 — WPF app (Phase 4) · ~1 day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 3.1 | `App.xaml` + pure-DI composition in `OnStartup` (ADR-09) + **global exception safety nets and local log** (ADR-13 Layer 3). | App starts; a forced test exception shows the friendly dialog, is logged, and the app keeps running. | ✅ |
| 3.2 | Main window layout per §11 (drop zone, file card, password, output, actions, progress, status). | Matches the UX spec screenshot. | ✅ |
| 3.3 | Fluent theme, light/dark following Windows, consistent spacing and sizes. | Looks right at 100%, 150%, 200% scaling. | ✅ |
| 3.4 | Drag-and-drop, native Open/Save dialogs, keyboard shortcuts, `AutomationProperties.Name` on all controls. | Full flow possible with keyboard only. | ✅ |
| 3.5 | Progress (%, MB/s, time left), Cancel, success panel (Open folder), friendly errors. | Manual flow test passes. | ✅ |
| 3.6 | App icon, title, About (version), minimum window size. | Done. | ✅ |
| 3.7 | FlaUI smoke test + Accessibility Insights FastPass. | 0 accessibility failures. | ✅ FlaUI tests (4) pass; FastPass automated checks: 0 failures. Tab stops, Narrator, dark mode and scaling not verified |

**Checkpoint 3:** What is XAML? How does a button reach the ViewModel's command? How does the UI stay
responsive during a 4 GB encryption? Why do sizes/spacing follow a 4/8 px grid?

### Stage 4 — Integration, manual and user testing (Phase 2) · ~½ day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 4.1 | Integration tests on the real file system (§9.2). | All pass. | ✅ |
| 4.2 | Large-file runs: 1 GB and >4 GB (manual or nightly, not every commit). | Round-trip OK, memory stays flat. | 🟡 1 GiB done (46 MB peak); 4.5 GiB blocked: Kaspersky kills the test exe (2026-11-24), rerun elsewhere |
| 4.3 | `TESTPLAN.md` manual script + exploratory session. | Every item ticked, bugs fixed. | 🟡 script written (docs/TESTPLAN.md); first hand run found and fixed 5 issues (no clear result, file and password not cleared, empty "Save as type", no Show password, misleading warning and buttons); full run (sections A–I) still to do |
| 4.4 | Short usability check with 2–3 non-technical people (task success, time to first encryption; SUS questionnaire optional). | All complete the task unaided; findings fixed or recorded. | 🟡 protocol written (docs/USABILITY.md); needs 3–5 real people |

### Stage 5 — Measure and harden (Phase 1 report) · ~½ day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 5.1 | BenchmarkDotNet: throughput by file size, KDF time (p50/p95), allocations. | Numbers in `QUALITY_REPORT.md`. | ⬜ |
| 5.2 | Visual Studio Performance Profiler (Alt+F2): startup, CPU, memory, UI thread. | Targets in §8 met or explained. | ⬜ |
| 5.3 | VS Code Metrics, VS Code Coverage, Stryker.NET mutation score. | Targets in §8 met. | ⬜ |
| 5.4 | Security: analyzers, NuGet audit, SonarQube for IDE, CodeQL (if GitHub), gitleaks, threat model (STRIDE). | 0 open high issues; threat table written. | ⬜ |
| 5.5 | Write `QUALITY_REPORT.md` (numbers + screenshots). | Done. | ⬜ |

**Checkpoint 5:** What is p95 and why report it instead of only the average? Why does memory stay flat?
What does a mutation score tell us that coverage does not?

### Stage 6 — Package and test on a clean machine (Phase 3) · ~½ day

| Step | Goal | Done when | Status |
|---|---|---|---|
| 6.1 | Publish profiles: `win-x64`, `win-arm64`, self-contained, single-file, ReadyToRun. | One `.exe` per architecture. | 🟡 `win-x64` self-contained single-file beta zip via `scripts/publish-beta.ps1` (2026-11-24); `win-arm64`, installer and signing not done |
| 6.2 | Test in **Windows Sandbox** (clean Windows, nothing installed) with a `.wsb` config. | Full flow works on first run. | ⬜ |
| 6.3 | Decide on installer (Velopack/MSIX) and code signing (Azure Artifact Signing). | Decision recorded (ADR). | ⬜ |
| 6.4 | `README.md` "Try it in 60 seconds" with screenshots/GIF; SHA-256 checksums; release zip. | A non-technical person can follow it. | 🟡 `docs/BETA_README.md` for friends done; the project README "Try it in 60 seconds" and screenshots still open |

### Stage 7 — Bonus: cross-platform (Phase 5, presented in Phase 6) · ~1 day after the required track

| Step | Goal | Done when | Status |
|---|---|---|---|
| 7.1 | Avalonia app reusing the same `MainViewModel`. | Same features as WPF. | ⬜ |
| 7.2 | Avalonia service implementations (StorageProvider dialogs, launcher, clipboard). | Works on Windows. | ⬜ |
| 7.3 | CI matrix on Windows, Ubuntu, macOS; cross-OS "golden file" tests (files encrypted on one OS decrypt on all). | All green. | ⬜ |
| 7.4 | Publish `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`; test Linux in WSL2/WSLg; macOS on a Mac or VM. | Runs on each. | ⬜ |
| 7.5 | Size comparison: WPF (no trimming) vs Avalonia trimmed/AOT. | Numbers in the report. | ⬜ |
| 7.6 | Optional `SecureFile.Cli`. | `securefile encrypt <in> <out>` works. | ⬜ |

### Stage 8 — Stakeholder suggestions (Phase 6) · ~2 hours

| Step | Goal | Status |
|---|---|---|
| 8.1 | Write `SUGGESTIONS.md` (§13): what, why, benefit, priority, effort — marking which are already built. | ⬜ |

### Stage 9 — Final deliverables · ~½ day (only after everything is tested and confirmed)

| Step | Goal | Status |
|---|---|---|
| 9.1 | Final review against the Definition of Done (below). | ⬜ |
| 9.2 | `docs/FINAL_GUIDE.md` — the detailed explanation document (§14.1). | ⬜ |
| 9.3 | Team Lead presentation (`.pptx`) (§14.2) + Q&A preparation. | ⬜ |
| 9.4 | 2-minute demo video as a backup for the live demo. | ⬜ |

### Definition of Done (whole project)

- [ ] R1–R6 and D1–D7 each have at least one passing test (§15).
- [ ] 0 analyzer warnings, 0 known vulnerable packages.
- [ ] Phase 1 targets in §8 met (or the gap is explained honestly).
- [ ] Clean-machine test passed (Windows Sandbox).
- [ ] You have passed every checkpoint and can explain every file.
- [ ] Every public member has XML docs; `CHANGELOG.md` is up to date.
- [ ] Every commit follows §1.5 (Conventional Commits, no AI attribution).
- [ ] Final guide and presentation reviewed by you.

---

## 8. Phase 1 — Measurements and targets

Targets are measured on one reference machine (recorded in the report: CPU, RAM, disk type).

### 8.1 Security

| Tool | What it measures | Target |
|---|---|---|
| .NET analyzers (built into the SDK; CA5350, CA5351, CA5379, CA5387, CA5388, CA5390, CA5401, CA5402 …) | Weak/broken algorithms, weak KDF, hard-coded keys, IV misuse | 0 warnings (build fails otherwise) |
| NuGet Audit (runs on restore) / `dotnet list package --vulnerable --include-transitive` | Known vulnerabilities in packages | 0 |
| SonarQube for IDE (VS extension) | Vulnerabilities, security hotspots | 0 vulnerabilities; all hotspots reviewed |
| CodeQL (GitHub) | Static security analysis | Only if the repository becomes public (free there; on a private repo it needs paid GitHub Advanced Security). Meanwhile: .NET security analyzers (G3) + SonarQube for IDE. |
| gitleaks (CI job, full history) + local pattern scan (verify.ps1) | Secrets committed by mistake | 0 |
| Known-answer tests (NIST SP 800-38A, RFC 5869, RFC 7914) | We use the algorithms correctly | 100% pass |
| Tamper/truncation tests (§9) | Integrity protection works | 100% detected |
| Threat model (Microsoft Threat Modeling Tool, STRIDE) | Threats considered and mitigated | Every threat has a mitigation or a stated limit |

### 8.2 Performance (desktop equivalents of web metrics like TTFB/p95)

| Metric | Tool | Target |
|---|---|---|
| Cold start → window visible ("time to first window", the desktop TTFB) | `Stopwatch` log + VS Performance Profiler / WPR-WPA | < 1.5 s cold, < 0.5 s warm |
| Throughput (MB/s) for 1 MB, 100 MB, 1 GB | BenchmarkDotNet | ≥ 70% of a plain file copy on the same disk |
| Key-derivation time (p50, p95) | BenchmarkDotNet | Reported; ~0.3–1 s is the intended security cost |
| Peak memory vs file size | VS Memory Usage, `dotnet-counters` | Flat (e.g. < 150 MB) for 1 MB and 4 GB alike |
| Allocations per operation | BenchmarkDotNet `MemoryDiagnoser`, VS .NET Object Allocation | No per-chunk **buffer** allocations (one pooled buffer reused); small async bookkeeping per chunk is measured in Stage 5 |
| UI responsiveness | VS profiler / WPA | UI thread never blocked > 50 ms; progress updates ≤ 10 per second |
| Published size | File size after publish | Reported per architecture (WPF cannot be trimmed; Avalonia can — bonus comparison) |

### 8.3 Modularity

| Tool | Target |
|---|---|
| CA1502/CA1505 analyzers (build-enforced) + VS *Analyze → Calculate Code Metrics* for the report | Maintainability Index ≥ 60 per type and method (Microsoft: 20–100 = good; first target of 70 proved too strict when applied to every small method — see LEARNING_LOG Step 1.1 gate); cyclomatic complexity ≤ 10 per method; inheritance depth ≤ 3 |
| Architecture tests (NetArchTest) | Core has no UI references; Presentation has no WPF/Avalonia references |
| Reuse demonstration | Core used by WPF, Avalonia, CLI and tests; `dotnet pack` builds a NuGet package |

### 8.4 Code quality

| Tool | Target |
|---|---|
| `dotnet format --verify-no-changes` | Passes |
| XML docs on public API (CS1591 as error) | 100% |
| VS Code Coverage (now in Community/Professional in VS 2026) or Coverlet | Core ≥ 90% lines, ≥ 85% branches |
| Stryker.NET mutation testing | ≥ 80% on Core |
| SonarQube | Maintainability A, duplication < 3% |

---

## 9. Phase 2 — Test catalog

IDs are used in the traceability table (§15). "Impossible" cases are things a normal user would never do —
we test them anyway.

### 9.1 Unit tests (Core and Presentation)

| ID | Area | Cases |
|---|---|---|
| T-01 | File details | 0 B, 1023 B, 1 KB, 1.5 GB, > 4 GB; no extension; `.tar.gz` → `.gz`; `.bashrc`; Unicode/Arabic names; spaces; long paths |
| T-02 | Round-trip sizes | 0, 1, 15, 16, 17, 31, 32, 33 bytes; buffer size −1/0/+1; random sizes (property-based) |
| T-03 | Randomness | Same file + same password encrypted twice → different outputs (salt/IV differ) |
| T-04 | Known answers | NIST SP 800-38A CBC-AES256; RFC 7914 PBKDF2-SHA256; RFC 5869 HKDF |
| T-05 | Wrong password | Fails with "wrong password or damaged file"; **no output file** |
| T-06 | Tampering | Flip one bit in: magic, version, iterations, salt, IV, first/middle/last ciphertext block, tag → all detected |
| T-07 | Truncation/extension | File cut inside each field; shorter than 89 bytes; extra bytes appended → all rejected |
| T-08 | Not our file | Random file, plain text, empty file, future version `2`, iterations `0` or huge → clear message |
| T-09 | Passwords | Empty, spaces only, 1 char, 10,000 chars, Arabic, emoji, NFC vs NFD (same password typed differently) → same key |
| T-10 | Cancel | Cancel at start/middle/end → temp deleted, source untouched, no output |
| T-11 | Progress | Starts at 0, ends at 100, never goes backwards, not too frequent |
| T-12 | ViewModel | Button enable/disable per state; auto-detect encrypt/decrypt; errors shown as messages |

### 9.2 Integration tests (real files, temp folders)

| ID | Case |
|---|---|
| T-20 | Output path = input path → refused |
| T-21 | Output exists → asks; on yes, replaced atomically |
| T-22 | Read-only folder / no permission → clear error, no partial file |
| T-23 | Disk full (simulated) → clear error, no partial file |
| T-24 | Source file locked by another program / deleted after selection / changed during encryption |
| T-25 | USB/network location removed mid-operation (manual) |
| T-26 | Encrypt an already encrypted file (allowed, decrypt twice to get back) |
| T-27 | Very large file (> 4 GB) round-trip with flat memory (manual/nightly) |
| T-28 | Golden files: committed encrypted samples always decrypt (protects the format from accidental changes) |
| T-29 | Bonus: files encrypted on Windows decrypt on Linux/macOS and vice versa |

### 9.3 UI, accessibility, manual, usability

| ID | Case |
|---|---|
| T-40 | FlaUI smoke test: select → encrypt → decrypt → compare |
| T-41 | Keyboard-only full flow; screen reader (Narrator) reads every control |
| T-42 | Accessibility Insights FastPass: 0 failures |
| T-43 | 100% / 150% / 200% display scaling; light and dark mode |
| T-44 | Manual script in `TESTPLAN.md` + 30-minute exploratory session |
| T-45 | Usability test with 3–5 non-technical people: success rate, time, SUS score |

---

## 10. Phase 3 — Delivery to the end user

| Item | Plan |
|---|---|
| Build | `dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true` |
| What the user gets | One `SecureFile.exe` (x64 and Arm64). Double-click. Nothing to install. |
| Clean-machine test | **Windows Sandbox** (built into Windows 11 Pro; enable "Windows Sandbox" in Windows Features, needs admin once). A `.wsb` file maps the release folder read-only. Every launch is a fresh Windows. |
| Other VMs | Hyper-V Quick Create (Windows/Ubuntu); WSL2 + WSLg for the Linux bonus build. |
| SmartScreen warning | Unsigned apps show "Windows protected your PC". Options: sign with **Azure Artifact Signing** (formerly Trusted Signing, ~$9.99/month), or document *More info → Run anyway*. |
| README | "Try it in 60 seconds": download → run → drop a file → set password → Encrypt. Screenshots + GIF. |
| Release | Zip + SHA-256 checksums + sample files folder + 2-minute demo video. |

---

## 11. Phase 4 — UI/UX specification

### 11.1 The flow (one window, top to bottom)

1. **Drop zone:** "Drop a file here or **Browse…**" (large target, also accepts keyboard).
2. **File card:** icon, **name**, **extension**, **size** (`1.5 MB`; tooltip with exact bytes like Explorer: `1.5 KB (1,536 bytes)`), last modified. "Change file" link.
   Reading the details runs off the UI thread (it can take seconds on an unavailable network drive).
3. **Mode:** Encrypt / Decrypt — preselected automatically from the file header; the user can switch. For a `.enc`
   file a hint says *"This is a SecureFile encrypted file — enter its password to decrypt it."* (a `.enc` file has no
   icon or double-click action, so users need this hint).
4. **Password:** field with show/hide; *Confirm password* (encrypt only); Caps Lock warning. Spaces are never trimmed;
   the wrong-password message adds "Check Caps Lock, the keyboard language and spaces at the start or end." (Review Board 1.3, R13).
   Warning text: **"If you forget this password, the file cannot be recovered. Nobody can reset it."**
   *Could (time-boxed):* strength meter and "Generate strong password". If generation is built, the app shows the
   password with *"Save this password somewhere safe now (for example in a password manager)"*, and any Copy button
   clears the clipboard after 30 seconds (clipboard history and cloud sync could otherwise keep it).
5. **Output:** suggested name and folder (same folder as the input; `name.ext.enc` or `.enc` removed), **Change…** opens the Save dialog (R5). Overwrite asks for confirmation.
6. **Primary button:** "Encrypt file" / "Decrypt file" — disabled until inputs are valid, with a hint why.
7. **Progress:** bar + % + MB/s + time left + **Cancel**. Inputs locked while working.
8. **Result:** success message with **Open folder**; or an error saying *what happened* and *what to do*.
   After encryption the message is explicit: **"Encrypted copy saved. Your original file was not changed."** —
   so nobody assumes the original is now protected.

### 11.2 Visual and interaction rules

| Rule | Value | Source |
|---|---|---|
| Design language | Fluent 2 (WPF Fluent theme), follows Windows light/dark | Microsoft Fluent 2 |
| Control height | 32 px standard | Fluent 2 |
| Click targets | ≥ 40 × 40 px for main actions (WCAG 2.2 minimum is 24 × 24) | Fluent / WCAG 2.2 SC 2.5.8 |
| Spacing | 4/8 px grid (8, 12, 16, 24) | Fluent 2 |
| Text | 14 px body, Segoe UI Variable; one font family | Fluent 2 |
| Contrast | ≥ 4.5:1 text, ≥ 3:1 UI parts | WCAG 2.2 AA |
| Window | Default ~800 × 560, minimum 640 × 480, resizes cleanly | — |
| Keyboard | Logical Tab order; Enter = primary action; Esc = cancel; Ctrl+O = browse | Windows conventions |
| Screen readers | Every control has `AutomationProperties.Name` | UI Automation |
| Scaling | Per-monitor DPI; tested at 100/150/200% | — |
| Messages | Plain words; no stack traces; details in a local log file | Nielsen heuristic #9 |
| Language | English first; Arabic with right-to-left layout as an optional extra (§13) | — |

### 11.3 How we check the UX

Heuristic review (Nielsen's 10 heuristics), accessibility tools (T-41–T-43), usability test (T-45, SUS ≥ 80).

---

## 12. Phase 5 — Cross-platform (bonus track)

| Topic | Decision |
|---|---|
| Crypto APIs | Only cross-platform abstractions: `Aes.Create()`, `HMACSHA256`/`IncrementalHash`, `Rfc2898DeriveBytes.Pbkdf2`, `HKDF`, `RandomNumberGenerator`. Supported on Windows (CNG), Linux (OpenSSL), macOS (Apple libraries; .NET 10 no longer uses OpenSSL on macOS). |
| Avoid | `AesCng`, `*CryptoServiceProvider`, `ProtectedData` (DPAPI), Windows certificate store — all Windows-only. |
| File format | Little-endian via `BinaryPrimitives`; no OS-specific bytes. |
| Passwords | Unicode NFC normalization (macOS vs Windows input difference). |
| Paths | `Path.Combine`; Linux paths are case-sensitive; different invalid file-name characters per OS. |
| Permissions | On Linux/macOS, decrypted files get owner-only permissions (`File.SetUnixFileMode`). |
| UI | Avalonia shell, native dialogs via `StorageProvider`, macOS conventions (Cmd key, button order). |
| Proof | CI on Windows/Ubuntu/macOS + golden-file cross-OS tests (T-29). |
| Distribution | macOS `.app` in `.dmg` (unsigned → *System Settings → Privacy & Security → Open Anyway*; signing needs an Apple Developer account, $99/year). Linux AppImage or `.tar.gz`. |

---

## 13. Phase 6 — Stakeholder suggestions

Priority uses **MoSCoW**: Must / Should / Could / Won't (this version).

### 13.1 Product Owner

| Suggestion | Why / benefit | Priority | In this build? |
|---|---|---|---|
| Password-based key (D1) | Without it, there is no safe key at all | Must | ✅ |
| Integrity check (D2) | Detects tampering and wrong passwords | Must | ✅ |
| Progress + Cancel (D4) | Large files feel safe and controllable | Must | ✅ |
| Drag-and-drop + auto-detect encrypt/decrypt | Fewer clicks, fewer mistakes | Should | ✅ |
| **Cross-platform (Windows, macOS, Linux)** | Same tool for every team member; proves modularity | Could (bonus) | ✅ Stage 7 |
| Command-line version | Automation for power users and IT | Could | ✅ Stage 7 (optional) |
| Right-click "Encrypt with SecureFile" in Explorer | Fits how people already work | Could | ⏸ v2 |
| Encrypt a whole folder (zip, then encrypt) | Common real need | Could | ⏸ v2 |
| Store the original file name inside the encrypted file | Better restore experience | Could | ⏸ v2 |
| Arabic UI with right-to-left layout | Local users | Could | ⏸ |
| Password strength meter + generator (with clipboard auto-clear) | Helps users pick strong passwords | Could (time-boxed) | ⏸ |
| Windows Arm64 build | Snapdragon laptops | Could | ⏸ |
| "Secure delete" the original | Users ask for it, but SSDs cannot guarantee it | Won't (explain why) | ❌ |

### 13.2 Business Analyst

| Suggestion | Why / benefit |
|---|---|
| Acceptance criteria (Given/When/Then) for R1–R6, D1–D7 | "Complete" becomes checkable |
| Numeric non-functional requirements (§8) | Performance/quality are proven, not claimed |
| Error catalog (every failure → one user message) | Consistent UX; easy to test |
| File format specification (`FORMAT.md`) | Other tools can interoperate; safe upgrades |
| Traceability matrix (§15) | Shows coverage of every requirement at a glance |
| Personas: non-technical office worker, IT admin | UI decisions grounded in real users |
| Privacy statement: no network, no telemetry; algorithms are FIPS-approved (not "certified") | Trust from enterprise users |

### 13.3 Project Manager

| Suggestion | Why / benefit |
|---|---|
| Ask the assessor early: is cross-platform welcome? is a password-based key OK? is HMAC on top of CBC OK? expected file sizes? | Removes the biggest risks on day 0 |
| Required track first, bonus second (this plan) | The required app is never at risk |
| Risk register (§16) | Known risks have a plan |
| Definition of Done (§7) | Clear finish line |
| ADRs in `docs/adr/` | Decisions are documented and defensible |
| Presentation built around the 6 grading criteria (§14.2) | Maps directly to how we are scored |
| Backup demo video | Live demo failure does not ruin the presentation |

---

## 14. Final deliverables: explanation guide and Team Lead presentation

Both are produced **only after** all stages are done, tested, and confirmed by you (Stage 9).

### 14.1 `docs/FINAL_GUIDE.md` — detailed explanation document

1. What the app does (one page, with screenshots)
2. How to run it (end user) and how to build it (developer)
3. Crypto concepts in plain words: AES, CBC, IV, padding, salt, PBKDF2, HKDF, HMAC, encrypt-then-MAC
4. The file format, byte by byte
5. Architecture: layers, dependency rule, why it is reusable
6. **Code walkthrough: every file, every class, every method, every important line**
7. **Identifier dictionary:** every name in the code and what it means
8. Every decision with its alternatives (the ADRs)
9. Tests: what each group proves
10. Measurements: the numbers and how they were taken
11. Known limits and future work (honest)
12. References

### 14.2 Team Lead presentation (`.pptx`) — plain language, on point

The audience is more experienced, so every slide states **a claim + the evidence**, and the speaker notes
hold the deeper answers.

| # | Slide | Content |
|---|---|---|
| 1 | Title | App name, your name, date |
| 2 | The task, done | R1–R6 checklist with ✅ and the proving test |
| 3 | Live demo flow | 3 screenshots: select → encrypt → decrypt (video as backup) |
| 4 | How the protection works | One simple diagram: password → key → AES-256-CBC → HMAC seal |
| 5 | Why these choices | CBC (required) + HMAC; PBKDF2 600k; HKDF; random salt/IV — each with "instead of …" |
| 6 | Safe by design | Wrong password/tamper detected; no partial files; no key stored anywhere |
| 7 | Architecture & reuse (E4) | Layer diagram; same Core in WPF, Avalonia, CLI, tests |
| 8 | Performance & size (E3) | MB/s, flat memory chart, start time, published size; what we optimized |
| 9 | Quality (E5) | Test count, coverage, mutation score, 0 analyzer warnings, CI |
| 10 | UI/UX (E6) | Before/after, design rules, accessibility, usability score |
| 11 | Bonus | Cross-platform + stakeholder suggestions |
| 12 | Honest limits & next steps | What it does not protect against; v2 ideas (AES-GCM, folder encryption) |
| 13 | References (E2) | Microsoft Learn, NIST, RFCs, OWASP |
| 14 | Questions | — |

**Q&A preparation (answers go in the speaker notes and the final guide):**
Why not AES-GCM? · Why is the IV not secret? · Why 600,000 iterations? · What happens if the user forgets the
password? · Why not store the key? · Why verify the HMAC before releasing the output? · Why HKDF? ·
Can the password stay in memory? · Why WPF over WinForms? · How does the UI not freeze? · How did you
measure performance? · Largest file tested? · How is the code reusable in another app? · What would you
change with more time?

---

## 15. Requirements traceability

| Requirement | Built in step | Proven by tests |
|---|---|---|
| R1 Select any file | 2.3, 3.4 | T-12, T-40, T-44 |
| R2 Show name/size/extension | 1.1, 3.2 | T-01, T-40 |
| R3 Encrypt AES-256-CBC | 1.3–1.5 | T-02, T-03, T-04 |
| R4 Decrypt to original | 1.6 | T-02, T-05–T-08, T-28 |
| R5 Choose output name/location | 2.3, 3.4 | T-20, T-21, T-40 |
| R6 C#, WPF, .NET crypto | 0.4, 3.x | Build + analyzers |
| D1 Password-based key | 1.3 | T-04, T-09 |
| D2 Integrity | 1.5, 1.6 | T-05, T-06, T-07 |
| D3 Streaming | 1.5, 1.6 | T-27, §8.2 memory |
| D4 Progress/cancel | 1.5, 3.5 | T-10, T-11 |
| D5 No partial output | 1.5, 1.6 | T-05, T-10, T-22, T-23 |
| D6 Clear errors | 1.7, 3.5 | T-08, T-12 |
| D7 Versioned format | 1.2 | T-08, T-28 |

---

## 16. Risks

| Risk | Impact | Plan |
|---|---|---|
| Assessor expects WinForms/WPF only | Bonus seen as off-topic | WPF is the main app; Avalonia only as a clearly labeled bonus |
| Learning loop takes longer than 3 days | Late delivery | Required track first; bonus time-boxed; checkpoints can overlap with coding |
| .NET 10 SDK missing on this machine | Build differences | Step 0.1 installs and pins it |
| SmartScreen/Gatekeeper warnings | Reviewer cannot open the app easily | README instructions; optional signing |
| macOS testing needs Apple hardware | Bonus not fully verified | CI macOS runner for automated tests; state the limit honestly |
| Live demo fails | Bad impression | Backup video + release `.exe` on a USB stick |
| Process overhead (lessons, Review Board, gate) exceeds the 3-day estimate | Late delivery if a deadline appears | Re-baselined to 8–10 days (§7); vertical slice first; *Could* items cut first; you confirmed quality over speed |
| Branch protection is not available for private repositories on the free GitHub plan | CI results cannot be made *required* before merging | Merge only on a green CI by rule; make the repo public (enables protection and CodeQL) or upgrade before sharing |
| Password forgotten by a user | Data loss | Clear warning in UI; documented as by-design |

---

## 17. Glossary (plain language)

| Term | Meaning |
|---|---|
| **AES** | The standard encryption algorithm. Same key locks and unlocks (symmetric). |
| **AES-256** | AES with a 32-byte (256-bit) key — the strongest AES key size. |
| **Block** | AES works on 16-byte pieces called blocks. |
| **CBC** | *Cipher Block Chaining*: each block is mixed with the previous encrypted block, so identical pieces of a file do not look identical when encrypted. |
| **IV** | *Initialization Vector*: a random 16-byte starting block for CBC. Not secret, must be unpredictable, new for every file. |
| **Padding (PKCS7)** | Extra bytes added so the data fills whole 16-byte blocks; removed on decrypt. |
| **Key** | The secret bytes AES uses. Here derived from the password, never stored. |
| **Salt** | Random bytes mixed with the password so the same password gives a different key in every file. Not secret. |
| **KDF** | *Key Derivation Function*: turns a password or secret into key bytes. |
| **PBKDF2** | A deliberately slow KDF for passwords; the slowness makes guessing expensive. |
| **Iterations** | How many times PBKDF2 repeats its work (600,000). More = slower guessing. |
| **HKDF** | A fast KDF that splits one strong key into several independent keys. |
| **HMAC** | A keyed "seal" over data. Changing even one bit breaks the seal. |
| **Tag** | The 32-byte HMAC result stored at the end of the file. |
| **Encrypt-then-MAC** | Encrypt first, then seal the encrypted bytes. The safe order. |
| **Constant-time comparison** | Comparing secrets in a way that always takes the same time, so timing leaks nothing. |
| **Atomic move** | The final file appears all at once, or not at all — never half-written. |
| **MVVM** | *Model–View–ViewModel*: View = XAML screen, ViewModel = screen logic and state, Model = Core logic. |
| **Data binding** | XAML controls automatically show and update ViewModel properties. |
| **Command** | A ViewModel action a button triggers (`EncryptCommand`). |
| **DI (dependency injection)** | Objects receive what they need through their constructor instead of creating it. |
| **Self-contained** | The `.exe` carries its own .NET runtime — nothing to install. |
| **Single-file** | Everything packed into one `.exe`. |
| **ReadyToRun** | Code pre-compiled ahead of time for faster start-up. |
| **Trimming / Native AOT** | Removing unused code / compiling fully to native code to make the app smaller and faster (Avalonia yes, WPF no). |
| **LTS** | *Long-Term Support*: .NET 10 is supported for 3 years. |
| **p95** | 95% of runs were at least this fast; shows the slow cases an average hides. |
| **Mutation testing** | A tool changes the code on purpose; good tests should fail. The % caught is the mutation score. |
| **SUS** | *System Usability Scale*: a 10-question survey giving a 0–100 usability score. |

---

## 18. References

**Provided**
- Cross-platform cryptography in .NET — https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography
- .NET cryptography model — https://learn.microsoft.com/en-us/dotnet/standard/security/cryptography-model
- Cryptographic services overview — https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-services
- System.Security.Cryptography (.NET 10) — https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography?view=net-10.0
- .NET 10 RC1 libraries release notes — https://github.com/dotnet/core/blob/main/release-notes/10.0/preview/rc1/libraries.md
- Create a C# WinForms app in Visual Studio — https://learn.microsoft.com/en-us/visualstudio/ide/create-csharp-winform-visual-studio?view=visualstudio
- *.NET 10 Cryptography: A Practical Beginner's Guide* (local file)

**Standards**
- NIST SP 800-38A — AES modes (CBC) and test vectors
- RFC 2104 — HMAC · RFC 4231 — HMAC-SHA256 test vectors
- RFC 8018 — PBKDF2 · RFC 7914 §11 — PBKDF2-HMAC-SHA256 test vectors
- RFC 5869 — HKDF and test vectors
- OWASP Password Storage Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- WCAG 2.2 · Microsoft Fluent 2 design system

**Tools**
- Visual Studio 2026 release notes — https://learn.microsoft.com/visualstudio/releases/2026/release-notes
- VS code quality tools (coverage editions) — https://learn.microsoft.com/en-us/visualstudio/test/improve-code-quality
- Trimming incompatibilities (WPF) — https://learn.microsoft.com/dotnet/core/deploying/trimming/incompatibilities
- Avalonia supported platforms — https://docs.avaloniaui.net/docs/supported-platforms
- Azure Artifact Signing — https://azure.microsoft.com/en-us/products/artifact-signing
- Velopack — https://www.nuget.org/packages/velopack

**Verified:** the OWASP iteration figure (600,000 for PBKDF2-HMAC-SHA256, read directly on 2026-11-24). **To verify when we reach the step:**
whether the WPF Fluent `ThemeMode` API is still marked experimental in .NET 10, and Velopack's Linux output formats.

---

## 19. Progress log

| Date | Stage/step | What happened |
|---|---|---|
| 2026-11-24 | Planning | Task analysed; six phases defined; UI decision: WPF (required) + Avalonia (bonus); crypto design agreed; this plan written. |
| 2026-11-24 | Environment check | SDKs installed: 9.0.308, 11.0.100 (no 10.0 SDK). Runtime 10.0.15 present. Existing `EncryptionHandling` = empty WinForms template. Not a git repo. |
| 2026-11-24 | Stage 0 | .NET 10 SDK 10.0.404 + GitHub CLI installed; solution, projects, shared build rules, packages, editorconfig, gitattributes, local tools, CI, docs created. Build: 0 warnings. Walkthrough in LEARNING_LOG.md. |
| 2026-11-24 | Step 1.1 | FileDetails, IFileDetailsReader, FileDetailsReader, FileSizeFormatter + 31 tests (incl. property-based), 100 % coverage. Style rules refined: interface members, static readonly naming. |
| 2026-11-24 | Step 1.1 revision | ADR-13 exception strategy; quality gate §1.7 with verify.ps1, Git hooks, metric limits (CA1502/CA1505), architecture tests; Stryker mutation score 100 %. |
| 2026-11-24 | Step 1.1 Review Board | 3 reviewer agents, 48 findings fixed or recorded; §1.8–1.10 principles, ADR-14, honest re-baseline; symlink support; CI runs the gate + gitleaks. Gate passed, mutation 100 %. |
| 2026-11-24 | Decision | Vertical slice first: after Step 1.7, build the thinnest end-to-end app before Step 1.8 and UI polish (§7). |
| 2026-11-24 | Step 1.2 | Encrypted file header: format constants, header write/read/validate, `SecureFileException` + `InvalidEncryptedFileException` (with `Reason`), 80 tests incl. golden bytes and fuzzing. Review Board: 0 High, 2 Medium + 3 Low fixed. Gate passed. |
| 2026-11-24 | Step 1.3 | Key derivation: NFC → UTF-8 → PBKDF2-HMAC-SHA256 → HKDF-Expand into two keys; `FileKeys` wipes keys on dispose. RFC 7914 / RFC 5869 vectors + Python-computed golden keys; PBKDF2 600k measured at ≈0.46 s. Review Board: 0 High, 3 Medium fixed (fail-closed normalisation, error naming, mutation exclusions with reasons). Gate passed, mutation 100 %. |
| 2026-11-24 | Step 1.5 | `FileEncryptionService.EncryptAsync`: own `TransformBlock` loop over one pooled 2 MiB buffer, HMAC over header + ciphertext, PBKDF2 in `Task.Run`, whole-percent progress, cancel; `AtomicFileWriter` (temp in output folder, fsync, atomic move, cleanup). Golden file from Python + OpenSSL; 76 new tests. Review Board (design + 2 code reviewers): 1 High (short-read test gap) + Medium findings fixed. Gate passed, mutation 100 %. |
| 2026-11-24 | Step 1.6 | `FileEncryptionService.DecryptAsync`: header and length checked before any work; one pass through HMAC + `TransformBlock` to a protected temp file (hidden on Windows, 0600 on Linux/macOS, emptied on failure); `FixedTimeEquals` before `TransformFinalBlock`; `AuthenticationFailedException` pulled forward from 1.7. 50 new tests (every bit-flip, truncation, wrong password, cancel leaves nothing). One reviewer (lean mode): 0 High, 2 Medium fixed. Gate passed, mutation 96.67 % (both survivors explained). |
| 2026-11-24 | Decision | Process: risk-based testing (Core full, ViewModels light, UI end-to-end + manual smoke), reviewer and LEARNING_LOG once per stage, Stryker once in a final hardening phase; automated gate unchanged on every push (§1.7 Cadence). |
| 2026-11-24 | Step 1.4 | `AesCbc` factory returns ready AES-256-CBC/PKCS7 encryptor/decryptor for (key, IV). NIST SP 800-38A F.2.5/F.2.6 + OpenSSL-checked padding cases. Review Board: 1 High for the 1.6 design recorded (no decrypting CryptoStream; finish last block only after HMAC check), 3 Medium fixed. Gate passed, mutation 100 %. |

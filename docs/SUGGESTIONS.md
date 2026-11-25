# SecureFile: suggestions and roadmap

For the Product Owner, Business Analyst, Project Manager and Team Lead. Refreshed in Stage 8 (2026-11-25) from
[REVIEW_BACKLOG.md](REVIEW_BACKLOG.md) (sections B and C) and [QUALITY_REPORT.md](QUALITY_REPORT.md) (Stages 5-7); every
"built" mark was checked against the repository, not from memory.

**How to read the tables.** Priority uses **MoSCoW**: Must / Should / Could / Won't (for this version). Effort: **S** = a
day or less, **M** = a few days, **L** = a week or more. Status: ✅ built and tested · 🟡 built, but not yet tried where
it matters · ⏸ not built. The "Ref" column points to the backlog item with the detail (a number like 7.4 is a plan step).

## 1. The idea in one sentence

**Boring, proven cryptography; an interface a non-technical person can use without help; and a promise that can be
checked: nothing leaves your computer.** Every suggestion below serves one of those three.

## 2. Already built

| What | Benefit | Status |
|---|---|---|
| AES-256 encryption with a tamper check, a key made from the password, a fresh random salt and starting value per file | A wrong password or a changed file is always detected; the same file never encrypts the same way twice | ✅ |
| Nothing half-written: the result appears only when it is complete and checked; cancel and close leave nothing behind | A failed run cannot leave a damaged file that looks real | ✅ |
| The app reads the file to decide Encrypt or Decrypt (a renamed file still works) | Fewer clicks, fewer mistakes | ✅ |
| Password rules done right: length (8+), confirm box, strength bar, Caps Lock warning (Windows), copy blocked | Fewer lost files and weak passwords | ✅ |
| **Windows app** (the required deliverable), as a single program with a published checksum | Nothing to install; the download can be verified | ✅ |
| **Command-line version** (encrypt / decrypt, hidden password typing, password from a pipe) for Windows, Linux and macOS | Automation and power users; same engine | ✅ Windows · 🟡 Linux/macOS run only by the automated tests |
| **macOS and Linux windows** (bonus), the same screen logic as Windows | Same tool for every team member; proves the layers are clean | 🟡 automated tests pass on all three systems; released as a **preview** in 0.1.0-beta2; nobody has reported using them on a real Mac or Linux desktop yet |
| **Cross-system files**: every build server encrypts a sample, every other one decrypts it | A file made on Windows opens on a Mac and on Linux, proven on each change | ✅ |
| **Size comparison**: Windows app 134 MB versus the trimmed macOS/Linux window 38 MB (35-40 MB on Linux and macOS) (downloads 58 versus 16 MB) | An informed choice for smaller downloads | ✅ (Native AOT size not measured) |
| Windows on ARM (Snapdragon) build | Newer laptops | 🟡 built, never run on an ARM machine |

## 3. Decisions we made on purpose

- **One encryption design, chosen by the app, never by the user and never at random.** A non-expert cannot judge
  algorithms, and hiding the method buys nothing: security must hold even if everything except the password is public
  (Kerckhoffs's principle). The file carries a version number, so a stronger design later is "version 2" and old files
  keep opening.
- **No "must contain a symbol" rules.** They push people to `Password1!`. Following NIST guidance, the app asks for
  length, flags well-known and predictable passwords, and accepts long phrases with spaces.
- **AI makes no security decisions.** No AI-chosen settings or passwords (a language model is not a source of secure
  randomness and the data would leave the computer). A simple local rule that says "this looks like a key or password
  file" is fine; an opt-in, local, advice-only assistant is possible later but never on the path that encrypts.
- **No "secure delete" of the original.** SSDs cannot guarantee it, so it would be a promise we cannot keep.

## 4. New suggestions

### 4.1 Security and file format

| Suggestion | Why / benefit | Priority | Effort | Ref |
|---|---|---|---|---|
| **File format version 2**: authenticate in chunks and store a quick password check | Today a wrong password on a very large file is found only at the end; this finds it at once | Should | L | B4 (6) |
| ✅ Same-file check on macOS/Linux by file identity, not by name (built in Stage 9.0) | A folder shortcut or a different letter case could make the output replace the input there (Windows is safe) | Should | M | C14 |
| ✅ Owner-only access rights on the temporary and result files (Windows; built in Stage 9.0) | Other accounts on a shared PC cannot read the work in progress | Should | S | C16 |
| Written file-format description with sample files | Other tools can read the format; upgrades are safer (the cross-system samples are a start) | Should | S | — |
| Free-disk-space check before a large run | A full disk is reported at the start, not at 99 % | Could | S | B4 (34) |
| Clean up leftovers after a crash or power cut on the next start | No hidden temporary file stays behind after a hard stop | Could | M | B4 (2) |
| Keep the original file name (encrypted) inside the file; option to hide it in the output name | Better restore; the name no longer reveals the content | Could | M | — |
| Optional key file in addition to the password | Two things needed to open a file | Could | M | — |
| Stronger password-to-key function (Argon2id) | Makes guessing on graphics cards much more expensive; .NET has no built-in version and the task requires built-in crypto, so it needs a reviewed library and a new format version | Could (after the course) | M | — |

### 4.2 Everyday experience

| Suggestion | Why / benefit | Priority | Effort | Ref |
|---|---|---|---|---|
| ✅ Clearer drop zone: "Drop a file here or Browse…", a highlight while dragging, a plain message for a folder or several files (built in Stage 9.0) | People see at once what to do | Should | S | B1 (12, 17, 31) |
| ✅ File card with folder, date, exact size and a tooltip for long names (built in Stage 9.0, as a tooltip) | Users can tell they picked the right file | Should | S | B1 (16, 33) |
| ✅ Keyboard and focus fixes (Enter moves to Confirm, Cancel focused while working, shortcut keys with "Show password"; built in Stage 9.0) | Keyboard users are not slowed down | Should | S | B1 (7, 8, 9, 11, 30) |
| ✅ Error dialogs say where the log file is (built in Stage 9.0) | Faster help when something goes wrong | Should | S | B1 (15) |
| Screen-reader announcements, high-contrast check, small screens and high scaling | Usable by everyone; needs a person with a screen reader | Should | M | B3 |
| **Passphrase generator** ("blue river coffee lamp"), local word list, secure random, clipboard cleared after 30 s | Memorable and strong passwords | Should | S | B2 (24, 38) |
| Success message reminds that the unencrypted original is still on disk | People do not think the original is gone | Could | S | B2 (25) |
| Larger list of common passwords, including Arabic | Better advice on weak passwords | Could | S | C3 |
| "This looks like a key or password file" hint | Helps people notice what they protect | Could | S | — |
| Right-click "Encrypt with SecureFile" in Explorer / Finder | Fits how people already work | Could | M | — |
| Encrypt a whole folder or a batch | A common real need (also for the command line) | Could | M | C11 |
| Arabic interface, right to left | Local users (Arabic file names already work) | Could | M | — |

### 4.3 Platforms, speed and shipping

| Suggestion | Why / benefit | Priority | Effort | Ref |
|---|---|---|---|---|
| Linux: ask before replacing an existing file in every Save dialog | Some Linux dialogs may not ask; check on a real desktop, then add our own question | Should | S | C19 |
| macOS/Linux window: Caps Lock warning, select the file in "Open folder", app icon on macOS | Same comfort as on Windows | Could | S-M | C20 |
| Code signing (Windows) and notarization (macOS) | Removes "unknown publisher" warnings; needs a paid certificate | Should | M | C7 |
| Smaller Windows download: compression gives 65 MB instead of 134 MB, about 0.2 s slower start | Faster download; a trade-off for the Product Owner to decide | Could | S | C1 |
| Faster large files: overlap computing with the final disk write | The throughput target (70 % of a plain copy) is not met today | Could | M | C13 |
| Installer (winget / MSIX) and a list of all components (SBOM) | Easier install and trust for companies | Could | M | — |
| Command line: explain how to pipe a password safely from older Windows PowerShell | Its pipe can turn non-English letters into `?`, locking a file with a password nobody can retype | Should | S | C17 |
| Command line: quiet and machine-readable output | Easier to use in scripts | Could | S | C11 |
| Native AOT build of the macOS/Linux window | Possibly smaller and faster to start; needs C++ build tools to measure | Could | S | 7.5 |

### 4.4 Process (Business Analyst and Project Manager)

| Suggestion | Why / benefit | Priority | Effort | Ref |
|---|---|---|---|---|
| A person runs the test plan and a short usability session with friends | So far only automated tests and the developer have used the app | Must (before a wider release) | S | C6 |
| Try the macOS and Linux windows and command line on real machines | Real keyboards, dialogs and terminals differ from the test servers | Must (before the "preview" label is dropped) | S | C2, C19, C21, 7.4 |
| Make the repository public (or upgrade the plan) | Enables free code scanning (CodeQL) and branch protection | Should | S | C9 |
| Run the release check on a machine without this antivirus, or exclude the app's temp folder | About 1 full test run in 4 fails here because the antivirus copies temp files | Should | S | C4 |
| Mutation testing on the command line and screen logic, as already done for the engine | Proves the tests catch mistakes there too | Could | S | C18, C22 |
| A second independent review after the backlog is burned down | Fresh eyes on the fixes | Should | S | D |

## 5. A short story for the presentation

1. **Proven building blocks, not clever ones.** One design, versioned, with a written reason for every option refused.
2. **The safe path is the easy path.** Only the action that fits the file is offered, bad passwords are stopped before
   they cost someone a file, and nothing half-written is left behind.
3. **One engine, every desktop.** Windows app, command line, and macOS/Linux windows share the same engine, and a file
   encrypted on one system is proven to open on the others.
4. **Find your own bugs first.** Independent reviews found the issues listed in the backlog; the serious ones are fixed,
   the rest are written down with reasons.
5. **Honest about limits.** No secure delete, no hidden AI, no claim of certification; untested platforms are marked as such.
6. **Next:** format version 2 with an instant wrong-password check, real-machine checks on macOS and Linux, and signed builds.

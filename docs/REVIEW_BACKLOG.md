# Backlog: everything found and not yet fixed

The one place to look when there is time to fix things. Detail, steps to reproduce and evidence for the 39 review items are
in [REVIEW_REPORT_2026-11-24.md](REVIEW_REPORT_2026-11-24.md) (numbers below are the report's own). Items found later are
listed in section C. When something is fixed, change its line here (and its status in the report), add a test where one
makes sense, and record it in the CHANGELOG.

The plan has a step for this: **5.6, "Burn down this backlog, then re-run an independent review"**.

## A. Fixed in Stage 4 (kept for the record)

1 Show password left the typed password on screen · 3 strength meter called repeats, runs and common words "Strong" · 4 no
reason when Encrypt is disabled · 5 `.enc` file with a damaged start · 10 Cancel shown as a failure and logged · 13 stale
progress text · 14 Open folder failure replaced the success message · test gaps for cancel, Open folder, strength · the
TESTPLAN rows that could no longer be done · CHANGELOG contradictions.

## B. Open: from the review (grouped; do in this order)

### B1. Do first: small, visible, low risk
- **9** Mnemonics `_Password` and `Co_nfirm` still target the collapsed password boxes when "Show password" is on (the F clash is fixed).
- **12** A bad drop or Browse clears the previous good selection; a dropped folder says "can't be found or opened" (should say it is a folder);
  several dropped files silently use the first; dropping while busy shows a copy cursor; only the dashed zone is a drop target.
- **17 / 31** Drop-zone text "Drop a file here or Browse…" and a drag-over highlight; the first-run text is repeated three times.
- **16 / 33** File card: a long name hides extension and size; add a tooltip, the folder, the date and the exact size (plan §11.1 promised them).
- **20** Cancelling the Save dialog leaves an old red message.
- **8 / 11 / 30** Show-password toggle moves focus off the checkbox; Enter in the first box should move to Confirm; newline-paste difference between box types.
- **7** Cancel is not focused while working; focus after a cancel; check that Esc still cancels when the focused box has just been disabled.
- **15** Error dialogs do not say where the log file is (`FileErrorLog.LogFilePath` exists) and have no owner window.
- **5 (rest)** The Save suggestion for a damaged `x.enc` becomes `x.enc.enc`.

### B2. Wording and small behaviour
- **18** The 8-character rule and the 1,024 limit count UTF-16 units (4 emoji pass; a long paste is cut silently, possibly at a surrogate).
- **19** Save dialog: "choose a different name" comes only after Windows asks about replacing; Decrypt of `x.enc` suggests an extensionless name;
  `.lnk` differs between Browse and drop; the Open dialog has no filter; the read-only-target message does not name the file.
- **21** File facts (size, encrypted or not) are read once when the file is chosen.
- **22** Progress text: estimate includes key derivation, "125 min 5 s", "0.0 MB/s", mixed culture formats, whole-percent steps.
- **24** Show password stays ticked for the next file; the visible box keeps an undo stack (`UndoLimit=0`); no clipboard clearing.
- **25** Success text does not say the unencrypted original is still on disk, or the folder.
- **28** USABILITY.md says "lock/unlock"; "Wrong password," is blunt; one vague message covers a deleted file, a folder and a broken link.
- **29** The error log keeps full paths and a stack trace for every wrong password; rotates once.
- **36 / 37 / 38** Single-instance warning; offer to delete or secure the original after encryption; clear the clipboard after a manager paste.

### B3. Accessibility and layout (need a person with a screen reader and different displays)
- **23** Caps Lock warning may linger after Tab, is not announced, and grows the window.
- **26** Window sizing: manual resize stops auto-height; small screens at 150-200 % scaling; secondary monitors; growing below the taskbar.
- **27** Live-region announcements of success and error, the strength bar value, high-contrast check, tooltip reasons for keyboard users (**32**).

### B4. Needs a decision or a bigger change
- **6 / 34** A wrong password on a huge file is reported only at the end; no free-disk-space check. Options: format v2 with a key-check
  value (see `docs/SUGGESTIONS.md`), or a note before starting and a space check.
- **2 (rest)** Crash, power loss, Windows logoff or the safety-net shutdown can still leave a `.~*.securefile-tmp` file; there is no startup
  sweep because temp files sit next to arbitrary targets. Idea: remember the output folder in the local log and offer a clean-up on the next start.
- **35** Cancelling "Reading the file details…" on a stalled network drive (the read is not cancellable).

### B5. Test gaps still open
- Window code-behind (password sync, Caps Lock, drop handler, closing) has no unit test; add a FlaUI case with Show password ticked and one for
  close-during-operation, or extract the sync into a testable class.
- VM: a progress value that is non-empty and then cleared (the fake never reports progress); a bad drop keeping the old selection; Esc with no
  operation; a file chosen after a failure; passwords surviving a file change. `RunPrimary_ChoosesTheOperationFromTheFileName` is misnamed.
- `ProgressTextFormatter` above one hour, 99 % at a tiny `elapsed`, a comma-decimal culture. The Save filter (`BuildSaveFilter`). Close during an operation.
- A "Cli is free of UI and network assemblies" rule in `SecureFile.Architecture.Tests` (the Cli project is not covered).

### B6. Documents still out of line
- PROJECT_PLAN §11.1 describes a mode switch, a "Change…" output button, a file-card tooltip and date, and "Drop a file here or Browse…"
  that do not exist (update the plan or build them). `FileErrorLog.LogFilePath` says the dialog shows the path (it does not).
- TESTPLAN: rows for Alt mnemonics, a dropped folder, several dropped files, Esc with no operation; section J sits before I.

## C. Open: found after the review

| # | Item | Why it matters |
|---|---|---|
| C1 | Windows app is 134 MB (self-contained, not trimmable) | Try `EnableCompressionInSingleFile`, or offer a framework-dependent build (small, needs the .NET 10 Desktop Runtime); plan §8.2 asks for the published size per architecture |
| C2 | Linux and macOS command-line binaries were built but **never run** on those systems; the interactive password prompt (`Console.ReadKey`, Ctrl+C as input) is untested on every OS, and Backspace removes one UTF-16 unit | Real keyboards and Unix terminals behave differently; first user feedback may find bugs |
| C3 | Strength check limits: look-alike letters (`P@ssw0rd`) not decoded; about 80 English words only (no Arabic list); few keyboard rows; the 50 % run rule is a guess | Advice only, but it is the first thing a reviewer will try |
| C4 | Kaspersky creates backup copies of temp files in test folders (`.~OUTPUT.*.SECUREFILE-TMP.tmp`, upper case), so about 1 full test run in 4 fails here with a different "…LeavesNothing" test | Exclude `%TEMP%\SecureFile*` in the antivirus, or run the gate where it is not installed; the delete retry in `AtomicFileWriter.TryDeleteFile` did not remove it |
| C5 | The 4.5 GiB large-file run (T-27) is blocked: the antivirus kills the test program | Only 1 GiB is measured (96 / 100 MB/s, 46 MB peak) |
| C6 | `docs/TESTPLAN.md` (A to J) has not been run by a person on the current build; the usability check with friends (`docs/USABILITY.md`) has not happened | Plan Steps 4.3 and 4.4 |
| C7 | Programs are not code-signed or notarized | SmartScreen and Gatekeeper warnings; plan Step 6.3 |
| C8 | macOS and Linux have no desktop window | Plan Steps 7.1 and 7.2 (Avalonia); releases for them are held back until then |
| C9 | GitHub branch and tag protection is unavailable on a private free repository | The release branch can be deleted on the website; make the repository public or upgrade the plan (see `docs/RELEASING.md`) |
| C10 | 2.5 coverage target (Presentation ≥ 85 %) has not been measured | Plan row 2.5 |
| C11 | CLI: `tar.gz` archives built on Windows may not carry the executable bit (README tells users `chmod +x`); no `--quiet`/`--json` mode; no folder or multi-file support | CLI polish |
| C12 | Original findings that stay as documented limits: no secure delete (SSDs cannot guarantee it); AES-CBC+HMAC format v1 only | See `docs/SUGGESTIONS.md` |
| C13 | Encrypt/decrypt do not overlap CPU work with the final disk flush: 100 MB takes about CPU time + disk time (1.4 s) instead of the larger of the two (about 0.9 s on the HDD) | Measured in Step 5.1; fix means a write-behind or periodic flush in the crash-safe path, so it needs its own design and tests |

## D. How to burn this down

1. B1 and B5 together (a fix and its test), one commit per item, run `scripts/verify.ps1` once at the end.
2. B2 and B6, then C1 (measure compression first) and C3.
3. B3 with a person and a screen reader; C2, C6 and C8 need other people or other machines.
4. B4 and C5, C7, C9 are decisions: write each into `docs/SUGGESTIONS.md` or an ADR before building.
5. Finish by re-running an independent review (same brief as the first) and update the report with the new findings.

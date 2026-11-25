# Independent review report: product owner and tester pass (2026-11-24)

The complete findings of the independent review (read-only, one pass) of the Windows app at commit `0841635`, kept here
so nothing depends on a chat. **Status** is as of the commit that added this file; `docs/REVIEW_BACKLOG.md` is the working
list to burn down and links back here for the detail.

- **Verified** = the reviewer confirmed it by reading the code path or running a test. **Suspected** = a person has to try it.
- The reviewer built the solution (0 warnings) and ran the tests, but did **not** run the app, the UI tests or the large-file run.
- **Evidence** (`file:line`) is as of the review; lines have moved since. Abbreviations: MW.cs = `src/SecureFile.Wpf/MainWindow.xaml.cs`,
  MW.xaml = `src/SecureFile.Wpf/MainWindow.xaml`, VM = `src/SecureFile.Presentation/Main/MainViewModel.cs`.
- Status words: **FIXED** (Stage 4), **PARTLY** (what is left is said), **OPEN** (to do), **DECISION** (needs a deliberate choice).

## Blocker

| # | Finding | Steps and expected result | Evidence | Status |
|---|---|---|---|---|
| 1 | "Show password" on: after a successful operation the typed passwords stay visible and the state is out of sync (verified) | Tick Show password, type both passwords in the visible boxes, encrypt. Expected: boxes cleared and equal to the ViewModel. Cause: `OnVisiblePasswordChanged` updated only the ViewModel; `OnViewModelPropertyChanged` compared only the hidden boxes. After that Encrypt was disabled with no reason | MW.cs:122-137, 169-176; VM:441-442 | **FIXED** (all four boxes compared). Window code still has no unit test (see test gaps) |
| 2 | Closing the window during an operation: no warning, temp file left behind; for Decrypt it holds unverified plaintext (verified in code, suspected in practice) | Start a large decrypt or encrypt, press X or Alt+F4. Expected: ask "Cancel and close?", cancel cleanly | no `Closing` handler; `AtomicFileWriter.WriteAsync`/`FillAsync` `finally` blocks never run (AtomicFileWriter.cs:87-94, 135-142); TESTPLAN H3 looked for `*.tmp` | **PARTLY**: the app now asks, cancels and closes cleanly. Still open: crash, power loss, Windows logoff/shutdown and the safety-net `Shutdown(1)` path (App.xaml.cs:73) can leave `.~*.securefile-tmp`; no startup sweep is possible because temp files sit next to arbitrary targets |

## Major

| # | Finding | Steps and expected result | Evidence | Status |
|---|---|---|---|---|
| 3 | Strength meter gave false assurance (verified) | `Password1234`, `aaaaaaaaaaaaaaaa`, `abcdefghijklmnop`, `1234567890123456` were green "Strong". Expected: at least repeat/sequence/common-password checks | `PasswordStrengthEvaluator` (length and variety only); PasswordStrengthEvaluatorTests.cs:15 pinned the weakness | **FIXED**. Limits left: look-alike letters (`P@ssw0rd`) not decoded, small English-only word list, few keyboard rows |
| 4 | Encrypt greyed out with no stated reason when the password is long enough and the confirmation is empty (verified) | `PasswordMatchText` was empty until the confirm box had text; Encrypt had no tooltip for this case; Enter did nothing. Plan §11.1(6): "disabled until valid, with a hint why" | VM:161; MW.xaml:239-248 | **FIXED** (prompt line and live "next step" line). Left: disabled buttons show tooltips only on hover, so keyboard and screen-reader users get nothing (see 32) |
| 5 | A `.enc`-named file without the SFEN signature silently offered only Encrypt (verified) | Damaged `report.pdf.enc` showed the amber "if you forget" box; Save suggestion `report.pdf.enc.enc`; Decrypt tooltip pointed the wrong way; same for a zero-byte `.enc`; `ErrorMessageProvider` still said "choose a file that ends in .enc" | VM:116-118; ErrorMessageProvider.cs:75 | **FIXED** (both buttons offered, explanation shown, message reworded). The Save suggestion `x.enc.enc` for such a file is unchanged |
| 6 | A wrong password on a large file is only reported at the very end (verified, by design) | Decrypt is verify-then-commit: a 10 GB file runs to 100 % (writing a full-size hidden temp plaintext file) then says "Wrong password". No key-check value in the header; no free-space pre-check, so disk-full also arrives late | `FileEncryptionService` design | **DECISION**: needs a format change (key-check value, format v2) or a UI note before starting; see `docs/SUGGESTIONS.md` |
| 7 | Keyboard flow: focus lost after every operation and dialog (verified) | While Working the password boxes are disabled and focus goes nowhere; after success/failure/cancel nothing is focused; Enter-to-retry needs Tab from the top; first run had no initial focus | no `Focus()` calls except in `OnShowPasswordChanged` | **PARTLY**: Browse at start, password box after a failure, Open folder after a success. Open: Cancel is not focused while working; with focus on a just-disabled box, whether Esc still cancels is **suspected-unverified**; focus after a cancel |
| 8 | "Show password" toggle is hostile to keyboard users and may scramble typing (partly verified, partly suspected) | Alt+S or Space calls `Focus()` on the password box, pulling focus off the checkbox; toggling rewrites both boxes; caret reset (suspected); password pasted with a newline may differ between PasswordBox and TextBox (suspected) | MW.cs:153; MW.cs:148-151 (CaretIndex only for the first box) | **PARTLY**: the caret is now put at the end in both visible boxes. Open: focus moves off the checkbox; newline paste difference |
| 9 | Mnemonics break or clash (verified) | `_Password` and `Con_firm` target `PasswordInput`/`ConfirmInput`; with Show password on those are collapsed, so Alt+P / Alt+F cannot focus the visible boxes. `Con_firm` and `Open _folder` both used F | MW.xaml:55, 131, 308 | **PARTLY**: the F clash is fixed (now `Co_nfirm`). **Open: the mnemonics still target the collapsed boxes in Show-password mode** |
| 10 | Cancel presented as a failure (verified) | `OperationCanceledException` went through `Fail()`: red banner, state Failed, stack trace logged as an error. Expected neutral banner, no log line | VM:355-358, 389-399 | **FIXED** |
| 11 | No reason when Encrypt/Decrypt/Enter is blocked; Enter in the first box does not move to Confirm (verified) | Enter with an empty Confirm does nothing | VM | **PARTLY**: the guidance line now says what to do. Open: Enter does not move focus to the confirm box |
| 12 | A bad drop or Browse throws away the previous good selection; drop handling gaps (verified) | `clearSelectionOnFailure: true`: dropping a folder or a locked path while file A is chosen forgets A and shows "Failed"; a dropped folder says "can't be found or opened" (should say it is a folder); several dropped files silently use the first; dropping while busy shows the Copy cursor and does nothing; only the dashed zone is a drop target (dropping on the password box or file card shows "not allowed") | VM:208, 230; FileDetailsReader.cs:23-26; MW.cs:90-94; `OnDragOver` ignores `IsBusy` | **PARTLY** (Stage 5): the previous good selection is now kept after a bad drop or Browse. Open: the folder wording, several dropped files, copy cursor while busy, drop target size |
| 13 | Progress area shows stale text (verified) | After an operation (or a cancel at 45 %) the next Browse shows the old "45 % · 12.3 MB/s · about 5 s left" | VM:254-261, 328-335 | **FIXED** (`ClearProgress`). No test shows a non-empty value being cleared (the fake never reports progress) |
| 14 | A failed "Open folder" message appeared inside the green success banner and replaced the "saved as…" text (verified) | `OpenFolder()` overwrote `StatusMessage` while State stayed Succeeded | VM:279-285 | **FIXED** |
| 15 | Error dialogs never say where the log file is (verified) | `FileErrorLog.LogFilePath` exists "so the friendly dialog can tell the user where it is" but is never used; the message boxes have no owner window | FileErrorLog.cs:43; App.xaml.cs:69, 79 | **OPEN** |
| 16 | The file card truncates away the important part (verified) | `FileSummary` is one string "name (.ext) · size" with ellipsis, so a long name cuts the extension and size; no tooltip (plan §11.1(2) promised exact bytes), no last-modified date, no folder (two same-named files look identical); an Arabic or mixed-direction name reorders the "(.ext) · size" part (suspected) | MW.xaml:50-51 | **OPEN** |

## Minor

| # | Finding | Evidence / detail | Status |
|---|---|---|---|
| 17 | Status wording stale after the content-based redesign; the first-run text repeated three times; the drop zone no longer says "drop" so drag-and-drop is undiscoverable (plan §11.1(1): "Drop a file here or Browse…"); no drag-over highlight | VM:260, 334 | **PARTLY**: the status line is now a live next step. Open: the drop wording, the drag highlight, the triple repetition at first run |
| 18 | The "8 characters" rule and `MaxLength=1024` count UTF-16 units, not characters | 4 emoji pass the minimum (VM CanEncrypt); NFD counts more than NFC; a pasted password over 1024 is silently truncated and can be cut at a surrogate, giving a lone surrogate | **OPEN** |
| 19 | Save dialog behaviours | Picking the input file: Windows asks "replace?" and only after Yes says "choose a different name" (VM:577-588); the Decrypt suggestion `x.enc` to `x` gives an extensionless name and an All-files filter; Open dialog dereferences `.lnk`, a drop gives the `.lnk` itself; Open dialog has no filter or remembered folder; replacing a read-only/hidden target says only "Access … denied" without naming the file | **OPEN** |
| 20 | Stale error banner while the user fixes the problem | After a wrong password or rejected password the red message stayed while retyping; cancelling the Save dialog leaves an old red message | **FIXED**: typing clears an old error (Stage 4); cancelling the Save dialog now returns to the guidance text (Stage 5, step 5.6) |
| 21 | Stale file facts | `SelectedFile` (`IsEncrypted`, size, name) is read once when chosen; if the file changes before the click the offered button can be wrong and the speed uses the old size | **OPEN** |
| 22 | Progress text edge cases | Estimate includes key-derivation time (about 0.5 s with no progress); over an hour shows "125 min 5 s"; very slow shows "0.0 MB/s"; time uses invariant culture, speed the current one; whole-percent steps can look stuck on huge files | **OPEN** |
| 23 | Caps Lock warning | Checked only on key-up and focus events; `LostKeyboardFocus` may fire while `IsKeyboardFocusWithin` is still true so it can linger after Tab (suspected); not a live region so screen readers do not hear it; its appearance grows the window by a line | **OPEN** |
| 24 | Show password persists across operations and files | Next password is typed in plain text unless unticked; the visible TextBox keeps an undo stack (Ctrl+Z) of password text in memory (set `UndoLimit=0`); no clipboard clearing after a password-manager paste | **OPEN** |
| 25 | Encrypt success message can be misread | "Your original file was not changed" is true, but a non-technical user may not realise the unencrypted original is still on disk; the banner shows only the file name, not the folder | **OPEN** |
| 26 | Window sizing (suspected, from code) | After a manual resize WPF sets `SizeToContent` to Manual, so it stops following the content; `MinHeight=480`/`Width=800` can exceed small screens at 150-200 % scaling (1366x768 at 200 % is 683x384 DIP); `MaxHeight` uses the primary monitor only; growing near the screen bottom can pass below the taskbar | **OPEN** |
| 27 | Accessibility (mostly suspected) | `AutomationProperties.LiveSetting="Polite"` on a TextBlock does not by itself raise `LiveRegionChanged` in WPF, so success/error may not be announced (TESTPLAN C2, C3); the strength `ProgressBar` exposes 15/35/65/100 and its text is not announced; Cancel (visible only while busy) is not focused; disabled buttons are not focusable so their tooltip reasons never reach keyboard users; no high-contrast check documented; the .NET 10 Fluent theme (`ThemeMode`) is experimental | **OPEN** |
| 28 | Terminology and wording | USABILITY.md uses "lock/unlock" while the app says Encrypt/Decrypt; the wrong-password banner starts bluntly "Wrong password,"; "The file or folder can't be found or opened" covers a deleted source, a dropped folder, a missing folder and a broken link | **OPEN** |
| 29 | The error log writes `exception.ToString()` with full paths | File names such as "TaxReturn2025.pdf" and the user name reach a plaintext log; rotates only once (`.1`); every cancel and wrong password adds a stack trace. No password leak (documented in FileErrorLog.cs:10-15) | **FIXED (Stage 9.0)** except rotation: cancels are not logged; paths become `<path>`; expected failures (wrong password, tampered file, disk errors) are logged without a stack trace; crashes keep it. Open: one rotation (`.1`) only |

## Suggestions

| # | Suggestion | Status |
|---|---|---|
| 30 | Initial focus on Browse; Enter in the first password box moves to Confirm | **PARTLY** (initial focus done; Enter-to-Confirm open) |
| 31 | Drag-over highlight and the text "Drop a file here or Browse…" | OPEN |
| 32 | A tooltip or inline hint for every disabled button, and an always-visible reason line | **PARTLY** (live guidance line; tooltips only on hover) |
| 33 | Folder path and last-modified date on the file card, with a tooltip for exact bytes (plan §11.1) | OPEN |
| 34 | Warn or pre-check free disk space before a large Decrypt or Encrypt | OPEN (see 6) |
| 35 | Cancel a file read on a stalled network drive: Cancel is visible but disabled and Esc does nothing during "Reading the file details…" (VM:264-275) | OPEN |
| 36 | A single-instance check or a warning when the same output is open in another instance (temp names are unique, so UX only) | OPEN |
| 37 | Offer to delete or secure the original after encryption (explain that it remains) | OPEN |
| 38 | Clear the clipboard after a password-manager paste, or document the limitation | OPEN |
| 39 | `ThrowTestErrorCommand` (Ctrl+Shift+F12) is correctly Debug-only: keep it that way for the demo build | **OK** (no action; published builds are Release) |

## Test gaps

| Gap | Status |
|---|---|
| No test for item 1 or any WPF code-behind (password sync, Caps Lock, drop handler, closing); the sync logic is the most defect-prone code and sits only behind the FlaUI test, which needs `SECUREFILE_UI_TESTS=1`. A seam or a FlaUI case with Show password on is needed | **OPEN** |
| No VM test for stale `ProgressText`/`ProgressPercentage` after Browse, cancel or failure | PARTLY (asserted empty after cancel; no test sees a non-empty value cleared) |
| No test that Cancel is not a failure or logged as an error | **FIXED** |
| No VM test for `OpenFolder` failure keeping the success state | **FIXED** |
| No test for drag-and-drop with a folder, several files, or while busy; none that a bad drop keeps the old selection (the code currently clears it) | **OPEN** |
| No VM test with a renamed encrypted file or an unsigned `.enc`; `RunPrimary_ChoosesTheOperationFromTheFileName` has a misleading name | PARTLY (tests added; the misleading test name remains) |
| `PasswordStrengthEvaluatorTests` enshrined the weakness (`abcdefghijklmnop` Strong); no common-password or repeat cases | **FIXED** |
| No test for close-during-operation and leftover temp files, nor for a startup sweep | **OPEN** |
| No FlaUI or integration test for the Save dialog filter or extension handling (`WpfFileDialogService.BuildSaveFilter`) | **OPEN** |
| No test for `ProgressTextFormatter` above one hour, 99 % at a very small `elapsed`, or a comma-decimal culture | **OPEN** |
| No test for Esc with no operation, for choosing a file after a failure, or for passwords surviving a file change | **OPEN** |
| Two large-file integration cases skip by default and the 4.5 GiB run is still open | **OPEN** (blocked by antivirus) |

## Documentation drift

| Drift | Status |
|---|---|
| TESTPLAN A13, A14, F6 described impossible steps (disabled buttons); A1 mentioned a single main button and focus; A18 merged two checks; H3 checked `*.tmp` instead of `.~*.securefile-tmp`; rows missing for Show-password-plus-success, close during an operation, Esc with no operation, Alt mnemonics, a dropped folder, several dropped files | **PARTLY**: A1, A13-A15, A18, F6, H3 rewritten; A19-A22 and section J added. Open: rows for Alt mnemonics, dropped folder, several files, Esc with no operation |
| CHANGELOG mixed name-based and content-based wording; said both "taller window" and "height follows content" | **FIXED** |
| `ErrorMessageProvider` and the Save suggestion still described ".enc" names | **PARTLY** (message reworded; the Save suggestion `x.enc.enc` for a damaged file remains) |
| PROJECT_PLAN §11.1 describes a mode switch, a "Change…" output button, a file-card tooltip with exact bytes, a last-modified line, the drop-zone text "Drop a file here or Browse…" and "disabled until valid, with a hint why" that do not exist, or only partly | **OPEN** (update the plan or build them) |
| `FileErrorLog.LogFilePath` docs say the dialog shows the path; it does not | **OPEN** |
| USABILITY.md says "lock/unlock" while the app says Encrypt/Decrypt | **OPEN** |

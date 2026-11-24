# SecureFile — manual test plan (Step 4.3, T-41, T-43, T-44, T-25, T-27)

Automated tests cover Core, the ViewModels, real-file integration and the FlaUI smoke test. This script covers what
a machine cannot judge: how the app feels and looks, the keyboard, a screen reader, scaling, themes, removable media
and real drag-and-drop. Run it on the Release build (`dotnet run --project src/SecureFile.Wpf -c Release`, or the
published exe). Tick each box; write the date, who ran it and the Windows version at the top of each run.

**Run:** date ____ · tester ____ · Windows ____ · build/commit ____

**Test data (create once, in an empty folder):** `small.txt` (a few lines), `empty.txt` (0 bytes), `photo.jpg` (any
picture), `عربي.txt` (Arabic file name), `with spaces and (brackets).txt`, `big.bin` (about 500 MB: `fsutil file
createnew big.bin 524288000`). Use the passphrase `Correct-Horse-9!` for the tests unless a step says otherwise.

Result column: ✅ pass · ❌ fail (write the bug id) · ➖ not applicable.

## A. Main flow (mouse)

| # | Step | Expected | Result |
|---|---|---|---|
| A1 | Start the app. | One window, no error, focus is in a sensible place, the main button is disabled. | |
| A2 | Click Browse, pick `small.txt`. | Path shown; the app offers Encrypt (not Decrypt). | |
| A3 | Type the passphrase, click Encrypt. | Progress shown, success message, `small.txt.enc` (or the app's extension) exists next to the source, source untouched. | |
| A4 | Click "Open folder". | Explorer opens with the new file selected or visible. | |
| A5 | Browse to the `.enc` file. | The app offers Decrypt. | |
| A6 | Decrypt with the same passphrase to a new name. | Success; restored file equals the original (`fc /b small.txt restored.txt`). | |
| A7 | Repeat A2–A6 with `photo.jpg`. | Picture opens normally after the round trip. | |
| A8 | Repeat with `empty.txt`. | Round trip works; restored file is 0 bytes. | |
| A9 | Repeat with `عربي.txt` and with `with spaces and (brackets).txt`. | Names are shown correctly; round trip works. | |
| A10 | Decrypt with a wrong passphrase. | Plain message ("wrong password or damaged file"), no output file, no crash, app stays usable. | |
| A11 | Encrypt when the output file already exists. | The app asks before replacing; "No" keeps the old file; "Yes" replaces it. | |
| A12 | Select an encrypted file, then a normal file, then rename an encrypted file to `x.bak` and select it. | Encrypted (any name): only Decrypt is enabled; normal file: only Encrypt is enabled (tooltips explain). Double encryption is covered by the integration test (T-26). | |

## B. Keyboard only (T-41) — do not touch the mouse

| # | Step | Expected | Result |
|---|---|---|---|
| B1 | Press Tab repeatedly through the whole window. | Every control gets a clearly visible focus; the order follows reading order (top→bottom, left→right); no control is skipped; focus never gets trapped. | |
| B2 | Shift+Tab backwards. | Reverse order, same controls. | |
| B3 | Ctrl+O. | Browse dialog opens; pick a file with the keyboard. | |
| B4 | Type the passphrase, press Enter. | Starts Encrypt or Decrypt (the one offered). | |
| B5 | Start the 500 MB file, press Esc. | Cancels; no output file left; inputs unlock. | |
| B6 | F1. | About dialog opens; Esc or Enter closes it and returns focus to the window. | |
| B7 | Complete one full encrypt + decrypt with the keyboard only. | Possible without the mouse. | |

## C. Screen reader (Narrator, Win+Ctrl+Enter) (T-41)

| # | Step | Expected | Result |
|---|---|---|---|
| C1 | Tab through the window with Narrator on. | Each control is read with a meaningful name and role (not "button" alone or the class name). | |
| C2 | Encrypt a file. | Progress and the final success/error message are announced without moving focus by hand. | |
| C3 | Wrong passphrase. | The error message is read. | |
| C4 | Open About (F1). | Dialog title and content are read; closing returns focus. | |

## D. Display (T-43)

| # | Step | Expected | Result |
|---|---|---|---|
| D1 | Windows light mode. | Text readable, all controls visible, contrast fine. | |
| D2 | Windows dark mode (switch while the app runs, then restart). | The app follows the theme; no white-on-white or black-on-black text. | |
| D3 | Display scaling 100%. | Layout correct. | |
| D4 | Display scaling 150%. | No clipped text or overlapping controls. | |
| D5 | Display scaling 200%, also text size 150% (Settings → Accessibility → Text size). | Still usable; About dialog is not cut off (known open item: About width at large text). | |
| D6 | Resize the window to the smallest allowed and maximize. | Nothing hidden; layout stays sensible. | |

## E. Drag and drop and clipboard

| # | Step | Expected | Result |
|---|---|---|---|
| E1 | Drag `small.txt` from Explorer onto the window. | File is selected as if browsed. | |
| E2 | Drag a folder onto the window. | Friendly refusal (a folder is not a file), no crash. | |
| E3 | Drag two files at once. | Defined behavior (one used or refused with a message), no crash. | |
| E4 | Paste a passphrase with Ctrl+V. | Works; the passphrase is not shown in clear text. | |

## F. Failure situations

| # | Step | Expected | Result |
|---|---|---|---|
| F1 | Select a file, delete it in Explorer, then click Encrypt. | Clear message that the file is gone; no crash (T-24). | |
| F2 | Open the source in another program that locks it (for example Word on a `.docx`), then encrypt. | Clear "in use" message; no partial output. | |
| F3 | Choose an output folder without write permission (for example `C:\Windows\System32`). | Clear permission message; no partial file (T-22). | |
| F4 | Start the 500 MB file on a USB stick or network share and unplug/disconnect mid-run (T-25). | Clear error, app recovers, no crash; no half-written file left under the final name. | |
| F5 | Fill a small virtual disk or USB stick, then encrypt a larger file to it (T-23). | "Disk full" style message; no partial file. | |
| F6 | Select a random non-SecureFile file and click Decrypt. | "Not a SecureFile file" message, no crash. | |
| F7 | Edit one byte of a `.enc` file with a hex editor, then decrypt. | Detected as damaged; no output. | |
| F8 | Start two operations quickly (double-click Encrypt). | Only one runs; inputs lock while working. | |

## G. Large file and performance

| # | Step | Expected | Result |
|---|---|---|---|
| G1 | Encrypt and decrypt `big.bin` (500 MB) while watching Task Manager. | Memory of the app stays low (tens of MB, not 500 MB); UI stays responsive; speed and time left shown; restored file identical (`fc /b` or `Get-FileHash`). | |
| G2 | `scripts/large-file-check.ps1` on a machine without the antivirus problem (T-27). | 1 GiB and 4.5 GiB pass, peak memory below 400 MB. 1 GiB already measured: 96 / 100 MB/s, 46 MB peak; 4.5 GiB is still open (see LEARNING_LOG, Stage 4). | |

## H. Install and uninstall hygiene

| # | Step | Expected | Result |
|---|---|---|---|
| H1 | Run the exe from a path with spaces and from a non-admin account. | Starts and works; no admin prompt. | |
| H2 | Check `%LOCALAPPDATA%\SecureFile\logs\securefile.log` after the failure tests. | Contains error types and times, never a passphrase or file content. | |
| H3 | Check no `*.tmp` or partial files remain next to the sources after all tests. | None. | |

## I. Exploratory session (30 minutes, T-44)

Charter: "Try to make the app crash, lose a file, show a confusing message, or leak a passphrase." Take notes while
working; one line per observation. Ideas: very long passphrase, passphrase with emoji, paths near 260 characters,
file names with trailing dots or spaces, switching theme mid-operation, minimizing during encryption, sleeping the
laptop during a long run, opening two app instances on the same file, changing the source while it is encrypted.

| Time | What I tried | What happened | Bug id / note |
|---|---|---|---|
| | | | |

## Bugs found

| Id | Where | Description | Severity | Fixed in |
|---|---|---|---|---|
| | | | | |

## Sign-off

Step 4.3 is done when every row is ✅ or ➖ (or has a bug that is fixed and re-tested), the exploratory table is
filled in, and the Stage 3 items that were never verified (Tab stops, Narrator, dark mode, 150%/200% scaling, real
drag-and-drop, Open folder) are covered by sections B–E above.

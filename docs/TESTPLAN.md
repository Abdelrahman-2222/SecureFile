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
| A1 | Start the app. | One window, no error; focus is on Browse; Encrypt and Decrypt are both disabled; no confirm box, strength bar or password warning yet. | |
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
| A13 | Select a normal file; type a 5-character password in both boxes. | Encrypt stays disabled. Strength bar is red with "Too short: use at least 8 characters." | |
| A14 | Type a valid password in the first box, then a different one in the second. | Before typing the second: "Type the password again to confirm it." (neutral). While different: "Passwords don't match yet." in red and Encrypt stays disabled; same text: green "Passwords match." and Encrypt becomes enabled. | |
| A15 | Type `blue river coffee lamp` in both boxes. | Bar green, "Strong.", "Passwords match."; Encrypt works. Then try `Password1234`, `aaaaaaaaaaaaaaaa`, `abcdefghijklmnop`: all must show "Weak" (red). A short password for Decrypt of an old file is accepted (no rules, no confirmation box). | |
| A16 | Turn Caps Lock on and click into any password box; press a key. | "Caps Lock is on." appears while the box has focus and disappears when Caps Lock is off or focus leaves. | |
| A17 | Tick "Show password". | Both boxes show their text; typing in either still works; unticking hides them again with the text kept. | |
| A18 | With "Show password" ticked, select the text and press Ctrl+C, Ctrl+X, right-click, and drag it out. | Nothing is copied; no context menu; pasting into the box still works. Also: with Show password ticked, encrypt successfully: both boxes and the password text are empty afterwards (nothing left on screen), and the next file starts clean. | |
| A19 | Start a large encryption, then close the window (X or Alt+F4). | A question "An operation is still running. Cancel it and close SecureFile? Nothing will be written."; No keeps working; Yes cancels, the window closes, and no partial or temp file is left. | || A20 | Start an operation and press Cancel (or Esc). | Neutral message "Cancelled. No file was written." (no red banner), the chosen file is still selected, no stale percentage or speed is shown on the next file. | || A21 | After a failure (for example a wrong password) look at the focus. After a success look at the focus. | Failure: focus is in the password box with its text selected. Success: focus is on Open folder. | || A22 | With Open folder unavailable (no Explorer, or simulate), click it. | The green success message stays and a note is added that the folder couldn't be opened. | |

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
| F6 | Select a random non-SecureFile file (Decrypt is disabled, Encrypt offered). Then rename a copy of it to `x.enc` and select that; click Decrypt. | The `.enc`-named file shows "ends in .enc but is not a valid SecureFile file" and both buttons are enabled; Decrypt gives "This isn't a file encrypted by SecureFile, or its start is damaged"; no crash, no output. | |
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
| H3 | Check that no leftover `.~*.securefile-tmp` (hidden) or other partial files remain next to the sources after all tests, including after a cancel and after closing the window during an operation. | None. | |

## J. Command-line version (`securefile`, any system)

Use the archive for your system from `artifacts/cli` (see `docs/CLI_README.md` for the one-time steps on Linux and macOS).

| # | Step | Expected | Result |
|---|---|---|---|
| J1 | `securefile --version`, `securefile --help`. | Version `0.1.0`; usage text on screen; exit code 0. | |
| J2 | `securefile encrypt <file>` and type a password (nothing must appear), use Backspace once, Enter; type it again. | The password is never echoed; Backspace works; `<file>.enc` appears; original unchanged. | |
| J3 | Type two different passwords. | "The two passwords are not the same"; no file written; exit code 1. | |
| J4 | Type a 5-character password. | "too short"; no file written. Then `Password1234`: accepted with a note that it is weak. | |
| J5 | `securefile decrypt <file>.enc -o restored` and the right password; compare with the original (`cmp` / `fc /b` / checksums). | Identical bytes. | |
| J6 | Decrypt with a wrong password. | "Wrong password…"; exit code 1; no output file. | |
| J7 | Encrypt again while `<file>.enc` exists; then add `--force`. | Refused with a hint; then replaced. | |
| J8 | Press Ctrl+C at the password question, and again in the middle of a big file (about 1 GB). | Message "Cancelled. No file was written."; exit code 130; no partial or hidden temp file left. | |
| J9 | `printf '%s\n' 'blue river coffee lamp' \| securefile encrypt <file> --password-stdin`, then without the flag in a pipe. | Works; without the flag it says how to pipe the password. | |
| J10 | Lock a file on one system, copy it, unlock it on another (Windows ↔ macOS ↔ Linux). | Opens and is identical (same format everywhere). | |
| J11 | Redirect output (`securefile encrypt f > out.txt 2> err.txt`). | `out.txt` has only the result line; the prompt, progress and errors are in `err.txt`. | |

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

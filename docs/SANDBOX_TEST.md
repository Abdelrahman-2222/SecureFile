# Clean-machine test in Windows Sandbox (plan Step 6.2)

**Status: waiting for the user.** Windows Sandbox is not enabled on the development PC (`WindowsSandbox.exe` does not exist),
and turning it on needs administrator rights and a reboot, so the test has not been run. `scripts/sandbox-test.wsb` is
written and untested. Record the result in the table at the end of this file.

Why: Windows Sandbox is a throw-away, clean Windows 10/11 with no .NET, no Visual Studio and no developer tools, which is
the closest thing to a friend's PC. It proves that the self-contained single-file exe starts and works with nothing installed.
Only the x64 build can be tested this way on an x64 PC (the arm64 build cannot run here).

## One-time setup (about 5 minutes plus a reboot)

Needs Windows 10/11 **Pro, Enterprise or Education** (this PC is Windows 11 Pro), virtualization on in the BIOS/UEFI.

1. Open PowerShell **as administrator** and run:
   ```powershell
   Enable-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM" -All
   ```
   (or: Start menu → "Turn Windows features on or off" → tick **Windows Sandbox** → OK).
2. Reboot when asked.
3. Kaspersky or another antivirus may scan the mapped folder; if it deletes or blocks `SecureFile.exe`, exclude
   `C:\SecureFileSandbox` (do not turn the antivirus off).

## Prepare the build

1. `powershell -ExecutionPolicy Bypass -File scripts/publish-beta.ps1 -Runtimes win-x64` (from PowerShell, not Git Bash).
2. Create `C:\SecureFileSandbox`, unzip `artifacts/beta/SecureFile-<version>-<label>-win-x64.zip` into it, so that
   `C:\SecureFileSandbox\SecureFile.exe` exists. Put a small `hello.txt` there too (any text). For row 8, also unzip
   `artifacts/cli/securefile-cli-<version>-<label>-win-x64.zip` into a subfolder `cli` of it.
3. Compare the zip's hash with `artifacts/beta/SHA256SUMS.txt` (the sandbox also prints the exe's hash).

## Run it

Double-click `scripts/sandbox-test.wsb`. A clean desktop opens (no network) with a PowerShell window that copied the exe to
`Desktop\SecureFile` and printed its SHA-256. The folder `Desktop\Release` is the read-only copy of your folder.

## Click list (tick each one; any ✗ is a bug to report)

| # | Do this | Expected | Result |
|---|---|---|---|
| 1 | Double-click `Desktop\SecureFile\SecureFile.exe` | Windows may show "Windows protected your PC": choose **More info → Run anyway**. The window opens within a few seconds. No error about .NET or missing DLLs. | |
| 2 | Look at the window | Layout complete, text readable, light or dark follows Windows. **F1** opens About with the version number. | |
| 3 | Browse to `Desktop\SecureFile\hello.txt` (copy it there first), type a password twice, **Encrypt file**, save as suggested | Green success message; `hello.txt.enc` appears next to it. | |
| 4 | Browse to the `.enc`, enter the **right** password, **Decrypt file**, save under a new name | Success; open the new file: same text as `hello.txt`. | |
| 5 | Decrypt again with a **wrong** password | Plain error message, no file created, no crash. | |
| 6 | Press **Win+R**, run `%LOCALAPPDATA%\SecureFile\logs` | The folder exists and `securefile.log` contains no password or file text. | |
| 7 | Encrypt a file of about 500 MB (create one in PowerShell: `fsutil file createnew big.bin 524288000`) | Progress bar moves, Cancel works (no leftover `.tmp` file), a full run succeeds. | |
| 8 | Optional: run `securefile.exe --version` from the CLI zip | Prints the version. | |
| 9 | Close the sandbox window | Everything is thrown away; nothing changed in `C:\SecureFileSandbox`. | |

## Result

| Date | Build (zip name, SHA-256 start) | Tester | Rows passed | Notes |
|---|---|---|---|---|
| _not run yet_ | | | | |

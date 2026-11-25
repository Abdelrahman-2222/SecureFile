# securefile: the command-line version (beta)

Locks a file with a password and unlocks it again, on **Windows, macOS and Linux**. It uses the same encryption engine as the
SecureFile Windows app (AES-256 with an integrity check), so a file locked on one system opens on another. Nothing is uploaded:
it works completely on your computer and needs no internet. There is nothing to install; the program is one file.

**This is an early test version. Please read the safety notes first.** Public releases so far attach only the Windows
build; the Linux and macOS builds are made with `scripts/publish-cli.ps1` and have only been run by the automated tests.

## Safety notes

1. **Test on copies.** `securefile` never changes your original file (it writes a new `.enc` file), but this is a beta, so keep
   a backup of anything you care about.
2. **If you forget the password, the file cannot be recovered. Nobody can reset it,** not even the author.
3. **The program is not signed yet,** so your system may warn you the first time (see your system below). Check the download
   against `SHA256SUMS.txt` before running it.

## Get it running

**Linux** (`securefile-cli-…-linux-x64.tar.gz` for most PCs, `linux-arm64` for Raspberry Pi and ARM servers)

```bash
tar -xzf securefile-cli-<version>-linux-x64.tar.gz
cd securefile-cli-<version>-linux-x64
chmod +x securefile            # only if "Permission denied" (older archives were packed without the program flag)
./securefile --version
```

**macOS** (`osx-arm64` for Apple-silicon Macs M1 and newer, `osx-x64` for Intel Macs)

```bash
tar -xzf securefile-cli-<version>-osx-arm64.tar.gz
cd securefile-cli-<version>-osx-arm64
chmod +x securefile            # only if "Permission denied"
xattr -d com.apple.quarantine securefile   # one time: tell macOS you trust this download
./securefile --version
```

If macOS still says it "cannot be opened because the developer cannot be verified", open **System Settings → Privacy &
Security** and choose **Open Anyway** next to the message, then run it again.

**Windows** (`win-x64`; `win-arm64` for ARM PCs such as Surface Pro X, built but not yet run on real ARM hardware): unzip, open PowerShell in the folder, run `.\securefile.exe --version`. If SmartScreen says
"Windows protected your PC", choose **More info → Run anyway**.

**Check the download** (any system): compare the archive's SHA-256 with the line in `SHA256SUMS.txt`:
`shasum -a 256 <file>` (macOS), `sha256sum <file>` (Linux) or `Get-FileHash <file> -Algorithm SHA256` (Windows PowerShell).

## Use it

```text
securefile encrypt <file> [--output <path>] [--force] [--password-stdin]
securefile decrypt <file> [--output <path>] [--force] [--password-stdin]
securefile --help | --version
```

**Lock a file.** You are asked for the password (nothing appears while you type) and then to type it again:

```text
$ ./securefile encrypt holiday.jpg
New password:
Type it again to confirm:
Encrypting… 100 %
Encrypted copy saved as "holiday.jpg.enc". Your original file was not changed.
```

**Unlock it.** You are asked for the password once:

```text
$ ./securefile decrypt holiday.jpg.enc -o holiday-restored.jpg
Password:
Decrypted file saved as "holiday-restored.jpg".
```

| Option | Meaning |
|---|---|
| `-o`, `--output <path>` | Where to write the result. Default: `<file>.enc` when encrypting; the name without `.enc` (or `<file>.decrypted`) when decrypting. |
| `-f`, `--force` | Replace the result file if it already exists. Without it the program refuses. |
| `--password-stdin` | Read the password from the first line of standard input, for scripts. |

**Password rules (when locking):** at least 8 characters, typed twice. The program also tells you if the password is weak
(repeats like `aaaaaaaa`, runs like `abcdefgh`, common words like `Password1234`); a few unrelated words are best.
Unlocking applies no rules: the file's password may have been chosen elsewhere.

**Why there is no `--password` option:** a password on the command line stays in your shell history and is visible to other
users on the machine in the list of running programs. Use the hidden prompt, or for scripts pipe it in:

```bash
read -rs PW; printf '%s\n' "$PW" | ./securefile encrypt notes.txt --password-stdin; unset PW
```

**Windows PowerShell 5.1 and non-English passwords:** the program reads the piped password as UTF-8, but the old Windows
PowerShell 5.1 (the blue window built into Windows) may turn letters such as `é`, `ü` or Arabic into `?` *before* they reach
the program. The file would then be locked with a password nobody can type again. Use PowerShell 7 (`pwsh`), which pipes
UTF-8, or first run this line in the same window:

```powershell
$OutputEncoding = [Text.UTF8Encoding]::new()
```

The hidden prompt (no `--password-stdin`) is not affected.

**Ctrl+C** cancels safely: no half-written file is left behind.

**Exit codes:** `0` done · `1` failed in an expected way (wrong password, damaged or unsupported file, no permission, file in
use, result already exists) · `2` wrong usage · `130` cancelled. Messages go to standard error; results go to standard output.

## What I would love to hear

Reply to the person who sent you this. Short answers are great.

1. Which system and version (for example "Ubuntu 24.04, x64" or "macOS 15, M2")? Did the one-time steps above work?
2. Did the warning for an unsigned program appear? What did it say?
3. Was anything confusing in the messages or the options? Did the password prompt behave (hidden, Backspace works)?
4. Did locking and unlocking a real file give back exactly your file? (`cmp original restored` or compare checksums.)
5. Does a file locked on one system unlock on another (for example locked on Windows, unlocked on a Mac)?
6. Anything missing: folders, many files at once, a window instead of a command line?

Things worth trying: a very large file, a file name with spaces or Arabic or emoji, a wrong password on purpose, Ctrl+C
during a big file, and a different system than your own.

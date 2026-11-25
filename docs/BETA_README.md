# SecureFile (beta): thank you for testing

SecureFile locks a file with a password so nobody else can open it, and unlocks it again. Nothing is uploaded: it works
completely on your computer and needs no internet.

**This is an early test version.** Please read the three safety notes first.

## Before you start: three safety notes

1. **Test on copies, not on your only copy.** SecureFile never changes your original file (it makes a new `.enc` file next
   to it), but this is a beta, so keep a backup of anything you care about.
2. **If you forget the password, the file cannot be recovered. Nobody can reset it,** not even the author. Use a password
   you will remember, and try one test file first.
3. **Windows or your antivirus may warn you.** The app is not digitally signed yet, so Windows SmartScreen can say
   "Windows protected your PC" (click **More info**, then **Run anyway**). A few antivirus programs also dislike programs that
   encrypt files, because ransomware does too. Check that the zip you got matches the code you were sent (see "Check the
   download" below). If your antivirus blocks it, please tell me which one; do not turn it off.

## Which computers

| Your computer | In this beta |
|---|---|
| **Windows 10 or 11, 64-bit** (almost every PC) | **Ready.** Use the `win-x64` zip and follow "How to run it" below. |
| **Windows 11 on an ARM PC** (Surface Pro X, Copilot+ PC; Settings → System → About says "ARM-based processor") | **Built but never run.** Use the `win-arm64` zip. It was made on another kind of PC and has not been tried on real ARM hardware, so please tell me if it starts. (The `win-x64` zip also runs on ARM PCs, a little slower.) |
| **macOS** and **Linux** | **Not in this release.** A desktop version for both is planned as the next step (a window, like the Windows one). Tell me your system and I will send you the first build when it exists. Your opinion on the idea still helps. |

## How to run it

1. Unzip the folder anywhere (for example your Desktop).
2. Double-click **SecureFile.exe**. There is nothing to install. Windows 10 or 11, 64-bit (x64 or ARM).

## Try it in one minute

**Lock a file**
1. Click **Browse…** (or drop a file on the dashed box) and choose a test file.
2. Type a password of at least 8 characters, then type it again in **Confirm password**. The bar shows how strong it is.
3. Click **Encrypt file**, choose where to save (the suggested name `yourfile.ext.enc` is fine), and wait for the green message.

**Unlock it again**
1. Browse to the `.enc` file you just made.
2. Type the same password and click **Decrypt file**. Save it under a new name and open it: it should be exactly your file.

Useful keys: **Ctrl+O** browse, **Enter** in the password box starts the action, **Esc** cancels, **F1** About.

## What I would love to hear

Please answer as honestly as you like, short answers are great. Reply to the person who sent you this.

1. How long until you had locked your first file? Did you need help? Where did you hesitate?
2. Was anything confusing: a word, a button, a message?
3. Did anything go wrong (a crash, a wrong message, a file that would not open, something that looked broken)?
   What did you click just before? A screenshot helps a lot.
4. Did Windows or your antivirus warn you? Which warning, which antivirus?
5. Did the password rules (8 characters, confirm box) feel right, too strict, or too loose?
6. Would you use it for real files? What is missing?
7. Your computer: Windows, macOS or Linux (and which version), screen size, light or dark mode; and what files you tried
   (types and sizes). If you are on macOS or Linux: what would you need from a version for your system?
8. A score from 1 (bad) to 10 (great), and one sentence why.

Things I especially want to try: a very large file (over 1 GB), a file with an Arabic or emoji name, a USB stick or network
folder, a wrong password on purpose, closing the window while it is working, and dark mode.

## Check the download

The person who sent you the zip also sent a long code (SHA-256). To check that your copy is the same, open PowerShell in the
folder with the zip and run:

```powershell
Get-FileHash .\SecureFile-0.1.0-beta1-win-x64.zip -Algorithm SHA256
```

The `Hash` line must match the code you were given. If it does not, do not run it and tell me.

## Where things are

- Your locked files: wherever you saved them, named `something.enc`.
- A small log of errors (never your password or file contents): `%LOCALAPPDATA%\SecureFile\logs\securefile.log`. If you
  report a problem, you may attach it; it contains file paths, so look at it first.
- To remove the app: delete the folder. It writes nothing else.

Thank you!

# SecureFile for macOS and Linux (preview)

SecureFile encrypts and decrypts files on this computer with AES-256 and a password. Nothing is sent over the network.
**If you forget the password, the file cannot be recovered.** Files encrypted here open in the Windows app and in the
command-line version, and the other way round.

This is a **preview** build: the window is tested automatically (without a screen) on Windows, Ubuntu and macOS in the
project's CI, but it has not yet been tried by hand on a real Mac or Linux desktop. Please report anything odd.

## Check the download

Compare the SHA-256 of the archive with the one published next to it:

```
shasum -a 256 SecureFile-*.tar.gz      # macOS
sha256sum SecureFile-*.tar.gz          # Linux
```

## macOS (Apple silicon: `osx-arm64`, Intel: `osx-x64`)

1. Double-click the `.tar.gz` to unpack it, then drag `SecureFile.app` into Applications.
2. The app is **not signed or notarized by Apple** yet, so the first start is refused ("cannot be opened" or "is
   damaged"). To allow it once, either:
   - open **System Settings → Privacy & Security**, scroll down, and choose **Open Anyway** next to SecureFile; or
   - in Terminal: `xattr -dr com.apple.quarantine /Applications/SecureFile.app`
3. Needs macOS 14 (Sonoma) or newer.

## Linux (`linux-x64`, `linux-arm64`)

```
tar -xzf SecureFile-<version>-linux-x64.tar.gz
cd SecureFile-<version>-linux-x64
./SecureFile
```

Needs a graphical desktop (X11, or Wayland with XWayland) and the usual desktop libraries (`libICU`, `fontconfig`); all
common desktop distributions have them. The Open and Save dialogs come from your desktop (GTK or the desktop portal).

## Where the log is

Errors are written only on this computer: `~/Library/Application Support/SecureFile/logs` (macOS) or
`~/.local/share/SecureFile/logs` (Linux). The log never contains passwords or file contents.

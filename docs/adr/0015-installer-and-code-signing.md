# ADR-15: Installer and code signing for Windows releases

- **Status:** **Proposed** (the decision is the user's; nothing has been bought, registered or uploaded)
- **Date:** 2026-11-25

## Context

SecureFile ships today as a self-contained single-file `SecureFile.exe` (134 MB, win-x64; 144 MB win-arm64) in a zip, with a
SHA-256 file (ADR-11, plan Steps 6.1 and 6.5). Nothing is installed; the app writes only the output files the user chooses, a hidden temp file next to them, its log folder, and the .NET runtime's self-extract cache under `%TEMP%`.
The exe is **not signed**, so Windows SmartScreen says "Windows protected your PC" and some antivirus programs distrust it
(it encrypts files, which ransomware also does). The audience is friends and a Team Lead now, possibly strangers later.
Constraints: one developer, no budget approved, a private repository, no ARM test machine, no company legal entity known.

Two separate questions: **how it is delivered** (a, b, c) and **whether and how it is signed** (d). Prices and eligibility
rules below are from memory of the vendors' public pages and change often: **verify them before spending money.**

## Options considered

### Delivery

| Option | Pros | Cons |
|---|---|---|
| **(a) Zip + SHA-256 only** (today) | Nothing to build or maintain; no install rights needed; uninstall = delete the folder; nothing left behind; fits "no telemetry, no network"; easy to test in Sandbox | No Start-menu entry, no update mechanism, user must unzip; the SmartScreen warning is the first thing a user sees; 56 MB download |
| **(b) MSIX package** | Clean install/uninstall, Start-menu entry, auto-update possible via App Installer, per-user | **Must be signed** with a certificate the PC trusts (self-signed needs the certificate installed first, which is worse for friends); packaging-project and manifest to maintain; limited to Windows 10 1809+; WPF + single-file needs care; users cannot just double-click an exe |
| **(c) Inno Setup or WiX installer** | Familiar setup wizard, shortcut, uninstaller, per-user install without admin possible (Inno); free tools | More to maintain and test (install, upgrade, uninstall paths); an installer exe is flagged by SmartScreen just like an unsigned exe; adds a second artifact to sign; little benefit for a one-file app |

### Code signing

| Option | Cost (verify) | Pros | Cons |
|---|---|---|---|
| **Azure Artifact Signing** (formerly Trusted Signing) | About US$10/month | Cheapest real signature; Microsoft-run, short-lived certificates, no hardware token; works in CI | Identity validation is required; availability was limited to some countries and to organizations or (in some regions) individuals, so **it may not be open to this user**; needs an Azure subscription and a signing step in `publish-*.ps1`; signing alone does not remove SmartScreen warnings on day one (reputation builds) |
| **OV certificate** (Sectigo, DigiCert, SSL.com ...) | About US$100-250 per year | Works anywhere; removes the "Unknown publisher" wording | Since 2023 the private key must be on a hardware token or cloud HSM (more cost, postage, a USB token to guard); SmartScreen reputation still has to be earned |
| **EV certificate** | About US$300-600 per year | Strongest identity check | Highest cost and paperwork; reportedly Microsoft no longer promises instant SmartScreen reputation for EV (verify), so the main selling point has weakened; needs a registered organization |
| **Self-signed certificate** (tests only) | Free | Good to test the signing step in the scripts and the MSIX path | Not trusted on any other PC; SmartScreen still warns; must **never** be shipped to friends as "signed" |
| **Unsigned + SHA-256 + docs** (today) | Free | Honest, simple | Warning on every new download until reputation exists; some antivirus programs may block |

## Decision (proposed)

1. **Delivery: (a) zip + SHA-256 for every release.** No installer, no MSIX, for now. A portable, self-contained exe is the
   simplest thing that a non-technical person can run and remove, and it needs no admin rights. Revisit (c) only if users
   ask for a Start-menu shortcut or auto-update; revisit (b) only after a real certificate exists.
2. **Signing now: none for the friends beta.** Keep the README's SmartScreen and SHA-256 instructions (Step 6.4).
3. **Signing for a public release: try Azure Artifact Signing first** (cheapest, no hardware). If the user's country or
   status is not eligible, an OV certificate in a cloud HSM is the fallback; EV is not worth it for a hobby/learning project.
4. A **self-signed certificate** may be used locally to test the signing step in the scripts, never in a release.
5. Signing, when it exists, must sign `SecureFile.exe` and `securefile.exe` **before** the zip and the SHA-256 are made, and
   the SHA-256 file stays (it also proves the zip was not changed after signing).

**What is needed from the user to go further:** a yes/no on spending about US$10/month (or US$100-250/year), proof of
identity for the validation, and a check of the current eligibility page. Nothing in this repository depends on the answer.

## Consequences

- Easier: no installer to test or maintain; the Sandbox test (Step 6.2) covers the whole delivery path.
- Harder: users keep seeing the SmartScreen warning until a signature and some download reputation exist; there is no
  automatic update, so every fix needs a new download and a new SHA-256.
- Later: add a signing step to `scripts/publish-beta.ps1` (for example `Azure.CodeSigning` via `signtool` with the dlib),
  secrets stay out of the repository (CI secret or interactive login); re-run Step 6.2 on the signed build.
- macOS and Linux have their own story (notarization needs a paid Apple Developer account); out of scope until Step 7.

## References

- Azure Artifact Signing: https://azure.microsoft.com/en-us/products/artifact-signing
- Microsoft SmartScreen reputation and code signing (Microsoft Learn, search "SmartScreen reputation for Windows app developers")
- CA/Browser Forum baseline requirements for code signing (hardware-protected keys since June 2023)
- Inno Setup https://jrsoftware.org/isinfo.php, WiX https://wixtoolset.org, MSIX https://learn.microsoft.com/windows/msix/
- Plan §Stage 6 rows 6.3 and the SmartScreen risk row; ADR-11 (self-contained single-file exe)

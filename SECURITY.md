# Security Policy

SecureFile encrypts files with AES-256-CBC and authenticates them with HMAC-SHA256 (encrypt-then-MAC), using keys
derived from a password with PBKDF2-HMAC-SHA256 and HKDF. The design and its reasons are in
[docs/FINAL_GUIDE.md](docs/FINAL_GUIDE.md) and the [architecture decision records](docs/adr/README.md).

## Supported versions

| Version | Supported |
|---|---|
| `0.1.0-beta3` (latest pre-release) and `main` | ✅ |
| `0.1.0-beta2`, `0.1.0-beta1` | ❌ (kept for the record; please update) |

## Reporting a vulnerability

Please **do not** open a public issue for a security problem. Report it privately with GitHub's
**[Report a vulnerability](https://github.com/Abdelrahman-2222/SecureFile/security/advisories/new)** form (Security tab),
or contact the maintainer through their GitHub profile ([@Abdelrahman-2222](https://github.com/Abdelrahman-2222)).
Include:

- what you found and where (file, line, or file-format field),
- how to reproduce it (a small sample file or steps),
- what an attacker could do with it.

You will get an answer within 7 days. Fixes are made on a branch, checked by the full quality gate and CI, and
noted in [CHANGELOG.md](CHANGELOG.md) once released.

## Scope

In scope: the encrypted file format, key derivation, encryption/decryption code, and handling of passwords and keys
in memory. Out of scope: a weak password chosen by the user, and attacks that need full control of the running
machine (a keylogger or memory dump while the app is open).

## Known limits (see [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md))

These are stated limits, not hidden ones; the full STRIDE table gives the mitigation and the test for every threat.

- The password also exists briefly as a .NET string, which cannot be wiped; a memory dump or crash dump while the app
  runs can reveal it. Keys and the byte copies of the password are zeroed.
- Plaintext is written to a hidden temp file in the output folder and renamed on success. After a hard crash a hidden
  `.~<name>.<8 hex>.securefile-tmp` file can remain, and an antivirus program may keep its own copy (an upper-case
  `...SECUREFILE-TMP.tmp`). There is no secure delete
  (SSDs cannot guarantee it); the original file is never deleted for you.
- The output folder must be trusted: another program with write rights there can create or replace files.
- Programs are not code-signed yet. Compare the SHA-256 shown with each release before running it.
- PBKDF2 is not memory-hard; a weak password can be guessed offline whatever the code does.
- Only file format version 1 exists; the version byte allows a stronger format later.

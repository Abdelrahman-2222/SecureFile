# Security Policy

SecureFile encrypts files with AES-256-CBC and authenticates them with HMAC-SHA256 (encrypt-then-MAC), using keys
derived from a password with PBKDF2-HMAC-SHA256 and HKDF. The design and its reasons are in
[docs/PROJECT_PLAN.md](docs/PROJECT_PLAN.md) (file format §5, ADRs).

## Supported versions

| Version | Supported |
|---|---|
| Latest commit on `main` | ✅ |
| Anything older | ❌ |

## Reporting a vulnerability

Please **do not** open a public issue for a security problem. Contact the maintainer privately through their
GitHub profile ([@Abdelrahman-2222](https://github.com/Abdelrahman-2222)) and include:

- what you found and where (file, line, or file-format field),
- how to reproduce it (a small sample file or steps),
- what an attacker could do with it.

You will get an answer within 7 days. Fixes are made on a branch, checked by the full quality gate and CI, and
noted in [CHANGELOG.md](CHANGELOG.md) once released.

## Scope

In scope: the encrypted file format, key derivation, encryption/decryption code, and handling of passwords and keys
in memory. Out of scope: a weak password chosen by the user, and attacks that need full control of the running
machine (a keylogger or memory dump while the app is open).

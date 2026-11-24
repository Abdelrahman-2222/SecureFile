# References (self-learning evidence)

Every source used to research and build SecureFile, grouped by topic, with the decision it supports.
Updated at every step. (Graded criterion E2 — "Self-Learning".)

## .NET cryptography

| Source | Used for |
|---|---|
| [Cross-platform cryptography in .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography) | AES-CBC, HMAC-SHA256 and SHA-2 are available on Windows (CNG), Linux (OpenSSL) and macOS; AES-GCM tag size and AES-CCM limits (Phase 5) |
| [.NET cryptography model](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptography-model) | Use factory methods (`Aes.Create()`); recommended algorithms per purpose; `Rfc2898DeriveBytes.Pbkdf2` |
| [Cryptographic services overview](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-services) | CBC as the default mode; IV is not secret; ECB is insecure |
| [System.Security.Cryptography API (.NET 10)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography?view=net-10.0) | API reference for every crypto type used |
| [.NET 10 RC1 libraries release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/preview/rc1/libraries.md) | What is new in .NET 10 cryptography (post-quantum APIs, AES-KWP) |
| *.NET 10 Cryptography: A Practical Beginner's Guide* (provided document) | Concepts: AEAD, HMAC, PBKDF2 vs HKDF, nonce/IV rules, common mistakes |

## Standards and security guidance

| Source | Used for |
|---|---|
| NIST SP 800-38A — *Recommendation for Block Cipher Modes of Operation* | CBC mode and the CBC-AES256 known-answer test vectors (F.2.5 / F.2.6) |
| RFC 2104 (HMAC), RFC 4231 (HMAC-SHA256 test vectors) | Integrity seal and its test vectors |
| RFC 8018 (PBKDF2), RFC 7914 §11 (PBKDF2-HMAC-SHA256 test vectors) | Password-based key derivation and its test vectors |
| RFC 5869 (HKDF) | Splitting one master key into the encryption and authentication keys (ADR-05) |
| [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) | 600,000 iterations for PBKDF2-HMAC-SHA256; PBKDF2 preferred when FIPS-140 is required (read 2026-11-24) |

## Exception handling

| Source | Used for |
|---|---|
| [Best practices for exceptions](https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions) | ADR-13: throw helpers, catch only what you can handle, `throw;`, custom exception rules |
| [Application.DispatcherUnhandledException (WPF)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-10.0) | UI-thread safety net |
| [AppDomain.UnhandledException](https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception?view=net-10.0) | Other-thread safety net (log only) |
| [TaskScheduler.UnobservedTaskException](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0) | Forgotten-task safety net |
| [Application.ThreadException (WinForms)](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.threadexception?view=windowsdesktop-10.0) | WinForms comparison |
| [FileInfo constructor](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.-ctor?view=net-10.0) / [FileInfo.Length](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.length?view=net-10.0) | Exceptions of the file details reader |

## Tooling and quality

| Source | Used for |
|---|---|
| [Visual Studio 2026 release notes](https://learn.microsoft.com/visualstudio/releases/2026/release-notes) | IDE features; code coverage in Community/Professional |
| [Improve code quality (Visual Studio)](https://learn.microsoft.com/en-us/visualstudio/test/improve-code-quality) | Code coverage, code metrics |
| [CA1502](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1502) / [CA1505](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1505) | Complexity and maintainability limits (G11) |
| [Trimming incompatibilities](https://learn.microsoft.com/dotnet/core/deploying/trimming/incompatibilities) | WPF cannot be trimmed; Avalonia can (ADR-01) |
| [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms) | Cross-platform bonus |
| [Azure Artifact Signing](https://azure.microsoft.com/en-us/products/artifact-signing) | Code signing option (Stage 6) |
| [Velopack](https://www.nuget.org/packages/velopack) | Installer/update option (Stage 6) |
| xUnit v3, CsCheck, NetArchTest, Stryker.NET, ReportGenerator, gitleaks (project pages) | Tests, property tests, architecture tests, mutation testing, coverage reports, secret scanning |

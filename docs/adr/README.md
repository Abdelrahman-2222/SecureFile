# Architecture Decision Records (ADRs)

An ADR is a short note that records **one important decision**: the situation, the options we compared,
what we chose, and what follows from it. They let anyone (including a Team Lead) see *why* the code looks
the way it does.

The decisions below are summarised in [PROJECT_PLAN.md §4](../PROJECT_PLAN.md#4-key-decisions-with-alternatives).
Each one gets its own file (using [0000-template.md](0000-template.md)) when the step that implements it is built.

| ADR | Decision | Status | Implemented in |
|---|---|---|---|
| ADR-01 | WPF for the required app, Avalonia as a cross-platform bonus | Accepted | Stage 0 / 7 |
| ADR-02 | The AES key is derived from a user password | Accepted | Stage 1.3 |
| ADR-03 | PBKDF2-HMAC-SHA256, 600,000 iterations | Accepted | Stage 1.3 |
| ADR-04 | AES-256-CBC + HMAC-SHA256 (encrypt-then-MAC) | Accepted | Stage 1.5–1.6 |
| ADR-05 | PBKDF2 → master key → HKDF split into two keys | Accepted | Stage 1.3 |
| ADR-06 | Streaming with CryptoStream and a reused buffer | Accepted | Stage 1.5 |
| ADR-07 | Decrypt to a temp file, verify, then atomic move | Accepted | Stage 1.6 |
| ADR-08 | CommunityToolkit.Mvvm for MVVM | Accepted | Stage 2 |
| ADR-09 | Pure dependency injection in `App.OnStartup` | Accepted | Stage 0 / 3.1 |
| ADR-10 | xUnit v3 on Microsoft.Testing.Platform | Accepted | Stage 0 |
| ADR-11 | Self-contained single-file `.exe` | Accepted | Stage 6 |
| ADR-12 | No telemetry, no network calls | Accepted | All |
| [ADR-13](0013-exception-handling.md) | Exception handling in three layers (Core throws, ViewModel handles expected, global safety nets) | Accepted | Steps 1.7, 2.6, 3.1 |

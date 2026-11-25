# SecureFile — Threat model (STRIDE)

Plan §8.1: every threat has a mitigation or a stated limit. Written in Step 5.4 from the code and tests as they are; it
is not a Microsoft Threat Modeling Tool file (that tool is a GUI; see the user checklist in `QUALITY_REPORT.md`).

## 1. What is protected, from whom

| Item | Detail |
|---|---|
| Assets | (A1) the content of the user's file; (A2) the password; (A3) the integrity of `.enc` files and of the released programs; (A4) availability of the user's data (no data loss) |
| Attacker 1 | Someone who gets a copy of an `.enc` file and has unlimited time (offline guessing, tampering) |
| Attacker 2 | Someone who hands the user a crafted `.enc` file or path (malicious input) |
| Attacker 3 | Another program or user on the same PC (temp files, memory, logs, clipboard) |
| Attacker 4 | Someone who tampers with the download or the build chain (supply chain) |
| Out of scope | An attacker who already controls the running PC (keylogger, malware with the user's rights, memory dump while the app runs), physical attacks, forensic recovery of deleted plaintext on SSDs, replay or rollback (swapping in another valid `.enc` made with the same password: the tag binds no file identity) |

Design in one line: AES-256-CBC with PKCS7 padding, HMAC-SHA256 over header and ciphertext (encrypt-then-MAC), keys from
PBKDF2-SHA256 (600,000 iterations, random 16-byte salt) split with HKDF into an encryption key and an authentication key
(`docs/PROJECT_PLAN.md` §5, `docs/adr`). Format version 1 only.

## 2. STRIDE table

Legend: S spoofing, T tampering, R repudiation, I information disclosure, D denial of service, E elevation of privilege.

| # | Asset / threat | STRIDE | Mitigation (code and test) | Residual risk or stated limit |
|---|---|---|---|---|
| 1 | **Malicious `.enc`: absurd iteration count** (a 2 billion iteration header makes the app hang) | D | The header reader rejects iteration counts outside 100,000-10,000,000 before any key derivation (`EncryptedFileFormat.IsSupportedIterationCount`). Tests: `Read_IterationsOutOfRange_ThrowsDamaged`, `Read_IterationsAtLimits_IsAccepted`, `Iterations_DefaultAndLimits_MatchTheSecurityDecision` | The maximum (10 M) still costs about 6 s of CPU; accepted. A file with a low iteration count (100,000) is accepted and weakens only that file |
| 2 | **Truncated, extended or random `.enc`** (size and field attacks, memory exhaustion) | D, T | Minimum length checked first (89 bytes); the file is streamed in 1 MiB chunks, never loaded whole; the HMAC is verified over the whole file before the result is committed (renamed); until then the plaintext sits only in the hidden temp file (verify-then-commit, review R1). Tests: `DecryptAsync_FileCutShortOrExtended_ThrowsAndLeavesNothing`, `Read_RandomBytes_ReturnsHeaderOrThrowsOnlyInvalidEncryptedFile`, `Read_SignatureFollowedByRandomBytes_...`, forbidden-pattern scan "no whole-file reads in Core"; memory flat at about 27 MB for 1 GiB (QUALITY_REPORT 5.2) | A huge file still takes proportional time |
| 3 | **Tampering with header, salt, IV, ciphertext or tag** | T | Encrypt-then-MAC: the tag covers header and ciphertext and is checked (constant-time) before the result is committed, so a flipped bit is always detected and no output file appears (unverified plaintext exists only in the hidden temp file until then; review R1). Tests: `DecryptAsync_OneBitFlipped_ThrowsAndLeavesNothing` (magic, version, iterations, salt, IV, first/middle/last block, tag), `DecryptAsync_ValidTagButInvalidPadding_IsDamagedAndLeavesNothing` | None known for format v1. Someone who knows the password can of course create valid files |
| 4 | **Wrong-password oracle / padding oracle** (telling a wrong password from a damaged file, or learning plaintext from padding errors) | I | Padding is only examined after the tag verified, so there is no padding oracle. A wrong password and a damaged file give the same `AuthenticationFailedException` ("wrong password or damaged file"). Tests: `Decrypt_WrongPassword_FailsWithAPlainMessageAndWritesNothing`, `DecryptAsync_WrongPassword_ThrowsAuthenticationFailedAndLeavesNothing` | The signature, version and length errors differ from the tag error on purpose (the file is simply not ours); they reveal no secret |
| 5 | **Offline password guessing** | I | PBKDF2-SHA256 with 600,000 iterations (OWASP 2023 value for SHA-256) and a random salt per file: about 0.36 s per guess on this machine (QUALITY_REPORT 5.1). Test: `EncryptAsync_FixedSaltAndIv_MatchesIndependentlyComputedGoldenFile`, RFC 7914 known answers. Strength meter warns about weak passwords | A weak password is breakable whatever the code does. PBKDF2 is not memory-hard; GPUs guess faster than a CPU. Argon2id is listed in `SUGGESTIONS.md` (§2.1) as a future format version |
| 6 | **Same salt or IV reused** | I | Fresh random salt and IV from the OS CSPRNG for every encryption. Test: `CreateNew_TwoHeaders_HaveDifferentRandomSaltsAndIvs`, T-03 (same file twice gives different output) | None |
| 7 | **Plaintext left in a temp file** (while writing, after a crash) | I | Output is written to a hidden temp file in the destination folder and renamed only after success (`AtomicFileWriter`); on failure the temp file is deleted, so the output folder holds nothing. Tests: `DecryptAsync_WhileWriting_TheTempFileIsHiddenOnWindows`, `...LeavesNothing` family, `WriteAsync_ContentWriterThrows_PassesTheExceptionOnAndLeavesNothing`, `CreateTempFilePath_RandomPart_IsAlwaysLowercaseHex` | Decryption streams unverified plaintext into the temp file and only the final rename waits for the tag, so a crafted file can write up to its own size of garbage there before it fails (the file is emptied and deleted on failure). On Windows the temp file inherits the folder permissions (hidden is cosmetic); on Linux/macOS it is mode 0600. After a hard crash or power loss a decrypted `.~<name>.<8 hex>.securefile-tmp` can remain (hidden, random name). Not wiped (no secure delete on SSDs, see 17) |
| 8 | **Antivirus holds or copies the temp file** (Kaspersky makes `.~OUTPUT.*.SECUREFILE-TMP.tmp` (upper case, the antivirus copy) backups) | I, D | Deleting the temp file is retried (commit `fix(core): retry deleting a temp file that a scanner holds`). Test: the `LeavesNothing` tests | An antivirus's own backup copy of a plaintext temp file is outside our control (about 1 full test run in 4 fails here because of it). Users who care should exclude the folder or not use such scanners on secrets |
| 9 | **Password in memory** | I | The password is turned into bytes in pinned arrays that are zeroed straight after key derivation; both keys are pinned and overwritten on `Dispose` (`KeyDerivation`, `FileKeys`). Test: `Dispose_OverwritesBothKeysWithZeros` | The password also exists as a .NET `string` (the `PasswordBox`/CLI input), which cannot be wiped and may reach the page file or a crash dump. A memory dump of the running app can reveal it. Stated limit |
| 10 | **Password in logs, error messages, clipboard, command line** | I | The CLI has no `--password` option on purpose (it would sit in shell history and the process list); input is a hidden prompt or `--password-stdin`. Error text never contains the password; the local error log records exception types and messages without it. The "Show password" box is a display switch only: copying from the revealed box is blocked. Tests: `Encrypt_ExpectedFailure_LogsItAndShowsThePlainMessage` and the CLI argument tests | The clipboard is not touched by the app. `--password-stdin` is visible to whoever can read the calling script. Review R1-style finding recorded in 5.4 review |
| 11 | **Path tricks: output is the input, overwrite** | T, D | Input and output being the same file is refused (`IsSameFile`, ignoring case and spelling) in the app and the CLI (the CLI helper has the test; Core has none, backlog C15); an existing output is never overwritten without confirmation or `--force`; a failed run keeps the existing file. Tests: `IsSameFile_IgnoresCaseAndTheWayThePathIsWritten`, `EncryptAsync_ExistingOutputWithoutOverwrite_ThrowsAtOnceAndKeepsTheFile`, `DecryptAsync_WrongPasswordWithOverwriteAllowed_KeepsTheExistingFile` | Core's check (`FileIdentity.AreSameFile`, Stage 9.0) follows every link and ignores case and Unicode normalisation, so a folder link or case alias of the input is refused (tests: `FileIdentityTests`). A hard link needs no check: the rename replaces only that name. Not detected: bind mounts. Backlog C14 (done), review R2 |
| 12 | **Symbolic links** | T, I | The size and name shown for a selected link come from the final target; a missing target is an error. Tests: `Read_SymbolicLinkToFile_ReturnsTargetSizeButLinkName`, `Read_ChainOfSymbolicLinks_ReturnsSizeOfFinalTarget`, `Read_SymbolicLinkToMissingFile_ThrowsFileNotFoundException` | An output path that is itself a link is replaced by the rename (the link, not its target, is replaced); see 13 |
| 13 | **TOCTOU on the output** (someone swaps the destination between our check and the rename) | T | The "exists" check is repeated by the final `File.Move(..., overwrite)` so the decision is made at the moment of the rename; the temp file lives in the same folder, so the rename is atomic on one volume | Another process with write rights to the destination folder can still create or replace files there; the output folder must be trusted. Stated limit |
| 14 | **Unsigned binaries, antivirus false positives, fake downloads** | S, T | Releases carry SHA-256 files (`SHA256SUMS.txt`, `.zip.sha256`) and the README tells users to compare. Reproducible build settings (`Deterministic`, CI normalised paths) | No code signing yet (Stage 6 decision): SmartScreen/Gatekeeper warnings, and a user who does not check the hash cannot tell a fake from the real file |
| 15 | **Supply chain: build tools and packages** | T, E | GitHub Actions pinned to commit SHAs; Dependabot weekly for NuGet and Actions; NuGet Audit on every restore (`NuGetAuditMode=all`, level low); `dotnet list package --vulnerable --include-transitive` gate in `verify.ps1`; central package versions with transitive pinning; gitleaks in CI | Pinned SHAs and Dependabot still trust the publisher at the moment of the bump. Packages are not signed-verified beyond NuGet's defaults |
| 16 | **Release integrity and provenance** | T, R | SHA-256 of every archive, built by a script from a tagged commit (the release notes name the commit) | No signature and no build attestation; the hash and the archive come from the same place, so they protect against corruption and mirrors, not against a hijacked release page |
| 17 | **Plaintext recovery after deletion** | I | Out of scope by design: the app never deletes the user's original; it says so | Original and decrypted files stay on disk until the user deletes them; SSDs cannot guarantee secure deletion |
| 18 | **Algorithm agility / weakness found later** | I | The header has a version byte; unknown versions are rejected with a clear message, so a stronger format can be added without silently misreading old files. One design chosen by the app, never by the user or at random (`SUGGESTIONS.md` §2.1). Tests: version/signature tests in `EncryptedFileHeader` | Only format v1 exists; migrating files means decrypting and encrypting again |
| 19 | **Repudiation / audit** | R | A local error log records failures (no secrets, rotated at a size limit). No telemetry; nothing leaves the PC | There is no audit trail of who encrypted what; not a goal |
| 20 | **Elevation of privilege** | E | The app needs no administrator rights and its manifest requests none (it does not state `asInvoker` explicitly; review item to add it); no services, no network, no file associations | None |

## 3. Tools and evidence (plan §8.1)

| Tool | Result |
|---|---|
| .NET security analyzers (`AnalysisLevel=latest-all`, warnings as errors) | 0 warnings; the build fails otherwise |
| NuGet Audit / `dotnet list package --vulnerable --include-transitive` | 0 vulnerable packages (re-run in Step 5.4, see QUALITY_REPORT) |
| gitleaks (CI, full history) and the local pattern scan in `verify.ps1` | no secrets found |
| Known-answer tests (NIST SP 800-38A, RFC 7914, RFC 5869) | pass |
| Tamper and truncation tests | 100 % detected |
| CodeQL | Needs a public repository (free there, paid GitHub Advanced Security on a private one); not run |
| SonarQube for IDE | A Visual Studio extension: user task (checklist) |
| Independent security review (subagent, read-only, one pass) | Results in section 4 |

## 4. Independent security review

One independent pass by a read-only subagent (malicious file, path, user, secrets, crypto use). The crypto core was found
sound (distinct HKDF labels, tag covers header and all ciphertext, constant-time compare, padding examined only after the
tag, fresh CSPRNG salt/IV, strict UTF-8 + NFC, no password in arguments or logs). Findings and decisions:

| # | Sev | Finding | Decision |
|---|---|---|---|
| R1 | Medium (doc) | Rows 2 and 3 claimed the tag is checked before any plaintext is written; the code streams unverified plaintext to the temp file and verifies before the rename | **Fixed (doc):** rows 2, 3, 7, 8 now say verify-then-commit. A first HMAC-only pass would double the reads (decrypt already costs CPU + disk, see 5.1); not done, the temp file is hidden, emptied and deleted on failure. Recorded as the decision |
| R2 | Low-Medium | Same-file check compares path text; on Linux/macOS a directory symlink or case alias lets the rename replace the input (Windows refuses) | **Fixed (Stage 9.0):** `FileIdentity.AreSameFile` resolves links along both paths and compares without case or normalisation; tests on the Ubuntu and macOS CI. Row 11 updated |
| R3 | Low | CLI crashed with a raw `ArgumentException` for `--output <folder>` or an invalid name | **Fixed:** `CheckPaths` refuses both with a plain message (test `Encrypt_OutputIsAFolderOrInvalidName_IsReportedWithoutACrash`) |
| R4 | Low | Temp-file name written wrongly; no deterministic test of the delete retry | Name **fixed**; retry test recorded as backlog C15 |
| R5 | Low | Windows temp file/result inherit folder ACL (hidden is not protection) | **Recorded** in row 7 as a stated limit; owner-only ACL is a possible later hardening (C16) |
| R6 | Low | PowerShell 5.1 pipes non-ASCII text as `?` into `--password-stdin`, so the file is locked with a password nobody can retype | **Recorded:** backlog C17 (read stdin as UTF-8 bytes; document in CLI README) |
| R7 | Info | The error log writes `exception.ToString()` (paths, user names, no secrets), no per-entry cap | Accepted (documented in the class) |
| R8 | Info | Replay/rollback (swapping in another valid `.enc` made with the same password) is not modelled | **Added** as out of scope: the tag does not bind a file identity |
| - | Info | Row 20 said `asInvoker` but the manifest does not state it | Wording fixed; adding the explicit level is backlog C16 |

Threat-table citations were checked by the reviewer against test names; `IsSameFile_...` is a CLI test only and Core has no
test of its same-file branch (backlog C15).

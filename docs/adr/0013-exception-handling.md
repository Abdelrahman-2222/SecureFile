# ADR-13: Exception handling in three layers

- **Status:** Accepted
- **Date:** 2026-11-24

## Context

The app must never crash on a user action, never show technical errors to a non-technical user, never leave a
half-written output file, and never silently hide a real failure. Errors come from three places: bad input
(paths, passwords), the environment (disk full, access denied, file in use, drive removed) and the encrypted
file itself (wrong password, tampering, not our format).

## Options considered

| Option | Pros | Cons |
|---|---|---|
| Catch `Exception` everywhere and show "Error" | App rarely closes | Hides bugs; catches unrecoverable states; useless messages. Against Microsoft guidance ("if you can't recover, don't catch"). |
| Catch nothing | Simple | Any problem closes the app. |
| Error codes / `bool` results everywhere | No exception cost | Easy to ignore by mistake; not idiomatic .NET. |
| **Three layers (chosen)** | Precise messages for expected errors, logged safety net for unexpected ones, no silent failures | A little more structure to explain |

## Decision

1. **Core throws** precise, documented exceptions (throw helpers, predefined types, two custom types for wrong
   password/tampering and invalid format). It catches only to clean up and rethrows with `throw;`.
   `using`/`finally` guarantee no side effects after a failure.
2. **Presentation handles expected exceptions** at each user command with specific `catch` blocks (most specific first)
   and turns them into user messages through one tested `ErrorMessageProvider`. It does not catch `Exception`.
3. **The WPF app installs global safety nets** in `App.OnStartup`:
   `Application.DispatcherUnhandledException` (UI thread: log, friendly dialog, `Handled = true`),
   `AppDomain.CurrentDomain.UnhandledException` (other threads: log; cannot prevent termination),
   `TaskScheduler.UnobservedTaskException` (log, `SetObserved()`).
4. A local log file records technical details without secrets.

## Failure map (Step 1.7)

Every failure in the test catalog (PROJECT_PLAN §9) ends in exactly one documented exception type. "At the call"
means the method throws before it returns a task; "on await" means the task fails.

| Failure (test catalog) | Exception from Core | When | Layer 2 message (ADR-13 table) | Proven by |
|---|---|---|---|---|
| Wrong password (T-05) | `AuthenticationFailedException` | on await, after the whole file was read; temp file deleted | "Wrong password, or the file was changed or damaged." | `DecryptAsync_WrongPassword_…` |
| Ciphertext or tag changed (T-06) | `AuthenticationFailedException` | on await; temp file deleted | same | `DecryptAsync_OneBitFlipped_…` |
| Header changed (T-06): magic / version / iterations | `InvalidEncryptedFileException` (`NotEncryptedFile` / `UnsupportedVersion` / `Damaged`) | on await, before any key or file | Reason-specific | `DecryptAsync_OneBitFlipped_…` |
| Cut short or extended (T-07) | `InvalidEncryptedFileException(Damaged)` (length not whole blocks) or `AuthenticationFailedException` (whole blocks) | on await | "damaged or incomplete" / "wrong password or changed" | `DecryptAsync_FileCutShortOrExtended_…` |
| Not our file, empty file, future version (T-08) | `InvalidEncryptedFileException` (Reason) | on await, before any key or file | Reason-specific | `DecryptAsync_ThreeByteTextFile_…`, header tests |
| **Valid tag, invalid padding** (only a foreign writer that knows the password) | `InvalidEncryptedFileException(Damaged)`, inner `CryptographicException` | on await; temp file deleted | "damaged or incomplete" | `DecryptAsync_ValidTagButInvalidPadding_IsDamaged…` |
| **File shrank while it was read** | `EndOfStreamException` (an `IOException`) | on await; temp file deleted | generic `IOException` text ("couldn't be read or written…") | `WriteDecryptedFileAsync_InputShorterThan…` |
| Empty password (T-09) | `ArgumentException` | at the call | not reached: the Encrypt/Decrypt buttons are disabled without a password | `…_EmptyArguments_…` |
| **Password with a lone surrogate** (T-09, cannot be typed) | `ArgumentException` (`password`) | **at the call** (was: on await, before Step 1.7) | not caught in Layer 2 (an `ArgumentException` is a programming error) → Layer 3 safety net; Step 1.8 adds a ViewModel check | `…_PasswordWithLoneSurrogate_…` |
| Cancel (T-10) | `OperationCanceledException` | on await; temp file deleted | "Cancelled. No file was written." | `…_Cancelled…` |
| No permission (T-22) | `UnauthorizedAccessException` | on await; nothing left behind | "Access … was denied" | `DecryptAsync_ExistingOutputIsReadOnlyOnWindows_…`, `…_OutputFolderIsReadOnlyOnLinuxOrMacOS_…` |
| Disk full (T-23) | `IOException` (HResult `0x80070070` / `ENOSPC`) | on await; temp file emptied and deleted | "not enough free space" | cleanup: `AtomicFileWriter` writer-throws tests; message: `ErrorMessageProvider` tests; real disk: Stage 4 |
| Input is a device or pipe (`CON`) | `NotSupportedException` | on await | "This item can't be opened as a file. Choose a regular file." | ViewModel error test |

**Decision — wrap `CryptographicException`:** bad padding after a valid tag is reported as
`InvalidEncryptedFileException(Damaged)` with the original as `InnerException`. *Why:* it is wrapped at the one line
where its meaning is certain (the final block, after the tag check), so the ViewModel handles one documented Core type
instead of catching the broad `CryptographicException`, which elsewhere could mean a platform crypto failure (a bug,
which must reach Layer 3). *Alternative:* catch `CryptographicException` in the ViewModel — rejected, too broad.

**Decision — keep `EndOfStreamException` as it is:** it already is an `IOException`, and the generic disk message is
right for it (a file that shrinks while it is read is a drive or sync problem). No new type (YAGNI).

**Decision — check the password's Unicode at the call:** the lone-surrogate `ArgumentException` used to arrive only
when the task was awaited (it came from the key derivation on a thread-pool thread). It is now checked with the other
arguments, so every invalid argument fails at the call, as the interface documents.

## Consequences

- Every expected failure has a specific, tested message.
- Unexpected failures are visible (log + dialog) instead of hidden.
- Rules for all code: no empty `catch`, no `throw ex;`, no `async void` outside UI event handlers, no exceptions for
  normal control flow (use `Try…` methods for routine checks).
- WinForms note: the equivalent of the WPF UI-thread handler is `Application.ThreadException` with
  `Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)`, set before `Application.Run`.

## References

- Best practices for exceptions — https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions
- Application.DispatcherUnhandledException (WPF) — https://learn.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-10.0
- AppDomain.UnhandledException — https://learn.microsoft.com/en-us/dotnet/api/system.appdomain.unhandledexception?view=net-10.0
- TaskScheduler.UnobservedTaskException — https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler.unobservedtaskexception?view=net-10.0
- Application.ThreadException (WinForms) — https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.application.threadexception?view=windowsdesktop-10.0
- FileInfo constructor / Length — https://learn.microsoft.com/en-us/dotnet/api/system.io.fileinfo.-ctor?view=net-10.0

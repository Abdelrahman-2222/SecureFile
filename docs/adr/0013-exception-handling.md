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

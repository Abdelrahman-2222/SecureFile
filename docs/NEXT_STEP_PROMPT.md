# Next step — copy-paste prompt for a new chat

Last finished (2026-11-24): Step 1.7, Steps 2.1–2.3 (slice form). Branch `feature/stage-1-core`.

```text
Branch feature/stage-1-core (don't create a branch). Follow CLAUDE.md. Finish the vertical slice: Steps 2.6, 3.1,
then the slice wrap-up. Design is decided — don't re-explore.

2.6 — src/SecureFile.Presentation/Errors/ErrorMessageProvider.cs (public static, GetMessage(Exception)), messages =
ADR-13 Layer 2 table (grep "Layer 2" in docs/PROJECT_PLAN.md). IOException split by HResult: disk full 0x80070070,
0x80070027, ENOSPC 28; exists 0x80070050, 0x800700B7, EEXIST 17; in use 0x80070020, 0x80070021. Keep each method
≤10 complexity. In MainViewModel (src/SecureFile.Presentation/Main/MainViewModel.cs): add IErrorLog ctor param;
wrap Browse and RunFileOperationAsync in catches for OperationCanceledException, SecureFileException, IOException,
UnauthorizedAccessException, NotSupportedException → log, State=Failed, StatusMessage=message, clear SelectedFile on
Browse failure. Never catch ArgumentException/Exception. Add public ResetAfterUnexpectedError() (State back to
FileSelected/NoFile). Tests: ErrorMessageProviderTests (one per row) + 2 ViewModel error tests (fakes in
tests/SecureFile.Presentation.Tests/TestDoubles.cs: FakeFileEncryptionService.ExceptionToThrow exists).

3.1 — src/SecureFile.Wpf: FileErrorLog : IErrorLog (%LOCALAPPDATA%\SecureFile\logs\securefile.log, lock, never
throws: catch IOException/UnauthorizedAccessException → Trace.TraceWarning); WpfFileDialogService (Microsoft.Win32
Open/SaveFileDialog, OverwritePrompt); App.OnStartup = composition root + DispatcherUnhandledException (log, friendly
MessageBox, Handled=true, vm.ResetAfterUnexpectedError; fatal types or 2nd error within 5 s → shut down),
AppDomain.UnhandledException (log), TaskScheduler.UnobservedTaskException (log, SetObserved). MainWindow(vm): plain
StackPanel — Browse button, FileSummary, PasswordBox (PasswordChanged → vm.Password), Encrypt, Decrypt, ProgressBar,
StatusMessage; AutomationIds on controls. #if DEBUG Ctrl+Shift+F12 throws to test the safety net.
Integration test: tests/SecureFile.Integration.Tests — reference Presentation, remove the "--ignore-exit-code 8" lines
from its csproj; one test: MainViewModel with real FileEncryptionService + FileDetailsReader + fake dialogs encrypts
then decrypts a temp file, bytes equal.
Manual smoke (run the app, UI Automation from PowerShell is fine): encrypt, decrypt, wrong password message, Save
cancel, forced error dialog + log line. Record results.

Wrap-up: one reviewer agent (Sonnet, all §1.8 roles, read-only, diff 70abfdb^..HEAD, report ≤ 30 lines); fix
High/Medium. LEARNING_LOG: one short "Vertical slice" section (built, decisions, review table, gate table, 5
questions) — append, don't read the file. Plan rows 2.6/3.1, CHANGELOG. Replace docs/NEXT_STEP_PROMPT.md. Push
(pre-push runs verify.ps1), confirm CI green with `gh run list -L 1`.

Decisions already made (record in the log): CryptographicException after valid tag wrapped as
InvalidEncryptedFileException(Damaged); EndOfStreamException stays IOException; lone-surrogate password refused at the
call; slice uses IFileDialogService + IErrorLog only; status text instead of IMessageService; Save dialog's overwrite
prompt → overwriteExisting: true; ViewModel pre-checks input == output.
```

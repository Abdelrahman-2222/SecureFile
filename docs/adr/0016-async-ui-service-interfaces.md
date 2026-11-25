# ADR-16: Asynchronous UI service interfaces (dialogs and folder opener)

- **Status:** Accepted
- **Date:** 2026-11-25
- **Step:** 7.1 / 7.2 (Avalonia windows for macOS and Linux)

## Context

`IFileDialogService` and `IFolderOpener` (Presentation) were synchronous: `string? ShowOpenFileDialog()`. That fits WPF,
whose dialogs are modal and block. Avalonia's `IStorageProvider` (Open/Save) and `ILauncher` (open a folder) only offer
`Task`-returning methods; on Linux the dialog is drawn by another process (the desktop portal) and answers later.

Options:

1. **Keep the interfaces synchronous; block in the Avalonia implementation** (`.GetAwaiter().GetResult()`). The dialog
   needs the UI thread to run, which is the thread now blocked: a deadlock or a frozen window. Rejected.
2. **Keep them synchronous; run a nested message loop** (`Dispatcher.UIThread.MainLoop(token)`) until the task ends.
   Works, but nested loops cause re-entrancy bugs (a second click handled "inside" the first) and are discouraged by
   the Avalonia team. Rejected.
3. **Make the interfaces asynchronous** (`Task<string?> ShowOpenFileDialogAsync()`, `Task<string?>
   ShowSaveFileDialogAsync(string)`, `Task<bool> OpenContainingFolderAsync(string)`). WPF wraps its synchronous answer in
   `Task.FromResult`; the ViewModel already runs these calls inside async commands. **Chosen.**

## Decision

Option 3. The ViewModel awaits the services with `ConfigureAwait(true)` (UI thread, as before). The command names do
not change (`BrowseCommand`, `OpenFolderCommand`), so neither window's XAML changed; `OpenFolderCommand` became an
async command.

## Consequences

- The same `MainViewModel` drives WPF and Avalonia without any platform branch.
- WPF behaviour is unchanged: `Task.FromResult` completes synchronously, so the order of state changes is the same, and
  all ViewModel tests passed after only renaming the fake methods.
- An async command cannot run twice at once (CommunityToolkit default), so a second Browse click while the dialog is
  open is ignored on every platform.
- Known gap (backlog C19): on Linux the portal's Save dialog may not be modal, so the main window stays clickable while
  it is open. The operation still uses the file chosen before the dialog opened; only the screen can show another file.
- The error log (`FileErrorLog`) moved from the WPF project to Presentation at the same time, so both apps share it.

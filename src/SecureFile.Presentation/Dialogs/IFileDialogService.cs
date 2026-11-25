namespace SecureFile.Presentation.Dialogs;

/// <summary>
/// Shows the operating system's Open and Save dialogs, so the ViewModel can ask the user for files without knowing
/// which user-interface framework draws them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an interface (Dependency Inversion, ADR-09):</b> a real dialog needs a window and a person to click it. The
/// ViewModel depends on this abstraction; the WPF and Avalonia apps supply the native dialogs, and the tests supply a
/// fake that returns a prepared answer at once.
/// </para>
/// <para>
/// <b>Why asynchronous (ADR-16):</b> Avalonia's <c>StorageProvider</c> (and the Linux desktop portal behind it) only
/// offers asynchronous dialogs; waiting for them synchronously on the UI thread would freeze or deadlock the window.
/// The WPF dialogs are synchronous and simply return a completed task.
/// </para>
/// <para>
/// Both methods return <see langword="null"/> when the user cancels: cancelling a dialog is a normal choice, not an
/// error, so it is not reported with an exception (ADR-13).
/// </para>
/// </remarks>
public interface IFileDialogService
{
    /// <summary>Asks the user to choose an existing file to encrypt or decrypt.</summary>
    /// <returns>The full path of the chosen file, or <see langword="null"/> if the user cancelled.</returns>
    Task<string?> ShowOpenFileDialogAsync();

    /// <summary>
    /// Asks the user where to save a result, starting with a suggested folder and name (task requirement R5).
    /// </summary>
    /// <param name="suggestedFilePath">The full path to suggest: its folder is opened and its name is pre-filled.</param>
    /// <returns>The full path the user chose, or <see langword="null"/> if the user cancelled.</returns>
    /// <remarks>
    /// <b>Contract:</b> if the chosen file already exists, the implementation must ask the user to confirm replacing
    /// it before returning it (the native Save dialogs do this). A returned path may therefore be overwritten.
    /// </remarks>
    Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath);
}

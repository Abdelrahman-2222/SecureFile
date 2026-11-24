namespace SecureFile.Presentation.Dialogs;

/// <summary>
/// Shows the operating system's Open and Save dialogs, so the ViewModel can ask the user for files without knowing
/// which user-interface framework draws them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an interface (Dependency Inversion, ADR-09):</b> a real dialog needs a window and a person to click it. The
/// ViewModel depends on this abstraction; the WPF app supplies the native dialogs, and the tests supply a fake that
/// returns a prepared answer at once. The Avalonia app (bonus) will supply its own implementation.
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
    string? ShowOpenFileDialog();

    /// <summary>
    /// Asks the user where to save a result, starting with a suggested folder and name (task requirement R5).
    /// </summary>
    /// <param name="suggestedFilePath">The full path to suggest: its folder is opened and its name is pre-filled.</param>
    /// <returns>The full path the user chose, or <see langword="null"/> if the user cancelled.</returns>
    /// <remarks>
    /// <b>Contract:</b> if the chosen file already exists, the implementation must ask the user to confirm replacing
    /// it before returning it (the native Save dialogs do this). A returned path may therefore be overwritten.
    /// </remarks>
    string? ShowSaveFileDialog(string suggestedFilePath);
}

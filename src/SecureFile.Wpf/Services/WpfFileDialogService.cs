using System.IO;
using Microsoft.Win32;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Wpf.Services;

/// <summary>The Windows Open and Save dialogs behind <see cref="IFileDialogService"/>.</summary>
/// <remarks>
/// The Save dialog has <see cref="FileDialog.CheckPathExists"/> and <c>OverwritePrompt</c> on, which fulfils the
/// interface contract: an existing file is only returned after the user confirmed replacing it. The WPF dialogs are
/// modal and synchronous, so the answer is wrapped in a completed task (the interface is asynchronous for Avalonia, ADR-16).
/// </remarks>
internal sealed class WpfFileDialogService : IFileDialogService
{
    /// <inheritdoc />
    public Task<string?> ShowOpenFileDialogAsync()
    {
        OpenFileDialog dialog = new() { CheckFileExists = true, Title = "Choose a file" };
        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    /// <inheritdoc />
    public Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath)
    {
        return Task.FromResult(ShowSaveFileDialog(suggestedFilePath));
    }

    /// <summary>Shows the modal Save dialog (the synchronous work behind <see cref="ShowSaveFileDialogAsync"/>).</summary>
    /// <param name="suggestedFilePath">The full path to suggest.</param>
    /// <returns>The chosen path, or <see langword="null"/> if the user cancelled.</returns>
    private static string? ShowSaveFileDialog(string suggestedFilePath)
    {
        SaveFileDialog dialog = new()
        {
            OverwritePrompt = true,
            CheckPathExists = true,
            Title = "Save as",
            InitialDirectory = Path.GetDirectoryName(suggestedFilePath),
            FileName = Path.GetFileName(suggestedFilePath),
            Filter = BuildSaveFilter(Path.GetExtension(suggestedFilePath)),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Builds the "Save as type" list: the suggested file's own type first, then all files.</summary>
    /// <param name="extension">The suggested file's extension with its dot (<c>.enc</c>, <c>.txt</c>), or empty.</param>
    /// <returns>A Windows dialog filter string.</returns>
    /// <remarks>
    /// Without a filter the dialog's "Save as type" box is empty, which looks broken. The first entry is selected, so
    /// a name typed without an extension gets the suggested one.
    /// </remarks>
    private static string BuildSaveFilter(string extension)
    {
        const string allFiles = "All files (*.*)|*.*";
        if (extension.Length < 2)
        {
            return allFiles;
        }

        string label = extension[1..].ToUpperInvariant();
        return $"{label} file (*{extension})|*{extension}|{allFiles}";
    }
}

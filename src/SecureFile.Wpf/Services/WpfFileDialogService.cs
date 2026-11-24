using System.IO;
using Microsoft.Win32;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Wpf.Services;

/// <summary>The Windows Open and Save dialogs behind <see cref="IFileDialogService"/>.</summary>
/// <remarks>
/// The Save dialog has <see cref="FileDialog.CheckPathExists"/> and <c>OverwritePrompt</c> on, which fulfils the
/// interface contract: an existing file is only returned after the user confirmed replacing it.
/// </remarks>
internal sealed class WpfFileDialogService : IFileDialogService
{
    /// <inheritdoc />
    public string? ShowOpenFileDialog()
    {
        OpenFileDialog dialog = new() { CheckFileExists = true, Title = "Choose a file" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? ShowSaveFileDialog(string suggestedFilePath)
    {
        SaveFileDialog dialog = new()
        {
            OverwritePrompt = true,
            CheckPathExists = true,
            Title = "Save as",
            InitialDirectory = Path.GetDirectoryName(suggestedFilePath),
            FileName = Path.GetFileName(suggestedFilePath),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

using System.ComponentModel;
using System.Diagnostics;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Wpf.Services;

/// <summary>Shows a file in Windows File Explorer with the file selected.</summary>
/// <remarks>
/// <b>Security:</b> the path goes in as one quoted argument of <c>explorer.exe</c> (<c>/select,"path"</c>), never
/// through a shell, so a file name cannot add commands. Explorer only displays; it does not run the file.
/// </remarks>
internal sealed class ExplorerFolderOpener : IFolderOpener
{
    /// <inheritdoc />
    public bool OpenContainingFolder(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        try
        {
            using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = false });
            return process is not null;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

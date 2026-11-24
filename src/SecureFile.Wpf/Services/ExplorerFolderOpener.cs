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
    /// <remarks>
    /// <see langword="true"/> means only that Explorer was started: <c>explorer.exe</c> exits with code 1 even when it
    /// succeeded, so its exit code is not used. The argument is built as one string because Explorer wants the exact
    /// form <c>/select,"path"</c>, which <see cref="ProcessStartInfo.ArgumentList"/> would quote differently.
    /// </remarks>
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

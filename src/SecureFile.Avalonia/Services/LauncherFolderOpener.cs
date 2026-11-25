using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Avalonia.Services;

/// <summary>Opens the folder of a result in the system file manager through Avalonia's <see cref="ILauncher"/>.</summary>
/// <remarks>
/// <para>
/// <b>Why the launcher and not a process:</b> each system has its own file manager (Explorer, Finder, Nautilus, Dolphin,
/// …); the launcher asks the system to open the folder with whatever is set up, without starting a shell, so a file
/// name cannot add commands. The folder is opened, not the file: the launcher would run a file with its default app.
/// </para>
/// <para>
/// Unlike Explorer's <c>/select</c>, the file is not highlighted; the success message names it, so it is easy to find.
/// </para>
/// </remarks>
/// <param name="findWindow">Returns the window whose launcher is used, or <see langword="null"/> before it exists.</param>
internal sealed class LauncherFolderOpener(Func<TopLevel?> findWindow) : IFolderOpener
{
    /// <inheritdoc />
    public async Task<bool> OpenContainingFolderAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        string? folder = Path.GetDirectoryName(filePath);
        ILauncher? launcher = findWindow()?.Launcher;
        if (string.IsNullOrEmpty(folder) || launcher is null)
        {
            return false;
        }

        return await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder)).ConfigureAwait(true);
    }
}

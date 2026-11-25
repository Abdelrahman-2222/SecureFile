using System.Globalization;
using CommunityToolkit.Mvvm.Input;

namespace SecureFile.Presentation.Main;

/// <summary>Dropping files onto the window (review item 12): one file is used, and the user is told when there were more.</summary>
/// <remarks>A second file for the same class, only to keep each file small.</remarks>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Shows the first of the dropped files, like <see cref="OpenFileCommand"/>, and says so when several were dropped.
    /// </summary>
    /// <param name="paths">The full paths of everything dropped; <see langword="null"/> or empty is ignored.</param>
    /// <returns>A task that completes when the details are shown.</returns>
    /// <remarks>
    /// The app works on one file at a time. Silently using the first of several files made people think all of them
    /// were encrypted, so the status line names the one that is used. Its availability (<see cref="CanBrowse"/>) is
    /// also what the windows use to refuse the copy cursor while an operation runs.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task OpenFilesAsync(IReadOnlyList<string>? paths)
    {
        if (paths is not { Count: > 0 } || string.IsNullOrEmpty(paths[0]))
        {
            return;
        }

        await RunGuardedAsync(() => LoadFileAsync(paths[0])).ConfigureAwait(true);
        if (paths.Count > 1 && State == MainViewState.FileSelected && SelectedFile is not null)
        {
            StatusMessage = string.Create(
                CultureInfo.CurrentCulture,
                $"You dropped {paths.Count} files. SecureFile works on one file at a time, so only \"{SelectedFile.Name}\" is used. {StatusMessage}");
        }
    }
}

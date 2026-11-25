using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SecureFile.Presentation.Dialogs;

namespace SecureFile.Avalonia.Services;

/// <summary>The system Open and Save dialogs through Avalonia's <see cref="IStorageProvider"/>.</summary>
/// <remarks>
/// <para>
/// <see cref="IStorageProvider"/> picks the native dialog of each system: the Windows common dialogs, the macOS
/// <c>NSOpenPanel</c>/<c>NSSavePanel</c>, and on Linux the desktop portal or GTK. It works with storage items rather
/// than paths (so it also fits sandboxed systems); this app needs real paths, so a choice without one
/// (<see cref="StorageProviderExtensions.TryGetLocalPath"/> returns <see langword="null"/>) counts as cancelled.
/// </para>
/// <para>
/// The Save dialog has <see cref="FilePickerSaveOptions.ShowOverwritePrompt"/> on, which fulfils the interface
/// contract on Windows and macOS. On Linux the prompt is drawn by the portal or GTK; it is on the manual checklist.
/// </para>
/// </remarks>
/// <param name="findWindow">Returns the window that owns the dialogs, or <see langword="null"/> before it exists.</param>
internal sealed class AvaloniaFileDialogService(Func<TopLevel?> findWindow) : IFileDialogService
{
    /// <inheritdoc />
    public async Task<string?> ShowOpenFileDialogAsync()
    {
        IStorageProvider? storage = findWindow()?.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(
            new FilePickerOpenOptions { Title = "Choose a file", AllowMultiple = false }).ConfigureAwait(true);
        return TakeFirstLocalPath(files);
    }

    /// <inheritdoc />
    public async Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath)
    {
        ArgumentNullException.ThrowIfNull(suggestedFilePath);
        IStorageProvider? storage = findWindow()?.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        using IStorageFolder? startFolder = await FindFolderAsync(storage, Path.GetDirectoryName(suggestedFilePath)).ConfigureAwait(true);
        string extension = Path.GetExtension(suggestedFilePath);
        var options = new FilePickerSaveOptions
        {
            Title = "Save as",
            SuggestedFileName = Path.GetFileName(suggestedFilePath),
            SuggestedStartLocation = startFolder,
            ShowOverwritePrompt = true,
            FileTypeChoices = BuildFileTypes(extension),
            DefaultExtension = extension.Length > 1 ? extension[1..] : null,
        };
        using IStorageFile? file = await storage.SaveFilePickerAsync(options).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    /// <summary>Turns a folder path into the storage folder the dialog opens in.</summary>
    /// <param name="storage">The storage provider of the window.</param>
    /// <param name="folderPath">The folder, or <see langword="null"/> when the suggestion has none.</param>
    /// <returns>The folder, or <see langword="null"/> when there is none or it cannot be opened (the dialog then picks one).</returns>
    private static Task<IStorageFolder?> FindFolderAsync(IStorageProvider storage, string? folderPath)
    {
        return string.IsNullOrEmpty(folderPath)
            ? Task.FromResult<IStorageFolder?>(null)
            : storage.TryGetFolderFromPathAsync(folderPath);
    }

    /// <summary>Returns the path of the first chosen file and releases all of them.</summary>
    /// <param name="files">What the dialog returned (empty when cancelled).</param>
    /// <returns>The local path, or <see langword="null"/>.</returns>
    private static string? TakeFirstLocalPath(IReadOnlyList<IStorageFile> files)
    {
        string? path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        foreach (IStorageFile file in files)
        {
            file.Dispose();
        }

        return path;
    }

    /// <summary>Builds the "file type" list: the suggested file's own type first, then all files (like the WPF filter).</summary>
    /// <param name="extension">The suggested file's extension with its dot, or empty.</param>
    /// <returns>The choices shown in the Save dialog.</returns>
    private static List<FilePickerFileType> BuildFileTypes(string extension)
    {
        var allFiles = new FilePickerFileType("All files") { Patterns = ["*"] };
        if (extension.Length < 2)
        {
            return [allFiles];
        }

        string label = extension[1..].ToUpperInvariant();
        return [new FilePickerFileType($"{label} file") { Patterns = [$"*{extension}"] }, allFiles];
    }
}

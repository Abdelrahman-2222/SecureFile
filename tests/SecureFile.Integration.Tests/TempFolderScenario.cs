using CommunityToolkit.Mvvm.Input;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;
using SecureFile.Presentation.Errors;
using SecureFile.Presentation.Main;

namespace SecureFile.Integration.Tests;

/// <summary>
/// A temporary folder plus a real <see cref="MainViewModel"/> (real Core services) whose dialogs answer from a script.
/// Each test gets its own folder and removes it afterwards, so tests never see each other's files.
/// </summary>
internal sealed class TempFolderScenario : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SecureFile-" + Guid.NewGuid().ToString("N"));
    private readonly ScriptedDialogs _dialogs = new();

    public TempFolderScenario()
    {
        Directory.CreateDirectory(_folder);
    }

    /// <summary>Gets the log the ViewModel writes handled errors to.</summary>
    public CollectingErrorLog ErrorLog { get; } = new();

    /// <summary>Builds a path inside the temporary folder.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string fileName)
    {
        return Path.Combine(_folder, fileName);
    }

    /// <summary>Lists the names of all files in the folder, sorted, to prove "no partial file is left behind".</summary>
    /// <returns>The file names.</returns>
    public string[] FileNames()
    {
        return [.. Directory.GetFiles(_folder).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];
    }

    /// <summary>Creates the ViewModel with the real Core services.</summary>
    /// <returns>A ready ViewModel.</returns>
    public MainViewModel CreateViewModel()
    {
        return new MainViewModel(new FileEncryptionService(), new FileDetailsReader(), _dialogs, ErrorLog, new NoFolderOpener());
    }

    /// <summary>Chooses a file the way Browse does.</summary>
    /// <param name="viewModel">The ViewModel.</param>
    /// <param name="path">The file to choose.</param>
    /// <returns>A task that completes when the file is shown.</returns>
    public Task BrowseAsync(MainViewModel viewModel, string path)
    {
        _dialogs.Open = path;
        return viewModel.BrowseCommand.ExecuteAsync(null);
    }

    /// <summary>Runs Encrypt or Decrypt with the Save dialog answering with a given path.</summary>
    /// <param name="command">The Encrypt or Decrypt command.</param>
    /// <param name="savePath">The path the user "picks" in the Save dialog.</param>
    /// <returns>A task that completes when the operation has ended.</returns>
    public Task RunAsync(IAsyncRelayCommand command, string savePath)
    {
        _dialogs.Save = savePath;
        return command.ExecuteAsync(null);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A scanner may still hold a file for a moment; a leftover temp folder must not fail a test.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: cleanup is best effort.
        }
    }
}

/// <summary>Dialogs that answer from a script instead of showing a window.</summary>
internal sealed class ScriptedDialogs : IFileDialogService
{
    public string? Open { get; set; }

    public string? Save { get; set; }

    public Task<string?> ShowOpenFileDialogAsync()
    {
        return Task.FromResult(Open);
    }

    public Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath)
    {
        return Task.FromResult(Save);
    }
}

/// <summary>A folder opener that does nothing and says it worked.</summary>
internal sealed class NoFolderOpener : IFolderOpener
{
    public Task<bool> OpenContainingFolderAsync(string filePath)
    {
        return Task.FromResult(true);
    }
}

/// <summary>An error log that remembers what it was given.</summary>
internal sealed class CollectingErrorLog : IErrorLog
{
    public List<Exception> Exceptions { get; } = [];

    public void Write(Exception exception)
    {
        Exceptions.Add(exception);
    }
}

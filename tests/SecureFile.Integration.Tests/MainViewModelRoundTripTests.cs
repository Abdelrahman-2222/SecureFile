using System.Security.Cryptography;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;
using SecureFile.Presentation.Errors;
using SecureFile.Presentation.Main;

namespace SecureFile.Integration.Tests;

/// <summary>
/// The vertical slice end to end without a window: the real ViewModel, the real Core services and a real temporary
/// file; only the dialogs and the log are fakes.
/// </summary>
public sealed class MainViewModelRoundTripTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SecureFile-" + Guid.NewGuid().ToString("N"));

    public MainViewModelRoundTripTests()
    {
        Directory.CreateDirectory(_folder);
    }

    [Fact]
    public async Task EncryptThenDecrypt_ThroughTheViewModel_RestoresTheOriginalBytes()
    {
        string original = Path.Combine(_folder, "report.bin");
        string encrypted = original + ".enc";
        string restored = Path.Combine(_folder, "restored.bin");
        byte[] content = new byte[100_000];
        RandomNumberGenerator.Fill(content);
        await File.WriteAllBytesAsync(original, content, TestContext.Current.CancellationToken);

        var dialogs = new ScriptedDialogs();
        var viewModel = new MainViewModel(new FileEncryptionService(), new FileDetailsReader(), dialogs, new IgnoringErrorLog());

        dialogs.Open = original;
        await viewModel.BrowseCommand.ExecuteAsync(null);
        viewModel.Password = "correct horse";
        dialogs.Save = encrypted;
        await viewModel.EncryptCommand.ExecuteAsync(null);
        Assert.Equal(MainViewState.Succeeded, viewModel.State);

        dialogs.Open = encrypted;
        await viewModel.BrowseCommand.ExecuteAsync(null);
        dialogs.Save = restored;
        await viewModel.DecryptCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal(content, await File.ReadAllBytesAsync(restored, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private sealed class ScriptedDialogs : IFileDialogService
    {
        public string? Open { get; set; }

        public string? Save { get; set; }

        public string? ShowOpenFileDialog()
        {
            return Open;
        }

        public string? ShowSaveFileDialog(string suggestedFilePath)
        {
            return Save;
        }
    }

    private sealed class IgnoringErrorLog : IErrorLog
    {
        public void Write(Exception exception)
        {
        }
    }
}

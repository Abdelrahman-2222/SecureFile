using System.Security.Cryptography;
using SecureFile.Presentation.Main;

namespace SecureFile.Integration.Tests;

/// <summary>
/// The vertical slice end to end without a window: the real ViewModel, the real Core services and a real temporary
/// file; only the dialogs and the log are fakes.
/// </summary>
public sealed class MainViewModelRoundTripTests : IDisposable
{
    private readonly TempFolderScenario _scenario = new();

    [Fact]
    public async Task EncryptThenDecrypt_ThroughTheViewModel_RestoresTheOriginalBytes()
    {
        string original = _scenario.PathOf("report.bin");
        string encrypted = original + ".enc";
        string restored = _scenario.PathOf("restored.bin");
        byte[] content = new byte[100_000];
        RandomNumberGenerator.Fill(content);
        await File.WriteAllBytesAsync(original, content, TestContext.Current.CancellationToken);
        MainViewModel viewModel = _scenario.CreateViewModel();

        await _scenario.BrowseAsync(viewModel, original);
        viewModel.Password = "correct horse";
        await _scenario.RunAsync(viewModel.EncryptCommand, encrypted);
        Assert.Equal(MainViewState.Succeeded, viewModel.State);

        await _scenario.BrowseAsync(viewModel, encrypted);
        viewModel.Password = "correct horse"; // cleared after the success above, as the app does
        await _scenario.RunAsync(viewModel.DecryptCommand, restored);

        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal(content, await File.ReadAllBytesAsync(restored, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _scenario.Dispose();
    }
}

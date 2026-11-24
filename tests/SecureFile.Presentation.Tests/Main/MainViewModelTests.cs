using System.Globalization;
using SecureFile.Core.Files;
using SecureFile.Presentation.Main;

namespace SecureFile.Presentation.Tests.Main;

/// <summary>
/// Tests for <see cref="MainViewModel"/> (test catalog T-12): the main path, the state changes and the error cases,
/// with fake services, so no window and no real file is needed.
/// </summary>
public sealed class MainViewModelTests
{
    private static readonly string InputPath = Path.Combine(Path.GetTempPath(), "report.pdf");

    private readonly FakeFileEncryptionService _encryption = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeFileDetailsReader _detailsReader = new();

    // ---- Browse (Step 2.2) -----------------------------------------------------------------------------------------

    [Fact]
    public void NewViewModel_HasNoFileAndOnlyBrowseAvailable()
    {
        MainViewModel viewModel = CreateViewModel();

        Assert.Equal(MainViewState.NoFile, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
        Assert.Equal("No file selected.", viewModel.FileSummary);
        Assert.True(viewModel.BrowseCommand.CanExecute(null));
        Assert.False(viewModel.EncryptCommand.CanExecute(null));
        Assert.False(viewModel.DecryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task Browse_FileChosen_ShowsItsDetailsAndSelectsIt()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = InputPath;
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.Equal(InputPath, viewModel.SelectedFile?.FullPath);
        string size = FileSizeFormatter.Format(1536, CultureInfo.CurrentCulture); // "1.5 KB", or "1,5 KB" in German
        Assert.Equal($"report.pdf (.pdf) · {size}", viewModel.FileSummary);
        Assert.Contains(nameof(MainViewModel.FileSummary), changedProperties); // the window is told to refresh it
    }

    [Fact]
    public async Task Browse_FileWithoutExtension_SaysSo()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = Path.Combine(Path.GetTempPath(), "README");

        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.StartsWith("README (no extension) · ", viewModel.FileSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_DialogCancelled_ChangesNothing()
    {
        MainViewModel viewModel = CreateViewModel();
        string messageBefore = viewModel.StatusMessage;

        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.NoFile, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
        Assert.Equal(messageBefore, viewModel.StatusMessage);
    }

    // ---- Encrypt / Decrypt (Step 2.3) ------------------------------------------------------------------------------

    [Fact]
    public async Task Encrypt_MainPath_SuggestsDotEncSavesThereAndSaysTheOriginalIsUnchanged()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        string output = InputPath + ".enc";
        _dialogs.SaveResult = output;

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Equal(output, _dialogs.SuggestedSavePath);
        Assert.Equal([("Encrypt", InputPath, output, "secret", true)], _encryption.Calls);
        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal("Encrypted copy saved as \"report.pdf.enc\". Your original file was not changed.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData("report.pdf.enc", "report.pdf")]
    [InlineData("report.pdf.ENC", "report.pdf")]
    [InlineData("report.bin", "report.bin.decrypted")] // never suggests the input itself
    [InlineData(".enc", ".enc.decrypted")]              // nothing would be left of the name
    public async Task Decrypt_SuggestsTheNameWithoutDotEnc(string inputName, string expectedName)
    {
        string input = Path.Combine(Path.GetTempPath(), inputName);
        MainViewModel viewModel = await CreateWithFileAsync(input);
        _dialogs.SaveResult = Path.Combine(Path.GetTempPath(), "out.pdf");

        await viewModel.DecryptCommand.ExecuteAsync(null);

        Assert.Equal(Path.Combine(Path.GetTempPath(), expectedName), _dialogs.SuggestedSavePath);
        Assert.Equal("Decrypt", Assert.Single(_encryption.Calls).Operation);
        Assert.Equal("Decrypted file saved as \"out.pdf\".", viewModel.StatusMessage);
    }

    [Fact]
    public async Task EncryptAndDecrypt_NeedAFileAndAPassword()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);

        viewModel.Password = string.Empty;
        Assert.False(viewModel.EncryptCommand.CanExecute(null));
        Assert.False(viewModel.DecryptCommand.CanExecute(null));

        viewModel.Password = " "; // spaces are characters, never trimmed
        Assert.True(viewModel.EncryptCommand.CanExecute(null));
        Assert.True(viewModel.DecryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task Encrypt_WhileRunning_IsWorkingAndEveryButtonIsDisabled()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        var release = new TaskCompletionSource();
        _encryption.WaitFor = release.Task;

        Task running = viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Working, viewModel.State);
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.BrowseCommand.CanExecute(null));
        Assert.False(viewModel.EncryptCommand.CanExecute(null));
        Assert.False(viewModel.DecryptCommand.CanExecute(null));

        release.SetResult();
        await running;
        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.True(viewModel.EncryptCommand.CanExecute(null)); // ready for the next operation
    }

    [Fact]
    public async Task Encrypt_SaveDialogCancelled_DoesNothing()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Empty(_encryption.Calls);
        Assert.Equal(MainViewState.FileSelected, viewModel.State);
    }

    [Fact]
    public async Task Encrypt_OutputIsTheInput_FailsWithAMessageAndDoesNotCallCore()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath.ToUpperInvariant();

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Empty(_encryption.Calls);
        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.StartsWith("Choose a different name", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NullService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("encryptionService", () => new MainViewModel(null!, _detailsReader, _dialogs));
        Assert.Throws<ArgumentNullException>("fileDetailsReader", () => new MainViewModel(_encryption, null!, _dialogs));
        Assert.Throws<ArgumentNullException>("fileDialogs", () => new MainViewModel(_encryption, _detailsReader, null!));
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_encryption, _detailsReader, _dialogs);
    }

    /// <summary>A ViewModel with <paramref name="path"/> chosen and the password "secret" typed.</summary>
    private async Task<MainViewModel> CreateWithFileAsync(string path)
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = path;
        await viewModel.BrowseCommand.ExecuteAsync(null);
        viewModel.Password = "secret";
        return viewModel;
    }
}

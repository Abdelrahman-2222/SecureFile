using System.Text;
using SecureFile.Core.Cryptography;
using SecureFile.Presentation.Main;

namespace SecureFile.Integration.Tests;

/// <summary>
/// Real-file scenarios from the test catalog (T-21, T-24, T-26, T-28) run through the real ViewModel and the real Core:
/// they prove that what the user sees (message, state) and what is left on disk (no partial file) agree.
/// </summary>
/// <remarks>
/// Cases already covered at unit level in Core (output = input, read-only output, cancel leaves nothing) are not
/// repeated here. T-23 (disk full) cannot be produced with real files on a developer machine; T-25 (drive removed
/// mid-operation) and T-27 (more than 4 GB) are manual or nightly runs (Steps 4.2 and 4.3).
/// </remarks>
public sealed class FileSystemScenarioTests : IDisposable
{
    private const string Password = "scenario-pass";

    private readonly TempFolderScenario _scenario = new();

    [Fact]
    public async Task Encrypt_OutputAlreadyExists_ReplacesItWithTheEncryptedFileAndLeavesNoTempFile()
    {
        string input = await WriteInputAsync("data.txt", "plain text");
        string output = _scenario.PathOf("data.txt.enc");
        await File.WriteAllTextAsync(output, "old content", TestContext.Current.CancellationToken);
        MainViewModel viewModel = await SelectAsync(input);

        await _scenario.RunAsync(viewModel.EncryptCommand, output);

        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        byte[] written = await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken);
        Assert.True(EncryptedFileFormat.HasSignature(written));
        Assert.Equal(["data.txt", "data.txt.enc"], _scenario.FileNames());
    }

    [Fact]
    public async Task Encrypt_SourceLockedByAnotherProgram_ShowsTheInUseMessageAndWritesNothing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows locks open files; Linux and macOS do not.");
        string input = await WriteInputAsync("locked.txt", "plain text");
        MainViewModel viewModel = await SelectAsync(input);
        using var lockHolder = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.None);

        await _scenario.RunAsync(viewModel.EncryptCommand, _scenario.PathOf("locked.txt.enc"));

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Equal("The file is being used by another program. Close it and try again.", viewModel.StatusMessage);
        Assert.Equal(["locked.txt"], _scenario.FileNames());
        Assert.Single(_scenario.ErrorLog.Exceptions);
    }

    [Fact]
    public async Task Encrypt_SourceDeletedAfterSelection_ShowsTheNotFoundMessageAndWritesNothing()
    {
        string input = await WriteInputAsync("gone.txt", "plain text");
        MainViewModel viewModel = await SelectAsync(input);
        File.Delete(input);

        await _scenario.RunAsync(viewModel.EncryptCommand, _scenario.PathOf("gone.txt.enc"));

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Equal("The file or folder can't be found or opened.", viewModel.StatusMessage);
        Assert.Empty(_scenario.FileNames());
    }

    [Fact]
    public async Task Decrypt_WrongPassword_ShowsTheFriendlyMessageAndWritesNothing()
    {
        string input = await WriteInputAsync("secret.txt", "plain text");
        string encrypted = _scenario.PathOf("secret.txt.enc");
        MainViewModel viewModel = await SelectAsync(input);
        await _scenario.RunAsync(viewModel.EncryptCommand, encrypted);
        await _scenario.BrowseAsync(viewModel, encrypted);
        viewModel.Password = "another-pass";

        await _scenario.RunAsync(viewModel.DecryptCommand, _scenario.PathOf("secret.out.txt"));

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.StartsWith("Wrong password, or the file was changed or damaged.", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(["secret.txt", "secret.txt.enc"], _scenario.FileNames());
    }

    [Fact]
    public async Task Decrypt_PlainTextFile_SaysItIsNotAnEncryptedFile()
    {
        string input = await WriteInputAsync("notes.txt", "this is not encrypted at all, just text of some length");
        MainViewModel viewModel = await SelectAsync(input);

        await _scenario.RunAsync(viewModel.DecryptCommand, _scenario.PathOf("notes.out"));

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.StartsWith("This isn't a file encrypted by SecureFile.", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(["notes.txt"], _scenario.FileNames());
    }

    [Fact]
    public async Task Encrypt_AnAlreadyEncryptedFile_NeedsTwoDecryptionsToGetBackTheOriginal()
    {
        string original = await WriteInputAsync("layers.txt", "two layers");
        string once = _scenario.PathOf("layers.once.enc");
        string twice = _scenario.PathOf("layers.twice.enc");
        string backOnce = _scenario.PathOf("layers.back1");
        string backTwice = _scenario.PathOf("layers.back2.txt");
        MainViewModel viewModel = await SelectAsync(original);

        await _scenario.RunAsync(viewModel.EncryptCommand, once);
        await _scenario.BrowseAsync(viewModel, once);
        await _scenario.RunAsync(viewModel.EncryptCommand, twice);
        await _scenario.BrowseAsync(viewModel, twice);
        await _scenario.RunAsync(viewModel.DecryptCommand, backOnce);
        await _scenario.BrowseAsync(viewModel, backOnce);
        await _scenario.RunAsync(viewModel.DecryptCommand, backTwice);

        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal("two layers", await File.ReadAllTextAsync(backTwice, TestContext.Current.CancellationToken));
        Assert.Equal(
            await File.ReadAllBytesAsync(once, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(backOnce, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Decrypt_CommittedGoldenFile_StillGivesTheKnownText()
    {
        // GoldenFiles/hello.txt.enc was written once by format version 1 and is committed. If this test fails, the
        // file format changed in a way that would make every file users have already encrypted unreadable.
        string golden = Path.Combine(AppContext.BaseDirectory, "GoldenFiles", "hello.txt.enc");
        string output = _scenario.PathOf("hello.txt");

        await new FileEncryptionService().DecryptAsync(golden, output, "golden-sample-pass", overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Hello, SecureFile!", await File.ReadAllTextAsync(output, Encoding.UTF8, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _scenario.Dispose();
    }

    private async Task<string> WriteInputAsync(string name, string text)
    {
        string path = _scenario.PathOf(name);
        await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        return path;
    }

    /// <summary>Creates a ViewModel with the file chosen and the password typed.</summary>
    private async Task<MainViewModel> SelectAsync(string path)
    {
        MainViewModel viewModel = _scenario.CreateViewModel();
        await _scenario.BrowseAsync(viewModel, path);
        viewModel.Password = Password;
        return viewModel;
    }
}

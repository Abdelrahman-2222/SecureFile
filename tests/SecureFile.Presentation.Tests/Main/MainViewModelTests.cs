using System.Globalization;
using SecureFile.Core.Cryptography;
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

    /// <summary>A password that Encrypt accepts: at least 8 characters, typed twice.</summary>
    private const string ValidPassword = "secret pass one";

    private readonly FakeFileEncryptionService _encryption = new();
    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeFileDetailsReader _detailsReader = new();
    private readonly FakeErrorLog _errorLog = new();
    private readonly FakeFolderOpener _folderOpener = new();

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
        Assert.Equal([("Encrypt", InputPath, output, ValidPassword, true)], _encryption.Calls);
        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal("Encrypted copy saved as \"report.pdf.enc\". Your original file was not changed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Encrypt_OnSuccess_ClearsTheFileAndThePasswordButKeepsTheResult()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Null(viewModel.SelectedFile);
        Assert.Equal("No file selected.", viewModel.FileSummary);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.True(viewModel.IsSucceeded);
        Assert.False(viewModel.IsFailed);
        Assert.True(viewModel.OpenFolderCommand.CanExecute(null)); // the result is still reachable
        Assert.False(viewModel.EncryptCommand.CanExecute(null));   // a new file and password are needed
    }

    [Theory]
    [InlineData("report.pdf", "If you forget this password")]
    [InlineData("report.pdf.enc", "Enter the password that was used")]
    public async Task PasswordHint_WarnsWhenChoosingAPasswordAndRemindsWhenDecrypting(string name, string expectedStart)
    {
        MainViewModel viewModel = await CreateWithFileAsync(Path.Combine(Path.GetTempPath(), name));

        Assert.StartsWith(expectedStart, viewModel.PasswordHint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("report.pdf", true)]
    [InlineData("report.pdf.enc", false)]
    public async Task IsEncryptOffered_IsOffForAnAlreadyEncryptedFile(string name, bool expected)
    {
        MainViewModel viewModel = await CreateWithFileAsync(Path.Combine(Path.GetTempPath(), name));

        Assert.Equal(expected, viewModel.IsEncryptOffered);
        Assert.Equal(!expected, viewModel.IsDecryptOffered); // exactly one of the two buttons is offered for a chosen file
        Assert.True(viewModel.EncryptCommand.CanExecute(null)); // the commands themselves stay available (T-24, T-08)
        Assert.True(viewModel.DecryptCommand.CanExecute(null));
    }

    // ---- Password rules when encrypting (Step 1.8 / UX spec §11.1) ------------------------------------------------

    [Fact]
    public async Task Encrypt_PasswordTooShort_IsRefusedWithAMessageAndDoesNotCallCore()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        viewModel.Password = "short7!";
        viewModel.ConfirmPassword = "short7!";
        _dialogs.SaveResult = InputPath + ".enc";

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Empty(_encryption.Calls);
        Assert.True(viewModel.IsFailed);
        Assert.Contains("at least 8 characters", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_PasswordsDoNotMatch_IsRefusedAndKeepsWhatWasTyped()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        viewModel.ConfirmPassword = ValidPassword + "x";
        _dialogs.SaveResult = InputPath + ".enc";

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Empty(_encryption.Calls);
        Assert.True(viewModel.IsFailed);
        Assert.Contains("not the same", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(ValidPassword, viewModel.Password);
    }

    [Fact]
    public async Task Encrypt_ConfirmationDiffersOnlyInCase_IsRefused()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        viewModel.ConfirmPassword = ValidPassword.ToUpperInvariant();
        _dialogs.SaveResult = InputPath + ".enc";

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Empty(_encryption.Calls);
        Assert.True(viewModel.IsFailed);
    }

    [Fact]
    public async Task Decrypt_AcceptsAShortPasswordWithoutConfirmation()
    {
        string encrypted = Path.Combine(Path.GetTempPath(), "report.pdf.enc");
        MainViewModel viewModel = await CreateWithFileAsync(encrypted);
        viewModel.Password = "old"; // set by another program or version: no rule is applied when decrypting
        viewModel.ConfirmPassword = string.Empty;
        _dialogs.SaveResult = Path.Combine(Path.GetTempPath(), "out.pdf");

        await viewModel.DecryptCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSucceeded);
        Assert.Equal("old", Assert.Single(_encryption.Calls).Password);
    }

    [Fact]
    public async Task Encrypt_OnSuccess_AlsoClearsTheConfirmation()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.ConfirmPassword);
    }

    [Fact]
    public void StrengthAndMatchTexts_FollowWhatIsTyped()
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.Equal(string.Empty, viewModel.StrengthText);
        Assert.Equal(string.Empty, viewModel.PasswordMatchText);

        viewModel.Password = "abc";
        Assert.Contains("Too short", viewModel.StrengthText, StringComparison.Ordinal);
        Assert.Equal(15, viewModel.StrengthPercent);

        viewModel.Password = "blue river coffee lamp";
        Assert.Equal("Strong.", viewModel.StrengthText);
        Assert.Equal(100, viewModel.StrengthPercent);

        viewModel.ConfirmPassword = "blue river";
        Assert.Contains("don't match", viewModel.PasswordMatchText, StringComparison.Ordinal);
        viewModel.ConfirmPassword = "blue river coffee lamp";
        Assert.Equal("Passwords match.", viewModel.PasswordMatchText);
    }

    [Theory]
    [InlineData("report.pdf", true, false)]
    [InlineData("report.pdf.enc", false, true)]
    public async Task IsEncryptingAndIsDecrypting_FollowTheChosenFile_AndAreBothOffWithoutOne(string name, bool encrypting, bool decrypting)
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.False(viewModel.IsEncrypting); // no file: no confirm box, no strength, no warning
        Assert.False(viewModel.IsDecrypting);

        viewModel = await CreateWithFileAsync(Path.Combine(Path.GetTempPath(), name));

        Assert.Equal(encrypting, viewModel.IsEncrypting);
        Assert.Equal(decrypting, viewModel.IsDecrypting);
    }

    [Fact]
    public void PasswordsMatch_IsTrueOnlyWhenBothAreTheSameAndNotEmpty()
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.False(viewModel.PasswordsMatch); // both empty is not a match

        viewModel.Password = "blue river";
        Assert.False(viewModel.PasswordsMatch);
        viewModel.ConfirmPassword = "blue river";
        Assert.True(viewModel.PasswordsMatch);
        viewModel.ConfirmPassword = "Blue river";
        Assert.False(viewModel.PasswordsMatch);
    }

    [Fact]
    public async Task Encrypt_OnFailure_KeepsFileAndPasswordAndMarksTheMessageAsFailed()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath; // the input itself: refused with a message

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsFailed);
        Assert.NotNull(viewModel.SelectedFile);
        Assert.Equal(ValidPassword, viewModel.Password);
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
        Assert.True(viewModel.DecryptCommand.CanExecute(null)); // Decrypt applies no password rules
        Assert.False(viewModel.EncryptCommand.CanExecute(null)); // Encrypt also needs a long enough, confirmed password
    }

    [Fact]
    public async Task Encrypt_IsDisabledUntilThePasswordIsLongEnoughAndConfirmed()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        Assert.True(viewModel.EncryptCommand.CanExecute(null));
        Assert.True(viewModel.RunPrimaryCommand.CanExecute(null));

        viewModel.ConfirmPassword = ValidPassword + "x"; // typed differently
        Assert.False(viewModel.EncryptCommand.CanExecute(null));
        Assert.False(viewModel.RunPrimaryCommand.CanExecute(null)); // Enter must not bypass it

        viewModel.ConfirmPassword = ValidPassword;
        Assert.True(viewModel.EncryptCommand.CanExecute(null));

        viewModel.Password = "short7!";
        viewModel.ConfirmPassword = "short7!"; // matching but too short
        Assert.False(viewModel.EncryptCommand.CanExecute(null));
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
        Assert.True(viewModel.BrowseCommand.CanExecute(null)); // ready for the next file (file and password were cleared)
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
        Assert.Throws<ArgumentNullException>("encryptionService", () => new MainViewModel(null!, _detailsReader, _dialogs, _errorLog, _folderOpener));
        Assert.Throws<ArgumentNullException>("fileDetailsReader", () => new MainViewModel(_encryption, null!, _dialogs, _errorLog, _folderOpener));
        Assert.Throws<ArgumentNullException>("fileDialogs", () => new MainViewModel(_encryption, _detailsReader, null!, _errorLog, _folderOpener));
        Assert.Throws<ArgumentNullException>("errorLog", () => new MainViewModel(_encryption, _detailsReader, _dialogs, null!, _folderOpener));
        Assert.Throws<ArgumentNullException>("folderOpener", () => new MainViewModel(_encryption, _detailsReader, _dialogs, _errorLog, null!));
    }

    // ---- Errors (Step 2.6) -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Encrypt_ExpectedFailure_LogsItAndShowsThePlainMessage()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        var failure = new AuthenticationFailedException();
        _encryption.ExceptionToThrow = failure;

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Equal("Wrong password, or the file was changed or damaged. Check Caps Lock, the keyboard language and spaces at the start or end.", viewModel.StatusMessage);
        Assert.Same(failure, Assert.Single(_errorLog.Exceptions));
        Assert.NotNull(viewModel.SelectedFile); // the user can fix the password and try again
    }

    [Fact]
    public async Task Browse_ReadFails_ClearsTheSelectionAndShowsTheMessage()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = InputPath;
        _detailsReader.ExceptionToThrow = new FileNotFoundException();

        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
        Assert.Equal("The file or folder can't be found or opened.", viewModel.StatusMessage);
        Assert.Single(_errorLog.Exceptions);
    }

    [Fact]
    public async Task Encrypt_ProgrammingError_IsNotCaught()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.ExceptionToThrow = new ArgumentException("bug");

        await Assert.ThrowsAsync<ArgumentException>(() => viewModel.EncryptCommand.ExecuteAsync(null));
        Assert.Empty(_errorLog.Exceptions);
    }

    [Fact]
    public async Task ResetAfterUnexpectedError_ReturnsToFileSelected()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.ExceptionToThrow = new IOException();
        await viewModel.EncryptCommand.ExecuteAsync(null);

        viewModel.ResetAfterUnexpectedError();

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.True(viewModel.EncryptCommand.CanExecute(null));
    }

    // ---- Cancel, Open folder, locked inputs (Step 3.5) -------------------------------------------------------------

    [Fact]
    public async Task Cancel_WhileRunning_StopsTheOperationAndSaysNothingWasWritten()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.WaitFor = new TaskCompletionSource().Task; // never completes: only Cancel ends it
        Task running = viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CanEditInputs);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await running;

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Equal("Cancelled. No file was written.", viewModel.StatusMessage);
        Assert.False(viewModel.CancelCommand.CanExecute(null));
        Assert.True(viewModel.CanEditInputs);
    }

    [Fact]
    public void Cancel_WhenNothingRuns_IsNotAvailable()
    {
        Assert.False(CreateViewModel().CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task OpenFolder_AfterSuccess_ShowsTheResultFile()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        string output = InputPath + ".enc";
        _dialogs.SaveResult = output;
        Assert.False(viewModel.OpenFolderCommand.CanExecute(null));

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSucceeded);
        Assert.True(viewModel.OpenFolderCommand.CanExecute(null));
        viewModel.OpenFolderCommand.Execute(null);
        Assert.Equal(output, _folderOpener.OpenedPath);
    }

    [Fact]
    public async Task OpenFolder_FileManagerFails_ShowsAHint()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _folderOpener.Result = false;
        await viewModel.EncryptCommand.ExecuteAsync(null);

        viewModel.OpenFolderCommand.Execute(null);

        Assert.Contains("couldn't be opened", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    // ---- Drop and primary action (Step 3.4) ------------------------------------------------------------------------

    [Fact]
    public async Task OpenFile_DroppedPath_SelectsItWithoutTheDialog()
    {
        MainViewModel viewModel = CreateViewModel();

        await viewModel.OpenFileCommand.ExecuteAsync(InputPath);

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.Equal(InputPath, viewModel.SelectedFile?.FullPath);
    }

    [Fact]
    public async Task OpenFile_ReadFails_ShowsTheMessage()
    {
        MainViewModel viewModel = CreateViewModel();
        _detailsReader.ExceptionToThrow = new UnauthorizedAccessException();

        await viewModel.OpenFileCommand.ExecuteAsync(InputPath);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
    }

    [Theory]
    [InlineData("report.pdf", "Encrypt")]
    [InlineData("report.pdf.enc", "Decrypt")]
    [InlineData("REPORT.ENC", "Decrypt")]
    public async Task RunPrimary_ChoosesTheOperationFromTheFileName(string fileName, string expectedOperation)
    {
        string path = Path.Combine(Path.GetTempPath(), fileName);
        MainViewModel viewModel = await CreateWithFileAsync(path);
        _dialogs.SaveResult = Path.Combine(Path.GetTempPath(), "out.bin");

        await viewModel.RunPrimaryCommand.ExecuteAsync(null);

        Assert.Equal(expectedOperation, Assert.Single(_encryption.Calls).Operation);
    }

    [Fact]
    public void RunPrimary_WithoutFileOrPassword_CannotRun()
    {
        MainViewModel viewModel = CreateViewModel();

        Assert.False(viewModel.RunPrimaryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Encrypt_PasswordWithLoneSurrogate_ShowsMessageAndDoesNotCallCore()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        char loneSurrogate = (char)0xD800; // lives in the body: xUnit v3 cannot serialize it in InlineData
        viewModel.Password = new string(['a', 'b', loneSurrogate]);

        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Empty(_encryption.Calls);
        Assert.Contains("password", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_UnexpectedException_LeavesTheWindowUsable()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.ExceptionToThrow = new ArgumentException("bug");

        await Assert.ThrowsAsync<ArgumentException>(() => viewModel.EncryptCommand.ExecuteAsync(null));

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
    }

    [Fact]
    public void ResetAfterUnexpectedError_WithoutFile_ReturnsToNoFile()
    {
        MainViewModel viewModel = CreateViewModel();

        viewModel.ResetAfterUnexpectedError();

        Assert.Equal(MainViewState.NoFile, viewModel.State);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_encryption, _detailsReader, _dialogs, _errorLog, _folderOpener);
    }

    /// <summary>A ViewModel with <paramref name="path"/> chosen and a valid password typed twice.</summary>
    private async Task<MainViewModel> CreateWithFileAsync(string path)
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = path;
        await viewModel.BrowseCommand.ExecuteAsync(null);
        viewModel.Password = ValidPassword;
        viewModel.ConfirmPassword = ValidPassword;
        return viewModel;
    }
}

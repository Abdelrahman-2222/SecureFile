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

        // The status adds something instead of repeating the drop zone and the file card (review item 17).
        Assert.Equal(MainViewModel.NoFileGuidance, viewModel.StatusMessage);
        Assert.DoesNotContain("Choose a file", viewModel.StatusMessage, StringComparison.Ordinal);
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
        Assert.Equal(MainViewModel.SuccessMessage(encrypt: true, "report.pdf", output), viewModel.StatusMessage);
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
        Assert.Equal(MainViewModel.SuccessMessage(encrypt: false, inputName, Path.Combine(Path.GetTempPath(), "out.pdf")), viewModel.StatusMessage);
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
    public async Task Encrypt_SaveDialogCancelledAfterAFailure_ClearsTheOldErrorMessage()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.ExceptionToThrow = new AuthenticationFailedException();
        await viewModel.EncryptCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsFailed);

        _dialogs.SaveResult = null; // the user cancels the Save dialog on the second try
        await viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsFailed);
        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.DoesNotContain("Wrong password", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.NotNull(viewModel.SelectedFile);
        Assert.Equal(ValidPassword, viewModel.Password);
    }

    [Fact]
    public async Task Browse_ReadFails_ShowsTheMessageAndSelectsNothing()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = InputPath;
        _detailsReader.ExceptionToThrow = new FileNotFoundException();

        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
        Assert.Equal("Couldn't open \"" + Path.GetFileName(InputPath) + "\". The file or folder can't be found or opened.", viewModel.StatusMessage);
        Assert.Single(_errorLog.Exceptions);
    }

    [Fact]
    public async Task OpenFile_BadDropAfterAGoodFile_KeepsThePreviousSelection()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _detailsReader.ExceptionToThrow = new FileNotFoundException();

        await viewModel.OpenFileCommand.ExecuteAsync(Path.Combine(Path.GetTempPath(), "gone.bin"));

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Equal(InputPath, viewModel.SelectedFile?.FullPath); // the good file stays, with the message about the bad drop
        Assert.Contains("\"gone.bin\"", viewModel.StatusMessage, StringComparison.Ordinal); // and the message names the bad one
        Assert.Equal(ValidPassword, viewModel.Password);
    }

    [Fact]
    public void SuccessMessage_NamesTheFolderAndSaysTheUnencryptedOriginalIsStillThere()
    {
        string output = Path.Combine(Path.GetTempPath(), "Taxes", "report.pdf.enc");

        Assert.Equal(
            "Encrypted copy saved as \"report.pdf.enc\" in the folder \"Taxes\". The unencrypted original \"report.pdf\" is still there: delete it yourself if only the encrypted copy should remain.",
            MainViewModel.SuccessMessage(encrypt: true, "report.pdf", output));
        Assert.Equal(
            "Decrypted file saved as \"report.pdf.enc\" in the folder \"Taxes\".",
            MainViewModel.SuccessMessage(encrypt: false, "report.pdf.enc", output));
    }

    [Fact]
    public void SuccessMessage_FileAtTheTopOfADrive_NamesTheDrive()
    {
        string root = Path.GetPathRoot(Path.GetTempPath())!;

        Assert.EndsWith($"in the folder \"{root}\".", MainViewModel.SuccessMessage(encrypt: false, "a.enc", Path.Combine(root, "a")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnterMovesToConfirm_OnlyWhileANewPasswordStillNeedsItsConfirmation()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        viewModel.ConfirmPassword = string.Empty;
        Assert.True(viewModel.EnterMovesToConfirm); // review items 11 and 30

        viewModel.ConfirmPassword = ValidPassword;
        Assert.False(viewModel.EnterMovesToConfirm); // both match: Enter encrypts

        _detailsReader.IsEncryptedOverride = true;
        MainViewModel decrypting = await CreateWithFileAsync(InputPath + ".enc");
        decrypting.ConfirmPassword = string.Empty;
        Assert.False(decrypting.EnterMovesToConfirm); // no confirmation when decrypting
    }

    [Fact]
    public void DescribeFile_GivesNameFolderDateAndExactSize()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Taxes");
        var modified = new DateTime(2026, 11, 25, 8, 30, 0, DateTimeKind.Utc);
        var file = new FileDetails(Path.Combine(folder, "a very long name that the card cuts.pdf"), "a very long name that the card cuts.pdf", ".pdf", 1_234_567, modified);

        string text = MainViewModel.DescribeFile(file, CultureInfo.InvariantCulture);

        string expectedDate = modified.ToLocalTime().ToString("g", CultureInfo.InvariantCulture);
        Assert.Equal(
            $"a very long name that the card cuts.pdf\nFolder: {folder}\nModified: {expectedDate}\nSize: 1,234,567 bytes (1.18 MB)",
            text);
    }

    [Fact]
    public async Task FileCardToolTip_OnlyWhileAFileIsChosen()
    {
        MainViewModel viewModel = CreateViewModel();
        Assert.Null(viewModel.FileCardToolTip);

        await viewModel.OpenFileCommand.ExecuteAsync(InputPath);

        Assert.StartsWith(Path.GetFileName(InputPath), viewModel.FileCardToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenFiles_DroppedFolder_SaysItIsAFolderAndLogsItAsExpected()
    {
        MainViewModel viewModel = CreateViewModel();
        string folder = Path.Combine(Path.GetTempPath(), "Holiday photos");
        _detailsReader.ExceptionToThrow = new PathIsFolderException("folder", folder);

        await viewModel.OpenFilesCommand.ExecuteAsync([folder]);

        Assert.Equal(MainViewState.Failed, viewModel.State);
        Assert.Contains("This is a folder.", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(1, _errorLog.ExpectedCount); // no stack trace for it in the log (review item 29)
    }

    [Fact]
    public async Task OpenFiles_SeveralDropped_UsesTheFirstAndSaysSo()
    {
        MainViewModel viewModel = CreateViewModel();
        string second = Path.Combine(Path.GetTempPath(), "second.txt");

        await viewModel.OpenFilesCommand.ExecuteAsync([InputPath, second, second]);

        Assert.Equal(InputPath, viewModel.SelectedFile?.FullPath);
        Assert.StartsWith(
            $"You dropped 3 files. SecureFile works on one file at a time, so only \"{Path.GetFileName(InputPath)}\" is used.",
            viewModel.StatusMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenFiles_NothingOrOneFile_BehavesLikeOpenFile()
    {
        MainViewModel viewModel = CreateViewModel();

        await viewModel.OpenFilesCommand.ExecuteAsync([]);
        Assert.Equal(MainViewState.NoFile, viewModel.State);

        await viewModel.OpenFilesCommand.ExecuteAsync([InputPath]);
        Assert.Equal(InputPath, viewModel.SelectedFile?.FullPath);
        Assert.DoesNotContain("You dropped", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browse_FileChosenAfterAFailure_ShowsGuidanceAndKeepsThePasswords()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _encryption.ExceptionToThrow = new AuthenticationFailedException();
        await viewModel.EncryptCommand.ExecuteAsync(null);

        _dialogs.OpenResult = Path.Combine(Path.GetTempPath(), "other.bin");
        await viewModel.BrowseCommand.ExecuteAsync(null);

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.False(viewModel.IsFailed);
        Assert.Equal("other.bin", viewModel.SelectedFile?.Name);
        Assert.Equal(ValidPassword, viewModel.Password); // the typed password survives a change of file
        Assert.Equal(ValidPassword, viewModel.ConfirmPassword);
    }

    [Fact]
    public async Task Cancel_WhenNothingIsRunning_DoesNothing()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        string message = viewModel.StatusMessage;

        Assert.False(viewModel.CancelCommand.CanExecute(null)); // what Esc triggers: not available while idle
        viewModel.CancelCommand.Execute(null);

        Assert.Equal(MainViewState.FileSelected, viewModel.State);
        Assert.Equal(message, viewModel.StatusMessage);
    }

    [Fact]
    public async Task Progress_ThenFailure_ClearsTheOldPercentageAndText()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        var release = new TaskCompletionSource();
        _encryption.WaitFor = release.Task;
        Task running = viewModel.EncryptCommand.ExecuteAsync(null);
        viewModel.OnProgress(42);
        Assert.Equal(42, viewModel.ProgressPercentage);
        Assert.NotEmpty(viewModel.ProgressText);

        _encryption.ExceptionToThrow = new AuthenticationFailedException();
        release.SetResult();
        await running;

        Assert.Equal(0, viewModel.ProgressPercentage);
        Assert.Equal(string.Empty, viewModel.ProgressText);
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

    [Fact]
    public async Task Progress_ManyReportsInstantly_ShowsTheFirstAndAlwaysTheFinalOne()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        var release = new TaskCompletionSource();
        _encryption.WaitFor = release.Task;
        Task running = viewModel.EncryptCommand.ExecuteAsync(null);

        viewModel.OnProgress(5);
        viewModel.OnProgress(6); // normally less than 100 ms later: skipped
        viewModel.OnProgress(7);
        // Wall-clock based, so only what holds on a slow machine too: the first report shows, nothing goes backwards
        // and nothing beyond what was reported appears (the exact 100 ms boundary needs an injectable clock: backlog C18).
        Assert.InRange(viewModel.ProgressPercentage, 5, 7);
        Assert.NotEqual(0, viewModel.ProgressPercentage);

        viewModel.OnProgress(100); // the end is never skipped
        Assert.Equal(100, viewModel.ProgressPercentage);

        release.SetResult();
        await running;
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

        Assert.Equal(MainViewState.FileSelected, viewModel.State); // back to the chosen file, not an error state
        Assert.False(viewModel.IsFailed);
        Assert.Equal("Cancelled. No file was written.", viewModel.StatusMessage);
        Assert.Empty(_errorLog.Exceptions); // cancelling is not an error: nothing is logged
        Assert.NotNull(viewModel.SelectedFile);
        Assert.Equal(0, viewModel.ProgressPercentage);
        Assert.Equal(string.Empty, viewModel.ProgressText);
        Assert.False(viewModel.CancelCommand.CanExecute(null));
        Assert.True(viewModel.CanEditInputs);
    }

    [Fact]
    public async Task SaveDialogOpen_LocksInputs_AndEncryptsWithThePasswordCheckedBefore()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        viewModel.Password = ValidPassword;
        viewModel.ConfirmPassword = ValidPassword;
        var save = new TaskCompletionSource<string?>();
        _dialogs.PendingSave = save.Task;

        Task running = viewModel.EncryptCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsChoosingOutput);
        Assert.False(viewModel.CanEditInputs);
        Assert.False(viewModel.BrowseCommand.CanExecute(null));
        Assert.False(viewModel.OpenFileCommand.CanExecute(InputPath));
        Assert.False(viewModel.OpenFilesCommand.CanExecute(null)); // the windows show no copy cursor while busy (item 12)
        Assert.False(viewModel.DecryptCommand.CanExecute(null));
        viewModel.Password = "x"; // a dialog that does not block the window (Linux portal) let the user type

        save.SetResult(InputPath + ".enc");
        await running;

        Assert.False(viewModel.IsChoosingOutput);
        Assert.Equal(ValidPassword, Assert.Single(_encryption.Calls).Password);
    }

    [Fact]
    public async Task OpenFolder_FileManagerFails_KeepsTheSuccessMessageAndAddsTheHint()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        _folderOpener.Result = false;
        await viewModel.EncryptCommand.ExecuteAsync(null);

        await viewModel.OpenFolderCommand.ExecuteAsync(null);
        await viewModel.OpenFolderCommand.ExecuteAsync(null); // twice: the hint is not appended twice

        Assert.True(viewModel.IsSucceeded);
        Assert.StartsWith("Encrypted copy saved as", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(viewModel.StatusMessage, "couldn't be opened"));
    }

    [Fact]
    public async Task EncryptedNameButNoSignature_OffersBothButtonsAndExplainsWhy()
    {
        _detailsReader.IsEncryptedOverride = false; // "report.pdf.enc" whose content is not ours: damaged or foreign
        MainViewModel viewModel = await CreateWithFileAsync(Path.Combine(Path.GetTempPath(), "report.pdf.enc"));

        Assert.True(viewModel.IsDecryptOffered); // lets the user try and get the exact reason
        Assert.True(viewModel.IsEncryptOffered);
        Assert.Contains("not a valid SecureFile file", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenamedEncryptedFile_IsRecognisedByContent_AndOnlyDecryptIsOffered()
    {
        _detailsReader.IsEncryptedOverride = true; // content has our signature, name is "report.bak"
        MainViewModel viewModel = await CreateWithFileAsync(Path.Combine(Path.GetTempPath(), "report.bak"));

        Assert.True(viewModel.IsDecrypting);
        Assert.True(viewModel.IsDecryptOffered);
        Assert.False(viewModel.IsEncryptOffered);
        Assert.False(viewModel.IsEncrypting);
    }

    [Fact]
    public async Task GuidanceLine_FollowsWhatIsTyped_WhenEncrypting()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = InputPath;
        await viewModel.BrowseCommand.ExecuteAsync(null);
        Assert.Equal("Enter a password of at least 8 characters.", viewModel.StatusMessage);

        viewModel.Password = "short";
        Assert.StartsWith("The password is too short", viewModel.StatusMessage, StringComparison.Ordinal);

        viewModel.Password = "blue river coffee lamp";
        Assert.Equal("Type the password again to confirm it.", viewModel.StatusMessage);

        viewModel.ConfirmPassword = "blue river";
        Assert.Equal("The two passwords are not the same yet.", viewModel.StatusMessage); // typed, but not acceptable: not "enter the password"

        viewModel.ConfirmPassword = "blue river coffee lamp";
        Assert.Equal("Ready. Choose Encrypt file.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task GuidanceLine_WhenDecrypting_AsksForTheOldPasswordThenSaysReady()
    {
        MainViewModel viewModel = CreateViewModel();
        _dialogs.OpenResult = Path.Combine(Path.GetTempPath(), "report.pdf.enc");
        await viewModel.BrowseCommand.ExecuteAsync(null);
        Assert.Equal("Enter the password that was used to encrypt this file.", viewModel.StatusMessage);

        viewModel.Password = "old";

        Assert.Equal("Ready. Choose Decrypt file.", viewModel.StatusMessage); // no length rule when decrypting
    }

    [Fact]
    public async Task Typing_AfterAFailure_ClearsTheOldErrorAndShowsTheNextStep()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath; // refused: the result can't replace the chosen file
        await viewModel.EncryptCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsFailed);

        viewModel.ConfirmPassword = "x";

        Assert.False(viewModel.IsFailed);
        Assert.Equal("The two passwords are not the same yet.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Typing_AfterASuccess_KeepsTheResultMessage()
    {
        MainViewModel viewModel = await CreateWithFileAsync(InputPath);
        _dialogs.SaveResult = InputPath + ".enc";
        await viewModel.EncryptCommand.ExecuteAsync(null);

        viewModel.Password = "something typed after";

        Assert.True(viewModel.IsSucceeded);
        Assert.StartsWith("Encrypted copy saved as", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordMatchText_PromptsForTheConfirmationAndOnlyMismatchIsFlagged()
    {
        MainViewModel viewModel = CreateViewModel();
        viewModel.Password = "short";
        Assert.Equal(string.Empty, viewModel.PasswordMatchText); // too short: the strength line speaks, not this one

        viewModel.Password = "blue river coffee lamp";
        Assert.Equal("Type the password again to confirm it.", viewModel.PasswordMatchText);
        Assert.False(viewModel.IsPasswordMismatch); // a prompt is not an error

        viewModel.ConfirmPassword = "blue";
        Assert.True(viewModel.IsPasswordMismatch);
        viewModel.ConfirmPassword = "blue river coffee lamp";
        Assert.False(viewModel.IsPasswordMismatch);
        Assert.True(viewModel.PasswordsMatch);
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
    public async Task RunPrimary_ChoosesTheOperationFromWhetherTheFileIsEncrypted(string fileName, string expectedOperation)
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

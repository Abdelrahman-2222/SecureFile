using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureFile.Core;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Core.Passwords;
using SecureFile.Presentation.Dialogs;
using SecureFile.Presentation.Errors;

namespace SecureFile.Presentation.Main;

/// <summary>
/// The state and commands behind the main window: choose a file, enter a password, encrypt or decrypt, read the result.
/// </summary>
/// <remarks>
/// <para>
/// <b>MVVM (ADR-08):</b> the window only binds to these properties and commands; all decisions are made here, so they
/// are tested without opening a window. <c>[ObservableProperty]</c> and <c>[RelayCommand]</c> are CommunityToolkit.Mvvm
/// source generators: they write the <c>INotifyPropertyChanged</c> property code and the command objects at compile
/// time (visible in the IDE under Dependencies → Analyzers).
/// </para>
/// <para>
/// <b>Threading:</b> every <c>await</c> uses <c>ConfigureAwait(true)</c> on purpose: after the work, the code must
/// continue on the UI thread, because it changes properties that the window shows.
/// </para>
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// The status line while no file is chosen. It adds what the drop zone ("Drop a file here or Browse…") and the file
    /// card ("No file selected.") do not say, instead of repeating them a third time (review item 17).
    /// </summary>
    internal const string NoFileGuidance = "Any file can be encrypted; a SecureFile file (.enc) is decrypted. The file you choose is never changed.";

    /// <summary>The ending added to a decrypted file's name when the encrypted file does not end in <c>.enc</c>.</summary>
    private const string DecryptedFileExtension = ".decrypted";

    private readonly IFileEncryptionService _encryptionService;
    private readonly IFileDetailsReader _fileDetailsReader;
    private readonly IFileDialogService _fileDialogs;
    private readonly IErrorLog _errorLog;
    private readonly IFolderOpener _folderOpener;
    private readonly TimeProvider _timeProvider;

    /// <summary>When the running operation started, as a <see cref="TimeProvider.GetTimestamp"/> value.</summary>
    private long _operationStart;

    /// <summary>The shortest time between two screen updates of the progress bar (10 per second).</summary>
    private static readonly TimeSpan MinimumProgressInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>The percentage at which the throttle never skips an update.</summary>
    private const int ProgressComplete = 100;

    /// <summary>When (time since <see cref="_operationStart"/>) the progress was last shown; used by the throttle.</summary>
    private TimeSpan _lastProgressUpdate;

    /// <summary>The name of the file being read by Browse or drop; <see langword="null"/> when no read is running.</summary>
    private string? _loadingFileName;

    /// <summary>Lets Cancel stop the running operation; <see langword="null"/> when none is running (a file read is not cancellable).</summary>
    private CancellationTokenSource? _operationCts;

    /// <summary>The file the last successful operation wrote, for "Open folder".</summary>
    private string? _lastOutputPath;

    /// <summary>The success message of the last operation, kept so a failed "Open folder" can add to it instead of replacing it.</summary>
    // Stryker disable once String : always set by a success before Open folder can read it (CanOpenFolder); equivalent mutant
    private string _lastSuccessMessage = string.Empty;

    /// <summary>Creates the ViewModel with the services it uses (pure dependency injection, ADR-09).</summary>
    /// <param name="encryptionService">Encrypts and decrypts files.</param>
    /// <param name="fileDetailsReader">Reads the name and size of the chosen file.</param>
    /// <param name="fileDialogs">Shows the Open and Save dialogs.</param>
    /// <param name="errorLog">Records the technical details of handled errors.</param>
    /// <param name="folderOpener">Shows the result in the file manager.</param>
    /// <param name="timeProvider">
    /// The clock for the progress throttle and speed; <see langword="null"/> for the system clock. Tests pass a fake one,
    /// so the 100 ms boundary is checked exactly instead of with the wall clock (backlog C18).
    /// </param>
    /// <exception cref="ArgumentNullException">A service is <see langword="null"/>.</exception>
    public MainViewModel(
        IFileEncryptionService encryptionService,
        IFileDetailsReader fileDetailsReader,
        IFileDialogService fileDialogs,
        IErrorLog errorLog,
        IFolderOpener folderOpener,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(encryptionService);
        ArgumentNullException.ThrowIfNull(fileDetailsReader);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(errorLog);
        ArgumentNullException.ThrowIfNull(folderOpener);

        _folderOpener = folderOpener;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _errorLog = errorLog;
        _encryptionService = encryptionService;
        _fileDetailsReader = fileDetailsReader;
        _fileDialogs = fileDialogs;
    }

    /// <summary>Gets the current state; it decides which commands can run (see <see cref="MainViewState"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy), nameof(CanEditInputs), nameof(IsSucceeded), nameof(IsFailed), nameof(IsProgressIndeterminate))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand), nameof(OpenFileCommand), nameof(OpenFilesCommand), nameof(EncryptCommand), nameof(DecryptCommand), nameof(RunPrimaryCommand), nameof(OpenFolderCommand))]
    public partial MainViewState State { get; private set; } = MainViewState.NoFile;

    /// <summary>Gets the details of the chosen file, or <see langword="null"/> when no file is chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSummary), nameof(FileCardToolTip), nameof(PasswordHint), nameof(IsEncryptOffered), nameof(IsDecryptOffered), nameof(IsEncrypting), nameof(IsDecrypting))]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand), nameof(DecryptCommand), nameof(RunPrimaryCommand))]
    public partial FileDetails? SelectedFile { get; private set; }

    /// <summary>Gets a value indicating whether a normal file is chosen, so a new password is being created (confirm box, strength, warning).</summary>
    public bool IsEncrypting => SelectedFile is { IsEncrypted: false };

    /// <summary>Gets a value indicating whether an encrypted file is chosen, so an existing password is being entered.</summary>
    public bool IsDecrypting => SelectedFile is { IsEncrypted: true };

    /// <summary>Gets a value indicating whether the password and its confirmation are the same (and not empty).</summary>
    public bool PasswordsMatch => Password.Length > 0 && string.Equals(Password, ConfirmPassword, StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether the window offers the Encrypt button: not for a file that is already encrypted.</summary>
    /// <remarks>
    /// Only the button is switched off; the command itself still works, because encrypting an encrypted file is
    /// allowed by the design (T-26) and is tested through the command.
    /// </remarks>
    public bool IsEncryptOffered => !IsEncryptedFileSelected();

    /// <summary>Gets a value indicating whether the window offers the Decrypt button: only for a file that is encrypted (or while none is chosen).</summary>
    /// <remarks>
    /// "Encrypted" is decided by the file's content (its SecureFile signature), not its name, so a renamed encrypted
    /// file is still offered. The command itself still works on any file and reports a plain message for a file that
    /// is not ours (T-08).
    /// </remarks>
    public bool IsDecryptOffered => SelectedFile is null || SelectedFile.IsEncrypted || HasEncryptedName(SelectedFile);

    /// <summary>Gets the note under the password box: a warning when a password is being chosen, a reminder when decrypting.</summary>
    /// <remarks>
    /// "If you forget this password…" matters when the user creates a password (encrypting). For a file that already
    /// ends in <c>.enc</c> the user is entering an old password, so the note says which one instead.
    /// </remarks>
    public string PasswordHint => IsEncryptedFileSelected()
        ? "Enter the password that was used to encrypt this file."
        : "If you forget this password, the file cannot be recovered. Nobody can reset it.";

    /// <summary>Gets or sets the password typed by the user. Spaces count as characters and are never trimmed.</summary>
    /// <remarks>
    /// A plain <see cref="string"/>: WPF's <c>PasswordBox</c> hands its text over as a string anyway, and the Core API
    /// takes one (PROJECT_PLAN §5.2 lists the copies we cannot wipe). The window sets it from code-behind, because
    /// <c>PasswordBox.Password</c> cannot be data-bound on purpose.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Strength), nameof(StrengthText), nameof(StrengthPercent), nameof(PasswordMatchText), nameof(PasswordsMatch), nameof(IsPasswordMismatch))]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand), nameof(DecryptCommand), nameof(RunPrimaryCommand))]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets the password typed a second time; it is only checked when encrypting (a typo there loses the file for good).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordMatchText), nameof(PasswordsMatch), nameof(IsPasswordMismatch))]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand), nameof(RunPrimaryCommand))]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Gets the rating of the typed password (advice; only "too short" is enforced, and only when encrypting).</summary>
    public PasswordStrength Strength => PasswordStrengthEvaluator.Evaluate(Password);

    /// <summary>Gets the strength as a bar fill from 0 to 100 for the window.</summary>
    public int StrengthPercent => Strength switch
    {
        PasswordStrength.Empty => 0,
        PasswordStrength.TooShort => 15,
        PasswordStrength.Weak => 35,
        PasswordStrength.Fair => 65,
        _ => 100,
    };

    /// <summary>Gets the sentence that goes with the strength bar; empty while nothing is typed.</summary>
    public string StrengthText => Strength switch
    {
        PasswordStrength.Empty => string.Empty,
        PasswordStrength.TooShort => string.Create(CultureInfo.CurrentCulture, $"Too short: use at least {PasswordStrengthEvaluator.MinimumLength} characters."),
        PasswordStrength.Weak => "Weak: avoid repeats, sequences and common words. A few unrelated words are safer.",
        PasswordStrength.Fair => "Fair.",
        _ => "Strong.",
    };

    /// <summary>Gets the line under the confirm box: "match", "don't match", or a prompt to type it again; empty while there is nothing to say.</summary>
    public string PasswordMatchText => ConfirmPassword.Length > 0
        ? (PasswordsMatch ? "Passwords match." : "Passwords don't match yet.")
        : Password.Length >= PasswordStrengthEvaluator.MinimumLength ? "Type the password again to confirm it." : string.Empty;

    /// <summary>Gets a value indicating whether a confirmation was started and differs from the password (the window colours the line red).</summary>
    public bool IsPasswordMismatch => ConfirmPassword.Length > 0 && !PasswordsMatch;

    /// <summary>Gets how much of the running operation is done, 0–100.</summary>
    [ObservableProperty]
    public partial int ProgressPercentage { get; private set; }

    /// <summary>Gets the progress line: percentage, speed and time left (see <see cref="ProgressTextFormatter"/>).</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the inputs (password, file choice) accept changes; they are locked while working.</summary>
    public bool CanEditInputs => !IsBusy && !IsFileDialogOpen;

    /// <summary>Gets a value indicating whether the Open or Save dialog is open, so the inputs and actions are locked meanwhile.</summary>
    /// <remarks>
    /// Not every system's dialog blocks the window: the Linux desktop portal draws it in another process, so the user could
    /// otherwise change the already-checked password, choose another file or start a second operation (ADR-16, C19). The
    /// Open dialog is locked too: a file picked there while an operation ran swapped the file mid-operation and re-enabled
    /// every command (review VM-1).
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditInputs))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand), nameof(OpenFileCommand), nameof(OpenFilesCommand), nameof(EncryptCommand), nameof(DecryptCommand), nameof(RunPrimaryCommand))]
    public partial bool IsFileDialogOpen { get; private set; }

    /// <summary>Gets a value indicating whether the last operation succeeded (the window shows the result panel).</summary>
    public bool IsSucceeded => State == MainViewState.Succeeded;

    /// <summary>Gets a value indicating whether the last operation failed (the window colours the message as an error).</summary>
    public bool IsFailed => State == MainViewState.Failed;

    /// <summary>Gets a value indicating whether the progress bar should only animate: busy, but with no percentage (reading a file).</summary>
    public bool IsProgressIndeterminate => IsBusy && _operationCts is null;

    /// <summary>Gets the message that tells the user what happened and what to do next.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = NoFileGuidance;

    /// <summary>Gets a value indicating whether an operation is running (the window shows a busy indicator).</summary>
    public bool IsBusy => State == MainViewState.Working;

    /// <summary>Gets the chosen file's name, extension and size in one line, for example <c>report.pdf (.pdf) · 1.5 MB</c>.</summary>
    public string FileSummary => SelectedFile is null
        ? "No file selected."
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{SelectedFile.Name} ({ExtensionText(SelectedFile.Extension)}) · {FileSizeFormatter.Format(SelectedFile.SizeInBytes, CultureInfo.CurrentCulture)}");

    /// <summary>Asks for a file and shows its details. Cancelling the dialog changes nothing.</summary>
    /// <returns>A task that completes when the details are shown.</returns>
    /// <remarks>
    /// The details are read on a thread-pool thread: on an unavailable network drive the lookup can take seconds, and
    /// the window must not freeze meanwhile (PROJECT_PLAN §11.1).
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private Task BrowseAsync()
    {
        return RunGuardedAsync(BrowseCoreAsync);
    }

    /// <summary>The Browse work itself, without the error handling around it.</summary>
    /// <returns>A task that completes when the details are shown.</returns>
    private async Task BrowseCoreAsync()
    {
        string? path = await ShowDialogLockedAsync(_fileDialogs.ShowOpenFileDialogAsync).ConfigureAwait(true);
        if (path is not null)
        {
            await LoadFileAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>Shows the details of a file dropped onto the window (the same as choosing it with Browse).</summary>
    /// <param name="path">The full path of the dropped file; <see langword="null"/> or empty is ignored.</param>
    /// <returns>A task that completes when the details are shown.</returns>
    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private Task OpenFileAsync(string? path)
    {
        return string.IsNullOrEmpty(path)
            ? Task.CompletedTask
            : RunGuardedAsync(() => LoadFileAsync(path));
    }

    /// <summary>
    /// Encrypts or decrypts depending on the chosen file: a name ending in <c>.enc</c> is decrypted, anything else is
    /// encrypted. This is what Enter in the password box does (PROJECT_PLAN §11.2: Enter = primary action).
    /// </summary>
    /// <returns>A task that completes when the operation has ended.</returns>
    [RelayCommand(CanExecute = nameof(CanRunPrimary))]
    private Task RunPrimaryAsync()
    {
        return IsEncryptedFileSelected() ? DecryptAsync() : EncryptAsync();
    }

    /// <summary>Tells whether the chosen file is a SecureFile-encrypted file (by content), so it is decrypted rather than encrypted.</summary>
    /// <returns><see langword="true"/> when a file is chosen and it starts with the SecureFile signature.</returns>
    private bool IsEncryptedFileSelected()
    {
        return SelectedFile is { IsEncrypted: true };
    }

    /// <summary>Reads a chosen file's details off the UI thread and selects it.</summary>
    /// <param name="path">The file to show.</param>
    /// <returns>A task that completes when the details are shown.</returns>
    private async Task LoadFileAsync(string path)
    {
        ClearProgress();
        State = MainViewState.Working;
        StatusMessage = "Reading the file details…";
        _loadingFileName = Path.GetFileName(path);
        SelectedFile = await Task.Run(() => _fileDetailsReader.Read(path)).ConfigureAwait(true);
        _loadingFileName = null;
        State = MainViewState.FileSelected;
        StatusMessage = GuidanceMessage();
    }

    /// <summary>
    /// Tells the user the next step for the chosen file, from what is typed so far: the line under the buttons while
    /// nothing is running or finished.
    /// </summary>
    /// <returns>The sentence to show.</returns>
    /// <remarks>
    /// It follows the password and confirmation, so it never says "enter the password" when one is already typed but
    /// not yet acceptable (too short, not confirmed, not the same).
    /// </remarks>
    private string GuidanceMessage()
    {
        if (SelectedFile is null)
        {
            return NoFileGuidance;
        }

        if (!SelectedFile.IsEncrypted && HasEncryptedName(SelectedFile))
        {
            return "This file ends in .enc but is not a valid SecureFile file: it may be damaged or made by another program. You can still try Decrypt to see what is wrong.";
        }

        return SelectedFile.IsEncrypted ? DecryptGuidance() : EncryptGuidance();
    }

    /// <summary>The next step when an encrypted file is chosen.</summary>
    /// <returns>The sentence to show.</returns>
    private string DecryptGuidance()
    {
        return Password.Length == 0
            ? "Enter the password that was used to encrypt this file."
            : "Ready. Choose Decrypt file.";
    }

    /// <summary>The next step when a normal file is chosen: a password, its length, its confirmation, then go.</summary>
    /// <returns>The sentence to show.</returns>
    private string EncryptGuidance()
    {
        if (Password.Length == 0)
        {
            return string.Create(CultureInfo.CurrentCulture, $"Enter a password of at least {PasswordStrengthEvaluator.MinimumLength} characters.");
        }

        if (Password.Length < PasswordStrengthEvaluator.MinimumLength)
        {
            return string.Create(CultureInfo.CurrentCulture, $"The password is too short: use at least {PasswordStrengthEvaluator.MinimumLength} characters.");
        }

        if (ConfirmPassword.Length == 0)
        {
            return "Type the password again to confirm it.";
        }

        return PasswordsMatch ? ReadyToEncryptGuidance() : "The two passwords are not the same yet.";
    }

    /// <summary>The "go" line, which admits a weak password instead of a plain "Ready".</summary>
    /// <returns>The sentence to show.</returns>
    /// <remarks>
    /// A weak password is allowed on purpose (only the minimum length is a rule; the meter is advice), but a red warning
    /// next to a plain "Ready" looked contradictory. The line now says both: it is weak, and encrypting is still possible.
    /// </remarks>
    private string ReadyToEncryptGuidance()
    {
        return Strength == PasswordStrength.Weak
            ? "The password is weak. You can still choose Encrypt file, but a few unrelated words would be safer."
            : "Ready. Choose Encrypt file.";
    }

    /// <summary>Keeps the guidance line in step with the typed password.</summary>
    /// <param name="value">The new password (unused: the properties are read).</param>
    partial void OnPasswordChanged(string value)
    {
        RefreshGuidance();
    }

    /// <summary>Keeps the guidance line in step with the typed confirmation.</summary>
    /// <param name="value">The new confirmation (unused: the properties are read).</param>
    partial void OnConfirmPasswordChanged(string value)
    {
        RefreshGuidance();
    }

    /// <summary>
    /// While a file is chosen and nothing is running, rewrites the guidance line; typing also clears an old error, which
    /// the user is now fixing.
    /// </summary>
    /// <remarks>
    /// Not while working, and not after a success (the result stays until a new file is chosen, and the program clears
    /// the passwords itself then).
    /// </remarks>
    private void RefreshGuidance()
    {
        if (SelectedFile is null || State is not (MainViewState.FileSelected or MainViewState.Failed))
        {
            return;
        }

        State = MainViewState.FileSelected;
        StatusMessage = GuidanceMessage();
    }

    /// <summary>Tells whether the file's name ends in <c>.enc</c>, whatever its content.</summary>
    /// <param name="file">The chosen file.</param>
    /// <returns><see langword="true"/> when the name ends in <c>.enc</c>.</returns>
    private static bool HasEncryptedName(FileDetails file)
    {
        return file.Name.EndsWith(EncryptedFileFormat.FileExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stops the running encryption or decryption; the output file is not created (atomic write, ADR-07).</summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _operationCts?.Cancel();
    }

    /// <summary>Cancel is available only while an operation (not a file read) is running.</summary>
    /// <returns><see langword="true"/> when there is something to cancel.</returns>
    private bool CanCancel()
    {
        return _operationCts is not null;
    }

    /// <summary>Shows the last result in the file manager; if that fails, says how to find it.</summary>
    /// <returns>A task that completes when the file manager was asked to open.</returns>
    /// <remarks>"File manager", not "File Explorer": the same ViewModel runs on Windows, macOS (Finder) and Linux.</remarks>
    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private async Task OpenFolderAsync()
    {
        if (!await _folderOpener.OpenContainingFolderAsync(_lastOutputPath!).ConfigureAwait(true))
        {
            // Appended to the success message (not replacing it): the operation did succeed, only showing it failed.
            StatusMessage = $"{_lastSuccessMessage} The folder couldn't be opened. Open it yourself in your file manager.";
        }
    }

    /// <summary>Open folder is available after a successful operation.</summary>
    /// <returns><see langword="true"/> when there is a result to show.</returns>
    private bool CanOpenFolder()
    {
        return State == MainViewState.Succeeded && _lastOutputPath is not null;
    }

    /// <summary>Encrypts the chosen file to a location the user picks (suggested: same folder, name + <c>.enc</c>).</summary>
    /// <returns>A task that completes when the encrypted file is in place.</returns>
    [RelayCommand(CanExecute = nameof(CanEncrypt))]
    private Task EncryptAsync()
    {
        return RunGuardedAsync(() => RunFileOperationAsync(encrypt: true));
    }

    /// <summary>Decrypts the chosen file to a location the user picks (suggested: same folder, name without <c>.enc</c>).</summary>
    /// <returns>A task that completes when the decrypted file is in place.</returns>
    [RelayCommand(CanExecute = nameof(CanRunFileOperation))]
    private Task DecryptAsync()
    {
        return RunGuardedAsync(() => RunFileOperationAsync(encrypt: false));
    }

    /// <summary>
    /// Puts the window back to a usable idle state after the app's global safety net (ADR-13, Layer 3) handled an
    /// unexpected error: no operation is shown as running, and the buttons follow the chosen file again.
    /// </summary>
    /// <remarks>
    /// Safe because every file operation is atomic (ADR-07): an interrupted one leaves no half-written result.
    /// </remarks>
    public void ResetAfterUnexpectedError()
    {
        if (State == MainViewState.Working)
        {
            return; // an operation is really running (the error came from elsewhere): do not re-enable the buttons under it
        }

        ResetToIdle();
    }

    /// <summary>Sets the idle state that matches whether a file is chosen.</summary>
    private void ResetToIdle()
    {
        ClearProgress();
        State = SelectedFile is null ? MainViewState.NoFile : MainViewState.FileSelected;
        StatusMessage = GuidanceMessage();
    }

    /// <summary>
    /// Runs a user action and handles the <b>expected</b> failures (ADR-13, Layer 2): log the details, show a plain message.
    /// </summary>
    /// <param name="action">The Browse or Encrypt/Decrypt work.</param>
    /// <returns>A task that completes when the action has ended, successfully or with a handled failure.</returns>
    /// <remarks>
    /// Only the specific types Core and the file system document are caught. <see cref="ArgumentException"/> (a
    /// programming error) and <see cref="Exception"/> are deliberately not caught: they go to the global safety net.
    /// <see cref="FileNotFoundException"/>, <see cref="DirectoryNotFoundException"/> and the like are covered by
    /// <see cref="IOException"/>.
    /// </remarks>
    private async Task RunGuardedAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            ShowCancelled(exception);
        }
        catch (SecureFileException exception)
        {
            Fail(exception);
        }
        catch (IOException exception)
        {
            Fail(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            Fail(exception);
        }
        catch (NotSupportedException exception)
        {
            Fail(exception);
        }
        finally
        {
            // A read that ended in an exception nobody here catches must not leave its file name behind for the next failure.
            _loadingFileName = null;

            // An unexpected exception (not caught above) ends the action while State is still Working; the global safety
            // net shows the dialog, and here the window is made usable again, because the operation is over.
            if (State == MainViewState.Working)
            {
                ResetToIdle();
            }
        }
    }

    /// <summary>Shows that the user cancelled: a neutral message, back to the chosen file, and nothing is logged.</summary>
    /// <param name="exception">The cancellation, used only for its standard message.</param>
    /// <remarks>
    /// Cancelling is something the user did on purpose, not an error: no red banner and no entry in the error log.
    /// </remarks>
    private void ShowCancelled(OperationCanceledException exception)
    {
        ClearProgress();
        State = SelectedFile is null ? MainViewState.NoFile : MainViewState.FileSelected;
        StatusMessage = ErrorMessageProvider.GetMessage(exception);
    }

    /// <summary>Forgets the progress of the last operation, so an old percentage or speed is never shown again.</summary>
    private void ClearProgress()
    {
        ProgressPercentage = 0;
        ProgressText = string.Empty;
    }

    /// <summary>Logs a handled exception and shows its plain message.</summary>
    /// <param name="exception">The expected exception.</param>
    /// <remarks>
    /// The chosen file is kept: a bad drop or Browse leaves the previous good selection in place (backlog B1 item 12), and
    /// a failed operation keeps its file so the user can fix the password and try again.
    /// </remarks>
    private void Fail(Exception exception)
    {
        _errorLog.WriteExpected(exception);
        ClearProgress();
        State = MainViewState.Failed;
        string message = ErrorMessageProvider.GetMessage(exception);

        // A failed Browse or drop leaves the previous file selected, so the message must name the file that failed
        // (second review R1): otherwise the old card beside an unnamed error looks like the new file.
        StatusMessage = _loadingFileName is null ? message : $"Couldn't open \"{_loadingFileName}\". {message}";
        _loadingFileName = null;
    }

    /// <summary>The shared flow of Encrypt and Decrypt: ask where to save, run the operation, report the result.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <returns>A task that completes when the operation has ended.</returns>
    /// <remarks>
    /// <c>overwriteExisting: true</c> is safe because the Save dialog has already asked the user before returning an
    /// existing file (<see cref="IFileDialogService.ShowSaveFileDialogAsync"/> contract). The input-equals-output check
    /// repeats the Core rule on purpose: Core enforces it with an <see cref="ArgumentException"/> (a programming
    /// error), while here the user gets a message and can choose again.
    /// </remarks>
    private async Task RunFileOperationAsync(bool encrypt)
    {
        if (RejectUnusablePassword() || (encrypt && RejectUnacceptableNewPassword()))
        {
            return;
        }

        // The checked password is kept here: the operation must use exactly what was checked, whatever happens to the
        // boxes while the Save dialog is open.
        string inputPath = SelectedFile!.FullPath;
        string password = Password;
        string? outputPath = await AskForOutputPathAsync(encrypt, inputPath).ConfigureAwait(true);
        if (outputPath is null)
        {
            return;
        }

        await ExecuteOperationAsync(encrypt, inputPath, outputPath, password).ConfigureAwait(true);
        ShowSuccess(encrypt, inputPath, outputPath);
    }

    /// <summary>Shows the result and forgets the file and the passwords.</summary>
    /// <param name="encrypt">Whether the finished operation was encryption.</param>
    /// <param name="inputPath">The file that was read (named in the reminder after encryption).</param>
    /// <param name="outputPath">The file that was written.</param>
    /// <remarks>
    /// The job is done, so the window shows the real new state (nothing chosen) and the passwords do not stay in memory
    /// or on screen longer than needed. On failure they are kept on purpose: the user fixes the password and tries again.
    /// </remarks>
    private void ShowSuccess(bool encrypt, string inputPath, string outputPath)
    {
        _lastOutputPath = outputPath;
        _lastSuccessMessage = SuccessMessage(encrypt, Path.GetFileName(inputPath), outputPath);
        State = MainViewState.Succeeded;
        StatusMessage = _lastSuccessMessage;
        SelectedFile = null;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    /// <summary>Runs the operation with a cancellation token and a clock for the progress line.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <param name="inputPath">The chosen file.</param>
    /// <param name="outputPath">Where the result goes.</param>
    /// <param name="password">The password that was checked before the Save dialog.</param>
    /// <returns>A task that completes when the operation has ended; throws <see cref="OperationCanceledException"/> after Cancel.</returns>
    private async Task ExecuteOperationAsync(bool encrypt, string inputPath, string outputPath, string password)
    {
        using var cancellation = new CancellationTokenSource();
        BeginOperation(encrypt, cancellation);
        try
        {
            await StartOperation(encrypt, inputPath, outputPath, password, cancellation.Token).ConfigureAwait(true);
        }
        finally
        {
            EndOperation();
        }
    }

    /// <summary>Switches to the Working state for a cancellable operation and starts the clock.</summary>
    /// <param name="encrypt">Whether the operation is encryption (decides the status text).</param>
    /// <param name="cancellation">The source that Cancel uses.</param>
    private void BeginOperation(bool encrypt, CancellationTokenSource cancellation)
    {
        _operationCts = cancellation;
        CancelCommand.NotifyCanExecuteChanged();
        _operationStart = _timeProvider.GetTimestamp();
        _lastProgressUpdate = TimeSpan.FromMilliseconds(-1) - MinimumProgressInterval; // the first report always shows
        State = MainViewState.Working;
        ProgressPercentage = 0;
        ProgressText = string.Empty;
        StatusMessage = encrypt ? "Encrypting…" : "Decrypting…";
    }

    /// <summary>Stops the clock and makes Cancel unavailable again; runs on success, failure and cancellation.</summary>
    private void EndOperation()
    {
        _operationCts = null;
        OnPropertyChanged(nameof(IsProgressIndeterminate));
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Shows the percentage, speed and time left; a late report that arrives after the end is ignored.</summary>
    /// <param name="percentage">How much is done, 0–100.</param>
    /// <remarks>
    /// The engine reports every whole percent, which on a fast disk is about 70 per second for a 100 MB file. The screen
    /// is updated at most once per <see cref="MinimumProgressInterval"/> (10 per second, plan §8.2); 100 % always shows.
    /// </remarks>
    internal void OnProgress(int percentage)
    {
        if (_operationCts is null)
        {
            return;
        }

        TimeSpan now = _timeProvider.GetElapsedTime(_operationStart);
        if (percentage < ProgressComplete && now - _lastProgressUpdate < MinimumProgressInterval)
        {
            return;
        }

        _lastProgressUpdate = now;
        ProgressPercentage = percentage;
        ProgressText = ProgressTextFormatter.Format(percentage, SelectedFile?.SizeInBytes ?? 0, now, CultureInfo.CurrentCulture);
    }

    /// <summary>Fails the action with a message when the password cannot be used.</summary>
    /// <returns><see langword="true"/> when the password was refused and the action must stop.</returns>
    private bool RejectUnusablePassword()
    {
        if (IsPasswordEncodable(Password))
        {
            return false;
        }

        State = MainViewState.Failed;
        StatusMessage = "The password contains a character that can't be used. Please type it again.";
        return true;
    }

    /// <summary>Fails the action when a password chosen for encryption is too short or was not typed the same twice.</summary>
    /// <returns><see langword="true"/> when the password was refused and the action must stop.</returns>
    /// <remarks>
    /// Only for encryption: a file's password may come from elsewhere, so Decrypt accepts any password. A mismatch is
    /// refused because a typo here would make the file impossible to open, and nobody can reset the password.
    /// </remarks>
    private bool RejectUnacceptableNewPassword()
    {
        if (Password.Length < PasswordStrengthEvaluator.MinimumLength)
        {
            State = MainViewState.Failed;
            StatusMessage = string.Create(CultureInfo.CurrentCulture, $"The password is too short. Use at least {PasswordStrengthEvaluator.MinimumLength} characters.");
            return true;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            State = MainViewState.Failed;
            StatusMessage = "The two passwords are not the same. Type the password again in both boxes.";
            return true;
        }

        return false;
    }

    /// <summary>Checks that the password can be turned into UTF-8 (a lone surrogate, for example from a bad paste, cannot).</summary>
    /// <param name="password">The typed password.</param>
    /// <returns><see langword="true"/> when Core will accept the password.</returns>
    /// <remarks>
    /// Core refuses such a password with an <see cref="ArgumentException"/> (a programming error). A user can reach it
    /// by pasting, so the ViewModel checks first and shows a message instead of the unexpected-error dialog.
    /// </remarks>
    private static bool IsPasswordEncodable(string password)
    {
        try
        {
            // Stryker disable once Boolean : the BOM flag only affects GetPreamble, never GetByteCount; equivalent mutant
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetByteCount(password);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Starts encryption or decryption with progress reported to <see cref="ProgressPercentage"/>.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <param name="inputPath">The chosen file.</param>
    /// <param name="outputPath">Where the result goes (the user confirmed replacing it if it exists).</param>
    /// <param name="password">The checked password.</param>
    /// <param name="cancellationToken">Cancelled when the user chooses Cancel.</param>
    /// <returns>The running operation.</returns>
    private Task StartOperation(bool encrypt, string inputPath, string outputPath, string password, CancellationToken cancellationToken)
    {
        var progress = new Progress<int>(OnProgress);
        return encrypt
            ? _encryptionService.EncryptAsync(inputPath, outputPath, password, overwriteExisting: true, progress, cancellationToken)
            : _encryptionService.DecryptAsync(inputPath, outputPath, password, overwriteExisting: true, progress, cancellationToken);
    }

    /// <summary>Shows the Save dialog with a suggestion and refuses the input itself as the output.</summary>
    /// <param name="encrypt">Whether the operation is encryption (decides the suggested name).</param>
    /// <param name="inputPath">The chosen file.</param>
    /// <returns>The output path, or <see langword="null"/> when the user cancelled or chose the input.</returns>
    private async Task<string?> AskForOutputPathAsync(bool encrypt, string inputPath)
    {
        string? outputPath = await ShowSaveDialogLockedAsync(encrypt ? SuggestEncryptedPath(inputPath) : SuggestDecryptedPath(inputPath)).ConfigureAwait(true);
        if (outputPath is not null && IsSameFile(inputPath, outputPath))
        {
            State = MainViewState.Failed;
            StatusMessage = "Choose a different name: the result can't replace the file you selected.";
            return null;
        }

        if (outputPath is null && State == MainViewState.Failed)
        {
            // Cancelling the Save dialog is the user's choice, not an error: an old red message from an earlier try must go
            // (backlog B1 item 20); the password and the file stay so a second try is one click.
            State = MainViewState.FileSelected;
            StatusMessage = GuidanceMessage();
        }

        return outputPath;
    }

    /// <summary>Shows the Save dialog with the inputs and actions locked until it closes (<see cref="IsFileDialogOpen"/>).</summary>
    /// <param name="suggestedFilePath">The path to suggest.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user cancelled.</returns>
    private Task<string?> ShowSaveDialogLockedAsync(string suggestedFilePath)
    {
        return ShowDialogLockedAsync(() => _fileDialogs.ShowSaveFileDialogAsync(suggestedFilePath));
    }

    /// <summary>Shows a file dialog with the inputs and actions locked until it closes (<see cref="IsFileDialogOpen"/>).</summary>
    /// <param name="showDialog">Opens the Open or Save dialog.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user cancelled.</returns>
    private async Task<string?> ShowDialogLockedAsync(Func<Task<string?>> showDialog)
    {
        IsFileDialogOpen = true;
        try
        {
            return await showDialog().ConfigureAwait(true);
        }
        finally
        {
            IsFileDialogOpen = false;
        }
    }

    /// <summary>Tells whether writing <paramref name="output"/> would replace <paramref name="input"/>.</summary>
    /// <param name="input">The chosen file.</param>
    /// <param name="output">The path from the Save dialog.</param>
    /// <returns><see langword="true"/> when both name the same file.</returns>
    /// <remarks>
    /// Uses Core's own check (links, case and Unicode aliases): a plain text comparison let a folder link through, and
    /// Core's <see cref="ArgumentException"/> then reached the safety net as an "unexpected" error (review VM-2). A link
    /// loop throws <see cref="IOException"/>, which the guarded command turns into a plain message.
    /// </remarks>
    private static bool IsSameFile(string input, string output)
    {
        return FileIdentity.AreSameFile(Path.GetFullPath(input), Path.GetFullPath(output));
    }

    /// <summary>
    /// The success message: the result's name and folder, and after encryption a reminder that the unencrypted original
    /// is still on the disk (review item 25).
    /// </summary>
    /// <param name="encrypt">Whether the operation was encryption.</param>
    /// <param name="inputName">The original file's name.</param>
    /// <param name="outputPath">The file that was written.</param>
    /// <returns>The message to show.</returns>
    /// <remarks>
    /// "Your original file was not changed" was true but read by some as "it is safe now"; the new text says plainly
    /// that the readable original is still there. Only the folder's name is shown, not its full path, so the banner
    /// stays short; Open folder shows the rest.
    /// </remarks>
    internal static string SuccessMessage(bool encrypt, string inputName, string outputPath)
    {
        string outputName = Path.GetFileName(outputPath);
        string folder = FolderName(outputPath);
        return encrypt
            ? $"Encrypted copy saved as \"{outputName}\" in the folder \"{folder}\". The unencrypted original \"{inputName}\" is still there: delete it yourself if only the encrypted copy should remain."
            : $"Decrypted file saved as \"{outputName}\" in the folder \"{folder}\".";
    }

    /// <summary>The name of the folder that holds <paramref name="filePath"/>, or the drive (such as <c>C:\</c>) at the top.</summary>
    /// <param name="filePath">A full file path.</param>
    /// <returns>The folder's own name.</returns>
    private static string FolderName(string filePath)
    {
        string folder = Path.GetDirectoryName(filePath) ?? filePath;
        string name = Path.GetFileName(folder);
        return name.Length > 0 ? name : folder;
    }

    /// <summary>Browse is available whenever no operation is running.</summary>
    /// <returns><see langword="true"/> when the user may choose a file.</returns>
    private bool CanBrowse()
    {
        return !IsBusy && !IsFileDialogOpen;
    }

    /// <summary>Encrypt also needs a password of at least the minimum length that was typed the same twice.</summary>
    /// <returns><see langword="true"/> when the user may start encryption.</returns>
    /// <remarks>
    /// The button is disabled until then, with the strength and match texts saying why. The same two checks still run
    /// when the operation starts (<see cref="RejectUnacceptableNewPassword"/>) as a safety net for any other caller.
    /// </remarks>
    private bool CanEncrypt()
    {
        return CanRunFileOperation() && Password.Length >= PasswordStrengthEvaluator.MinimumLength && PasswordsMatch;
    }

    /// <summary>Enter in the password box needs what the operation it would start needs.</summary>
    /// <returns><see langword="true"/> when Enter may start Encrypt or Decrypt.</returns>
    private bool CanRunPrimary()
    {
        return IsEncryptedFileSelected() ? CanRunFileOperation() : CanEncrypt();
    }

    /// <summary>Encrypt and Decrypt need a chosen file and a password, and no operation may be running.</summary>
    /// <returns><see langword="true"/> when the user may start an operation.</returns>
    private bool CanRunFileOperation()
    {
        return SelectedFile is not null && Password.Length > 0 && !IsBusy && !IsFileDialogOpen;
    }

    /// <summary>Suggests <c>report.pdf.enc</c> next to <c>report.pdf</c>.</summary>
    /// <param name="inputPath">The file to encrypt.</param>
    /// <returns>The suggested output path.</returns>
    private static string SuggestEncryptedPath(string inputPath)
    {
        return inputPath + EncryptedFileFormat.FileExtension;
    }

    /// <summary>
    /// Suggests <c>report.pdf</c> next to <c>report.pdf.enc</c>; a file without the <c>.enc</c> ending gets
    /// <c>.decrypted</c> added, so the suggestion never names the input itself.
    /// </summary>
    /// <param name="inputPath">The file to decrypt.</param>
    /// <returns>The suggested output path.</returns>
    private static string SuggestDecryptedPath(string inputPath)
    {
        string fileName = Path.GetFileName(inputPath);
        bool hasEncryptedEnding = fileName.Length > EncryptedFileFormat.FileExtension.Length
            && fileName.EndsWith(EncryptedFileFormat.FileExtension, StringComparison.OrdinalIgnoreCase);

        return hasEncryptedEnding
            ? inputPath[..^EncryptedFileFormat.FileExtension.Length]
            : inputPath + DecryptedFileExtension;
    }

    /// <summary>Shows an extension for people: <c>.pdf</c>, or "no extension".</summary>
    /// <param name="extension">The extension including the dot, or an empty string.</param>
    /// <returns>The text to show.</returns>
    private static string ExtensionText(string extension)
    {
        return extension.Length == 0 ? "no extension" : extension;
    }
}

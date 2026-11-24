using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureFile.Core;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
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
    /// <summary>The ending added to a decrypted file's name when the encrypted file does not end in <c>.enc</c>.</summary>
    private const string DecryptedFileExtension = ".decrypted";

    private readonly IFileEncryptionService _encryptionService;
    private readonly IFileDetailsReader _fileDetailsReader;
    private readonly IFileDialogService _fileDialogs;
    private readonly IErrorLog _errorLog;

    /// <summary>Creates the ViewModel with the services it uses (pure dependency injection, ADR-09).</summary>
    /// <param name="encryptionService">Encrypts and decrypts files.</param>
    /// <param name="fileDetailsReader">Reads the name and size of the chosen file.</param>
    /// <param name="fileDialogs">Shows the Open and Save dialogs.</param>
    /// <param name="errorLog">Records the technical details of handled errors.</param>
    /// <exception cref="ArgumentNullException">A service is <see langword="null"/>.</exception>
    public MainViewModel(IFileEncryptionService encryptionService, IFileDetailsReader fileDetailsReader, IFileDialogService fileDialogs, IErrorLog errorLog)
    {
        ArgumentNullException.ThrowIfNull(encryptionService);
        ArgumentNullException.ThrowIfNull(fileDetailsReader);
        ArgumentNullException.ThrowIfNull(fileDialogs);
        ArgumentNullException.ThrowIfNull(errorLog);

        _errorLog = errorLog;
        _encryptionService = encryptionService;
        _fileDetailsReader = fileDetailsReader;
        _fileDialogs = fileDialogs;
    }

    /// <summary>Gets the current state; it decides which commands can run (see <see cref="MainViewState"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand), nameof(EncryptCommand), nameof(DecryptCommand))]
    public partial MainViewState State { get; private set; } = MainViewState.NoFile;

    /// <summary>Gets the details of the chosen file, or <see langword="null"/> when no file is chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSummary))]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand), nameof(DecryptCommand))]
    public partial FileDetails? SelectedFile { get; private set; }

    /// <summary>Gets or sets the password typed by the user. Spaces count as characters and are never trimmed.</summary>
    /// <remarks>
    /// A plain <see cref="string"/>: WPF's <c>PasswordBox</c> hands its text over as a string anyway, and the Core API
    /// takes one (PROJECT_PLAN §5.2 lists the copies we cannot wipe). The window sets it from code-behind, because
    /// <c>PasswordBox.Password</c> cannot be data-bound on purpose.
    /// </remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EncryptCommand), nameof(DecryptCommand))]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Gets how much of the running operation is done, 0–100.</summary>
    [ObservableProperty]
    public partial int ProgressPercentage { get; private set; }

    /// <summary>Gets the message that tells the user what happened and what to do next.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "Choose a file to encrypt or decrypt.";

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
        return RunGuardedAsync(BrowseCoreAsync, clearSelectionOnFailure: true);
    }

    /// <summary>The Browse work itself, without the error handling around it.</summary>
    /// <returns>A task that completes when the details are shown.</returns>
    private async Task BrowseCoreAsync()
    {
        string? path = _fileDialogs.ShowOpenFileDialog();
        if (path is null)
        {
            return;
        }

        State = MainViewState.Working;
        StatusMessage = "Reading the file details…";
        SelectedFile = await Task.Run(() => _fileDetailsReader.Read(path)).ConfigureAwait(true);
        State = MainViewState.FileSelected;
        StatusMessage = "Enter the password, then choose Encrypt or Decrypt.";
    }

    /// <summary>Encrypts the chosen file to a location the user picks (suggested: same folder, name + <c>.enc</c>).</summary>
    /// <returns>A task that completes when the encrypted file is in place.</returns>
    [RelayCommand(CanExecute = nameof(CanRunFileOperation))]
    private Task EncryptAsync()
    {
        return RunGuardedAsync(() => RunFileOperationAsync(encrypt: true), clearSelectionOnFailure: false);
    }

    /// <summary>Decrypts the chosen file to a location the user picks (suggested: same folder, name without <c>.enc</c>).</summary>
    /// <returns>A task that completes when the decrypted file is in place.</returns>
    [RelayCommand(CanExecute = nameof(CanRunFileOperation))]
    private Task DecryptAsync()
    {
        return RunGuardedAsync(() => RunFileOperationAsync(encrypt: false), clearSelectionOnFailure: false);
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
        ProgressPercentage = 0;
        State = SelectedFile is null ? MainViewState.NoFile : MainViewState.FileSelected;
        StatusMessage = SelectedFile is null
            ? "Choose a file to encrypt or decrypt."
            : "Enter the password, then choose Encrypt or Decrypt.";
    }

    /// <summary>
    /// Runs a user action and handles the <b>expected</b> failures (ADR-13, Layer 2): log the details, show a plain message.
    /// </summary>
    /// <param name="action">The Browse or Encrypt/Decrypt work.</param>
    /// <param name="clearSelectionOnFailure">Whether a failure also forgets the chosen file (true for Browse, whose file could not be read).</param>
    /// <returns>A task that completes when the action has ended, successfully or with a handled failure.</returns>
    /// <remarks>
    /// Only the specific types Core and the file system document are caught. <see cref="ArgumentException"/> (a
    /// programming error) and <see cref="Exception"/> are deliberately not caught: they go to the global safety net.
    /// <see cref="FileNotFoundException"/>, <see cref="DirectoryNotFoundException"/> and the like are covered by
    /// <see cref="IOException"/>.
    /// </remarks>
    private async Task RunGuardedAsync(Func<Task> action, bool clearSelectionOnFailure)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            Fail(exception, clearSelectionOnFailure);
        }
        catch (SecureFileException exception)
        {
            Fail(exception, clearSelectionOnFailure);
        }
        catch (IOException exception)
        {
            Fail(exception, clearSelectionOnFailure);
        }
        catch (UnauthorizedAccessException exception)
        {
            Fail(exception, clearSelectionOnFailure);
        }
        catch (NotSupportedException exception)
        {
            Fail(exception, clearSelectionOnFailure);
        }
    }

    /// <summary>Logs a handled exception and shows its plain message.</summary>
    /// <param name="exception">The expected exception.</param>
    /// <param name="clearSelection">Whether to forget the chosen file.</param>
    private void Fail(Exception exception, bool clearSelection)
    {
        _errorLog.Write(exception);
        if (clearSelection)
        {
            SelectedFile = null;
        }

        State = MainViewState.Failed;
        StatusMessage = ErrorMessageProvider.GetMessage(exception);
    }

    /// <summary>The shared flow of Encrypt and Decrypt: ask where to save, run the operation, report the result.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <returns>A task that completes when the operation has ended.</returns>
    /// <remarks>
    /// <c>overwriteExisting: true</c> is safe because the Save dialog has already asked the user before returning an
    /// existing file (<see cref="IFileDialogService.ShowSaveFileDialog"/> contract). The input-equals-output check
    /// repeats the Core rule on purpose: Core enforces it with an <see cref="ArgumentException"/> (a programming
    /// error), while here the user gets a message and can choose again.
    /// </remarks>
    private async Task RunFileOperationAsync(bool encrypt)
    {
        string inputPath = SelectedFile!.FullPath;
        string? outputPath = AskForOutputPath(encrypt, inputPath);
        if (outputPath is null)
        {
            return;
        }

        State = MainViewState.Working;
        ProgressPercentage = 0;
        StatusMessage = encrypt ? "Encrypting…" : "Decrypting…";
        await StartOperation(encrypt, inputPath, outputPath).ConfigureAwait(true);

        State = MainViewState.Succeeded;
        StatusMessage = SuccessMessage(encrypt, Path.GetFileName(outputPath));
    }

    /// <summary>Starts encryption or decryption with progress reported to <see cref="ProgressPercentage"/>.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <param name="inputPath">The chosen file.</param>
    /// <param name="outputPath">Where the result goes (the user confirmed replacing it if it exists).</param>
    /// <returns>The running operation.</returns>
    private Task StartOperation(bool encrypt, string inputPath, string outputPath)
    {
        var progress = new Progress<int>(percentage => ProgressPercentage = percentage);
        return encrypt
            ? _encryptionService.EncryptAsync(inputPath, outputPath, Password, overwriteExisting: true, progress)
            : _encryptionService.DecryptAsync(inputPath, outputPath, Password, overwriteExisting: true, progress);
    }

    /// <summary>Shows the Save dialog with a suggestion and refuses the input itself as the output.</summary>
    /// <param name="encrypt">Whether the operation is encryption (decides the suggested name).</param>
    /// <param name="inputPath">The chosen file.</param>
    /// <returns>The output path, or <see langword="null"/> when the user cancelled or chose the input.</returns>
    private string? AskForOutputPath(bool encrypt, string inputPath)
    {
        string? outputPath = _fileDialogs.ShowSaveFileDialog(encrypt ? SuggestEncryptedPath(inputPath) : SuggestDecryptedPath(inputPath));
        if (outputPath is not null && IsSameFile(inputPath, outputPath))
        {
            State = MainViewState.Failed;
            StatusMessage = "Choose a different name: the result can't replace the file you selected.";
            return null;
        }

        return outputPath;
    }

    /// <summary>Compares full paths ignoring case, like Core's own check (Windows and macOS ignore case).</summary>
    /// <param name="first">One path.</param>
    /// <param name="second">The other path.</param>
    /// <returns><see langword="true"/> when both name the same file.</returns>
    private static bool IsSameFile(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The success message; after encryption it says the original is unchanged (PROJECT_PLAN §11.1).</summary>
    /// <param name="encrypt">Whether the operation was encryption.</param>
    /// <param name="outputName">The result's file name (never the full path).</param>
    /// <returns>The message to show.</returns>
    private static string SuccessMessage(bool encrypt, string outputName)
    {
        return encrypt
            ? $"Encrypted copy saved as \"{outputName}\". Your original file was not changed."
            : $"Decrypted file saved as \"{outputName}\".";
    }

    /// <summary>Browse is available whenever no operation is running.</summary>
    /// <returns><see langword="true"/> when the user may choose a file.</returns>
    private bool CanBrowse()
    {
        return !IsBusy;
    }

    /// <summary>Encrypt and Decrypt need a chosen file and a password, and no operation may be running.</summary>
    /// <returns><see langword="true"/> when the user may start an operation.</returns>
    private bool CanRunFileOperation()
    {
        return SelectedFile is not null && Password.Length > 0 && !IsBusy;
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

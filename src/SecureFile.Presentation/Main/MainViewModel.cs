using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;

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
    private readonly IFileDetailsReader _fileDetailsReader;
    private readonly IFileDialogService _fileDialogs;

    /// <summary>Creates the ViewModel with the services it uses (pure dependency injection, ADR-09).</summary>
    /// <param name="fileDetailsReader">Reads the name and size of the chosen file.</param>
    /// <param name="fileDialogs">Shows the Open and Save dialogs.</param>
    /// <exception cref="ArgumentNullException">A service is <see langword="null"/>.</exception>
    public MainViewModel(IFileDetailsReader fileDetailsReader, IFileDialogService fileDialogs)
    {
        ArgumentNullException.ThrowIfNull(fileDetailsReader);
        ArgumentNullException.ThrowIfNull(fileDialogs);

        _fileDetailsReader = fileDetailsReader;
        _fileDialogs = fileDialogs;
    }

    /// <summary>Gets the current state; it decides which commands can run (see <see cref="MainViewState"/>).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    public partial MainViewState State { get; private set; } = MainViewState.NoFile;

    /// <summary>Gets the details of the chosen file, or <see langword="null"/> when no file is chosen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileSummary))]
    public partial FileDetails? SelectedFile { get; private set; }

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
    private async Task BrowseAsync()
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

    /// <summary>Browse is available whenever no operation is running.</summary>
    /// <returns><see langword="true"/> when the user may choose a file.</returns>
    private bool CanBrowse()
    {
        return !IsBusy;
    }

    /// <summary>Shows an extension for people: <c>.pdf</c>, or "no extension".</summary>
    /// <param name="extension">The extension including the dot, or an empty string.</param>
    /// <returns>The text to show.</returns>
    private static string ExtensionText(string extension)
    {
        return extension.Length == 0 ? "no extension" : extension;
    }
}

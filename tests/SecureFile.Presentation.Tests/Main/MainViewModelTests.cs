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

    private readonly FakeFileDialogService _dialogs = new();
    private readonly FakeFileDetailsReader _detailsReader = new();

    [Fact]
    public void NewViewModel_HasNoFileAndOnlyBrowseAvailable()
    {
        MainViewModel viewModel = CreateViewModel();

        Assert.Equal(MainViewState.NoFile, viewModel.State);
        Assert.Null(viewModel.SelectedFile);
        Assert.Equal("No file selected.", viewModel.FileSummary);
        Assert.True(viewModel.BrowseCommand.CanExecute(null));
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

    [Fact]
    public void Constructor_NullService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("fileDetailsReader", () => new MainViewModel(null!, _dialogs));
        Assert.Throws<ArgumentNullException>("fileDialogs", () => new MainViewModel(_detailsReader, null!));
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_detailsReader, _dialogs);
    }
}

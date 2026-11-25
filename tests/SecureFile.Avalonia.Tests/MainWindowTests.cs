using System.Security.Cryptography;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Dialogs;
using SecureFile.Presentation.Errors;
using SecureFile.Presentation.Main;

namespace SecureFile.Avalonia.Tests;

/// <summary>
/// The Avalonia window end to end: real XAML bindings, real ViewModel, real Core, real temporary files. Buttons are
/// pressed through their accessibility peers (what a screen reader does), text goes in through the text boxes.
/// </summary>
public sealed class MainWindowTests : IDisposable
{
    private const string Password = "correct horse battery";

    private readonly string _folder = Directory.CreateTempSubdirectory("securefile-avalonia-").FullName;
    private readonly ScriptedDialogs _dialogs = new();

    [Fact]
    public Task EncryptThenDecrypt_ThroughTheWindow_RestoresTheOriginalBytes()
    {
        return TestApplication.RunOnUiThreadAsync(EncryptThenDecryptAsync);
    }

    [Fact]
    public Task PasswordBoxes_HideTheText_RefuseCopy_AndRevealOnlyWithShowPassword()
    {
        return TestApplication.RunOnUiThreadAsync(PasswordBoxesAsync);
    }

    [Fact]
    public Task WeakPassword_ColoursTheStrengthBarRed_AndStrongTurnsItGreen()
    {
        return TestApplication.RunOnUiThreadAsync(StrengthColourAsync);
    }

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    private async Task EncryptThenDecryptAsync()
    {
        string original = Path.Combine(_folder, "report.bin");
        byte[] content = RandomNumberGenerator.GetBytes(200_000);
        await File.WriteAllBytesAsync(original, content, TestContext.Current.CancellationToken);
        (MainWindow window, MainViewModel viewModel) = Open();

        await ChooseFileAsync(window, viewModel, original);
        Find<TextBox>(window, "PasswordInput").Text = Password;
        Find<TextBox>(window, "ConfirmInput").Text = Password;
        _dialogs.Save = original + ".enc";
        await ClickAndWaitAsync(window, "EncryptButton", viewModel);
        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal(string.Empty, Find<TextBox>(window, "PasswordInput").Text); // cleared on screen too

        string restored = Path.Combine(_folder, "restored.bin");
        await ChooseFileAsync(window, viewModel, original + ".enc");
        Find<TextBox>(window, "PasswordInput").Text = Password;
        _dialogs.Save = restored;
        await ClickAndWaitAsync(window, "DecryptButton", viewModel);

        Assert.Equal(MainViewState.Succeeded, viewModel.State);
        Assert.Equal(content, await File.ReadAllBytesAsync(restored, TestContext.Current.CancellationToken));
        window.Close();
    }

    private async Task PasswordBoxesAsync()
    {
        string original = Path.Combine(_folder, "notes.txt");
        await File.WriteAllTextAsync(original, "hello", TestContext.Current.CancellationToken);
        (MainWindow window, MainViewModel viewModel) = Open();
        await ChooseFileAsync(window, viewModel, original);
        TextBox password = Find<TextBox>(window, "PasswordInput");
        password.Text = Password;
        password.SelectAll();

        Assert.NotEqual(default, password.PasswordChar);
        Assert.False(password.RevealPassword);
        Assert.False(password.CanCopy);

        Find<CheckBox>(window, "ShowPasswordCheck").IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.True(password.RevealPassword);
        Assert.True(Find<TextBox>(window, "ConfirmInput").RevealPassword);

        // Avalonia itself allows Copy once the text is revealed; the window's clipboard guard must stop it.
        IClipboard clipboard = window.Clipboard ?? throw new InvalidOperationException("The headless window has no clipboard.");
        await clipboard.SetTextAsync("before");
        password.SelectAll();
        password.Copy();
        password.Cut();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("before", await clipboard.TryGetTextAsync());
        Assert.Equal(Password, password.Text); // Cut removed nothing either
        window.Close();
    }

    private async Task StrengthColourAsync()
    {
        string original = Path.Combine(_folder, "photo.jpg");
        await File.WriteAllBytesAsync(original, [1, 2, 3], TestContext.Current.CancellationToken);
        (MainWindow window, MainViewModel viewModel) = Open();
        await ChooseFileAsync(window, viewModel, original);
        ProgressBar bar = Find<ProgressBar>(window, "StrengthBar");

        Find<TextBox>(window, "PasswordInput").Text = "abc";
        Assert.Contains("critical", bar.Classes);

        Find<TextBox>(window, "PasswordInput").Text = "violet-lantern-orbit-73";
        Assert.Contains("success", bar.Classes);
        Assert.DoesNotContain("critical", bar.Classes);
        window.Close();
    }

    /// <summary>Creates and shows the window with the real services, except the dialogs and the folder opener.</summary>
    private (MainWindow Window, MainViewModel ViewModel) Open()
    {
        var viewModel = new MainViewModel(
            new FileEncryptionService(),
            new FileDetailsReader(),
            _dialogs,
            new FileErrorLog(Path.Combine(_folder, "test.log")),
            new NoFolderOpener());
        var window = new MainWindow(viewModel);
        window.Show();
        return (window, viewModel);
    }

    /// <summary>Clicks Browse (the scripted dialog answers with the path) and waits until the file is selected.</summary>
    private async Task ChooseFileAsync(MainWindow window, MainViewModel viewModel, string path)
    {
        _dialogs.Open = path;
        Click(window, "BrowseButton");
        await WaitUntilAsync(() => viewModel.SelectedFile?.FullPath == path && !viewModel.IsBusy);
    }

    /// <summary>Clicks a button and waits until the operation it started has ended.</summary>
    private static async Task ClickAndWaitAsync(MainWindow window, string buttonName, MainViewModel viewModel)
    {
        Dispatcher.UIThread.RunJobs(); // let the bindings enable the button first
        Click(window, buttonName);
        await WaitUntilAsync(() => viewModel.State is MainViewState.Succeeded or MainViewState.Failed);
    }

    /// <summary>Presses a button the way assistive technology does (its automation "Invoke"), which runs its command.</summary>
    private static void Click(MainWindow window, string buttonName)
    {
        Button button = Find<Button>(window, buttonName);
        Assert.True(button.IsEffectivelyEnabled, $"{buttonName} is disabled.");
        var invoker = (IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(button);
        invoker.Invoke();
    }

    /// <summary>Lets the UI thread run until the condition holds (the work itself runs on the thread pool).</summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the window.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
    }

    private static T Find<T>(MainWindow window, string name)
        where T : Control
    {
        return window.FindControl<T>(name) ?? throw new InvalidOperationException($"No control named {name}.");
    }

    /// <summary>Dialogs that answer from a script instead of showing a window.</summary>
    private sealed class ScriptedDialogs : IFileDialogService
    {
        public string? Open { get; set; }

        public string? Save { get; set; }

        public Task<string?> ShowOpenFileDialogAsync()
        {
            return Task.FromResult(Open);
        }

        public Task<string?> ShowSaveFileDialogAsync(string suggestedFilePath)
        {
            return Task.FromResult(Save);
        }
    }

    /// <summary>A folder opener that does nothing and says it worked.</summary>
    private sealed class NoFolderOpener : IFolderOpener
    {
        public Task<bool> OpenContainingFolderAsync(string filePath)
        {
            return Task.FromResult(true);
        }
    }
}

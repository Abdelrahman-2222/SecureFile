using System.Windows;
using System.Windows.Threading;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Errors;
using SecureFile.Presentation.Main;
using SecureFile.Wpf.Services;

namespace SecureFile.Wpf;

/// <summary>
/// The WPF application object: starts the app and owns application-wide resources (App.xaml).
/// </summary>
/// <remarks>
/// <para>
/// This class is the <b>composition root</b> (ADR-09, "pure DI"): the one place where the app's objects are created
/// and connected.
/// </para>
/// <para>
/// It also hosts ADR-13 <b>Layer 3</b>, the global safety nets for errors nobody expected: they log the details,
/// tell the user in plain words, and keep the app running when that is safe.
/// </para>
/// </remarks>
internal sealed partial class App : Application
{
    /// <summary>A second unexpected error within this time means the state is not trustworthy: the app closes.</summary>
    private static readonly TimeSpan RepeatedErrorWindow = TimeSpan.FromSeconds(5);

    private FileErrorLog? _errorLog;
    private MainViewModel? _viewModel;
    private DateTime _lastUnexpectedErrorUtc = DateTime.MinValue;

    /// <summary>
    /// Runs once when the application starts: registers the safety nets, builds the objects and shows the window.
    /// </summary>
    /// <param name="e">Start-up information from WPF, such as command-line arguments.</param>
    /// <remarks>
    /// The safety nets come first so that an error during start-up is also logged. The window is created here in code
    /// instead of with <c>StartupUri</c>, so it can receive its ViewModel through its constructor.
    /// </remarks>
    protected override void OnStartup(StartupEventArgs e)
    {
        _errorLog = new FileErrorLog();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        _viewModel = new MainViewModel(new FileEncryptionService(), new FileDetailsReader(), new WpfFileDialogService(), _errorLog, new ExplorerFolderOpener());
        MainWindow mainWindow = new(_viewModel);
        mainWindow.Show();
    }

    /// <summary>An unexpected error on the UI thread: log it, tell the user, and keep running unless that is unsafe.</summary>
    /// <param name="sender">The dispatcher.</param>
    /// <param name="e">The error; <see cref="DispatcherUnhandledExceptionEventArgs.Handled"/> keeps the app alive.</param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _errorLog?.Write(e.Exception);
        e.Handled = true;

        string logFilePath = _errorLog?.LogFilePath ?? string.Empty;
        if (MustClose(e.Exception))
        {
            ShowSafetyNetMessage(SafetyNetMessages.MustClose(logFilePath), MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _viewModel?.ResetAfterUnexpectedError();
        ShowSafetyNetMessage(SafetyNetMessages.Recovered(logFilePath), MessageBoxImage.Warning);
    }

    /// <summary>Decides whether the app must close: a fatal error, or a second one within <see cref="RepeatedErrorWindow"/>.</summary>
    /// <param name="exception">The unexpected error.</param>
    /// <returns><see langword="true"/> when continuing would not be safe.</returns>
    private bool MustClose(Exception exception)
    {
        DateTime now = DateTime.UtcNow;
        bool mustClose = IsFatal(exception) || now - _lastUnexpectedErrorUtc < RepeatedErrorWindow;
        _lastUnexpectedErrorUtc = now;
        return mustClose;
    }

    /// <summary>Shows a safety-net message, owned by the main window while it is open (review item 15).</summary>
    /// <param name="text">The message, which names the log file.</param>
    /// <param name="icon">Warning when the app keeps running, Error when it closes.</param>
    /// <remarks>
    /// With the main window as owner the message stays on top of it and is centred on it, instead of possibly opening
    /// behind it, where the frozen-looking window would be all the user sees.
    /// </remarks>
    private void ShowSafetyNetMessage(string text, MessageBoxImage icon)
    {
        if (MainWindow is { IsLoaded: true } owner)
        {
            MessageBox.Show(owner, text, "SecureFile", MessageBoxButton.OK, icon);
        }
        else
        {
            MessageBox.Show(text, "SecureFile", MessageBoxButton.OK, icon);
        }
    }

    /// <summary>An unexpected error on another thread: only log it, because the runtime closes the app anyway.</summary>
    /// <param name="sender">The app domain.</param>
    /// <param name="e">The error details.</param>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _errorLog?.Write(exception);
        }
    }

    /// <summary>A faulted task nobody awaited: log it and mark it observed, so the bug is not hidden.</summary>
    /// <param name="sender">The task scheduler.</param>
    /// <param name="e">The task's exception.</param>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _errorLog?.Write(e.Exception);
        e.SetObserved();
    }

    /// <summary>Whether the error signals a broken process, where continuing would not be safe.</summary>
    /// <param name="exception">The unexpected error.</param>
    /// <returns><see langword="true"/> for errors such as out of memory or an invalid program.</returns>
    private static bool IsFatal(Exception exception)
    {
        return exception is OutOfMemoryException or InvalidProgramException or AccessViolationException;
    }
}

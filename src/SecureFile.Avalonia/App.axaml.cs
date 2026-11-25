using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SecureFile.Avalonia.Services;
using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;
using SecureFile.Presentation.Errors;
using SecureFile.Presentation.Main;

namespace SecureFile.Avalonia;

/// <summary>The Avalonia application object: the composition root and the global safety nets (ADR-09, ADR-13).</summary>
/// <remarks>
/// <para>
/// The same objects as the WPF app are built here: the same Core services, the same <see cref="MainViewModel"/> and the
/// same <see cref="FileErrorLog"/>. Only the three UI services differ (Avalonia dialogs and launcher, Step 7.2).
/// </para>
/// <para>
/// ADR-13 Layer 3 works as in WPF: an unexpected UI-thread error is logged, the user is told in plain words, and the
/// app keeps running unless the error is fatal or a second one follows within a few seconds.
/// </para>
/// </remarks>
internal sealed partial class App : Application
{
    /// <summary>A second unexpected error within this time means the state is not trustworthy: the app closes.</summary>
    private static readonly TimeSpan RepeatedErrorWindow = TimeSpan.FromSeconds(5);

    private FileErrorLog? _errorLog;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private DateTime _lastUnexpectedErrorUtc = DateTime.MinValue;

    /// <summary>True once the "must close" message is shown, so later errors do not stack more dialogs on top.</summary>
    private bool _closing;

    /// <summary>Loads App.axaml (theme and colours).</summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Registers the safety nets, builds the objects and shows the main window.</summary>
    /// <remarks>
    /// The services get the window through a function, not directly: the window needs the ViewModel in its constructor,
    /// and the ViewModel needs the services, so the window is looked up only when a dialog is actually shown.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        _errorLog = new FileErrorLog();
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            TopLevel? FindWindow() => _mainWindow;
            _viewModel = new MainViewModel(
                new FileEncryptionService(),
                new FileDetailsReader(),
                new AvaloniaFileDialogService(FindWindow),
                _errorLog,
                new LauncherFolderOpener(FindWindow));
            _mainWindow = new MainWindow(_viewModel);
            desktop.MainWindow = _mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>An unexpected error on the UI thread: log it, tell the user, and keep running unless that is unsafe.</summary>
    /// <param name="sender">The dispatcher.</param>
    /// <param name="e">The error; <see cref="DispatcherUnhandledExceptionEventArgs.Handled"/> keeps the app alive.</param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _errorLog?.Write(e.Exception);
        e.Handled = true;
        if (_closing)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        bool mustClose = IsFatal(e.Exception) || now - _lastUnexpectedErrorUtc < RepeatedErrorWindow;
        _lastUnexpectedErrorUtc = now;

        if (mustClose)
        {
            CloseAfterFatalError();
            return;
        }

        _viewModel?.ResetAfterUnexpectedError();
        MessageDialog.ShowInformation(_mainWindow, SafetyNetMessages.Recovered(_errorLog?.LogFilePath ?? string.Empty), onClosed: null);
    }

    /// <summary>Tells the user the app must close, then exits with code 1 when the message is dismissed.</summary>
    private void CloseAfterFatalError()
    {
        _closing = true;
        MessageDialog.ShowInformation(
            _mainWindow,
            SafetyNetMessages.MustClose(_errorLog?.LogFilePath ?? string.Empty),
            () => (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1));
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

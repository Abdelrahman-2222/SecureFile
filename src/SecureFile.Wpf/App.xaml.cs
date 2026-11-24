using System.Windows;

namespace SecureFile.Wpf;

/// <summary>
/// The WPF application object: starts the app and owns application-wide resources (App.xaml).
/// </summary>
/// <remarks>
/// This class is the <b>composition root</b> (ADR-09, "pure DI"): the one place where the app's objects
/// are created and connected. Stage 3.1 adds the services and the main ViewModel here.
/// </remarks>
internal sealed partial class App : Application
{
    /// <summary>
    /// Runs once when the application starts: creates and shows the main window.
    /// </summary>
    /// <param name="e">Start-up information from WPF, such as command-line arguments.</param>
    /// <remarks>
    /// The window is created here in code instead of with <c>StartupUri</c> in App.xaml, so that
    /// later the window can receive its ViewModel through its constructor.
    /// </remarks>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow mainWindow = new();
        mainWindow.Show();
    }
}

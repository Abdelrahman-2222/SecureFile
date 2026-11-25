using Avalonia;

namespace SecureFile.Avalonia;

/// <summary>The entry point of the cross-platform desktop app.</summary>
/// <remarks>
/// Avalonia needs the builder in a separate method (<see cref="BuildAvaloniaApp"/>): the XAML previewer in the IDE calls
/// it without running <see cref="Main"/>. Nothing may touch Avalonia before <c>StartWithClassicDesktopLifetime</c>.
/// </remarks>
internal static class Program
{
    /// <summary>Starts the app and runs it until the main window closes.</summary>
    /// <param name="args">Command-line arguments (unused; the command-line version is <c>securefile</c>).</param>
    /// <returns>The process exit code: 0 normally, 1 after a fatal error.</returns>
    /// <remarks><c>[STAThread]</c> is required on Windows for drag-and-drop and the native dialogs (COM).</remarks>
    [STAThread]
    public static int Main(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Configures Avalonia: the platform back-end for this OS, the bundled Inter font and logging to Trace.</summary>
    /// <returns>The configured builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}

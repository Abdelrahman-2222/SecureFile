using System.Globalization;
using System.Reflection;
using System.Windows;

namespace SecureFile.Wpf;

/// <summary>The About dialog: the app name, its version and a one-line description.</summary>
/// <remarks>
/// The version is read from the assembly, which gets it from <c>Directory.Build.props</c>, so there is one place to
/// change it and the dialog can never disagree with the file properties shown in Explorer.
/// </remarks>
internal sealed partial class AboutWindow : Window
{
    /// <summary>Creates the dialog and shows the version.</summary>
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = string.Create(CultureInfo.CurrentCulture, $"Version {GetVersionText()}");
    }

    /// <summary>Reads the product version, without the build-metadata part (<c>+commit hash</c>).</summary>
    /// <returns>For example <c>0.1.0</c>.</returns>
    private static string GetVersionText()
    {
        string? informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string version = informational ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? version[..plus] : version;
    }
}

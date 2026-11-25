using System.Globalization;
using System.Reflection;
using Avalonia.Controls;

namespace SecureFile.Avalonia;

/// <summary>The About dialog: the app name, its version and a one-line description (the same text as the WPF dialog).</summary>
/// <remarks>The version comes from the assembly (<c>Directory.Build.props</c>), so both apps always show the same one.</remarks>
internal sealed partial class AboutWindow : Window
{
    /// <summary>Creates the dialog, shows the version and lets Close (or Esc) close it.</summary>
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = string.Create(CultureInfo.CurrentCulture, $"Version {GetVersionText()}");
        CloseButton.Click += (_, _) => Close();
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

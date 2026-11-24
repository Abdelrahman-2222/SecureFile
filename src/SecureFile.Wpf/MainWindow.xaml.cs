using System.Windows;

namespace SecureFile.Wpf;

/// <summary>
/// The application's main window. Its layout is defined in MainWindow.xaml.
/// </summary>
/// <remarks>
/// This file stays almost empty on purpose: with MVVM, the window's logic lives in the ViewModel
/// (SecureFile.Presentation), where it can be unit-tested without opening a window.
/// </remarks>
internal sealed partial class MainWindow : Window
{
    /// <summary>
    /// Creates the window and loads its XAML layout.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}

using System.Windows;
using SecureFile.Presentation.Main;

namespace SecureFile.Wpf;

/// <summary>
/// The application's main window. Its layout is defined in MainWindow.xaml.
/// </summary>
/// <remarks>
/// This file stays small on purpose: with MVVM, the window's logic lives in the ViewModel
/// (SecureFile.Presentation), where it can be unit-tested without opening a window. The only code here is what
/// binding cannot do: <c>PasswordBox.Password</c> is not a dependency property (on purpose, so the password is not
/// kept in a bindable property), so the window copies it to the ViewModel when it changes.
/// </remarks>
internal sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>Creates the window, loads its XAML layout and connects it to its ViewModel.</summary>
    /// <param name="viewModel">The state and commands the window shows.</param>
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
#if DEBUG
        // Test hook for the global safety net (ADR-13 Layer 3): Ctrl+Shift+F12 throws an unexpected exception.
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            new ThrowTestErrorCommand(),
            System.Windows.Input.Key.F12,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));
#endif
    }

    /// <summary>Copies the typed password to the ViewModel.</summary>
    /// <param name="sender">The password box.</param>
    /// <param name="e">Unused.</param>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordInput.Password;
    }

#if DEBUG
    /// <summary>A command that only throws, to test the safety net by hand.</summary>
    private sealed class ThrowTestErrorCommand : System.Windows.Input.ICommand
    {
        /// <summary>Never raised: the command is always available.</summary>
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public bool CanExecute(object? parameter)
        {
            return true;
        }

        /// <inheritdoc />
        public void Execute(object? parameter)
        {
            throw new InvalidOperationException("Test error from Ctrl+Shift+F12 (debug builds only).");
        }
    }
#endif
}

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

    /// <summary>True while the code itself writes the two password boxes, so their change events are not sent to the ViewModel.</summary>
    private bool _syncingPassword;

    /// <summary>Creates the window, loads its XAML layout and connects it to its ViewModel.</summary>
    /// <param name="viewModel">The state and commands the window shows.</param>
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
#if DEBUG
        // Test hook for the global safety net (ADR-13 Layer 3): Ctrl+Shift+F12 throws an unexpected exception.
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            new ThrowTestErrorCommand(),
            System.Windows.Input.Key.F12,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));
#endif
    }

    /// <summary>Opens the About dialog.</summary>
    /// <param name="sender">The About button.</param>
    /// <param name="e">Unused.</param>
    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    /// <summary>F1 opens the About dialog (the usual key for help and information).</summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key that was pressed.</param>
    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F1)
        {
            OnAboutClick(sender, e);
            e.Handled = true;
        }
    }

    /// <summary>Shows the "copy" cursor over the drop zone for files, and "not allowed" for anything else.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drag information.</param>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Hands the first dropped file to the ViewModel; the ViewModel decides whether it can be used.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drop information.</param>
    /// <remarks>Only the first file is used: the app works on one file at a time.</remarks>
    private void OnFileDropped(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths
            && _viewModel.OpenFileCommand.CanExecute(paths[0]))
        {
            _viewModel.OpenFileCommand.Execute(paths[0]);
        }

        e.Handled = true;
    }

    /// <summary>Copies the typed password to the ViewModel.</summary>
    /// <param name="sender">The password box.</param>
    /// <param name="e">Unused.</param>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingPassword)
        {
            _viewModel.Password = PasswordInput.Password;
        }
    }

    /// <summary>Copies the password typed in the visible text box (while "Show password" is on) to the ViewModel.</summary>
    /// <param name="sender">The visible password text box.</param>
    /// <param name="e">Unused.</param>
    private void OnVisiblePasswordChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_syncingPassword)
        {
            _viewModel.Password = PasswordVisibleInput.Text;
        }
    }

    /// <summary>Swaps the hidden and the visible password box when "Show password" changes, keeping the text.</summary>
    /// <param name="sender">The check box.</param>
    /// <param name="e">Unused.</param>
    private void OnShowPasswordChanged(object sender, RoutedEventArgs e)
    {
        bool show = ShowPasswordCheck.IsChecked == true;
        SetBoxes(_viewModel.Password);
        PasswordVisibleInput.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PasswordInput.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        if (show)
        {
            PasswordVisibleInput.Focus();
            PasswordVisibleInput.CaretIndex = PasswordVisibleInput.Text.Length;
        }
        else
        {
            PasswordInput.Focus();
        }
    }

    /// <summary>When the ViewModel changes the password (it clears it after a success), brings both boxes in line.</summary>
    /// <param name="sender">The ViewModel.</param>
    /// <param name="e">The name of the changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Password) && PasswordInput.Password != _viewModel.Password)
        {
            SetBoxes(_viewModel.Password);
        }
    }

    /// <summary>Writes the same text into both password boxes without sending it back to the ViewModel.</summary>
    /// <param name="password">The password to show (empty clears both).</param>
    private void SetBoxes(string password)
    {
        _syncingPassword = true;
        try
        {
            PasswordInput.Password = password;
            PasswordVisibleInput.Text = password;
        }
        finally
        {
            _syncingPassword = false;
        }
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

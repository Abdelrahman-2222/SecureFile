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
        WirePasswordBoxes();
        Loaded += (_, _) => BrowseButton.Focus(); // a keyboard user starts on the first action
#if DEBUG
        // Test hook for the global safety net (ADR-13 Layer 3): Ctrl+Shift+F12 throws an unexpected exception.
        InputBindings.Add(new System.Windows.Input.KeyBinding(
            new ThrowTestErrorCommand(),
            System.Windows.Input.Key.F12,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));
#endif
    }

    /// <summary>Connects what binding cannot: no copying out of the visible boxes, and the Caps Lock check on all four boxes.</summary>
    private void WirePasswordBoxes()
    {
        BlockCopyOut(PasswordVisibleInput);
        BlockCopyOut(ConfirmVisibleInput);
        foreach (System.Windows.Controls.Control box in new System.Windows.Controls.Control[] { PasswordInput, PasswordVisibleInput, ConfirmInput, ConfirmVisibleInput })
        {
            box.PreviewKeyUp += UpdateCapsLockWarning;
            box.GotKeyboardFocus += UpdateCapsLockWarning;
            box.LostKeyboardFocus += UpdateCapsLockWarning;
        }
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

    /// <summary>
    /// Shows the "copy" cursor and highlights the drop zone for files, and "not allowed" for anything else.
    /// </summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drag information.</param>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool accepted = AcceptsDrop(e);
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        SetDropHighlight(accepted);
        e.Handled = true;
    }

    /// <summary>Removes the highlight when the drag leaves the drop zone without dropping.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">Unused.</param>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
    }

    /// <summary>Tells whether a drop would be used: it must carry files, and no operation may be running.</summary>
    /// <param name="e">The drag information.</param>
    /// <returns><see langword="true"/> when the drop zone accepts it.</returns>
    /// <remarks>While busy the drop would do nothing, so the cursor says "not allowed" instead of "copy" (review item 12).</remarks>
    private bool AcceptsDrop(DragEventArgs e)
    {
        return e.Data.GetDataPresent(DataFormats.FileDrop) && _viewModel.OpenFilesCommand.CanExecute(null);
    }

    /// <summary>Gives the drop zone the theme's accent border while a usable drag is over it (review item 31).</summary>
    /// <param name="on">Whether to highlight.</param>
    /// <remarks>
    /// The theme resource is referenced (not copied), so the highlight follows light, dark and high-contrast themes. Only
    /// the border changes: the transparent background must stay, because without a background WPF stops sending drag events.
    /// </remarks>
    private void SetDropHighlight(bool on)
    {
        DropZone.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, on ? "AccentFillColorDefaultBrush" : "ControlStrongStrokeColorDefaultBrush");
    }

    /// <summary>Hands the dropped paths to the ViewModel; it uses the first and says so when there were more.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drop information.</param>
    private void OnFileDropped(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths
            && _viewModel.OpenFilesCommand.CanExecute(paths))
        {
            _viewModel.OpenFilesCommand.Execute(paths);
        }

        e.Handled = true;
    }

    /// <summary>Copies the typed password to the ViewModel.</summary>
    /// <param name="sender">The password box.</param>
    /// <param name="e">Unused.</param>
    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword)
        {
            return;
        }

        if (ReferenceEquals(sender, ConfirmInput))
        {
            _viewModel.ConfirmPassword = ConfirmInput.Password;
        }
        else
        {
            _viewModel.Password = PasswordInput.Password;
        }
    }

    /// <summary>Copies the password typed in the visible text box (while "Show password" is on) to the ViewModel.</summary>
    /// <param name="sender">The visible password text box.</param>
    /// <param name="e">Unused.</param>
    private void OnVisiblePasswordChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_syncingPassword)
        {
            return;
        }

        if (ReferenceEquals(sender, ConfirmVisibleInput))
        {
            _viewModel.ConfirmPassword = ConfirmVisibleInput.Text;
        }
        else
        {
            _viewModel.Password = PasswordVisibleInput.Text;
        }
    }

    /// <summary>Swaps the hidden and the visible boxes (password and confirmation) when "Show password" changes, keeping the text.</summary>
    /// <param name="sender">The check box.</param>
    /// <param name="e">Unused.</param>
    private void OnShowPasswordChanged(object sender, RoutedEventArgs e)
    {
        bool show = ShowPasswordCheck.IsChecked == true;
        SetBoxes(_viewModel.Password, _viewModel.ConfirmPassword);
        SwapVisibility(PasswordInput, PasswordVisibleInput, show);
        SwapVisibility(ConfirmInput, ConfirmVisibleInput, show);

        // Alt+P and Alt+N must reach the box that is shown; a collapsed target cannot take the focus (review item 9).
        PasswordLabel.Target = show ? PasswordVisibleInput : PasswordInput;
        ConfirmLabel.Target = show ? ConfirmVisibleInput : ConfirmInput;
        (show ? PasswordVisibleInput : (System.Windows.Controls.Control)PasswordInput).Focus();
    }

    /// <summary>Shows either the hidden (dots) or the visible (text) box of one field, never both.</summary>
    /// <param name="hiddenBox">The password box.</param>
    /// <param name="visibleBox">The plain text box in the same place.</param>
    /// <param name="showText"><see langword="true"/> to show the text box.</param>
    private static void SwapVisibility(System.Windows.Controls.Control hiddenBox, System.Windows.Controls.Control visibleBox, bool showText)
    {
        visibleBox.Visibility = showText ? Visibility.Visible : Visibility.Collapsed;
        hiddenBox.Visibility = showText ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>When the ViewModel changes the password (it clears it after a success), brings both boxes in line.</summary>
    /// <param name="sender">The ViewModel.</param>
    /// <param name="e">The name of the changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Password) or nameof(MainViewModel.ConfirmPassword) && BoxesDifferFromViewModel())
        {
            SetBoxes(_viewModel.Password, _viewModel.ConfirmPassword);
        }
        else if (e.PropertyName == nameof(MainViewModel.State))
        {
            OnStateChanged();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsChoosingOutput))
        {
            OnChoosingOutputChanged();
        }
        else if (e.PropertyName == nameof(MainViewModel.SelectedFile))
        {
            // A new file (or none, after a success) starts hidden again, so the next password is never typed in plain
            // text by accident (review item 24). A failure keeps the file, so the choice stays for the retry.
            ShowPasswordCheck.IsChecked = false;
        }
    }

    /// <summary>Tells whether any of the four boxes shows something other than the ViewModel's password or confirmation.</summary>
    /// <returns><see langword="true"/> when the boxes must be rewritten.</returns>
    /// <remarks>
    /// All four are compared, hidden and visible: typing in the visible box changes only that one, so comparing just
    /// the hidden boxes missed the clearing after a success and left the typed password on screen.
    /// </remarks>
    private bool BoxesDifferFromViewModel()
    {
        return PasswordInput.Password != _viewModel.Password
            || PasswordVisibleInput.Text != _viewModel.Password
            || ConfirmInput.Password != _viewModel.ConfirmPassword
            || ConfirmVisibleInput.Text != _viewModel.ConfirmPassword;
    }

    /// <summary>Writes the texts into the hidden and visible boxes of both fields without sending them back to the ViewModel.</summary>
    /// <param name="password">The password to show (empty clears both of its boxes).</param>
    /// <param name="confirmation">The confirmation to show (empty clears both of its boxes).</param>
    private void SetBoxes(string password, string confirmation)
    {
        _syncingPassword = true;
        try
        {
            PasswordInput.Password = password;
            PasswordVisibleInput.Text = password;
            ConfirmInput.Password = confirmation;
            ConfirmVisibleInput.Text = confirmation;

            // Setting Text puts the caret at the start; typing afterwards must continue at the end.
            PasswordVisibleInput.CaretIndex = password.Length;
            ConfirmVisibleInput.CaretIndex = confirmation.Length;
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    /// <summary>Stops a visible password from being copied, cut or dragged out of its text box.</summary>
    /// <param name="box">The plain text box that shows a password while "Show password" is on.</param>
    /// <remarks>
    /// The hidden <c>PasswordBox</c> already refuses copy and cut; the text box used for "Show password" would not,
    /// and a password on the clipboard stays there (clipboard history and cloud sync can keep it). Pasting stays
    /// allowed, so a password manager still works. The context menu is removed because its Copy and Cut would do nothing.
    /// </remarks>
    private static void BlockCopyOut(System.Windows.Controls.TextBox box)
    {
        DataObject.AddCopyingHandler(box, (_, args) => args.CancelCommand());
        box.ContextMenu = null;
    }

    /// <summary>Shows the Caps Lock warning while a password box has the focus and Caps Lock is on.</summary>
    /// <param name="sender">One of the password boxes.</param>
    /// <param name="e">Unused.</param>
    /// <remarks>
    /// Checked on focus and after every key release, which covers pressing Caps Lock inside the box. A wrong password
    /// from Caps Lock is a common cause of "I can't open my file".
    /// </remarks>
    private void UpdateCapsLockWarning(object sender, RoutedEventArgs e)
    {
        bool boxFocused = PasswordInput.IsKeyboardFocusWithin || PasswordVisibleInput.IsKeyboardFocusWithin
            || ConfirmInput.IsKeyboardFocusWithin || ConfirmVisibleInput.IsKeyboardFocusWithin;
        CapsLockWarning.Visibility = boxFocused && System.Windows.Input.Keyboard.IsKeyToggled(System.Windows.Input.Key.CapsLock)
            ? Visibility.Visible
            : Visibility.Collapsed;
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

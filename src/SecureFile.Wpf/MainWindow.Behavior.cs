using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SecureFile.Presentation.Main;

namespace SecureFile.Wpf;

/// <summary>Window behaviour that reacts to the ViewModel's state: where the focus goes, and closing during an operation.</summary>
/// <remarks>
/// A second file for the same window class, only to keep each file small. Everything here is what binding cannot do.
/// </remarks>
internal sealed partial class MainWindow
{
    /// <summary>The state before the latest change, so that "back from Working" can be told from "just selected".</summary>
    private MainViewState _lastState = MainViewState.NoFile;

    /// <summary>True after the user chose "Cancel and close": the window closes as soon as the cancelled operation has cleaned up.</summary>
    private bool _closeWhenIdle;

    /// <summary>Asks before closing while an encryption or decryption runs, and cancels it cleanly if the user agrees.</summary>
    /// <param name="e">The closing information; cancelled here while an operation runs.</param>
    /// <remarks>
    /// Why: closing the process mid-operation skips the clean-up, so a half-written temporary file (for Decrypt it holds
    /// unverified plaintext) stays next to the target. Cancelling first lets the atomic writer delete it (ADR-07).
    /// A file read cannot be cancelled and leaves nothing behind, so it does not block closing.
    /// </remarks>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel || !_viewModel.CancelCommand.CanExecute(null))
        {
            return;
        }

        e.Cancel = true;
        if (_closeWhenIdle)
        {
            return; // already waiting for the cancelled operation to finish
        }

        MessageBoxResult answer = MessageBox.Show(
            this,
            "An operation is still running. Cancel it and close SecureFile? Nothing will be written.",
            "SecureFile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            _closeWhenIdle = true;
            _viewModel.CancelCommand.Execute(null);
        }
    }

    /// <summary>Reacts to a state change: finishes a pending close, or moves the focus to the next sensible control.</summary>
    private void OnStateChanged()
    {
        MainViewState previous = _lastState;
        MainViewState state = _viewModel.State;
        _lastState = state;

        if (_closeWhenIdle && state != MainViewState.Working)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, Close);
            return;
        }

        Action? focus = state switch
        {
            MainViewState.Succeeded => () => OpenFolderButton.Focus(),
            MainViewState.Failed => FocusAfterFailure,
            MainViewState.FileSelected when previous == MainViewState.Working => FocusPasswordBox,
            _ => null,
        };

        if (focus is not null)
        {
            // After the binding has enabled the controls again; focusing a disabled control does nothing.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, focus);
        }
    }

    /// <summary>After a failure: back to the password (all selected, to retype it) or, when no file is left, to Browse.</summary>
    private void FocusAfterFailure()
    {
        if (_viewModel.SelectedFile is null)
        {
            BrowseButton.Focus();
            return;
        }

        FocusPasswordBox();
        if (ShowPasswordCheck.IsChecked == true)
        {
            PasswordVisibleInput.SelectAll();
        }
        else
        {
            PasswordInput.SelectAll();
        }
    }

    /// <summary>Puts the focus in the password box that is currently shown (hidden or visible).</summary>
    private void FocusPasswordBox()
    {
        Control box = ShowPasswordCheck.IsChecked == true ? PasswordVisibleInput : PasswordInput;
        box.Focus();
    }
}

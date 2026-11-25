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
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        // The question runs its own message loop, so the operation may have ended while it was open. Then there is
        // nothing to cancel: close now. Waiting for "idle" would leave the window open and close it later, on the next
        // state change, without asking (review UI-1; the Avalonia window already checks again).
        if (!_viewModel.CancelCommand.CanExecute(null))
        {
            e.Cancel = false;
            return;
        }

        _closeWhenIdle = true;
        _viewModel.CancelCommand.Execute(null);
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

        Action? focus = ChooseFocus(state, previous);
        if (focus is not null)
        {
            // After the binding has enabled the controls again; focusing a disabled control does nothing.
            Dispatcher.BeginInvoke(DispatcherPriority.Input, focus);
        }
    }

    /// <summary>Picks where the focus goes after a state change, or <see langword="null"/> to leave it.</summary>
    /// <param name="state">The new state.</param>
    /// <param name="previous">The state before it.</param>
    /// <returns>The action that moves the focus, or <see langword="null"/>.</returns>
    private Action? ChooseFocus(MainViewState state, MainViewState previous)
    {
        return state switch
        {
            MainViewState.Succeeded => () => OpenFolderButton.Focus(),
            MainViewState.Failed => FocusAfterFailure,
            MainViewState.FileSelected when previous == MainViewState.Working => FocusPasswordBox,
            MainViewState.NoFile when previous == MainViewState.Working => () => BrowseButton.Focus(),

            // The disabled password boxes cannot keep the focus; Cancel is the one thing left to do (review item 7).
            MainViewState.Working when _viewModel.CancelCommand.CanExecute(null) => () => CancelButton.Focus(),
            _ => null,
        };
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

    /// <summary>
    /// After the Save dialog closed without starting an operation (the user cancelled it), puts the focus back in the
    /// password box: the button that opened it was disabled while it was open and lost the focus (review item 7).
    /// </summary>
    private void OnFileDialogOpenChanged()
    {
        if (!_viewModel.IsFileDialogOpen)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                // Checked when it runs: a chosen file has started the operation by then, which focuses Cancel instead.
                if (_viewModel.State is MainViewState.FileSelected or MainViewState.Failed)
                {
                    FocusPasswordBox();
                }
            });
        }
    }

    /// <summary>Enter in the first password box: on to the confirmation while it is still needed, otherwise run (items 11 and 30).</summary>
    /// <param name="sender">The hidden or visible first password box.</param>
    /// <param name="e">The key that was pressed.</param>
    private void OnFirstPasswordKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Return)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel.EnterMovesToConfirm)
        {
            (ShowPasswordCheck.IsChecked == true ? ConfirmVisibleInput : (Control)ConfirmInput).Focus();
        }
        else if (_viewModel.RunPrimaryCommand.CanExecute(null))
        {
            _viewModel.RunPrimaryCommand.Execute(null);
        }
    }
}

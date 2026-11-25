using Avalonia.Controls;
using Avalonia.Threading;
using SecureFile.Presentation.Main;

namespace SecureFile.Avalonia;

/// <summary>Window behaviour that reacts to the ViewModel's state: where the focus goes, and closing during an operation.</summary>
/// <remarks>The same rules as the WPF window (src/SecureFile.Wpf/MainWindow.Behavior.cs), with Avalonia's API.</remarks>
internal sealed partial class MainWindow
{
    /// <summary>The state before the latest change, so that "back from Working" can be told from "just selected".</summary>
    private MainViewState _lastState = MainViewState.NoFile;

    /// <summary>True after the user chose "Cancel and close": the window closes as soon as the cancelled operation has cleaned up.</summary>
    private bool _closeWhenIdle;

    /// <summary>True while the "cancel and close?" question is on screen, so a second close click does not open another.</summary>
    private bool _askingToClose;

    /// <summary>Asks before closing while an encryption or decryption runs, and cancels it cleanly if the user agrees.</summary>
    /// <param name="e">The closing information; cancelled here while an operation runs.</param>
    /// <remarks>
    /// Why: closing mid-operation skips the clean-up, so a half-written temporary file (for Decrypt it holds unverified
    /// plaintext) stays next to the target. Avalonia dialogs are asynchronous, so the close is always refused first and
    /// the answer arrives later: Yes cancels the operation, and <see cref="OnStateChanged"/> closes the window once the
    /// operation has ended.
    /// </remarks>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (e.Cancel || !_viewModel.CancelCommand.CanExecute(null))
        {
            return;
        }

        e.Cancel = true;
        if (_closeWhenIdle || _askingToClose)
        {
            return; // already waiting for the answer or for the cancelled operation to finish
        }

        _askingToClose = true;
        MessageDialog.AskYesNo(this, "An operation is still running. Cancel it and close SecureFile? Nothing will be written.", OnCloseAnswer);
    }

    /// <summary>Cancels the operation and marks the window to close when the user answered Yes.</summary>
    /// <param name="cancelAndClose"><see langword="true"/> for Yes.</param>
    private void OnCloseAnswer(bool cancelAndClose)
    {
        _askingToClose = false;
        if (!cancelAndClose)
        {
            return;
        }

        if (!_viewModel.CancelCommand.CanExecute(null))
        {
            Close(); // the operation finished while the question was open
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
            Dispatcher.UIThread.Post(Close, DispatcherPriority.Input);
            return;
        }

        Action? focus = ChooseFocus(state, previous);
        if (focus is not null)
        {
            // After the binding has enabled the controls again; focusing a disabled control does nothing.
            Dispatcher.UIThread.Post(focus, DispatcherPriority.Input);
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
            MainViewState.FileSelected when previous == MainViewState.Working => () => PasswordInput.Focus(),
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

        PasswordInput.Focus();
        PasswordInput.SelectAll();
    }

    /// <summary>
    /// After the Save dialog closed without starting an operation (the user cancelled it), puts the focus back in the
    /// password box: the button that opened it was disabled while it was open and lost the focus (review item 7).
    /// </summary>
    private void OnFileDialogOpenChanged()
    {
        if (!_viewModel.IsFileDialogOpen)
        {
            Dispatcher.UIThread.Post(
                () =>
                {
                    // Checked when it runs: a chosen file has started the operation by then, which focuses Cancel instead.
                    if (_viewModel.State is MainViewState.FileSelected or MainViewState.Failed)
                    {
                        PasswordInput.Focus();
                    }
                },
                DispatcherPriority.Input);
        }
    }

    /// <summary>Enter in the first password box: on to the confirmation while it is still needed, otherwise run (items 11 and 30).</summary>
    /// <param name="sender">The first password box.</param>
    /// <param name="e">The key that was pressed.</param>
    private void OnFirstPasswordKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != global::Avalonia.Input.Key.Enter)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel.EnterMovesToConfirm)
        {
            ConfirmInput.Focus();
        }
        else if (_viewModel.RunPrimaryCommand.CanExecute(null))
        {
            _viewModel.RunPrimaryCommand.Execute(null);
        }
    }
}

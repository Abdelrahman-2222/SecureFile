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

        Action? focus = state switch
        {
            MainViewState.Succeeded => () => OpenFolderButton.Focus(),
            MainViewState.Failed => FocusAfterFailure,
            MainViewState.FileSelected when previous == MainViewState.Working => () => PasswordInput.Focus(),
            _ => null,
        };

        if (focus is not null)
        {
            // After the binding has enabled the controls again; focusing a disabled control does nothing.
            Dispatcher.UIThread.Post(focus, DispatcherPriority.Input);
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

        PasswordInput.Focus();
        PasswordInput.SelectAll();
    }
}

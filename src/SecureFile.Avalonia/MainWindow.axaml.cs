using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SecureFile.Core.Passwords;
using SecureFile.Presentation.Main;

namespace SecureFile.Avalonia;

/// <summary>The main window of the cross-platform app. Its layout is defined in MainWindow.axaml.</summary>
/// <remarks>
/// <para>
/// The same <see cref="MainViewModel"/> as the WPF window drives it, so every rule (password checks, messages, states)
/// is shared and already unit-tested. The code here is only what binding cannot do: drag-and-drop, the About dialog,
/// the strength colour and tooltips, blocking the clipboard, and the closing and focus behaviour (in the second file).
/// </para>
/// <para>
/// Unlike WPF, Avalonia's <see cref="TextBox"/> hides a password itself (<c>PasswordChar</c>, <c>RevealPassword</c>),
/// so the passwords are simple two-way bindings and no "copy the box into the ViewModel" code is needed.
/// </para>
/// </remarks>
internal sealed partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>Creates the window, loads its layout and connects it to its ViewModel.</summary>
    /// <param name="viewModel">The state and commands the window shows.</param>
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        WireEvents();
        ApplyStrengthColour();
        ApplyButtonTooltips();
    }

    /// <summary>Connects the events that XAML cannot express: drop zone, About, F1 and the clipboard guard.</summary>
    private void WireEvents()
    {
        WireDropZone();
        AboutButton.Click += (_, _) => ShowAbout();
        KeyDown += OnWindowKeyDown;
        Opened += (_, _) => BrowseButton.Focus(); // a keyboard user starts on the first action
        PasswordInput.KeyDown += OnFirstPasswordKeyDown;
        foreach (TextBox box in new[] { PasswordInput, ConfirmInput })
        {
            box.CopyingToClipboard += BlockClipboard;
            box.CuttingToClipboard += BlockClipboard;
        }
    }

    /// <summary>Connects the drop zone: cursor and highlight while dragging, the file on drop.</summary>
    private void WireDropZone()
    {
        DropZone.AddHandler(DragDrop.DragEnterEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        DropZone.AddHandler(DragDrop.DragLeaveEvent, (_, _) => DropZone.Classes.Set("dragover", false));
        DropZone.AddHandler(DragDrop.DropEvent, OnFileDropped);
    }

    /// <summary>Opens the About dialog.</summary>
    private void ShowAbout()
    {
        _ = new AboutWindow().ShowDialog(this);
    }

    /// <summary>F1 opens the About dialog (the usual key for help and information).</summary>
    /// <param name="sender">The window.</param>
    /// <param name="e">The key that was pressed.</param>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            ShowAbout();
            e.Handled = true;
        }
    }

    /// <summary>Shows the "copy" cursor and highlights the drop zone for files, and "not allowed" for anything else.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drag information.</param>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool accepted = AcceptsDrop(e);
        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Classes.Set("dragover", accepted); // accent border (review item 31), styled in MainWindow.axaml
        e.Handled = true;
    }

    /// <summary>Tells whether a drop would be used: it must carry files, and no operation may be running.</summary>
    /// <param name="e">The drag information.</param>
    /// <returns><see langword="true"/> when the drop zone accepts it.</returns>
    /// <remarks>While busy the drop would do nothing, so the cursor says "not allowed" instead of "copy" (review item 12).</remarks>
    private bool AcceptsDrop(DragEventArgs e)
    {
        return e.DataTransfer.Contains(DataFormat.File) && _viewModel.OpenFilesCommand.CanExecute(null);
    }

    /// <summary>Hands the dropped paths to the ViewModel; it uses the first and says so when there were more.</summary>
    /// <param name="sender">The drop zone.</param>
    /// <param name="e">The drop information.</param>
    /// <remarks>An item without a local path (for example in a sandbox) is left out.</remarks>
    private void OnFileDropped(object? sender, DragEventArgs e)
    {
        DropZone.Classes.Set("dragover", false);
        string[] paths = [.. (e.DataTransfer.TryGetFiles() ?? []).Select(item => item.TryGetLocalPath()).OfType<string>()];
        if (paths.Length > 0 && _viewModel.OpenFilesCommand.CanExecute(paths))
        {
            _viewModel.OpenFilesCommand.Execute(paths);
        }

        e.Handled = true;
    }

    /// <summary>Stops a password from being copied or cut to the clipboard, also while "Show password" is on.</summary>
    /// <param name="sender">One of the password boxes.</param>
    /// <param name="e">The clipboard event; handling it cancels the copy.</param>
    /// <remarks>
    /// A password on the clipboard stays there (clipboard history and cloud sync can keep it). Pasting stays allowed, so
    /// a password manager still works. Avalonia refuses Copy in a hidden password box, but allows it once
    /// <c>RevealPassword</c> is on ("Show password"), so this guard is what keeps the revealed text off the clipboard
    /// (covered by the headless window test).
    /// </remarks>
    private static void BlockClipboard(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>Keeps the colour of the strength bar and tooltips in step with the ViewModel.</summary>
    /// <param name="sender">The ViewModel.</param>
    /// <param name="e">The name of the changed property.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Password):
                ApplyStrengthColour();
                break;
            case nameof(MainViewModel.SelectedFile):
                ApplyButtonTooltips();
                ShowPasswordCheck.IsChecked = false; // a new file starts hidden again (review item 24)
                break;
            case nameof(MainViewModel.State):
                OnStateChanged();
                break;
            case nameof(MainViewModel.IsChoosingOutput):
                OnChoosingOutputChanged();
                break;
        }
    }

    /// <summary>Red for too short (blocks), amber for weak and fair (advice), green for strong (the WPF window's DataTriggers).</summary>
    private void ApplyStrengthColour()
    {
        PasswordStrength strength = _viewModel.Strength;
        // Red only for what blocks (too short); a weak password is allowed, so it is a caution like Fair.
        bool critical = strength == PasswordStrength.TooShort;
        bool caution = strength is PasswordStrength.Weak or PasswordStrength.Fair;
        bool success = strength == PasswordStrength.Strong;
        foreach (StyledElement element in new StyledElement[] { StrengthBar, StrengthLabel })
        {
            element.Classes.Set("critical", critical);
            element.Classes.Set("caution", caution);
            element.Classes.Set("success", success);
        }
    }

    /// <summary>Explains a disabled Encrypt or Decrypt button; no tooltip while the button can be used.</summary>
    private void ApplyButtonTooltips()
    {
        ToolTip.SetTip(EncryptButton, _viewModel.IsEncryptOffered ? null : "This file is already encrypted. Use Decrypt.");
        ToolTip.SetTip(DecryptButton, _viewModel.IsDecryptOffered ? null : "This file is not encrypted by SecureFile. Use Encrypt.");
    }
}

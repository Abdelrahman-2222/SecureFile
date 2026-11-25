using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;

namespace SecureFile.Avalonia;

/// <summary>A small modal message window: Avalonia has no built-in message box (WPF's <c>MessageBox</c>).</summary>
/// <remarks>
/// Built in code rather than XAML because it is only a text and one or two buttons. Every answer arrives through a
/// callback: Avalonia dialogs are asynchronous, and the callers (a window closing, the safety net) are event handlers
/// that must return at once.
/// </remarks>
internal static class MessageDialog
{
    /// <summary>Shows a message with an OK button.</summary>
    /// <param name="owner">The window to centre on and block, or <see langword="null"/> when there is none.</param>
    /// <param name="message">The text to show.</param>
    /// <param name="onClosed">Runs after the dialog closed, or <see langword="null"/>.</param>
    public static void ShowInformation(Window? owner, string message, Action? onClosed)
    {
        Show(owner, message, [("OK", true)], _ => onClosed?.Invoke());
    }

    /// <summary>Asks a yes/no question; "No" is the default, so Enter does not confirm by accident.</summary>
    /// <param name="owner">The window to centre on and block.</param>
    /// <param name="question">The question to show.</param>
    /// <param name="onAnswer">Receives <see langword="true"/> for Yes, <see langword="false"/> for No or closing the dialog.</param>
    public static void AskYesNo(Window owner, string question, Action<bool> onAnswer)
    {
        Show(owner, question, [("Yes", false), ("No", true)], onAnswer);
    }

    /// <summary>Builds and shows the dialog.</summary>
    /// <param name="owner">The owner window, or <see langword="null"/>.</param>
    /// <param name="message">The text.</param>
    /// <param name="buttons">The buttons from left to right, with which one is the default (Enter and Esc).</param>
    /// <param name="onAnswer">Receives <see langword="true"/> when the first button was clicked.</param>
    private static void Show(Window? owner, string message, (string Text, bool IsDefault)[] buttons, Action<bool> onAnswer)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new(0, 24, 0, 0) };
        Window dialog = CreateWindow(owner, message, row);
        var answer = new StrongBox<bool>(false);
        AddButtons(row, dialog, buttons, answer);
        dialog.Closed += (_, _) => onAnswer(answer.Value);
        ShowWindow(dialog, owner);
    }

    /// <summary>Adds the buttons; a click records whether it was the first one and closes the dialog.</summary>
    /// <param name="row">The button row.</param>
    /// <param name="dialog">The dialog the buttons close.</param>
    /// <param name="buttons">The captions, with which one is the default.</param>
    /// <param name="answer">Set to <see langword="true"/> when the first button is clicked.</param>
    private static void AddButtons(StackPanel row, Window dialog, (string Text, bool IsDefault)[] buttons, StrongBox<bool> answer)
    {
        for (int index = 0; index < buttons.Length; index++)
        {
            bool isFirst = index == 0;
            Button button = CreateButton(buttons[index].Text, buttons[index].IsDefault);
            button.Click += (_, _) =>
            {
                answer.Value = isFirst;
                dialog.Close();
            };
            row.Children.Add(button);
        }
    }

    /// <summary>Creates the dialog window with the message above the button row.</summary>
    /// <param name="owner">The owner window, or <see langword="null"/> (decides where the dialog appears).</param>
    /// <param name="message">The text.</param>
    /// <param name="buttonRow">The panel that will hold the buttons.</param>
    /// <returns>The window, not yet shown.</returns>
    private static Window CreateWindow(Window? owner, string message, StackPanel buttonRow)
    {
        var text = new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, FontSize = 14 };
        return new Window
        {
            Title = "SecureFile",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Content = new StackPanel { Margin = new(24), Children = { text, buttonRow } },
        };
    }

    /// <summary>Creates one dialog button; the default one also answers Enter and Esc.</summary>
    /// <param name="text">The caption.</param>
    /// <param name="isDefault">Whether Enter and Esc press it.</param>
    /// <returns>The button.</returns>
    private static Button CreateButton(string text, bool isDefault)
    {
        return new Button { Content = text, MinWidth = 96, MinHeight = 40, IsDefault = isDefault, IsCancel = isDefault };
    }

    /// <summary>Shows the dialog modal over its owner, or on its own when the owner is missing or hidden.</summary>
    /// <param name="dialog">The dialog.</param>
    /// <param name="owner">The owner window, or <see langword="null"/>.</param>
    private static void ShowWindow(Window dialog, Window? owner)
    {
        if (owner is { IsVisible: true })
        {
            _ = dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }
}

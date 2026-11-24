using System.Text;

namespace SecureFile.Cli;

/// <summary>Reads a password from the real keyboard, key by key, without echoing it.</summary>
/// <remarks>
/// Works on Windows, macOS and Linux terminals (<see cref="Console.ReadKey(bool)"/> with <c>intercept: true</c> shows
/// nothing). Backspace removes the last character; Enter ends the answer. The question goes to standard error so that
/// standard output stays clean for pipes.
/// </remarks>
internal sealed class ConsolePasswordPrompt : IPasswordPrompt
{
    private readonly TextWriter _error;

    /// <summary>Creates the prompt.</summary>
    /// <param name="error">Where the question is written (standard error).</param>
    public ConsolePasswordPrompt(TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _error = error;
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The user pressed Ctrl+C at the prompt.</exception>
    public string? Ask(string question)
    {
        _error.Write(question + " ");
        _error.Flush();

        // Ctrl+C arrives as a key here (instead of the cancel event), otherwise it would be ignored while the program waits.
        bool previous = Console.TreatControlCAsInput;
        Console.TreatControlCAsInput = true;
        try
        {
            return ReadHidden();
        }
        finally
        {
            Console.TreatControlCAsInput = previous;
        }
    }

    /// <summary>Reads keys until Enter, without showing them.</summary>
    /// <returns>The typed text.</returns>
    private string ReadHidden()
    {
        StringBuilder typed = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                _error.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                _error.WriteLine();
                throw new OperationCanceledException("The password prompt was cancelled.");
            }

            Apply(key, typed);
        }
    }

    /// <summary>Adds a typed character, or removes the last one on Backspace; other keys (arrows, Tab…) are ignored.</summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="typed">The password so far.</param>
    private static void Apply(ConsoleKeyInfo key, StringBuilder typed)
    {
        if (key.Key == ConsoleKey.Backspace)
        {
            if (typed.Length > 0)
            {
                typed.Length--;
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            typed.Append(key.KeyChar);
        }
    }
}

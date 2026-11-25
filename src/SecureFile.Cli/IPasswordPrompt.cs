namespace SecureFile.Cli;

/// <summary>Asks the person at the keyboard for a password without showing it.</summary>
/// <remarks>An interface so tests can answer without a keyboard.</remarks>
internal interface IPasswordPrompt
{
    /// <summary>Shows a question and reads the answer.</summary>
    /// <param name="question">The text shown before the (invisible) answer, for example <c>Password:</c>.</param>
    /// <returns>The typed text, or <see langword="null"/> when input ended (no answer).</returns>
    string? Ask(string question);
}

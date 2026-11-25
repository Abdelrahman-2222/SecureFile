namespace SecureFile.Cli;

/// <summary>Everything the application reads from and writes to, handed in so tests can use plain in-memory objects.</summary>
/// <param name="Input">Standard input.</param>
/// <param name="Output">Standard output: results and help text (safe to pipe).</param>
/// <param name="Error">Standard error: questions, progress and messages for the person at the keyboard.</param>
/// <param name="InputRedirected">Whether standard input comes from a pipe or a file, so there is no keyboard to ask.</param>
/// <param name="ShowProgress">Whether to draw a progress line (only on a real terminal, never in logs).</param>
/// <remarks>
/// Only <c>Program</c> touches the real <see cref="Console"/>; everything else writes through these objects.
/// </remarks>
internal sealed record CliEnvironment(
    TextReader Input,
    TextWriter Output,
    TextWriter Error,
    bool InputRedirected,
    bool ShowProgress);

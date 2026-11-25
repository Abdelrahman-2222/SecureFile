using SecureFile.Core.Cryptography;

namespace SecureFile.Cli;

/// <summary>The entry point: connects the real console to <see cref="CliApplication"/>.</summary>
/// <remarks>
/// The only place that touches <see cref="Console"/>; everything else works on the streams it hands in, which is why the
/// rest can be tested with plain text readers and writers.
/// </remarks>
internal static class Program
{
    /// <summary>Starts the program.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    private static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellation = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true; // let the running operation clean up (no half-written file) and exit with code 130
            cancellation.Cancel();
        };

        using TextReader input = OpenInput();
        CliEnvironment environment = new(
            input,
            Console.Out,
            Console.Error,
            InputRedirected: Console.IsInputRedirected,
            ShowProgress: !Console.IsErrorRedirected);
        CliApplication application = new(environment, new FileEncryptionService(), new ConsolePasswordPrompt(Console.Error));
        return await application.RunAsync(args, cancellation.Token).ConfigureAwait(false);
    }

    /// <summary>Returns standard input; when it is redirected (a pipe or a file) it is always read as UTF-8.</summary>
    /// <returns>The reader for standard input.</returns>
    /// <remarks>
    /// <c>Console.In</c> on Windows decodes redirected input with the console's code page (often OEM 437), while Linux and
    /// macOS use UTF-8. A non-ASCII password piped to <c>--password-stdin</c> then became different bytes, and so a
    /// different key, on Windows: a file encrypted there would not open elsewhere. UTF-8 everywhere fixes that (a byte
    /// order mark is still recognised). Windows PowerShell 5.1 itself sends non-ASCII as "?" (backlog C17).
    /// </remarks>
    private static TextReader OpenInput()
    {
        return Console.IsInputRedirected
            ? new StreamReader(Console.OpenStandardInput(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            : Console.In;
    }
}

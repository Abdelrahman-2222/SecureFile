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

        CliEnvironment environment = new(
            Console.In,
            Console.Out,
            Console.Error,
            InputRedirected: Console.IsInputRedirected,
            ShowProgress: !Console.IsErrorRedirected);
        CliApplication application = new(environment, new FileEncryptionService(), new ConsolePasswordPrompt(Console.Error));
        return await application.RunAsync(args, cancellation.Token).ConfigureAwait(false);
    }
}

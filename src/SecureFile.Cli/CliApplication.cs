using SecureFile.Core;
using SecureFile.Core.Cryptography;
using SecureFile.Presentation.Errors;

namespace SecureFile.Cli;

/// <summary>The command-line program: reads the command, gets the password, runs the engine and reports the result.</summary>
/// <remarks>
/// <para>
/// <b>Error handling follows ADR-13:</b> Core throws documented exceptions; this class catches only those expected types
/// (never <see cref="Exception"/>) and shows the same plain messages as the Windows app (<see cref="ErrorMessageProvider"/>).
/// Anything else is a bug and ends the program with its normal crash report.
/// </para>
/// <para>
/// <b>Channels:</b> results and help go to standard output; questions, progress, notes and errors go to standard error, so
/// <c>securefile encrypt a.txt &gt; log.txt</c> never mixes a password prompt into a file.
/// </para>
/// </remarks>
internal sealed class CliApplication
{
    private const string HelpText = """
        securefile: lock and unlock a file with a password (AES-256, nothing leaves your computer)

        Usage:
          securefile encrypt <file> [--output <path>] [--force] [--password-stdin]
          securefile decrypt <file> [--output <path>] [--force] [--password-stdin]
          securefile --help | --version

        Options:
          -o, --output <path>   Where to write the result (default: <file>.enc, or <file> without .enc)
          -f, --force           Replace the result file if it already exists
          --password-stdin      Read the password from the first line of standard input (for scripts)

        The password is asked for without showing it. When encrypting it needs at least 8 characters and is asked twice.
        There is no --password option on purpose: a password on the command line stays in your shell history.
        Your original file is never changed. If you forget the password the file cannot be recovered: nobody can reset it.

        Exit codes: 0 done, 1 failed (wrong password, damaged file, no permission...), 2 wrong usage, 130 cancelled (Ctrl+C).
        """;

    private readonly CliEnvironment _environment;
    private readonly IFileEncryptionService _service;
    private readonly PasswordResolver _passwords;

    /// <summary>Creates the program with everything it talks to.</summary>
    /// <param name="environment">Standard streams and terminal facts.</param>
    /// <param name="service">The encryption engine.</param>
    /// <param name="prompt">The hidden password prompt.</param>
    public CliApplication(CliEnvironment environment, IFileEncryptionService service, IPasswordPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(prompt);
        _environment = environment;
        _service = service;
        _passwords = new PasswordResolver(environment, prompt);
    }

    /// <summary>Runs the program.</summary>
    /// <param name="args">The command-line arguments (without the program name).</param>
    /// <param name="cancellationToken">Cancelled when the user presses Ctrl+C.</param>
    /// <returns>The process exit code (see <see cref="ExitCodes"/>).</returns>
    public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        CliParseResult parsed = CliArgumentParser.Parse(args);
        if (parsed.Options is null)
        {
            return ReportUsageError(parsed.Error!);
        }

        try
        {
            return parsed.Options.Command switch
            {
                CliCommand.Help => Show(HelpText),
                CliCommand.Version => Show(VersionText()),
                _ => await RunOperationAsync(parsed.Options, cancellationToken).ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException)
        {
            return ReportCancelled();
        }
    }

    /// <summary>Explains a command line that was not understood.</summary>
    /// <param name="error">What is wrong.</param>
    /// <returns>The usage exit code.</returns>
    private int ReportUsageError(string error)
    {
        _environment.Error.WriteLine("Error: " + error);
        _environment.Error.WriteLine("Try: securefile --help");
        return ExitCodes.Usage;
    }

    /// <summary>Tells the user the run was cancelled and nothing was written.</summary>
    /// <returns>The cancelled exit code.</returns>
    private int ReportCancelled()
    {
        _environment.Error.WriteLine("Cancelled. No file was written.");
        return ExitCodes.Cancelled;
    }

    /// <summary>Writes text to standard output.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Always success.</returns>
    private int Show(string text)
    {
        _environment.Output.WriteLine(text);
        return ExitCodes.Success;
    }

    /// <summary>The text of <c>--version</c>.</summary>
    /// <returns>For example <c>securefile 0.1.0</c>.</returns>
    private static string VersionText()
    {
        Version? version = typeof(CliApplication).Assembly.GetName().Version;
        return version is null ? "securefile" : $"securefile {version.Major}.{version.Minor}.{version.Build}";
    }

    /// <summary>Checks the paths, gets the password and runs the encryption or decryption.</summary>
    /// <param name="options">The valid command line.</param>
    /// <param name="cancellationToken">Cancelled on Ctrl+C.</param>
    /// <returns>The exit code.</returns>
    private async Task<int> RunOperationAsync(CliOptions options, CancellationToken cancellationToken)
    {
        string input = options.Input!;
        string output = OutputPathResolver.Resolve(options.Command, input, options.Output);

        string? refusal = CheckPaths(options, input, output);
        if (refusal is not null)
        {
            return Fail(refusal);
        }

        PasswordResult password = _passwords.Resolve(options);
        return password.Password is null
            ? Fail(password.Error!)
            : await ExecuteAsync(options.Command == CliCommand.Encrypt, input, output, password.Password, options.Force, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Refuses missing input, the input as its own output, and replacing a file without <c>--force</c>.</summary>
    /// <param name="options">The command line.</param>
    /// <param name="input">The chosen file.</param>
    /// <param name="output">Where the result would go.</param>
    /// <returns>The reason to stop, or <see langword="null"/> when all is well.</returns>
    private static string? CheckPaths(CliOptions options, string input, string output)
    {
        if (!File.Exists(input))
        {
            return $"The file \"{input}\" can't be found (or it is a folder).";
        }

        if (OutputPathResolver.IsSameFile(input, output))
        {
            return "The result can't replace the file you chose. Give a different name with --output.";
        }

        // The engine rejects a folder or an invalid file name with ArgumentException, which is not an "expected failure"
        // here (it would also hide programming errors), so these two user mistakes are caught up front (security review R3).
        string fileName = Path.GetFileName(output);
        if (Directory.Exists(output) || fileName.Length == 0 || fileName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return $"\"{output}\" is a folder or not a valid file name. Use --output to choose a file name.";
        }

        return File.Exists(output) && !options.Force
            ? $"\"{output}\" already exists. Use --force to replace it, or --output to choose another name."
            : null;
    }

    /// <summary>Runs the engine with a progress line and turns the expected failures into plain messages.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <param name="input">The chosen file.</param>
    /// <param name="output">The result path.</param>
    /// <param name="password">The accepted password.</param>
    /// <param name="force">Whether an existing result file may be replaced (already checked against <c>--force</c>).</param>
    /// <param name="cancellationToken">Cancelled on Ctrl+C.</param>
    /// <returns>The exit code.</returns>
    private async Task<int> ExecuteAsync(bool encrypt, string input, string output, string password, bool force, CancellationToken cancellationToken)
    {
        ProgressLine? progress = _environment.ShowProgress ? new ProgressLine(_environment.Error, encrypt ? "Encrypting" : "Decrypting") : null;
        try
        {
            await RunEngineAsync(encrypt, input, output, password, force, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (SecureFileException exception)
        {
            return FailFrom(exception);
        }
        catch (IOException exception)
        {
            return FailFrom(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return FailFrom(exception);
        }
        catch (NotSupportedException exception)
        {
            return FailFrom(exception);
        }
        finally
        {
            progress?.Finish();
        }

        return ReportSuccess(encrypt, output);
    }

    /// <summary>Calls the encryption or decryption of the engine.</summary>
    /// <param name="encrypt"><see langword="true"/> to encrypt, <see langword="false"/> to decrypt.</param>
    /// <param name="input">The chosen file.</param>
    /// <param name="output">The result path.</param>
    /// <param name="password">The accepted password.</param>
    /// <param name="force">Whether an existing result file may be replaced.</param>
    /// <param name="progress">Receives the percentage, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancelled on Ctrl+C.</param>
    /// <returns>The running operation.</returns>
    private Task RunEngineAsync(bool encrypt, string input, string output, string password, bool force, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        return encrypt
            ? _service.EncryptAsync(input, output, password, force, progress, cancellationToken)
            : _service.DecryptAsync(input, output, password, force, progress, cancellationToken);
    }

    /// <summary>Shows the plain message for an expected failure.</summary>
    /// <param name="exception">The expected exception.</param>
    /// <returns>The failure exit code.</returns>
    private int FailFrom(Exception exception)
    {
        return Fail(ErrorMessageProvider.GetMessage(exception));
    }

    /// <summary>Tells the user where the result is.</summary>
    /// <param name="encrypt">Whether the operation was encryption.</param>
    /// <param name="output">The result path.</param>
    /// <returns>The success exit code.</returns>
    private int ReportSuccess(bool encrypt, string output)
    {
        return Show(encrypt
            ? $"Encrypted copy saved as \"{output}\". Your original file was not changed."
            : $"Decrypted file saved as \"{output}\".");
    }

    /// <summary>Writes an error line to standard error.</summary>
    /// <param name="message">What went wrong, in plain words.</param>
    /// <returns>The failure exit code.</returns>
    private int Fail(string message)
    {
        _environment.Error.WriteLine("Error: " + message);
        return ExitCodes.Failure;
    }
}

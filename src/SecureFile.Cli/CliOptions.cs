namespace SecureFile.Cli;

/// <summary>What the user asked the program to do.</summary>
internal enum CliCommand
{
    /// <summary>Show the usage text.</summary>
    Help,

    /// <summary>Show the version number.</summary>
    Version,

    /// <summary>Encrypt a file.</summary>
    Encrypt,

    /// <summary>Decrypt a file.</summary>
    Decrypt,
}

/// <summary>The parsed command line.</summary>
/// <param name="Command">What to do.</param>
/// <param name="Input">The file to encrypt or decrypt; <see langword="null"/> for help and version.</param>
/// <param name="Output">The result path chosen with <c>--output</c>, or <see langword="null"/> for the default name.</param>
/// <param name="Force">Whether an existing result file may be replaced (<c>--force</c>).</param>
/// <param name="PasswordFromStdin">Whether the password is the first line of standard input (<c>--password-stdin</c>).</param>
internal sealed record CliOptions(
    CliCommand Command,
    string? Input = null,
    string? Output = null,
    bool Force = false,
    bool PasswordFromStdin = false);

/// <summary>The outcome of reading the command line: the options, or a message saying what is wrong.</summary>
/// <param name="Options">The options when the command line is valid; otherwise <see langword="null"/>.</param>
/// <param name="Error">The problem when the command line is not valid; otherwise <see langword="null"/>.</param>
internal sealed record CliParseResult(CliOptions? Options, string? Error);

/// <summary>The process exit codes, so scripts can tell the outcomes apart.</summary>
internal static class ExitCodes
{
    /// <summary>The operation finished.</summary>
    public const int Success = 0;

    /// <summary>The operation failed in an expected way (wrong password, damaged file, no permission, file in use…).</summary>
    public const int Failure = 1;

    /// <summary>The command line was not understood.</summary>
    public const int Usage = 2;

    /// <summary>The user cancelled with Ctrl+C (the usual shell convention: 128 + signal 2).</summary>
    public const int Cancelled = 130;
}

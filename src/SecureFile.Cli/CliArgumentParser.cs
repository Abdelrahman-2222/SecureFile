namespace SecureFile.Cli;

/// <summary>Reads the command line into <see cref="CliOptions"/>.</summary>
/// <remarks>
/// Written by hand (about forty lines) instead of using a parsing library: the program has two commands and four options,
/// and one fewer dependency keeps the trimmed executable small and the review short.
/// There is deliberately no <c>--password</c> option: a password on the command line ends up in the shell history and in
/// the list of running programs that other users can read. The password comes from a hidden prompt or from standard input.
/// </remarks>
internal static class CliArgumentParser
{
    /// <summary>Parses the arguments.</summary>
    /// <param name="args">The arguments after the program name.</param>
    /// <returns>The options, or an error message for the user.</returns>
    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            return Ok(new CliOptions(CliCommand.Help));
        }

        if (args[0] is "--version" or "-v")
        {
            return Ok(new CliOptions(CliCommand.Version));
        }

        CliCommand? command = args[0].ToUpperInvariant() switch
        {
            "ENCRYPT" => CliCommand.Encrypt,
            "DECRYPT" => CliCommand.Decrypt,
            _ => null,
        };

        return command is null
            ? Fail($"Unknown command \"{args[0]}\". Use encrypt or decrypt.")
            : ParseOperation(command.Value, args);
    }

    /// <summary>Reads the file name and the options that follow <c>encrypt</c> or <c>decrypt</c>.</summary>
    /// <param name="command">The command.</param>
    /// <param name="args">All arguments (the first one is the command).</param>
    /// <returns>The options, or an error message.</returns>
    private static CliParseResult ParseOperation(CliCommand command, IReadOnlyList<string> args)
    {
        OperationArguments collected = new();
        for (int i = 1; i < args.Count; i++)
        {
            if (!collected.OptionsEnded && IsHelp(args[i]))
            {
                return Ok(new CliOptions(CliCommand.Help)); // "encrypt --help" is a question, not a mistake (review CLI-7)
            }

            string? problem = collected.Apply(args, ref i);
            if (problem is not null)
            {
                return Fail(problem);
            }
        }

        return collected.Input is null
            ? Fail("No input file given.")
            : Ok(new CliOptions(command, collected.Input, collected.Output, collected.Force, collected.PasswordFromStdin));
    }

    /// <summary>Collects the file name and options of one <c>encrypt</c> or <c>decrypt</c> command, one argument at a time.</summary>
    private sealed class OperationArguments
    {
        /// <summary>Gets the input file name once it was given.</summary>
        public string? Input { get; private set; }

        /// <summary>Gets the path given with <c>--output</c>.</summary>
        public string? Output { get; private set; }

        /// <summary>Gets a value indicating whether <c>--force</c> was given.</summary>
        public bool Force { get; private set; }

        /// <summary>Gets a value indicating whether <c>--password-stdin</c> was given.</summary>
        public bool PasswordFromStdin { get; private set; }

        /// <summary>Gets a value indicating whether <c>--</c> was seen: every later argument is a file name.</summary>
        /// <remarks>The usual convention, so a file whose name starts with <c>-</c> can be given (review CLI-6).</remarks>
        public bool OptionsEnded { get; private set; }

        /// <summary>Takes the argument at position <paramref name="index"/>, and the next one when the option has a value.</summary>
        /// <param name="args">All arguments.</param>
        /// <param name="index">The position of the argument; moved forward when a value was consumed.</param>
        /// <returns>The problem with the argument, or <see langword="null"/> when it was accepted.</returns>
        public string? Apply(IReadOnlyList<string> args, ref int index)
        {
            string arg = args[index];
            if (OptionsEnded || !arg.StartsWith('-'))
            {
                return ApplyFile(arg);
            }

            if (arg == "--")
            {
                OptionsEnded = true;
                return null;
            }

            return arg is "--output" or "-o" ? ApplyOutput(args, ref index) : ApplyFlag(arg);
        }

        /// <summary>Takes <c>--output</c> and its value.</summary>
        /// <param name="args">All arguments.</param>
        /// <param name="index">The position of the option; moved to its value.</param>
        /// <returns>The problem, or <see langword="null"/>.</returns>
        /// <remarks>
        /// A value that looks like an option is refused: <c>-o --force</c> would otherwise write a file named
        /// <c>--force</c> and silently drop the option. A second <c>--output</c> is refused like a second input file
        /// instead of quietly winning (review CLI-5).
        /// </remarks>
        private string? ApplyOutput(IReadOnlyList<string> args, ref int index)
        {
            string arg = args[index];
            if (Output is not null)
            {
                return $"{arg} was given twice.";
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith('-'))
            {
                return $"{arg} needs a file name (write ./-name for a name that starts with -).";
            }

            Output = args[++index];
            return null;
        }

        /// <summary>Takes a name that is not an option: the one input file.</summary>
        /// <param name="arg">The argument.</param>
        /// <returns>The problem, or <see langword="null"/>.</returns>
        private string? ApplyFile(string arg)
        {
            if (Input is not null)
            {
                return $"Only one input file is allowed, but \"{arg}\" came after \"{Input}\".";
            }

            Input = arg;
            return null;
        }

        /// <summary>Takes an option that has no value.</summary>
        /// <param name="arg">The option.</param>
        /// <returns>The problem (unknown option), or <see langword="null"/>.</returns>
        private string? ApplyFlag(string arg)
        {
            switch (arg)
            {
                case "--force" or "-f":
                    Force = true;
                    return null;
                case "--password-stdin":
                    PasswordFromStdin = true;
                    return null;
                default:
                    return $"Unknown option \"{arg}\".";
            }
        }
    }

    /// <summary>Tells whether an argument asks for help.</summary>
    /// <param name="arg">The first argument.</param>
    /// <returns><see langword="true"/> for <c>help</c>, <c>--help</c>, <c>-h</c> and <c>/?</c>.</returns>
    private static bool IsHelp(string arg)
    {
        return arg is "help" or "--help" or "-h" or "/?";
    }

    /// <summary>Wraps valid options.</summary>
    /// <param name="options">The options.</param>
    /// <returns>A successful result.</returns>
    private static CliParseResult Ok(CliOptions options)
    {
        return new CliParseResult(options, null);
    }

    /// <summary>Wraps an error message.</summary>
    /// <param name="error">What is wrong.</param>
    /// <returns>A failed result.</returns>
    private static CliParseResult Fail(string error)
    {
        return new CliParseResult(null, error);
    }
}

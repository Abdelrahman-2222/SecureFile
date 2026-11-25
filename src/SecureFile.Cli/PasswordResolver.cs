using System.Text;
using SecureFile.Core.Passwords;

namespace SecureFile.Cli;

/// <summary>The outcome of getting a password: the password, or a message saying why there is none.</summary>
/// <param name="Password">The password; <see langword="null"/> when there is an error.</param>
/// <param name="Error">What went wrong; <see langword="null"/> when there is a password.</param>
internal sealed record PasswordResult(string? Password, string? Error);

/// <summary>Gets the password from standard input or from a hidden prompt, and applies the same rules as the Windows app.</summary>
/// <remarks>
/// When encrypting, the password must have at least <see cref="PasswordStrengthEvaluator.MinimumLength"/> characters and,
/// when typed at the keyboard, must be typed twice. When decrypting no rule is applied except "not empty": the file's password
/// may have been chosen by another program or version. A piped password (<c>--password-stdin</c>) is read as one line and
/// is not asked twice, because a script cannot answer a second question.
/// </remarks>
internal sealed class PasswordResolver
{
    private readonly CliEnvironment _environment;
    private readonly IPasswordPrompt _prompt;

    /// <summary>Creates the resolver.</summary>
    /// <param name="environment">Standard streams and whether there is a keyboard.</param>
    /// <param name="prompt">The hidden-input prompt for a person at the keyboard.</param>
    public PasswordResolver(CliEnvironment environment, IPasswordPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(prompt);
        _environment = environment;
        _prompt = prompt;
    }

    /// <summary>Gets a password that is acceptable for the command.</summary>
    /// <param name="options">The command line (decides the source and whether the rules for a new password apply).</param>
    /// <returns>The password, or an explanation.</returns>
    public PasswordResult Resolve(CliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool isNewPassword = options.Command == CliCommand.Encrypt;
        PasswordResult typed = ReadPassword(options, isNewPassword);
        if (typed.Password is null)
        {
            return typed;
        }

        string? problem = Check(typed.Password, isNewPassword);
        if (problem is not null)
        {
            return new PasswordResult(null, problem);
        }

        if (isNewPassword)
        {
            AdviseOnStrength(typed.Password);
        }

        return typed;
    }

    /// <summary>Reads the password from the chosen source (and the confirmation when a person types a new one).</summary>
    /// <param name="options">The command line.</param>
    /// <param name="isNewPassword">Whether a new password is being chosen (encrypting).</param>
    /// <returns>The raw typed password, or an explanation.</returns>
    private PasswordResult ReadPassword(CliOptions options, bool isNewPassword)
    {
        if (options.PasswordFromStdin)
        {
            // Typed at a terminal, the line would be echoed and stay in the scrollback (review CLI-3).
            return _environment.InputRedirected
                ? ReadFromStandardInput()
                : new PasswordResult(null, "--password-stdin needs the password piped in. Leave it out to type the password hidden.");
        }

        return _environment.InputRedirected
            ? new PasswordResult(null, "There is no keyboard to ask for the password. Pipe it in with --password-stdin.")
            : AskAtKeyboard(isNewPassword);
    }

    /// <summary>Reads the first line of standard input as the password.</summary>
    /// <returns>The line, or an explanation when there is none.</returns>
    private PasswordResult ReadFromStandardInput()
    {
        string? line = _environment.Input.ReadLine();
        return line is null
            ? new PasswordResult(null, "No password was given on standard input.")
            : new PasswordResult(line, null);
    }

    /// <summary>Asks a person at the keyboard (twice for a new password).</summary>
    /// <param name="isNewPassword">Whether a new password is being chosen (encrypting).</param>
    /// <returns>The typed password, or an explanation.</returns>
    private PasswordResult AskAtKeyboard(bool isNewPassword)
    {
        string? first = _prompt.Ask(isNewPassword ? "New password:" : "Password:");
        if (first is null)
        {
            return new PasswordResult(null, "No password was entered.");
        }

        if (isNewPassword && _prompt.Ask("Type it again to confirm:") != first)
        {
            return new PasswordResult(null, "The two passwords are not the same. Nothing was encrypted.");
        }

        return new PasswordResult(first, null);
    }

    /// <summary>Applies the rules: not empty, usable as UTF-8, and long enough when it is a new password.</summary>
    /// <param name="password">The typed password.</param>
    /// <param name="isNewPassword">Whether a new password is being chosen.</param>
    /// <returns>The problem, or <see langword="null"/> when the password is acceptable.</returns>
    private static string? Check(string password, bool isNewPassword)
    {
        if (password.Length == 0)
        {
            return "The password can't be empty.";
        }

        if (!IsEncodable(password))
        {
            return "The password contains a character that can't be used. Please type it again.";
        }

        return isNewPassword && password.Length < PasswordStrengthEvaluator.MinimumLength
            ? string.Create(System.Globalization.CultureInfo.CurrentCulture, $"The password is too short. Use at least {PasswordStrengthEvaluator.MinimumLength} characters.")
            : null;
    }

    /// <summary>Checks that the password can be turned into UTF-8 (a lone surrogate, for example from a bad paste, cannot).</summary>
    /// <param name="password">The typed password.</param>
    /// <returns><see langword="true"/> when Core will accept it.</returns>
    /// <remarks>Core refuses such a password with an <see cref="ArgumentException"/> (a programming error); a user can reach it by pasting, so it is checked first and explained.</remarks>
    private static bool IsEncodable(string password)
    {
        try
        {
            // Stryker disable once Boolean : the BOM flag only affects GetPreamble, never GetByteCount; equivalent mutant
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetByteCount(password);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Says so when a new password is only Weak or Fair (advice, never a refusal).</summary>
    /// <param name="password">The accepted password.</param>
    private void AdviseOnStrength(string password)
    {
        PasswordStrength strength = PasswordStrengthEvaluator.Evaluate(password);
        string? advice = strength switch
        {
            PasswordStrength.Weak => "Note: this password is weak. Avoid repeats, sequences and common words; a few unrelated words are safer.",
            PasswordStrength.Fair => "Note: this password is fair. A longer one is safer.",
            _ => null,
        };

        if (advice is not null)
        {
            _environment.Error.WriteLine(advice);
        }
    }
}

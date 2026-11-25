using SecureFile.Core.Passwords;

namespace SecureFile.Cli.Tests;

/// <summary>
/// Tests for <see cref="PasswordResolver"/>: the prompts, the rules for a new password and the strength advice. Written
/// in Stage 9.1 to kill the mutants Stryker found alive there (C22).
/// </summary>
public sealed class PasswordResolverTests : IDisposable
{
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _error.Dispose();
    }

    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("environment", () => new PasswordResolver(null!, new RecordingPrompt()));
        Assert.Throws<ArgumentNullException>("prompt", () => new PasswordResolver(Keyboard(), null!));
        Assert.Throws<ArgumentNullException>("options", () => new PasswordResolver(Keyboard(), new RecordingPrompt()).Resolve(null!));
    }

    [Fact]
    public void Encrypt_AtTheKeyboard_AsksForANewPasswordAndItsConfirmation()
    {
        var prompt = new RecordingPrompt("blue river coffee lamp", "blue river coffee lamp");

        PasswordResult result = Resolver(prompt).Resolve(Options(CliCommand.Encrypt));

        Assert.Equal("blue river coffee lamp", result.Password);
        Assert.Equal(["New password:", "Type it again to confirm:"], prompt.Questions);
    }

    [Fact]
    public void Decrypt_AtTheKeyboard_AsksOnceWithoutTheWordNew()
    {
        var prompt = new RecordingPrompt("old");

        PasswordResult result = Resolver(prompt).Resolve(Options(CliCommand.Decrypt));

        Assert.Equal("old", result.Password);
        Assert.Equal(["Password:"], prompt.Questions);
    }

    [Fact]
    public void NothingEntered_GivesAPlainMessage()
    {
        PasswordResult result = Resolver(new RecordingPrompt()).Resolve(Options(CliCommand.Decrypt));

        Assert.Null(result.Password);
        Assert.Equal("No password was entered.", result.Error);
    }

    [Fact]
    public void NewPassword_OfExactlyTheMinimumLength_IsAccepted_AndOneLessIsRefused()
    {
        string exact = new('k', PasswordStrengthEvaluator.MinimumLength);
        string shorter = exact[1..];

        PasswordResult accepted = Resolver(new RecordingPrompt(exact, exact)).Resolve(Options(CliCommand.Encrypt));
        PasswordResult refused = Resolver(new RecordingPrompt(shorter, shorter)).Resolve(Options(CliCommand.Encrypt));

        Assert.Equal(exact, accepted.Password);
        Assert.Null(refused.Password);
        Assert.Equal("The password is too short. Use at least 8 characters.", refused.Error);
    }

    [Fact]
    public void EmptyLineFromStandardInput_IsRefusedAsEmpty()
    {
        var resolver = new PasswordResolver(Piped("\n"), new RecordingPrompt());

        PasswordResult result = resolver.Resolve(Options(CliCommand.Decrypt, fromStdin: true));

        Assert.Equal("The password can't be empty.", result.Error);
    }

    [Fact]
    public void LoneSurrogate_IsRefusedWithAPlainMessage()
    {
        // Built here, not in [InlineData]: xUnit serialises InlineData as UTF-8, which turns a lone surrogate into U+FFFD.
        string broken = "abcdefgh" + '\uD800';

        PasswordResult result = Resolver(new RecordingPrompt(broken)).Resolve(Options(CliCommand.Decrypt));

        Assert.Null(result.Password);
        Assert.Equal("The password contains a character that can't be used. Please type it again.", result.Error);
    }

    [Theory]
    [InlineData("Password1234", "weak")]
    [InlineData("Hjkmnp7!x", "fair")]
    public void NewPassword_WeakOrFair_IsAcceptedWithAdviceOnStandardError(string password, string word)
    {
        PasswordResult result = Resolver(new RecordingPrompt(password, password)).Resolve(Options(CliCommand.Encrypt));

        Assert.Equal(password, result.Password);
        Assert.Contains($"this password is {word}", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NewPassword_Strong_GetsNoAdvice()
    {
        const string strong = "blue river coffee lamp";

        Resolver(new RecordingPrompt(strong, strong)).Resolve(Options(CliCommand.Encrypt));

        Assert.Equal(string.Empty, _error.ToString());
    }

    /// <summary>A resolver for a person at the keyboard, writing advice to <see cref="_error"/>.</summary>
    private PasswordResolver Resolver(IPasswordPrompt prompt)
    {
        return new PasswordResolver(Keyboard(), prompt);
    }

    /// <summary>An environment with a keyboard (nothing piped in).</summary>
    private CliEnvironment Keyboard()
    {
        return new CliEnvironment(TextReader.Null, TextWriter.Null, _error, InputRedirected: false, ShowProgress: false);
    }

    /// <summary>An environment whose standard input is a pipe holding <paramref name="stdin"/>.</summary>
    private CliEnvironment Piped(string stdin)
    {
        return new CliEnvironment(new StringReader(stdin), TextWriter.Null, _error, InputRedirected: true, ShowProgress: false);
    }

    /// <summary>The options of one command on a file.</summary>
    private static CliOptions Options(CliCommand command, bool fromStdin = false)
    {
        return new CliOptions(command, "a.txt", null, false, fromStdin);
    }

    /// <summary>Answers from a fixed list (then <see langword="null"/>) and records every question.</summary>
    private sealed class RecordingPrompt(params string[] answers) : IPasswordPrompt
    {
        private readonly Queue<string> _answers = new(answers);

        public List<string> Questions { get; } = [];

        public string? Ask(string question)
        {
            Questions.Add(question);
            return _answers.TryDequeue(out string? answer) ? answer : null;
        }
    }
}

using SecureFile.Core.Cryptography;

namespace SecureFile.Cli.Tests;

/// <summary>
/// Tests for the whole command-line program with real files in a temporary folder and the real engine. Only the keyboard
/// is replaced: passwords come from a text reader (<c>--password-stdin</c>) or from a fake prompt.
/// </summary>
public sealed class CliApplicationTests : IDisposable
{
    private const string Phrase = "blue river coffee lamp";

    private readonly string _folder = Directory.CreateTempSubdirectory("SecureFileCli_").FullName;

    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task EncryptThenDecrypt_ThroughStandardInput_RestoresTheOriginalAndNeverPrintsThePassword()
    {
        string original = Write("report.txt", "the secret text");
        string encrypted = original + ".enc";
        string restored = Path.Combine(_folder, "restored.txt");

        RunResult encrypt = await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");
        RunResult decrypt = await RunAsync(["decrypt", encrypted, "-o", restored, "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Success, encrypt.Code);
        Assert.Equal(ExitCodes.Success, decrypt.Code);
        Assert.Contains("Encrypted copy saved as", encrypt.Out, StringComparison.Ordinal);
        Assert.Equal("the secret text", await File.ReadAllTextAsync(restored, TestContext.Current.CancellationToken));
        Assert.Equal("the secret text", await File.ReadAllTextAsync(original, TestContext.Current.CancellationToken)); // original untouched
        Assert.DoesNotContain(Phrase, encrypt.Out + encrypt.Err + decrypt.Out + decrypt.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decrypt_WithoutOutput_RemovesDotEncFromTheName()
    {
        string original = Write("notes.txt", "x");
        await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");
        File.Delete(original);

        RunResult decrypt = await RunAsync(["decrypt", original + ".enc", "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Success, decrypt.Code);
        Assert.True(File.Exists(original));
    }

    [Fact]
    public async Task Decrypt_WrongPassword_FailsWithAPlainMessageAndWritesNothing()
    {
        string original = Write("a.txt", "x");
        await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");
        string output = Path.Combine(_folder, "out.txt");

        RunResult result = await RunAsync(["decrypt", original + ".enc", "-o", output, "--password-stdin"], stdin: "another phrase here\n");

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("Wrong password", result.Err, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Decrypt_APlainFile_SaysItIsNotAnEncryptedFile()
    {
        string plain = Write("plain.txt", "not encrypted");

        RunResult result = await RunAsync(["decrypt", plain, "-o", Path.Combine(_folder, "o.txt"), "--password-stdin"], stdin: "whatever\n");

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("isn't a file encrypted by SecureFile", result.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_ShortPassword_IsRefusedAndNothingIsWritten()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original, "--password-stdin"], stdin: "short7!\n");

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("too short", result.Err, StringComparison.Ordinal);
        Assert.False(File.Exists(original + ".enc"));
    }

    [Fact]
    public async Task Decrypt_AcceptsAShortOldPassword()
    {
        string original = Write("a.txt", "x");
        await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");

        RunResult result = await RunAsync(["decrypt", original + ".enc", "-o", Path.Combine(_folder, "o.txt"), "--password-stdin"], stdin: "old\n");

        Assert.Contains("Wrong password", result.Err, StringComparison.Ordinal); // refused for being wrong, not for being short
        Assert.DoesNotContain("too short", result.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_WeakPassword_IsAcceptedWithANote()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original, "--password-stdin"], stdin: "Password1234\n");

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.Contains("weak", result.Err, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Encrypt_ResultExists_IsRefusedWithoutForceAndReplacedWithIt()
    {
        string original = Write("a.txt", "x");
        string target = Write("a.txt.enc", "old content");

        RunResult refused = await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");
        Assert.Equal(ExitCodes.Failure, refused.Code);
        Assert.Contains("--force", refused.Err, StringComparison.Ordinal);
        Assert.Equal("old content", await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken));

        RunResult forced = await RunAsync(["encrypt", original, "--force", "--password-stdin"], stdin: Phrase + "\n");
        Assert.Equal(ExitCodes.Success, forced.Code);
        Assert.True(EncryptedFileFormat.HasSignature(await File.ReadAllBytesAsync(target, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Encrypt_OutputIsTheInput_IsRefused()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original, "-o", original.ToUpperInvariant(), "--force", "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Equal("x", await File.ReadAllTextAsync(original, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Encrypt_OutputIsTheInputThroughAFolderLink_IsRefusedWithAPlainMessage()
    {
        // Review CLI-1: a text comparison let this through and the engine's ArgumentException crashed the program.
        string original = Write("a.txt", "x");
        string alias = Path.Combine(_folder, "alias");
        try
        {
            Directory.CreateSymbolicLink(alias, _folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"Symbolic links cannot be created here: {exception.Message}");
        }

        RunResult result = await RunAsync(["encrypt", original, "-o", Path.Combine(alias, "a.txt"), "--force", "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("can't replace the file you chose", result.Err, StringComparison.Ordinal);
        Assert.Equal("x", await File.ReadAllTextAsync(original, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Encrypt_OutputIsAFolderOrInvalidName_IsReportedWithoutACrash()
    {
        string original = Write("a.txt", "x");
        string folder = Path.GetDirectoryName(original)!;

        RunResult toFolder = await RunAsync(["encrypt", original, "-o", folder, "--force", "--password-stdin"], stdin: Phrase + "\n");
        RunResult missingFolder = await RunAsync(["encrypt", original, "-o", Path.Combine(folder, "missing") + Path.DirectorySeparatorChar, "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Failure, toFolder.Code);
        Assert.Contains("--output", toFolder.Err, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Failure, missingFolder.Code); // a trailing separator means "a folder": there is no file name
        Assert.Contains("not a valid file name", missingFolder.Err, StringComparison.Ordinal);

        // "|" is an invalid file-name character on Windows only (Linux and macOS accept it, and a NUL cannot even be passed
        // as an argument there), so the invalid-name case runs on Windows alone.
        if (OperatingSystem.IsWindows())
        {
            string badPath = Path.Combine(folder, "bad|name");
            RunResult badName = await RunAsync(["encrypt", original, "-o", badPath, "--password-stdin"], stdin: Phrase + "\n");

            Assert.Equal(ExitCodes.Failure, badName.Code);
            Assert.Contains("not a valid file name", badName.Err, StringComparison.Ordinal);
            Assert.False(File.Exists(badPath)); // nothing was created
        }
    }

    [Fact]
    public async Task Encrypt_MissingFileOrFolder_IsReported()
    {
        RunResult missing = await RunAsync(["encrypt", Path.Combine(_folder, "nope.txt"), "--password-stdin"], stdin: Phrase + "\n");
        RunResult folder = await RunAsync(["encrypt", _folder, "--password-stdin"], stdin: Phrase + "\n");

        Assert.Equal(ExitCodes.Failure, missing.Code);
        Assert.Contains("can't be found", missing.Err, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Failure, folder.Code);
    }

    [Fact]
    public async Task Encrypt_AtTheKeyboard_AsksTwiceAndRefusesDifferentAnswers()
    {
        string original = Write("a.txt", "x");

        RunResult mismatch = await RunAsync(["encrypt", original], prompt: new ScriptedPrompt(Phrase, "something else entirely"));
        Assert.Equal(ExitCodes.Failure, mismatch.Code);
        Assert.Contains("not the same", mismatch.Err, StringComparison.Ordinal);
        Assert.False(File.Exists(original + ".enc"));

        RunResult matching = await RunAsync(["encrypt", original], prompt: new ScriptedPrompt(Phrase, Phrase));
        Assert.Equal(ExitCodes.Success, matching.Code);
        Assert.True(File.Exists(original + ".enc"));
    }

    [Fact]
    public async Task Decrypt_AtTheKeyboard_AsksOnce()
    {
        string original = Write("a.txt", "x");
        await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n");
        var prompt = new ScriptedPrompt(Phrase);

        RunResult result = await RunAsync(["decrypt", original + ".enc", "-o", Path.Combine(_folder, "o.txt")], prompt: prompt);

        Assert.Equal(ExitCodes.Success, result.Code);
        Assert.Equal(1, prompt.Asked);
    }

    [Fact]
    public async Task NoKeyboardAndNoPasswordFlag_ExplainsHowToPipeThePassword()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original], inputRedirected: true);

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("--password-stdin", result.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyStandardInput_IsReportedAsNoPassword()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original, "--password-stdin"], stdin: string.Empty);

        Assert.Equal(ExitCodes.Failure, result.Code);
        Assert.Contains("No password", result.Err, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelled_ExitsWith130AndLeavesNoResult()
    {
        string original = Write("a.txt", "x");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        RunResult result = await RunAsync(["encrypt", original, "--password-stdin"], stdin: Phrase + "\n", token: cancelled.Token);

        Assert.Equal(ExitCodes.Cancelled, result.Code);
        Assert.Contains("Cancelled", result.Err, StringComparison.Ordinal);
        Assert.Equal(["a.txt"], Directory.GetFileSystemEntries(_folder).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public async Task CtrlCAtThePasswordPrompt_CountsAsCancel()
    {
        string original = Write("a.txt", "x");

        RunResult result = await RunAsync(["encrypt", original], prompt: new CancellingPrompt());

        Assert.Equal(ExitCodes.Cancelled, result.Code);
        Assert.False(File.Exists(original + ".enc"));
    }

    [Fact]
    public async Task HelpAndVersion_GoToStandardOutputWithExitCodeZero()
    {
        RunResult help = await RunAsync(["--help"]);
        RunResult version = await RunAsync(["--version"]);

        Assert.Equal(ExitCodes.Success, help.Code);
        Assert.Contains("securefile encrypt", help.Out, StringComparison.Ordinal);
        Assert.Contains("--password-stdin", help.Out, StringComparison.Ordinal);
        Assert.Equal(string.Empty, help.Err);
        Assert.Equal(ExitCodes.Success, version.Code);
        Assert.StartsWith("securefile 0.1.0", version.Out, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommand_GivesUsageErrorOnStandardError()
    {
        RunResult result = await RunAsync(["shred", "a.txt"]);

        Assert.Equal(ExitCodes.Usage, result.Code);
        Assert.Contains("Unknown command", result.Err, StringComparison.Ordinal);
        Assert.Equal(string.Empty, result.Out);
    }

    /// <summary>Writes a small text file into the temporary folder.</summary>
    private string Write(string name, string content)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Runs the program once with in-memory streams.</summary>
    private static async Task<RunResult> RunAsync(string[] args, string stdin = "", IPasswordPrompt? prompt = null, bool inputRedirected = false, CancellationToken? token = null)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        bool redirected = inputRedirected || stdin.Length > 0 || (prompt is null && args.Contains("--password-stdin", StringComparer.Ordinal));
        var environment = new CliEnvironment(input, output, error, redirected, ShowProgress: false);
        var application = new CliApplication(environment, new FileEncryptionService(), prompt ?? new ScriptedPrompt());

        int code = await application.RunAsync(args, token ?? TestContext.Current.CancellationToken);

        return new RunResult(code, output.ToString(), error.ToString());
    }

    /// <summary>The exit code and both output streams of one run.</summary>
    private sealed record RunResult(int Code, string Out, string Err);

    /// <summary>Answers from a fixed list and counts the questions; returns <see langword="null"/> when the list is used up.</summary>
    private sealed class ScriptedPrompt : IPasswordPrompt
    {
        private readonly Queue<string> _answers;

        public ScriptedPrompt(params string[] answers)
        {
            _answers = new Queue<string>(answers);
        }

        public int Asked { get; private set; }

        public string? Ask(string question)
        {
            Asked++;
            return _answers.Count > 0 ? _answers.Dequeue() : null;
        }
    }

    /// <summary>A prompt where the user presses Ctrl+C.</summary>
    private sealed class CancellingPrompt : IPasswordPrompt
    {
        public string? Ask(string question)
        {
            throw new OperationCanceledException();
        }
    }
}

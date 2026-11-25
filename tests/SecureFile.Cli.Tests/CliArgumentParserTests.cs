
namespace SecureFile.Cli.Tests;

/// <summary>Tests for reading the command line.</summary>
public sealed class CliArgumentParserTests
{
    [Theory]
    [InlineData]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("/?")]
    public void Parse_NoArgumentsOrHelp_ShowsHelp(params string[] args)
    {
        CliParseResult result = CliArgumentParser.Parse(args);

        Assert.Equal(CliCommand.Help, result.Options?.Command);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Parse_Version_ShowsVersion(string arg)
    {
        Assert.Equal(CliCommand.Version, CliArgumentParser.Parse([arg]).Options?.Command);
    }

    [Fact]
    public void Parse_EncryptWithAllOptions_FillsEveryField()
    {
        CliParseResult result = CliArgumentParser.Parse(["encrypt", "a.txt", "-o", "b.bin", "--force", "--password-stdin"]);

        CliOptions options = Assert.IsType<CliOptions>(result.Options);
        Assert.Equal(CliCommand.Encrypt, options.Command);
        Assert.Equal("a.txt", options.Input);
        Assert.Equal("b.bin", options.Output);
        Assert.True(options.Force);
        Assert.True(options.PasswordFromStdin);
    }

    [Fact]
    public void Parse_DecryptWithOptionsBeforeTheFile_Works()
    {
        CliOptions options = Assert.IsType<CliOptions>(CliArgumentParser.Parse(["DECRYPT", "--output", "x", "a.enc"]).Options);

        Assert.Equal(CliCommand.Decrypt, options.Command); // the command word is not case-sensitive
        Assert.Equal("a.enc", options.Input);
        Assert.Equal("x", options.Output);
    }

    [Theory]
    [InlineData("shred", "a.txt")]                    // unknown command
    [InlineData("encrypt")]                           // no file
    [InlineData("encrypt", "a.txt", "b.txt")]         // two files
    [InlineData("encrypt", "a.txt", "--password", "x")] // a password on the command line is refused on purpose
    [InlineData("encrypt", "a.txt", "--output")]      // option without value
    [InlineData("decrypt", "a.enc", "--nope")]        // unknown option
    [InlineData("encrypt", "a.txt", "-o", "--force")] // an option is not a file name (review CLI-5)
    [InlineData("encrypt", "a.txt", "-o", "x", "-o", "y")] // a second --output does not quietly win
    [InlineData("encrypt", "a.txt", "--", "b.txt")]   // still one input file after --
    public void Parse_InvalidCommandLine_GivesAMessageAndNoOptions(params string[] args)
    {
        CliParseResult result = CliArgumentParser.Parse(args);

        Assert.Null(result.Options);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Theory]
    [InlineData("encrypt", "--help")]
    [InlineData("decrypt", "a.enc", "-h")]
    public void Parse_HelpAfterTheCommand_ShowsHelp(params string[] args)
    {
        Assert.Equal(CliCommand.Help, CliArgumentParser.Parse(args).Options?.Command);
    }

    [Fact]
    public void Parse_DoubleDash_TakesTheNextArgumentAsAFileEvenWhenItStartsWithADash()
    {
        CliOptions options = Assert.IsType<CliOptions>(CliArgumentParser.Parse(["encrypt", "-f", "--", "-notes.txt"]).Options);

        Assert.Equal("-notes.txt", options.Input);
        Assert.True(options.Force);
    }
}

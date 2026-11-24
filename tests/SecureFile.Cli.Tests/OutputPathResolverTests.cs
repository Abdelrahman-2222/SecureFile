
namespace SecureFile.Cli.Tests;

/// <summary>Tests for the default result names and the same-file check.</summary>
public sealed class OutputPathResolverTests
{
    [Fact]
    public void Resolve_Encrypt_AddsDotEnc()
    {
        Assert.Equal(Path.Combine("d", "report.pdf.enc"), OutputPathResolver.Resolve(CliCommand.Encrypt, Path.Combine("d", "report.pdf"), null));
    }

    [Theory]
    [InlineData("report.pdf.enc", "report.pdf")]
    [InlineData("report.pdf.ENC", "report.pdf")]
    [InlineData("report.bin", "report.bin.decrypted")] // never suggests the input itself
    [InlineData(".enc", ".enc.decrypted")]              // nothing would be left of the name
    public void Resolve_Decrypt_RemovesDotEncOrAddsDecrypted(string inputName, string expectedName)
    {
        Assert.Equal(expectedName, OutputPathResolver.Resolve(CliCommand.Decrypt, inputName, null));
    }

    [Fact]
    public void Resolve_ChosenOutput_IsUsedAsIs()
    {
        Assert.Equal("elsewhere.bin", OutputPathResolver.Resolve(CliCommand.Encrypt, "a.txt", "elsewhere.bin"));
    }

    [Fact]
    public void IsSameFile_IgnoresCaseAndTheWayThePathIsWritten()
    {
        string full = Path.GetFullPath("Data.txt");

        Assert.True(OutputPathResolver.IsSameFile("Data.txt", full.ToUpperInvariant()));
        Assert.True(OutputPathResolver.IsSameFile(Path.Combine(".", "Data.txt"), "Data.txt"));
        Assert.False(OutputPathResolver.IsSameFile("Data.txt", "Data.txt.enc"));
    }
}

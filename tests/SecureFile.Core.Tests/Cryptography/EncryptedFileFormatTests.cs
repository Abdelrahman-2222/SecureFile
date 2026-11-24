using System.Text;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="EncryptedFileFormat"/> (test catalog T-07, T-08). The layout values are a promise to every file
/// already written, so they are pinned here: changing one must fail a test on purpose.
/// </summary>
public sealed class EncryptedFileFormatTests
{
    [Fact]
    public void Layout_Version1_MatchesTheDocumentedFormat()
    {
        // PROJECT_PLAN §5.1. If this test fails, old encrypted files would no longer open.
        Assert.Equal(".enc", EncryptedFileFormat.FileExtension);
        Assert.Equal(4, EncryptedFileFormat.SignatureLength);
        Assert.Equal(1, EncryptedFileFormat.CurrentVersion);
        Assert.Equal(4, EncryptedFileFormat.VersionOffset);
        Assert.Equal(5, EncryptedFileFormat.IterationsOffset);
        Assert.Equal(9, EncryptedFileFormat.SaltOffset);
        Assert.Equal(16, EncryptedFileFormat.SaltLength);
        Assert.Equal(25, EncryptedFileFormat.IvOffset);
        Assert.Equal(16, EncryptedFileFormat.IvLength);
        Assert.Equal(41, EncryptedFileFormat.HeaderLength);
        Assert.Equal(16, EncryptedFileFormat.AesBlockLength);
        Assert.Equal(32, EncryptedFileFormat.AuthenticationTagLength);
        Assert.Equal(89, EncryptedFileFormat.MinimumFileLength);
    }

    [Fact]
    public void Iterations_DefaultAndLimits_MatchTheSecurityDecision()
    {
        Assert.Equal(600_000, EncryptedFileFormat.DefaultIterations);   // OWASP, ADR-03
        Assert.Equal(100_000, EncryptedFileFormat.MinimumIterations);
        Assert.Equal(10_000_000, EncryptedFileFormat.MaximumIterations);
    }

    [Fact]
    public void Signature_IsTheAsciiLettersSFEN()
    {
        Assert.Equal("SFEN", Encoding.ASCII.GetString(EncryptedFileFormat.Signature));
    }

    [Theory]
    [InlineData("SFEN", true)]
    [InlineData("SFEN and more data", true)]
    [InlineData("", false)]                 // empty file
    [InlineData("SFE", false)]              // shorter than the signature
    [InlineData("sfen", false)]             // case matters: bytes, not text
    [InlineData("XSFEN", false)]            // must be at the very start
    [InlineData("%PDF-1.7", false)]         // a normal file
    public void HasSignature_VariousStarts_ReturnsExpectedResult(string fileStart, bool expected)
    {
        bool result = EncryptedFileFormat.HasSignature(Encoding.ASCII.GetBytes(fileStart));

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(99_999, false)]
    [InlineData(100_000, true)]
    [InlineData(600_000, true)]
    [InlineData(10_000_000, true)]
    [InlineData(10_000_001, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(int.MinValue, false)]
    [InlineData(int.MaxValue, false)]
    public void IsSupportedIterationCount_Boundaries_ReturnsExpectedResult(int iterations, bool expected)
    {
        Assert.Equal(expected, EncryptedFileFormat.IsSupportedIterationCount(iterations));
    }

    [Theory]
    [InlineData(89L, 16L)]                              // encrypted empty file: one block of padding
    [InlineData(105L, 32L)]                             // two blocks
    [InlineData(73L + (16L * 65_536), 16L * 65_536)]    // 1 MiB of ciphertext
    [InlineData(73L + (16L << 32), 16L << 32)]          // 64 GiB: lengths are 64-bit, no int overflow
    public void GetCiphertextLength_PossibleLength_ReturnsLengthWithoutHeaderAndTag(long fileLength, long expected)
    {
        Assert.Equal(expected, EncryptedFileFormat.GetCiphertextLength(fileLength));
    }

    [Theory]
    [InlineData(0L)]                // empty file
    [InlineData(41L)]               // header only
    [InlineData(73L)]               // header and tag, no ciphertext
    [InlineData(88L)]               // one byte short of the smallest file
    [InlineData(90L)]               // one byte too many
    [InlineData(104L)]              // one byte short of two blocks
    [InlineData(long.MaxValue)]     // impossible size, and not whole blocks
    public void GetCiphertextLength_ImpossibleLength_ThrowsDamaged(long fileLength)
    {
        InvalidEncryptedFileException exception =
            Assert.Throws<InvalidEncryptedFileException>(() => EncryptedFileFormat.GetCiphertextLength(fileLength));

        Assert.Equal(InvalidEncryptedFileReason.Damaged, exception.Reason);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), "The exception must explain the problem (it is logged).");
    }

    [Fact]
    public void GetCiphertextLength_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => EncryptedFileFormat.GetCiphertextLength(-1));

        Assert.Equal("encryptedFileLength", exception.ParamName);
    }
}

using System.Text;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="KeyDerivation"/> (test catalog T-04 known answers, T-09 passwords).
/// </summary>
/// <remarks>
/// Known-answer tests compare our output with values published by someone else, so they prove we call the right
/// algorithm with the right settings (for example SHA-256 and not SHA-1). Our own "golden" values were computed
/// with a different implementation (Python <c>hashlib</c>, <c>hmac</c>, <c>unicodedata</c>), not with this code.
/// </remarks>
public sealed class KeyDerivationTests
{
    private const int TestIterations = 100_000; // the lowest accepted value: keeps the tests fast (about 70 ms each)

    private static readonly byte[] TestSalt = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");

    // ---- Published known answers -------------------------------------------------------------------------------

    [Theory]
    [InlineData( // RFC 7914 §11, first PBKDF2-HMAC-SHA256 vector
        "passwd", "salt", 1,
        "55AC046E56E3089FEC1691C22544B605F94185216DDE0465E68B9D57C20DACBC"
        + "49CA9CCCF179B645991664B39D77EF317C71B845B1E30BD509112041D3A19783")]
    [InlineData( // RFC 7914 §11, second PBKDF2-HMAC-SHA256 vector
        "Password", "NaCl", 80_000,
        "4DDCD8F60B98BE21830CEE5EF22701F9641A4418D04C0414AEFF08876B34AB56"
        + "A1D425A1225833549ADB841B51C9B3176A272BDEBBA1D078478F62B397F33C8D")]
    public void DeriveMasterKey_Rfc7914Vectors_MatchPublishedOutput(string password, string salt, int iterations, string expectedHex)
    {
        byte[] output = new byte[64];

        KeyDerivation.DeriveMasterKey(Encoding.ASCII.GetBytes(password), Encoding.ASCII.GetBytes(salt), iterations, output);

        Assert.Equal(expectedHex, Convert.ToHexString(output));
    }

    [Theory]
    [InlineData( // RFC 5869 Appendix A.1, test case 1 (PRK → OKM)
        "077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5",
        "F0F1F2F3F4F5F6F7F8F9",
        "3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865")]
    [InlineData( // RFC 5869 Appendix A.3, test case 3 (empty info)
        "19EF24A32C717B167F33A91D6F648BDF96596776AFDB6377AC434C1C293CCB04",
        "",
        "8DA4E775A563C18F715F802A063C5A31B8A11F5C5EE1879EC3454E5F3C738D2D9D201395FAA4B61A96C8")]
    public void ExpandKey_Rfc5869Vectors_MatchPublishedOutput(string prkHex, string infoHex, string expectedHex)
    {
        byte[] output = new byte[42];

        KeyDerivation.ExpandKey(Convert.FromHexString(prkHex), Convert.FromHexString(infoHex), output);

        Assert.Equal(expectedHex, Convert.ToHexString(output));
    }

    // ---- The whole chain: password → two keys ---------------------------------------------------------------------

    [Theory]
    [InlineData( // plain ASCII
        "correct horse battery staple",
        "13AA004FC3E25656F033CB9B11EC45AF570020FB47F6E45C3EF8B2D97C03B116",
        "E1737343359D391559BEA38359A944214420C75D609CC6FEB2FD77F88005ED61")]
    [InlineData( // accent, Arabic and an emoji: exercises NFC and multi-byte UTF-8
        "caf\u00E9 \u0643\u0644\u0645\u0629 \U0001F511",
        "FBDC6DE3CAA2F82B9EE9EA5116B21898EC4AAB62D016C8274BF19F08FCA04350",
        "1DBE67786916C66A53E9363A7D5D2D37F3D469BFCB1B5491F518E33BE79B4A4C")]
    public void DeriveKeys_KnownInputs_MatchIndependentlyComputedKeys(
        string password, string expectedEncryptionKeyHex, string expectedAuthenticationKeyHex)
    {
        using FileKeys keys = KeyDerivation.DeriveKeys(password, NewHeader(TestIterations));

        Assert.Equal(expectedEncryptionKeyHex, Convert.ToHexString(keys.EncryptionKey));
        Assert.Equal(expectedAuthenticationKeyHex, Convert.ToHexString(keys.AuthenticationKey));
    }

    [Fact]
    public void DeriveKeys_SamePasswordTypedDifferently_GivesTheSameKeys()
    {
        // "café" with é as one character (Windows) and as e + combining accent (common on macOS): NFC makes them equal.
        using FileKeys composed = KeyDerivation.DeriveKeys("caf\u00E9", NewHeader(TestIterations));
        using FileKeys decomposed = KeyDerivation.DeriveKeys("cafe\u0301", NewHeader(TestIterations));

        Assert.Equal(composed.EncryptionKey.ToArray(), decomposed.EncryptionKey.ToArray());
        Assert.Equal(composed.AuthenticationKey.ToArray(), decomposed.AuthenticationKey.ToArray());
    }

    [Fact]
    public void DeriveKeys_AnyInput_GivesTwoDifferentKeys()
    {
        using FileKeys keys = KeyDerivation.DeriveKeys("password", NewHeader(TestIterations));

        Assert.NotEqual(keys.EncryptionKey.ToArray(), keys.AuthenticationKey.ToArray());
    }

    [Theory]
    [InlineData("correct horse battery stapl")]   // one character less
    [InlineData("Correct horse battery staple")]  // case matters
    [InlineData("correct horse battery staple ")] // a trailing space is part of the password
    public void DeriveKeys_SlightlyDifferentPassword_GivesDifferentKeys(string otherPassword)
    {
        using FileKeys original = KeyDerivation.DeriveKeys("correct horse battery staple", NewHeader(TestIterations));
        using FileKeys other = KeyDerivation.DeriveKeys(otherPassword, NewHeader(TestIterations));

        Assert.NotEqual(original.EncryptionKey.ToArray(), other.EncryptionKey.ToArray());
    }

    [Fact]
    public void DeriveKeys_DifferentSaltOrIterations_GivesDifferentKeys()
    {
        byte[] otherSalt = [.. TestSalt];
        otherSalt[^1] ^= 1; // flip one bit
        using FileKeys original = KeyDerivation.DeriveKeys("password", NewHeader(TestIterations));
        using FileKeys withOtherSalt = KeyDerivation.DeriveKeys("password", new EncryptedFileHeader(TestIterations, otherSalt, new byte[16]));
        using FileKeys withOtherIterations = KeyDerivation.DeriveKeys("password", NewHeader(TestIterations + 1));

        Assert.NotEqual(original.EncryptionKey.ToArray(), withOtherSalt.EncryptionKey.ToArray());
        Assert.NotEqual(original.EncryptionKey.ToArray(), withOtherIterations.EncryptionKey.ToArray());
    }

    [Fact]
    public void DeriveKeys_IvIsNotPartOfTheKey_GivesTheSameKeys()
    {
        // The IV only starts CBC; the keys depend on password, salt and iterations alone.
        using FileKeys first = KeyDerivation.DeriveKeys("password", new EncryptedFileHeader(TestIterations, TestSalt, new byte[16]));
        using FileKeys second = KeyDerivation.DeriveKeys("password", new EncryptedFileHeader(TestIterations, TestSalt, Enumerable.Repeat((byte)0xFF, 16).ToArray()));

        Assert.Equal(first.EncryptionKey.ToArray(), second.EncryptionKey.ToArray());
    }

    [Theory]
    [InlineData(" ")]       // a single space is allowed: Core does not judge password strength (Step 1.8 does)
    [InlineData("a")]
    public void DeriveKeys_ShortPassword_IsAccepted(string password)
    {
        using FileKeys keys = KeyDerivation.DeriveKeys(password, NewHeader(TestIterations));

        Assert.Equal(KeyDerivation.KeyLength, keys.EncryptionKey.Length);
    }

    [Fact]
    public void DeriveKeys_VeryLongPassword_IsAccepted()
    {
        using FileKeys keys = KeyDerivation.DeriveKeys(new string('x', 10_000), NewHeader(TestIterations));

        Assert.Equal(KeyDerivation.KeyLength, keys.AuthenticationKey.Length);
    }

    [Fact]
    public void DeriveKeys_EmptyPassword_ThrowsArgumentException()
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => KeyDerivation.DeriveKeys("", NewHeader(TestIterations)));

        Assert.Equal("password", exception.ParamName);
        Assert.Contains("must not be empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DeriveKeys_NullHeader_ThrowsArgumentNullException()
    {
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => KeyDerivation.DeriveKeys("password", null!));

        Assert.Equal("header", exception.ParamName);
    }

    [Fact]
    public void DeriveKeys_PasswordWithLoneSurrogate_ThrowsArgumentException()
    {
        // "Impossible" input: half of an emoji, which no keyboard can type. It must not become a replacement character,
        // and the error must name OUR parameter (like every guard clause), not an internal one of the runtime.
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => KeyDerivation.DeriveKeys("abc\uD800", NewHeader(TestIterations)));

        Assert.Equal("password", exception.ParamName);
        Assert.Contains("invalid Unicode", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException); // the runtime's original error is kept for the log
    }

    // ---- Password to bytes ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("abc", "616263")]
    [InlineData("\u00E9", "C3A9")]              // é as one character
    [InlineData("e\u0301", "C3A9")]             // e + combining accent → normalised to the same é
    [InlineData("\u212B", "C385")]              // Ångström sign → letter Å (NFC maps it)
    [InlineData("\uFB01", "EFAC81")]            // the ligature \uFB01 stays as it is: NFC, not NFKC
    [InlineData("\u0643\u0644\u0645\u0629", "D983D984D985D8A9")] // Arabic \u0643\u0644\u0645\u0629 ("password")
    [InlineData("\U0001F511", "F09F9491")]      // emoji: 4 bytes in UTF-8
    [InlineData("  ", "2020")]                  // spaces are kept, never trimmed
    public void PasswordToBytes_VariousText_GivesNfcUtf8Bytes(string password, string expectedHex)
    {
        byte[] bytes = KeyDerivation.PasswordToBytes(password);

        Assert.Equal(expectedHex, Convert.ToHexString(bytes));
    }

    private static EncryptedFileHeader NewHeader(int iterations)
    {
        return new EncryptedFileHeader(iterations, TestSalt, new byte[16]);
    }
}

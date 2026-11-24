using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="AesCbc"/> (test catalog T-04 known answers): the encryptor and decryptor the app uses produce
/// standard AES-256-CBC with PKCS7 padding.
/// </summary>
/// <remarks>
/// Expected values come from outside this code: NIST SP 800-38A Appendix F.2.5 / F.2.6 (read from the NIST PDF), and
/// OpenSSL 3.2 (<c>openssl enc -aes-256-cbc</c>) for padding. OpenSSL also reproduced the NIST blocks, so three
/// independent sources agree.
/// </remarks>
[SuppressMessage(
    "Security",
    "CA5401:Do not use CreateEncryptor with non-default IV",
    Justification = "Known-answer tests need the fixed, published IV. The app itself always uses the random IV from the file header.")]
public sealed class AesCbcTests
{
    // NIST SP 800-38A, F.2.5 CBC-AES256.Encrypt and F.2.6 CBC-AES256.Decrypt (same key, IV and blocks).
    private const string NistKeyHex = "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4";
    private const string NistIvHex = "000102030405060708090A0B0C0D0E0F";

    private const string NistPlaintextHex =
        "6BC1BEE22E409F96E93D7E117393172A"
        + "AE2D8A571E03AC9C9EB76FAC45AF8E51"
        + "30C81C46A35CE411E5FBC1191A0A52EF"
        + "F69F2445DF4F9B17AD2B417BE66C3710";

    private const string NistCiphertextHex =
        "F58C4C04D6E5F1BA779EABFB5F7BFBD6"
        + "9CFC4E967EDB808D679F777BC6702C7D"
        + "39F23369A9D9BACFA530E26304231461"
        + "B2EB05E2C39BE9FCDA6C19078C6A9D1B";

    // The NIST vector has exactly four blocks and no padding. PKCS7 always adds a block, so our encryptor writes these
    // four NIST blocks followed by one padding block, which OpenSSL computed for the same key, IV and plaintext.
    private const string OpenSslPaddingBlockHex = "3F461796D6B0D6B2E0C2A72B4D80E644";

    private static readonly byte[] NistKey = Convert.FromHexString(NistKeyHex);
    private static readonly byte[] NistIv = Convert.FromHexString(NistIvHex);

    [Fact]
    public void CreateEncryptor_NistF25Vector_WritesThePublishedBlocksThenOnePaddingBlock()
    {
        byte[] plaintext = Convert.FromHexString(NistPlaintextHex);

        byte[] ciphertext = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext);

        Assert.Equal(NistCiphertextHex + OpenSslPaddingBlockHex, Convert.ToHexString(ciphertext));
    }

    [Fact]
    public void CreateDecryptor_NistF26Vector_GivesThePublishedPlaintext()
    {
        byte[] ciphertext = Convert.FromHexString(NistCiphertextHex + OpenSslPaddingBlockHex);

        byte[] plaintext = Transform(AesCbc.CreateDecryptor(NistKey, NistIv), ciphertext);

        Assert.Equal(NistPlaintextHex, Convert.ToHexString(plaintext));
    }

    [Theory]
    [InlineData(0, "7E9248E5D829CA7593F0C549DB2F5B8C")]                                  // empty file → one full block of padding
    [InlineData(1, "6A25D28D7977F0FD2B884ED22AD37F95")]                                  // 1 byte + 15 bytes of padding
    [InlineData(15, "F49C72D7BC9F89D734D06618393CE688")]                                 // 15 + 1
    [InlineData(16, "F1538A28599A3E6289751A11D75B8242CE98EB2DDD6E256048BDA60968050665")] // a full block still gets a padding block
    [InlineData(17, "F1538A28599A3E6289751A11D75B824213810F8E37E0361A48D6032FE7342D4A")] // 16 + (1 + 15)
    public void EncryptThenDecrypt_VariousLengths_MatchesOpenSslAndRoundTrips(int length, string expectedHex)
    {
        // The plaintext is `length` letters 'A' (as `head -c n | tr '\0' A` gave to OpenSSL).
        byte[] plaintext = Enumerable.Repeat((byte)'A', length).ToArray();

        byte[] ciphertext = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext);
        byte[] decrypted = Transform(AesCbc.CreateDecryptor(NistKey, NistIv), ciphertext);

        Assert.Equal(expectedHex, Convert.ToHexString(ciphertext));
        Assert.Equal(((length / 16) + 1) * 16, ciphertext.Length); // the formula in PROJECT_PLAN §5.1
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Ciphertext_OfEmptyFile_HoldsSixteenPaddingBytesOfValue16()
    {
        // PKCS7 made visible: decrypt the empty file's block WITHOUT removing padding (plain .NET AES, not our
        // factory, because the app never turns padding off). The empty input became 16 bytes, each holding 16 (0x10).
        using var aes = Aes.Create();
        aes.Key = NistKey;

        byte[] raw = aes.DecryptCbc(Convert.FromHexString("7E9248E5D829CA7593F0C549DB2F5B8C"), NistIv, PaddingMode.None);

        Assert.Equal(Enumerable.Repeat((byte)0x10, 16).ToArray(), raw);
    }

    [Fact]
    public void CreateEncryptor_SameDataWithDifferentIv_GivesDifferentCiphertext()
    {
        // Why every file gets a random IV: with the same key and IV, the same data would give the same bytes,
        // and an observer could tell that two files are identical.
        byte[] plaintext = Enumerable.Repeat((byte)'A', 32).ToArray();
        byte[] otherIv = [.. NistIv];
        otherIv[0] ^= 1;

        byte[] first = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext);
        byte[] again = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext);
        byte[] withOtherIv = Transform(AesCbc.CreateEncryptor(NistKey, otherIv), plaintext);

        Assert.Equal(first, again);
        Assert.NotEqual(first, withOtherIv);
    }

    [Fact]
    public void Decrypt_CiphertextWithOneBitChanged_GivesNoErrorAndAPredictableChange()
    {
        // The reason for the HMAC (ADR-04): CBC alone does NOT notice tampering. Flipping one bit in ciphertext
        // block 1 garbles plaintext block 1 and flips exactly the same bit in plaintext block 2 — silently.
        byte[] plaintext = Enumerable.Repeat((byte)'A', 32).ToArray();
        byte[] ciphertext = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext);

        ciphertext[0] ^= 0x01;
        byte[] decrypted = Transform(AesCbc.CreateDecryptor(NistKey, NistIv), ciphertext);

        Assert.Equal((byte)('A' ^ 0x01), decrypted[16]);              // the attacker chose this change
        Assert.Equal(plaintext[17..], decrypted[17..]);                // the rest of block 2 is untouched
        Assert.NotEqual(plaintext[..16], decrypted[..16]);             // block 1 is garbage, but no exception
    }

    [Fact]
    public void Decrypt_WithThisWrongKey_ThrowsBecauseThePaddingIsBroken()
    {
        // For THIS fixed wrong key the padding comes out broken, so .NET throws. About 1 random wrong key in 256
        // would produce valid-looking padding and return garbage without any error — which is why the HMAC, not the
        // padding, decides "wrong password".
        byte[] ciphertext = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), Enumerable.Repeat((byte)'A', 17).ToArray());
        byte[] wrongKey = new byte[KeyDerivation.KeyLength];

        Assert.Throws<CryptographicException>(() => Transform(AesCbc.CreateDecryptor(wrongKey, NistIv), ciphertext));
    }

    [Fact]
    public void Decryptor_TransformBlock_HoldsBackTheLastBlockUntilFinalBlock()
    {
        // The behaviour Step 1.6 relies on: the last block (which carries the padding) is only released by
        // TransformFinalBlock, so the app can verify the HMAC first and only then finish the decryption.
        byte[] plaintext = Enumerable.Repeat((byte)'A', 20).ToArray();
        byte[] ciphertext = Transform(AesCbc.CreateEncryptor(NistKey, NistIv), plaintext); // 2 blocks
        using ICryptoTransform decryptor = AesCbc.CreateDecryptor(NistKey, NistIv);
        byte[] output = new byte[ciphertext.Length];

        int released = decryptor.TransformBlock(ciphertext, 0, ciphertext.Length, output, 0);
        byte[] rest = decryptor.TransformFinalBlock([], 0, 0);

        Assert.Equal(16, released);                                       // only the first block came out
        byte[] combined = [.. output[..released], .. rest];
        Assert.Equal(plaintext, combined);                                // the final call returned the rest, unpadded
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]    // would silently be AES-128
    [InlineData(24)]    // would silently be AES-192
    [InlineData(31)]
    [InlineData(33)]
    public void Create_KeyNot32Bytes_ThrowsArgumentException(int length)
    {
        ArgumentException encryptorException =
            Assert.Throws<ArgumentException>(() => AesCbc.CreateEncryptor(new byte[length], NistIv));
        ArgumentException decryptorException =
            Assert.Throws<ArgumentException>(() => AesCbc.CreateDecryptor(new byte[length], NistIv));

        Assert.Equal("key", encryptorException.ParamName);
        Assert.Equal("key", decryptorException.ParamName);
        Assert.Contains("An AES-256 key must be exactly 32 bytes", encryptorException.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void Create_IvNot16Bytes_ThrowsArgumentException(int length)
    {
        ArgumentException encryptorException =
            Assert.Throws<ArgumentException>(() => AesCbc.CreateEncryptor(NistKey, new byte[length]));
        ArgumentException decryptorException =
            Assert.Throws<ArgumentException>(() => AesCbc.CreateDecryptor(NistKey, new byte[length]));

        Assert.Equal("iv", encryptorException.ParamName);
        Assert.Equal("iv", decryptorException.ParamName);
        Assert.Contains("The IV must be exactly 16 bytes", encryptorException.Message, StringComparison.Ordinal);
    }

    private static byte[] Transform(ICryptoTransform transform, byte[] input)
    {
        using (transform)
        {
            return transform.TransformFinalBlock(input, 0, input.Length);
        }
    }
}

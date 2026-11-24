using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>Tests for <see cref="FileKeys"/>: key access, wiping on dispose, and guard clauses.</summary>
public sealed class FileKeysTests
{
    [Fact]
    public void Keys_BeforeDispose_ReturnTheGivenBytes()
    {
        byte[] encryptionKey = Filled(0x11);
        byte[] authenticationKey = Filled(0x22);

        using FileKeys keys = new(encryptionKey, authenticationKey);

        Assert.Equal(Filled(0x11), keys.EncryptionKey.ToArray());
        Assert.Equal(Filled(0x22), keys.AuthenticationKey.ToArray());
    }

    [Fact]
    public void Dispose_OverwritesBothKeysWithZeros()
    {
        // The test keeps its own reference to the arrays, so it can look at the memory after disposing.
        byte[] encryptionKey = Filled(0x11);
        byte[] authenticationKey = Filled(0x22);
        FileKeys keys = new(encryptionKey, authenticationKey);

        keys.Dispose();

        Assert.All(encryptionKey, value => Assert.Equal(0, value));
        Assert.All(authenticationKey, value => Assert.Equal(0, value));
    }

    [Fact]
    public void Dispose_KeyReadBeforeDispose_NowShowsZeros()
    {
        // A span is a window onto the same memory, not a copy: anything that still looks at the key after Dispose
        // sees zeros. That is why keys are used inside the using-block and never stored elsewhere.
        using FileKeys keys = new(Filled(0x11), Filled(0x22));
        ReadOnlySpan<byte> encryptionKeyView = keys.EncryptionKey;

        keys.Dispose();

        Assert.True(encryptionKeyView.IndexOfAnyExcept((byte)0) < 0, "Every byte must be zero after Dispose.");
    }

    [Fact]
    public void Keys_AfterDispose_ThrowObjectDisposedException()
    {
        FileKeys keys = new(Filled(0x11), Filled(0x22));
        keys.Dispose();

        Assert.Throws<ObjectDisposedException>(() => keys.EncryptionKey.Length);
        Assert.Throws<ObjectDisposedException>(() => keys.AuthenticationKey.Length);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        FileKeys keys = new(Filled(0x11), Filled(0x22));

        keys.Dispose();
        Exception? exception = Record.Exception(keys.Dispose);

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void Constructor_WrongKeyLength_ThrowsArgumentException(int length)
    {
        ArgumentException encryptionException =
            Assert.Throws<ArgumentException>(() => new FileKeys(new byte[length], Filled(0x22)));
        ArgumentException authenticationException =
            Assert.Throws<ArgumentException>(() => new FileKeys(Filled(0x11), new byte[length]));

        Assert.Equal("encryptionKey", encryptionException.ParamName);
        Assert.Equal("authenticationKey", authenticationException.ParamName);
        Assert.Contains("exactly 32 bytes", encryptionException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NullKey_ThrowsArgumentNullException()
    {
        ArgumentNullException encryptionException =
            Assert.Throws<ArgumentNullException>(() => new FileKeys(null!, Filled(0x22)));
        ArgumentNullException authenticationException =
            Assert.Throws<ArgumentNullException>(() => new FileKeys(Filled(0x11), null!));

        Assert.Equal("encryptionKey", encryptionException.ParamName);
        Assert.Equal("authenticationKey", authenticationException.ParamName);
    }

    private static byte[] Filled(byte value)
    {
        return Enumerable.Repeat(value, KeyDerivation.KeyLength).ToArray();
    }
}

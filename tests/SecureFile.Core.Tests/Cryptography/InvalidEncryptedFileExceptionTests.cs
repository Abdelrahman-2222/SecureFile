using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>Tests for <see cref="InvalidEncryptedFileException"/>: the standard constructors and the reason.</summary>
public sealed class InvalidEncryptedFileExceptionTests
{
    [Fact]
    public void DefaultConstructor_HasDefaultMessageAndNotEncryptedFileReason()
    {
        InvalidEncryptedFileException exception = new();

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.Equal(InvalidEncryptedFileReason.NotEncryptedFile, exception.Reason);
        Assert.IsAssignableFrom<SecureFileException>(exception);
    }

    [Fact]
    public void MessageConstructor_KeepsMessage()
    {
        InvalidEncryptedFileException exception = new("bad header");

        Assert.Equal("bad header", exception.Message);
        Assert.Equal(InvalidEncryptedFileReason.NotEncryptedFile, exception.Reason);
    }

    [Fact]
    public void InnerExceptionConstructor_KeepsMessageAndCause()
    {
        IOException cause = new("disk error");

        InvalidEncryptedFileException exception = new("bad header", cause);

        Assert.Equal("bad header", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }

    [Theory]
    [InlineData(InvalidEncryptedFileReason.NotEncryptedFile)]
    [InlineData(InvalidEncryptedFileReason.UnsupportedVersion)]
    [InlineData(InvalidEncryptedFileReason.Damaged)]
    public void ReasonConstructor_KeepsMessageAndReason(InvalidEncryptedFileReason reason)
    {
        InvalidEncryptedFileException exception = new("bad header", reason);

        Assert.Equal("bad header", exception.Message);
        Assert.Equal(reason, exception.Reason);
    }

    [Fact]
    public void ReasonConstructor_UndefinedReason_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new InvalidEncryptedFileException("bad header", (InvalidEncryptedFileReason)99));

        Assert.Equal("reason", exception.ParamName);

        Assert.Contains("defined value", exception.Message, StringComparison.Ordinal);
    }
}

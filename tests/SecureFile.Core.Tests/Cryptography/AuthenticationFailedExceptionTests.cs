using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>Tests for <see cref="AuthenticationFailedException"/>: the three standard constructors.</summary>
public sealed class AuthenticationFailedExceptionTests
{
    [Fact]
    public void DefaultConstructor_HasTheDefaultMessageAndIsASecureFileException()
    {
        AuthenticationFailedException exception = new();

        Assert.Equal(AuthenticationFailedException.DefaultMessage, exception.Message);
        Assert.IsAssignableFrom<SecureFileException>(exception);
    }

    [Fact]
    public void MessageConstructor_KeepsMessage()
    {
        AuthenticationFailedException exception = new("tag mismatch");

        Assert.Equal("tag mismatch", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void InnerExceptionConstructor_KeepsMessageAndCause()
    {
        IOException cause = new("disk error");

        AuthenticationFailedException exception = new("tag mismatch", cause);

        Assert.Equal("tag mismatch", exception.Message);
        Assert.Same(cause, exception.InnerException);
    }
}

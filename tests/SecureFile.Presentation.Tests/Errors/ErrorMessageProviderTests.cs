using SecureFile.Core.Cryptography;
using SecureFile.Presentation.Errors;

namespace SecureFile.Presentation.Tests.Errors;

/// <summary>Tests for <see cref="ErrorMessageProvider"/>: one test per row of the ADR-13 Layer 2 table.</summary>
public sealed class ErrorMessageProviderTests
{
    private const string DiskFull = "There is not enough free space to save the file. Free up space or choose another drive.";
    private const string Exists = "A file with this name already exists. Choose another name or confirm replacing it.";
    private const string InUse = "The file is being used by another program. Close it and try again.";
    private const string OtherIo = "The file couldn't be read or written. Check that the drive is still connected and try again.";

    [Fact]
    public void Cancelled_SaysNoFileWasWritten()
    {
        Assert.Equal("Cancelled. No file was written.", ErrorMessageProvider.GetMessage(new OperationCanceledException()));
    }

    [Fact]
    public void WrongPassword_SaysPasswordOrChanged()
    {
        Assert.Equal(
            "Wrong password, or the file was changed or damaged. Check Caps Lock, the keyboard language and spaces at the start or end.",
            ErrorMessageProvider.GetMessage(new AuthenticationFailedException()));
    }

    [Theory]
    [InlineData(InvalidEncryptedFileReason.NotEncryptedFile, "This isn't a file encrypted by SecureFile, or its start is damaged. Choose the encrypted file you want to open.")]
    [InlineData(InvalidEncryptedFileReason.UnsupportedVersion, "This file was made by a newer version of SecureFile. Update the app to open it.")]
    [InlineData(InvalidEncryptedFileReason.Damaged, "This encrypted file is damaged or incomplete, so it can't be decrypted.")]
    public void InvalidEncryptedFile_MessageFollowsTheReason(InvalidEncryptedFileReason reason, string expected)
    {
        Assert.Equal(expected, ErrorMessageProvider.GetMessage(new InvalidEncryptedFileException("x", reason)));
    }

    [Fact]
    public void NotSupported_SaysChooseRegularFile()
    {
        Assert.Equal("This item can't be opened as a file. Choose a regular file.", ErrorMessageProvider.GetMessage(new NotSupportedException()));
    }

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(DirectoryNotFoundException))]
    public void NotFound_SaysCantBeFound(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.Equal("The file or folder can't be found or opened.", ErrorMessageProvider.GetMessage(exception));
    }

    [Fact]
    public void AccessDenied_SaysCheckPermissions()
    {
        Assert.Equal(
            "Access to this file or folder was denied. Choose another folder or check its permissions.",
            ErrorMessageProvider.GetMessage(new UnauthorizedAccessException()));
    }

    [Fact]
    public void PathTooLong_SaysChooseShorter()
    {
        Assert.Equal("The path is too long. Choose a shorter folder or name.", ErrorMessageProvider.GetMessage(new PathTooLongException()));
    }

    [Theory]
    [InlineData(unchecked((int)0x80070070), DiskFull)]
    [InlineData(unchecked((int)0x80070027), DiskFull)]
    [InlineData(28, DiskFull)]
    [InlineData(unchecked((int)0x80070050), Exists)]
    [InlineData(unchecked((int)0x800700B7), Exists)]
    [InlineData(17, Exists)]
    [InlineData(unchecked((int)0x80070020), InUse)]
    [InlineData(unchecked((int)0x80070021), InUse)]
    [InlineData(5, OtherIo)]
    public void IoException_MessageFollowsTheErrorCode(int hResult, string expected)
    {
        Assert.Equal(expected, ErrorMessageProvider.GetMessage(new IOException("x", hResult)));
    }

    [Fact]
    public void EndOfStream_IsTheGenericIoMessage()
    {
        Assert.Equal(OtherIo, ErrorMessageProvider.GetMessage(new EndOfStreamException()));
    }

    [Fact]
    public void Message_NeverContainsTheRawExceptionText()
    {
        string message = ErrorMessageProvider.GetMessage(new IOException(@"C:\Users\secret-name\file.txt"));

        Assert.DoesNotContain("secret-name", message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullException_Throws()
    {
        Assert.Throws<ArgumentNullException>("exception", () => ErrorMessageProvider.GetMessage(null!));
    }
}

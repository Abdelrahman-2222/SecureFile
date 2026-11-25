using SecureFile.Core.Cryptography;
using SecureFile.Presentation.Errors;

namespace SecureFile.Presentation.Tests.Errors;

/// <summary>
/// Tests for <see cref="FileErrorLog"/> and <see cref="LogEntryFormatter"/>: no full paths in the log, and no stack
/// trace for an expected failure (review item 29).
/// </summary>
public sealed class FileErrorLogTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("SecureFileTests_");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Theory]
    [InlineData(@"Could not find file 'C:\Users\Sara\TaxReturn2025.pdf'.", "Could not find file '<path>.pdf'.")]
    [InlineData("Could not find file '/home/sara/TaxReturn2025.pdf'.", "Could not find file '<path>.pdf'.")]
    [InlineData(@"Access to the path ""\\server\share\salaries.xlsx"" is denied.", @"Access to the path ""<path>.xlsx"" is denied.")]
    [InlineData(@"   at X.Y() in D:\My Work\src\File.cs:line 42", "   at X.Y() in <path>.cs:line 42")]
    [InlineData("   at X.Y() in /_/src/SecureFile.Core/File.cs:line 7", "   at X.Y() in <path>.cs:line 7")]
    [InlineData("The password is wrong, or the file was changed.", "The password is wrong, or the file was changed.")]
    [InlineData("Use either and/or 1/2 of it.", "Use either and/or 1/2 of it.")]
    public void RemovePaths_ReplacesEveryPathButKeepsTheRest(string text, string expected)
    {
        Assert.Equal(expected, LogEntryFormatter.RemovePaths(text));
    }

    [Fact]
    public void WriteExpected_WrongPassword_WritesTypeAndMessageWithoutAStackTrace()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "test.log"));

        log.WriteExpected(Thrown(new AuthenticationFailedException()));

        string text = File.ReadAllText(log.LogFilePath);
        Assert.Contains(typeof(AuthenticationFailedException).FullName!, text, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_Crash_KeepsTheStackTraceAndInnerExceptionButNoPaths()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "test.log"));
        var inner = new FileNotFoundException(@"Could not find file 'C:\Users\Sara\TaxReturn2025.pdf'.");

        log.Write(Thrown(new InvalidOperationException("broken", inner)));

        string text = File.ReadAllText(log.LogFilePath);
        Assert.Contains(" at ", text, StringComparison.Ordinal);
        Assert.Contains("---> System.IO.FileNotFoundException", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Sara", text, StringComparison.Ordinal);
        Assert.DoesNotContain("TaxReturn2025", text, StringComparison.Ordinal);
    }

    /// <summary>Throws and catches <paramref name="exception"/>, so it carries a real stack trace like a logged one.</summary>
    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught) when (ReferenceEquals(caught, exception))
        {
            return caught;
        }
    }
}

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
    [InlineData(@"Could not find file 'C:\Users\O'Brien\Documents\tax.pdf'.", "Could not find file '<path>.pdf'.")] // quote in the path
    [InlineData(@"Could not find file 'C:\Users\Sara\Sara's taxes 2025.pdf'.", "Could not find file '<path>.pdf'.")]
    [InlineData("Could not find file 'Sara_TaxReturn2025.pdf'.", "Could not find file '<path>.pdf'.")] // a bare file name
    [InlineData("See https://aka.ms/x for details.", "See https://aka.ms/x for details.")] // a web address stays
    [InlineData("Disk full on /dev/sda1 while writing the file", "Disk full on <path> while writing the file")]
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

    [Fact]
    public void Write_LogFolderMissing_CreatesItAndWrites()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "logs", "deeper", "test.log"));

        log.WriteExpected(new InvalidOperationException("x"));

        Assert.True(File.Exists(log.LogFilePath));
    }

    [Fact]
    public void Write_LogOverOneMegabyte_MovesItToDotOneAndStartsANewFile()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "test.log"));
        File.WriteAllText(log.LogFilePath, new string('o', 1_000_001));

        log.WriteExpected(new InvalidOperationException("fresh"));

        Assert.Equal(1_000_001, new FileInfo(log.LogFilePath + ".1").Length);
        string text = File.ReadAllText(log.LogFilePath);
        Assert.Contains("fresh", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ooo", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_LogPathIsAFolder_DoesNotThrow()
    {
        // Logging must never turn into a second failure: the write fails inside and is dropped.
        var log = new FileErrorLog(_folder.FullName);

        log.Write(new InvalidOperationException("x"));

        Assert.True(Directory.Exists(_folder.FullName));
    }

    [Fact]
    public void Write_SecondRotation_ReplacesTheOlderCopy()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "test.log"));
        File.WriteAllText(log.LogFilePath + ".1", "oldest");
        File.WriteAllText(log.LogFilePath, new string('o', 1_000_001));

        log.WriteExpected(new InvalidOperationException("fresh"));

        Assert.Equal(1_000_001, new FileInfo(log.LogFilePath + ".1").Length); // one older copy is kept, not two
    }

    [Fact]
    public void Write_LogOfExactlyOneMegabyte_IsNotRotatedYet()
    {
        var log = new FileErrorLog(Path.Combine(_folder.FullName, "test.log"));
        File.WriteAllText(log.LogFilePath, new string('o', 1_000_000));

        log.WriteExpected(new InvalidOperationException("appended"));

        Assert.False(File.Exists(log.LogFilePath + ".1"));
        Assert.StartsWith("ooo", File.ReadAllText(log.LogFilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_TwoExceptions_GivesTheOuterThenTheInnerAfterAnArrow()
    {
        var chain = new InvalidOperationException("outer", new ArgumentException("inner"));

        Assert.Equal(
            $"System.InvalidOperationException: outer{Environment.NewLine} ---> System.ArgumentException: inner",
            LogEntryFormatter.Format(chain, includeStackTrace: false));
    }

    [Fact]
    public void Format_DeepChain_WritesAtMostFiveInnerExceptions()
    {
        Exception chain = new InvalidOperationException("level 8");
        for (int level = 7; level >= 1; level--)
        {
            chain = new InvalidOperationException($"level {level}", chain);
        }

        string text = LogEntryFormatter.Format(chain, includeStackTrace: false);

        Assert.Equal(5, text.Split(" ---> ").Length - 1);
        Assert.Contains("level 6", text, StringComparison.Ordinal);
        Assert.DoesNotContain("level 7", text, StringComparison.Ordinal);
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

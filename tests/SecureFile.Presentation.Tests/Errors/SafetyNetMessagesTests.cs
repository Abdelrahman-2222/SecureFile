using SecureFile.Presentation.Errors;

namespace SecureFile.Presentation.Tests.Errors;

/// <summary>Tests for <see cref="SafetyNetMessages"/>: both safety-net dialogs name the log file (review item 15).</summary>
public sealed class SafetyNetMessagesTests
{
    private const string LogPath = @"C:\Users\Sara\AppData\Local\SecureFile\logs\securefile.log";

    [Fact]
    public void Recovered_SaysNothingChangedAndNamesTheLogFile()
    {
        string text = SafetyNetMessages.Recovered(LogPath);

        Assert.StartsWith("Something unexpected went wrong. Your files were not changed.", text, StringComparison.Ordinal);
        Assert.EndsWith(LogPath, text, StringComparison.Ordinal);
    }

    [Fact]
    public void MustClose_SaysTheAppClosesAndNamesTheLogFile()
    {
        string text = SafetyNetMessages.MustClose(LogPath);

        Assert.Contains("must close", text, StringComparison.Ordinal);
        Assert.EndsWith(LogPath, text, StringComparison.Ordinal);
    }
}

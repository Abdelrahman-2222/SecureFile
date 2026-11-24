using System.Globalization;
using SecureFile.Presentation.Main;

namespace SecureFile.Presentation.Tests.Main;

/// <summary>Tests for <see cref="ProgressTextFormatter"/>.</summary>
public sealed class ProgressTextFormatterTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 10)]
    public void Format_AtTheEnds_ShowsOnlyThePercentage(int percentage, int elapsedSeconds)
    {
        string text = ProgressTextFormatter.Format(percentage, TenMegabytes, TimeSpan.FromSeconds(elapsedSeconds), CultureInfo.InvariantCulture);

        Assert.Equal($"{percentage}%", text);
    }

    [Fact]
    public void Format_TooEarly_ShowsOnlyThePercentage()
    {
        string text = ProgressTextFormatter.Format(10, TenMegabytes, TimeSpan.FromMilliseconds(100), CultureInfo.InvariantCulture);

        Assert.Equal("10%", text);
    }

    [Fact]
    public void Format_HalfDoneAfterTwoSeconds_ShowsSpeedAndTimeLeft()
    {
        // 5 MB in 2 s = 2.5 MB/s; the other half takes 2 s more.
        string text = ProgressTextFormatter.Format(50, TenMegabytes, TimeSpan.FromSeconds(2), CultureInfo.InvariantCulture);

        Assert.Equal("50% · 2.5 MB/s · about 2 s left", text);
    }

    [Fact]
    public void Format_LongRemainingTime_UsesMinutes()
    {
        string text = ProgressTextFormatter.Format(10, TenMegabytes, TimeSpan.FromSeconds(13), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 1 min 57 s left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_PercentageOutsideRange_IsClamped()
    {
        Assert.Equal("100%", ProgressTextFormatter.Format(150, TenMegabytes, TimeSpan.FromSeconds(5), CultureInfo.InvariantCulture));
    }
}

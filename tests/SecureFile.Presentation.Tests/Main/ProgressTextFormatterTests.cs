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
    public void Format_ExactlySixtySecondsLeft_SwitchesToMinutes()
    {
        // Half done after 60 s: 60 s left, the first value written in minutes (the boundary, found by Stryker).
        string text = ProgressTextFormatter.Format(50, TenMegabytes, TimeSpan.FromSeconds(60), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 1 min 0 s left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NullFormatProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("formatProvider", () => ProgressTextFormatter.Format(50, TenMegabytes, TimeSpan.FromSeconds(2), null!));
    }

    [Fact]
    public void Format_LongRemainingTime_UsesMinutes()
    {
        string text = ProgressTextFormatter.Format(10, TenMegabytes, TimeSpan.FromSeconds(13), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 1 min 57 s left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_MoreThanOneHourLeft_UsesHoursAndMinutes()
    {
        // 1% after 75 s: 99 x 75 s = 7,425 s = 2 h 3 min 45 s left; the seconds are dropped.
        string text = ProgressTextFormatter.Format(1, TenMegabytes, TimeSpan.FromSeconds(75), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 2 h 3 min left", text, StringComparison.Ordinal);
        Assert.DoesNotContain("125", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExactlyOneHourLeft_ShowsOneHourZeroMinutes()
    {
        // 50% after 3,600 s: another 3,600 s left.
        string text = ProgressTextFormatter.Format(50, TenMegabytes, TimeSpan.FromSeconds(3600), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 1 h 0 min left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NinetyNinePercentAfterHalfASecond_RoundsTheTimeLeftUpToOneSecond()
    {
        string text = ProgressTextFormatter.Format(99, TenMegabytes, TimeSpan.FromMilliseconds(500), CultureInfo.InvariantCulture);

        Assert.EndsWith("about 1 s left", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CommaDecimalCulture_UsesTheCultureForTheSpeedOnly()
    {
        string text = ProgressTextFormatter.Format(50, TenMegabytes, TimeSpan.FromSeconds(2), CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal("50% · 2,5 MB/s · about 2 s left", text);
    }

    [Fact]
    public void Format_PercentageOutsideRange_IsClamped()
    {
        Assert.Equal("100%", ProgressTextFormatter.Format(150, TenMegabytes, TimeSpan.FromSeconds(5), CultureInfo.InvariantCulture));
    }
}

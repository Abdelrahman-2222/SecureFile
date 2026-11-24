using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>Tests for <see cref="PercentageProgress"/>: whole percents, reported only when they go up.</summary>
public sealed class PercentageProgressTests
{
    [Fact]
    public void Advance_ThirdsOfTheTotal_ReportsRoundedDownWholePercents()
    {
        var reports = new List<int>();
        var progress = new PercentageProgress(new SynchronousProgress(reports), totalLength: 3);

        progress.Advance(1);
        progress.Advance(1);
        progress.Advance(1);

        Assert.Equal([33, 66, 100], reports);
    }

    [Fact]
    public void Advance_PercentDoesNotChange_ReportsOnlyOnce()
    {
        var reports = new List<int>();
        var progress = new PercentageProgress(new SynchronousProgress(reports), totalLength: 1000);

        progress.Advance(1); // 0 %
        progress.Advance(1); // still 0 %
        progress.Advance(8); // 1 %
        progress.Advance(0); // still 1 %

        Assert.Equal([0, 1], reports);
    }

    [Fact]
    public void Advance_EmptyTotal_ReportsComplete()
    {
        var reports = new List<int>();

        new PercentageProgress(new SynchronousProgress(reports), totalLength: 0).Advance(0);

        Assert.Equal([PercentageProgress.CompletePercentage], reports);
    }

    [Fact]
    public void Advance_NoReceiver_DoesNothing()
    {
        var progress = new PercentageProgress(progress: null, totalLength: 10);

        progress.Advance(10); // must not throw
    }

    private sealed class SynchronousProgress(List<int> reports) : IProgress<int>
    {
        public void Report(int value)
        {
            reports.Add(value);
        }
    }
}

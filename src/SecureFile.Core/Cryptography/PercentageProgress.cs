namespace SecureFile.Core.Cryptography;

/// <summary>
/// Turns "bytes processed" into a whole percentage (0–100) and reports it only when it goes up, so any file causes at
/// most 101 reports.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why throttle here (Review Board 1.5, R3):</b> one report per 1 MiB chunk would be about 10,000 reports for a
/// 10 GB file — about 1,000 per second on a fast disk, each one a message to the UI thread. A progress bar cannot show
/// more than whole percents anyway, and §8.2 asks for at most about 10 UI updates per second.
/// </para>
/// <para>Used by encryption now and by decryption in Step 1.6.</para>
/// <para>Complexity: O(1) time and space per call.</para>
/// </remarks>
/// <param name="progress">The receiver, or <see langword="null"/> when nobody listens.</param>
/// <param name="totalLength">The number of bytes that make 100 %: the file length when the operation started.</param>
internal sealed class PercentageProgress(IProgress<int>? progress, long totalLength)
{
    /// <summary>The percentage reported when everything is done.</summary>
    internal const int CompletePercentage = 100;

    private long _bytesDone;
    private int _lastReportedPercentage = -1;

    /// <summary>Adds processed bytes and reports the new whole percentage if it went up.</summary>
    /// <param name="byteCount">How many more bytes were processed (0 for the final, empty read).</param>
    /// <remarks>
    /// An empty file is 100 % done after its only round. The value is capped at 100: on Linux and macOS file locks are
    /// only advisory, so another program could make the file grow while we read it.
    /// </remarks>
    public void Advance(int byteCount)
    {
        _bytesDone += byteCount;
        int percentage = totalLength == 0
            ? CompletePercentage
            // Stryker disable once Linq,Statement : the cap is only reachable when the file grows while open, which Windows prevents (FileShare.Read); no test on Windows can observe it
            : (int)Math.Min(CompletePercentage, _bytesDone * CompletePercentage / totalLength);

        if (percentage > _lastReportedPercentage)
        {
            _lastReportedPercentage = percentage;
            progress?.Report(percentage);
        }
    }
}

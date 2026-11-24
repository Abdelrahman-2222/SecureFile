using System.Globalization;

namespace SecureFile.Presentation.Main;

/// <summary>Builds the progress line shown under the bar: <c>45% · 12.3 MB/s · about 5 s left</c> (PROJECT_PLAN §11.1).</summary>
/// <remarks>
/// <para>
/// <b>Why computed here:</b> Core reports only a percentage (<see cref="IProgress{T}"/> of <see cref="int"/>), which keeps
/// its API small. Speed and time left follow from the file size, the percentage and the time since the start, so no
/// other layer needs to change.
/// </para>
/// <para>
/// Early readings are too noisy to be useful (a few milliseconds of data), so the speed and time are left out until a
/// short time has passed and some progress has been made.
/// </para>
/// </remarks>
public static class ProgressTextFormatter
{
    /// <summary>The shortest running time after which a speed estimate is shown.</summary>
    private static readonly TimeSpan MinimumElapsedForEstimate = TimeSpan.FromMilliseconds(500);

    private const double BytesPerMegabyte = 1024d * 1024d;

    /// <summary>Formats the progress line.</summary>
    /// <param name="percentage">How much is done, 0–100.</param>
    /// <param name="totalBytes">The size of the file being processed.</param>
    /// <param name="elapsed">The time since the operation started.</param>
    /// <param name="formatProvider">The culture for numbers.</param>
    /// <returns>The percentage, followed by the speed and the time left when they can be estimated.</returns>
    public static string Format(int percentage, long totalBytes, TimeSpan elapsed, IFormatProvider formatProvider)
    {
        ArgumentNullException.ThrowIfNull(formatProvider);

        int clamped = Math.Clamp(percentage, 0, 100);
        string percentText = string.Create(formatProvider, $"{clamped}%");
        if (clamped == 0 || clamped == 100 || elapsed < MinimumElapsedForEstimate)
        {
            return percentText;
        }

        double doneBytes = totalBytes * (clamped / 100d);
        double megabytesPerSecond = doneBytes / BytesPerMegabyte / elapsed.TotalSeconds;
        var remaining = TimeSpan.FromSeconds(elapsed.TotalSeconds * (100 - clamped) / clamped);
        return string.Create(formatProvider, $"{percentText} · {megabytesPerSecond:0.0} MB/s · about {FormatRemaining(remaining)} left");
    }

    /// <summary>Shows a duration in plain words: <c>5 s</c>, or <c>2 min 5 s</c>.</summary>
    /// <param name="remaining">The time left.</param>
    /// <returns>The text to show.</returns>
    private static string FormatRemaining(TimeSpan remaining)
    {
        int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        return seconds < 60
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds} s")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} min {seconds % 60} s");
    }
}

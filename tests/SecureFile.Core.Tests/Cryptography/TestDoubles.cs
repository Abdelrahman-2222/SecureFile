namespace SecureFile.Core.Tests.Cryptography;

/// <summary>A stream that returns at most a few bytes per read, like a slow network drive.</summary>
internal sealed class TrickleStream(byte[] content) : MemoryStream(content)
{
    private const int MaximumBytesPerRead = 7_777; // odd, so reads never line up with 16-byte blocks

    public int ReadCount { get; private set; }

    public override int Read(Span<byte> buffer)
    {
        ReadCount++;
        return base.Read(buffer[..Math.Min(buffer.Length, MaximumBytesPerRead)]);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadCount++;
        return base.ReadAsync(buffer[..Math.Min(buffer.Length, MaximumBytesPerRead)], cancellationToken);
    }
}

/// <summary>
/// Records every report synchronously, in order (the real <see cref="Progress{T}"/> posts reports asynchronously,
/// so their order and timing are not exact), and can run an action on each report.
/// </summary>
internal sealed class RecordingProgress(Action<int>? onReport = null) : IProgress<int>
{
    public List<int> Reports { get; } = [];

    public void Report(int value)
    {
        Reports.Add(value);
        onReport?.Invoke(value);
    }
}

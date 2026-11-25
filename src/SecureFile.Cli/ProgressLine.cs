namespace SecureFile.Cli;

/// <summary>Draws "Encrypting… 45 %" on one terminal line, rewriting it as the percentage grows.</summary>
/// <remarks>
/// Written to standard error and only when it is a terminal, so redirected output and log files stay clean. It is not
/// <see cref="Progress{T}"/> on purpose: that type posts to another thread, so the last percentage could appear after the
/// final message. Core reports from one thread at a time, so a plain writer is safe.
/// </remarks>
internal sealed class ProgressLine : IProgress<int>
{
    private readonly TextWriter _error;
    private readonly string _label;
    private int _last = -1;

    /// <summary>Creates a progress line.</summary>
    /// <param name="error">Where the line is drawn (standard error).</param>
    /// <param name="label">The words before the percentage, for example <c>Encrypting</c>.</param>
    public ProgressLine(TextWriter error, string label)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(label);
        _error = error;
        _label = label;
    }

    /// <inheritdoc />
    public void Report(int value)
    {
        if (value == _last)
        {
            return;
        }

        _last = value;
        _error.Write($"\r{_label}… {value} %");
    }

    /// <summary>Ends the line so the next message starts on a fresh one.</summary>
    public void Finish()
    {
        if (_last >= 0)
        {
            _error.WriteLine();
        }
    }
}

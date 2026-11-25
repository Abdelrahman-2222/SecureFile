using System.Diagnostics;

namespace SecureFile.Presentation.Errors;

/// <summary>
/// Writes handled and unexpected errors to a text file under the user's local application data folder (ADR-13).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why local only:</b> nothing is sent over the network (ADR-12). Only the exception's type and message are
/// written, plus the stack trace for a crash; Core messages never contain passwords, keys or file contents. Paths are
/// replaced by <c>&lt;path&gt;</c> (<see cref="LogEntryFormatter"/>), so neither the user name nor a telling file name
/// such as "TaxReturn2025.pdf" reaches the plaintext log (review item 29).
/// </para>
/// <para>
/// <b>Never throws:</b> a failing log must not turn a handled error into a crash. If the file cannot be written, a
/// warning goes to <see cref="Trace"/> and the error is dropped.
/// </para>
/// <para>
/// <b>Why in Presentation:</b> it is plain file I/O with no UI framework in it, and both desktop apps (WPF and Avalonia)
/// need exactly the same log, so it lives next to <see cref="IErrorLog"/> instead of being copied (moved here in Stage 7).
/// </para>
/// </remarks>
public sealed class FileErrorLog : IErrorLog
{
    /// <summary>The log is moved to <c>.1</c> and restarted above this size, so it cannot grow without bound (1 MB).</summary>
    private const long MaxLogBytes = 1_000_000;

    private readonly object _lock = new();
    private readonly string _logFilePath;

    /// <summary>
    /// Creates a log that writes to <c>SecureFile/logs/securefile.log</c> in the user's local application data folder:
    /// <c>%LOCALAPPDATA%</c> on Windows, <c>~/Library/Application Support</c> on macOS, <c>~/.local/share</c> on Linux.
    /// </summary>
    public FileErrorLog()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureFile", "logs", "securefile.log"))
    {
    }

    /// <summary>Creates a log that writes to a given file (used by tests and by the default constructor).</summary>
    /// <param name="logFilePath">The full path of the log file; its folder is created when needed.</param>
    public FileErrorLog(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    /// <summary>Gets the full path of the log file; the safety-net dialogs show it (<see cref="SafetyNetMessages"/>, review item 15).</summary>
    public string LogFilePath => _logFilePath;

    /// <inheritdoc />
    public void Write(Exception exception)
    {
        Append(exception, includeStackTrace: true);
    }

    /// <inheritdoc />
    public void WriteExpected(Exception exception)
    {
        Append(exception, includeStackTrace: false);
    }

    /// <summary>Formats one entry (paths removed) and appends it with a time stamp, never throwing.</summary>
    /// <param name="exception">The error to record.</param>
    /// <param name="includeStackTrace">Whether stack traces are written (crashes only).</param>
    /// <remarks>The formatting is inside the <c>try</c> too, so a failure there is dropped like a failed write (Stage 9.0 review).</remarks>
    private void Append(Exception exception, bool includeStackTrace)
    {
        try
        {
            string entry = $"{DateTimeOffset.Now:O} {LogEntryFormatter.Format(exception, includeStackTrace)}{Environment.NewLine}{Environment.NewLine}";
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
                RotateIfTooBig();
                File.AppendAllText(_logFilePath, entry);
            }
        }
        catch (IOException logFailure)
        {
            Trace.TraceWarning($"The error log could not be written: {logFailure.GetType().Name}");
        }
        catch (UnauthorizedAccessException logFailure)
        {
            Trace.TraceWarning($"The error log could not be written: {logFailure.GetType().Name}");
        }
        catch (NotSupportedException logFailure)
        {
            Trace.TraceWarning($"The error log could not be written: {logFailure.GetType().Name}");
        }
        catch (ArgumentException logFailure)
        {
            Trace.TraceWarning($"The error log could not be written: {logFailure.GetType().Name}");
        }
    }

    /// <summary>Starts a new file when the current one is over <see cref="MaxLogBytes"/>, keeping one older copy.</summary>
    private void RotateIfTooBig()
    {
        var info = new FileInfo(_logFilePath);
        if (info.Exists && info.Length > MaxLogBytes)
        {
            File.Move(_logFilePath, _logFilePath + ".1", overwrite: true);
        }
    }
}

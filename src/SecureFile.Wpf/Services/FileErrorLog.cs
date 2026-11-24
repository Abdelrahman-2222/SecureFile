using System.Diagnostics;
using System.IO;
using SecureFile.Presentation.Errors;

namespace SecureFile.Wpf.Services;

/// <summary>
/// Writes handled and unexpected errors to a text file under the user's local application data folder (ADR-13).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why local only:</b> nothing is sent over the network (ADR-12). Only the exception's type, message and stack trace
/// are written; Core messages never contain passwords, keys or file contents. A file-system exception can contain a
/// full path (and so the user name); that is acceptable because the log never leaves the computer.
/// </para>
/// <para>
/// <b>Never throws:</b> a failing log must not turn a handled error into a crash. If the file cannot be written, a
/// warning goes to <see cref="Trace"/> and the error is dropped.
/// </para>
/// </remarks>
internal sealed class FileErrorLog : IErrorLog
{
    /// <summary>The log is moved to <c>.1</c> and restarted above this size, so it cannot grow without bound (1 MB).</summary>
    private const long MaxLogBytes = 1_000_000;

    private readonly object _lock = new();
    private readonly string _logFilePath;

    /// <summary>Creates a log that writes to <c>%LOCALAPPDATA%\SecureFile\logs\securefile.log</c>.</summary>
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

    /// <summary>Gets the full path of the log file, so the friendly error dialog can tell the user where it is.</summary>
    public string LogFilePath => _logFilePath;

    /// <inheritdoc />
    public void Write(Exception exception)
    {
        string entry = $"{DateTimeOffset.Now:O} {exception}{Environment.NewLine}{Environment.NewLine}";
        try
        {
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

namespace SecureFile.Presentation.Errors;

/// <summary>
/// Records the technical details of an error in a local log, while the user sees only a plain message (ADR-13).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an interface:</b> the ViewModel logs every error it handles, but must not know where the log is kept. The
/// WPF app writes a small file under the user's local application data folder; the tests use a fake that only
/// collects the exceptions.
/// </para>
/// <para>
/// <b>Rules for implementations:</b> never throw (a failing log must not turn a handled error into a crash), never
/// send anything over the network (ADR-12), and write only the exception: Core messages never contain passwords, keys
/// or file contents.
/// </para>
/// </remarks>
public interface IErrorLog
{
    /// <summary>Writes the exception's type, message and stack trace to the log.</summary>
    /// <param name="exception">The error to record.</param>
    void Write(Exception exception);
}

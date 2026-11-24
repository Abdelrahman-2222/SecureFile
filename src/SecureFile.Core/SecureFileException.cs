namespace SecureFile.Core;

/// <summary>
/// The common base of the errors that are specific to SecureFile (for example a file that is not a SecureFile
/// encrypted file). Errors that .NET already describes well, such as a missing file, use the standard .NET types.
/// </summary>
/// <remarks>
/// <para>
/// Exception-handling strategy ADR-13, Layer 1: Core throws precise exceptions; the user interface decides what to
/// show. The base type lets a caller that only needs "a SecureFile error" (for example the command-line bonus, which
/// maps errors to exit codes) handle every one of them with a single <c>catch</c>, while the app still catches the
/// specific types to show a specific message.
/// </para>
/// <para>
/// It is <see langword="abstract"/> because "a SecureFile error" on its own says nothing: every real error is one
/// of the concrete types that derive from it.
/// </para>
/// </remarks>
public abstract class SecureFileException : Exception
{
    /// <summary>Creates the exception with the default .NET message.</summary>
    protected SecureFileException()
    {
    }

    /// <summary>Creates the exception with a message that explains the problem.</summary>
    /// <param name="message">A technical description for the log. It must never contain a password or a key.</param>
    protected SecureFileException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">A technical description for the log. It must never contain a password or a key.</param>
    /// <param name="innerException">The original exception, kept so the log shows the full cause.</param>
    protected SecureFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

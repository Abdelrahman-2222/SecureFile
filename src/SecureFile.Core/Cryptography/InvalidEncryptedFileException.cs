namespace SecureFile.Core.Cryptography;

/// <summary>
/// The file given for decryption is not a valid SecureFile encrypted file: it is a different kind of file, it was
/// made by a newer version of SecureFile, or it is damaged. <see cref="Reason"/> says which.
/// </summary>
/// <remarks>
/// <para>
/// This exception is thrown <b>before</b> any key is derived or any output is written, so the user gets a fast and
/// accurate answer and no file is created. A wrong password is a different error (it can only be detected after the
/// key is derived), reported in Step 1.7.
/// </para>
/// <para>The message is technical text for the log; the user interface shows its own fixed text (ADR-13).</para>
/// </remarks>
public sealed class InvalidEncryptedFileException : SecureFileException
{
    /// <summary>
    /// Creates the exception with the default message and <see cref="InvalidEncryptedFileReason.NotEncryptedFile"/>.
    /// </summary>
    public InvalidEncryptedFileException()
    {
    }

    /// <summary>
    /// Creates the exception with a message and <see cref="InvalidEncryptedFileReason.NotEncryptedFile"/>.
    /// </summary>
    /// <param name="message">A technical description for the log.</param>
    public InvalidEncryptedFileException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message, the exception that caused it, and
    /// <see cref="InvalidEncryptedFileReason.NotEncryptedFile"/>.
    /// </summary>
    /// <param name="message">A technical description for the log.</param>
    /// <param name="innerException">The original exception, kept so the log shows the full cause.</param>
    public InvalidEncryptedFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with a message and the exact reason.</summary>
    /// <param name="message">A technical description for the log.</param>
    /// <param name="reason">Why the file was rejected.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined value.</exception>
    public InvalidEncryptedFileException(string message, InvalidEncryptedFileReason reason)
        : base(message)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason must be a defined value.");
        }

        Reason = reason;
    }

    /// <summary>Gets why the file was rejected.</summary>
    public InvalidEncryptedFileReason Reason { get; }
}

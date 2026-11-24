namespace SecureFile.Core.Cryptography;

/// <summary>
/// The file given for decryption is not a valid SecureFile encrypted file: it is a different kind of file, it was
/// made by a newer version of SecureFile, or it is damaged. <see cref="Reason"/> says which.
/// </summary>
/// <remarks>
/// <para>
/// For a bad header or length this exception is thrown <b>before</b> any key is derived or any output is written, so
/// the user gets a fast and accurate answer and no file is created. One <see cref="InvalidEncryptedFileReason.Damaged"/>
/// case comes at the end instead: the tag matched but the padding is invalid (a file written by another program that
/// knows the password); the temp file is then deleted. A wrong password is a different error,
/// <see cref="AuthenticationFailedException"/>.
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
        Reason = CheckedReason(reason);
    }

    /// <summary>Creates the exception with a message, the exact reason, and the exception that caused it.</summary>
    /// <param name="message">A technical description for the log.</param>
    /// <param name="reason">Why the file was rejected.</param>
    /// <param name="innerException">The original exception, kept so the log shows the full cause.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined value.</exception>
    public InvalidEncryptedFileException(string message, InvalidEncryptedFileReason reason, Exception innerException)
        : base(message, innerException)
    {
        Reason = CheckedReason(reason);
    }

    /// <summary>Gets why the file was rejected.</summary>
    public InvalidEncryptedFileReason Reason { get; }

    /// <summary>Returns <paramref name="reason"/> if it is a defined value (guard clause shared by the constructors).</summary>
    /// <param name="reason">The reason to check.</param>
    /// <returns>The same reason.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a defined value.</exception>
    private static InvalidEncryptedFileReason CheckedReason(InvalidEncryptedFileReason reason)
    {
        return Enum.IsDefined(reason)
            ? reason
            : throw new ArgumentOutOfRangeException(nameof(reason), reason, "The reason must be a defined value.");
    }
}

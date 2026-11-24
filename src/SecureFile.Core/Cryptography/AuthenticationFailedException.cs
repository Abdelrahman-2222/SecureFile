namespace SecureFile.Core.Cryptography;

/// <summary>
/// Decryption was refused because the authentication tag does not match: the password is wrong, or the file was
/// changed or damaged after it was encrypted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one exception for both causes:</b> the HMAC cannot tell them apart — a wrong password gives a wrong
/// authentication key, and a changed byte gives a different tag; both end in the same mismatch. Guessing which one it
/// was would mislead the user, so the app says "wrong password, or the file was changed or damaged".
/// </para>
/// <para>
/// It is thrown only after the whole file was read and <b>before</b> the last block is decrypted, so the padding of an
/// unverified file is never examined, and no output file is left behind (ADR-07).
/// </para>
/// <para>The message is technical text for the log; the user interface shows its own fixed text (ADR-13).</para>
/// </remarks>
public sealed class AuthenticationFailedException : SecureFileException
{
    /// <summary>The message used by the parameterless constructor.</summary>
    internal const string DefaultMessage = "The authentication tag does not match: wrong password, or the file was changed or damaged.";

    /// <summary>Creates the exception with the default message.</summary>
    public AuthenticationFailedException()
        : base(DefaultMessage)
    {
    }

    /// <summary>Creates the exception with a message that explains the problem.</summary>
    /// <param name="message">A technical description for the log. It must never contain a password or a key.</param>
    public AuthenticationFailedException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and the exception that caused it.</summary>
    /// <param name="message">A technical description for the log. It must never contain a password or a key.</param>
    /// <param name="innerException">The original exception, kept so the log shows the full cause.</param>
    public AuthenticationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

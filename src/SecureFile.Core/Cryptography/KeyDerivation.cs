using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SecureFile.Core.Cryptography;

/// <summary>
/// Turns the user's password into the two keys for one file (PROJECT_PLAN §5.2):
/// password → NFC normalisation → UTF-8 → PBKDF2-HMAC-SHA256 (salt, iterations) → 32-byte master key →
/// HKDF-Expand with two different labels → encryption key and authentication key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why PBKDF2:</b> a password is short and guessable, so the function is made deliberately slow
/// (600,000 rounds, about 0.4–0.5 s on a 2018 laptop) to slow down an attacker who tries millions of passwords. It is
/// the password-based key function built into .NET (the task requires the built-in crypto APIs); Argon2 would be
/// stronger against graphics-card attacks but needs a third-party library.
/// </para>
/// <para>
/// <b>Why HKDF after PBKDF2 (ADR-05):</b> asking PBKDF2 for 64 bytes would run the slow part <b>twice</b> for us,
/// but an attacker can test a guess with only one half, so we would pay double for no extra safety. Instead PBKDF2
/// runs once and the fast HKDF-Expand splits its result into two independent keys. The labels keep the two keys
/// separate ("domain separation"): the encryption key is never reused as the authentication key.
/// </para>
/// <para>
/// <b>Memory hygiene, and its limits:</b> our own temporary copies of the password and the master key are in pinned
/// arrays (the garbage collector never moves them, so it leaves no stray copies of them) or on the stack, and are
/// overwritten with zeros in <c>finally</c> blocks, also when an exception is thrown. Copies outside our control —
/// the caller's immutable <see cref="string"/>, the password box, and buffers inside the runtime and the operating
/// system's crypto library — cannot be wiped by us (PROJECT_PLAN §5.2).
/// </para>
/// <para>
/// <b>Run it off the UI thread:</b> it is CPU-bound for about half a second and cannot be cancelled in the middle.
/// </para>
/// <para>
/// Complexity: O(i) time for i iterations (plus O(p) for a password of p characters); O(p) extra space for the
/// temporary password bytes, O(1) for the keys.
/// </para>
/// </remarks>
internal static class KeyDerivation
{
    /// <summary>The length of the master key and of each derived key: 32 bytes (256 bits).</summary>
    internal const int KeyLength = 32;

    /// <summary>The strict UTF-8 encoder: it throws on invalid text instead of silently replacing it.</summary>
    /// <remarks>
    /// The default <see cref="Encoding.UTF8"/> would turn every invalid character into the same replacement
    /// character, so two different invalid passwords would give the same key. Today the normalisation step already
    /// rejects invalid text before it reaches the encoder; strict mode is the second guard (defence in depth).
    /// </remarks>
    // Stryker disable once Boolean : equivalent mutants - GetBytes never writes a byte-order mark, and normalisation rejects invalid text first
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Gets the HKDF label for the encryption key. The version in the label ties the key to format v1.</summary>
    private static ReadOnlySpan<byte> EncryptionKeyLabel => "SecureFile v1 encryption"u8;

    /// <summary>Gets the HKDF label for the authentication key.</summary>
    private static ReadOnlySpan<byte> AuthenticationKeyLabel => "SecureFile v1 authentication"u8;

    /// <summary>Derives the encryption key and the authentication key for one file.</summary>
    /// <param name="password">The password as typed. It is normalised, so the same visible text gives the same key on every OS.</param>
    /// <param name="header">The file's header: it supplies the salt and the iteration count.</param>
    /// <returns>The two keys. The caller must dispose them (<c>using</c>) to wipe them.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="password"/> is empty, or contains invalid Unicode (a lone surrogate, which no keyboard can
    /// produce).
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="header"/> is <see langword="null"/>.</exception>
    public static FileKeys DeriveKeys(ReadOnlySpan<char> password, EncryptedFileHeader header)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("The password must not be empty.", nameof(password));
        }

        ArgumentNullException.ThrowIfNull(header);

        byte[] passwordBytes = PasswordToBytes(password);
        Span<byte> masterKey = stackalloc byte[KeyLength];
        try
        {
            DeriveMasterKey(passwordBytes, header.Salt.Span, header.Iterations, masterKey);
            return SplitMasterKey(masterKey);
        }
        // Stryker disable once Block : wiping a temporary is not observable by a test; checked by review (G9)
        finally
        {
            // Stryker disable once Statement : wiping a temporary is not observable by a test; checked by review (G9)
            CryptographicOperations.ZeroMemory(passwordBytes);
            // Stryker disable once Statement : wiping a temporary is not observable by a test; checked by review (G9)
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    /// <summary>
    /// Converts a password to bytes the same way on every OS: Unicode normalisation form C (NFC), then strict UTF-8.
    /// </summary>
    /// <param name="password">The password as typed.</param>
    /// <returns>A new pinned array with the password bytes. The caller must wipe it.</returns>
    /// <exception cref="ArgumentException">The password contains invalid Unicode (a lone surrogate).</exception>
    /// <remarks>
    /// <para>
    /// <b>Why normalise:</b> "é" can be stored as one character (U+00E9, Windows keyboards) or as "e" plus an accent
    /// mark (U+0065 U+0301, common on macOS). They look identical but are different bytes, so without normalising,
    /// a file encrypted on a Mac could refuse the "same" password on Windows.
    /// </para>
    /// <para>
    /// <b>Why NFC and not NFKC:</b> NFC only joins letters with their accents. NFKC also rewrites "look-alikes"
    /// (the ligature "ﬁ" becomes "fi", "²" becomes "2"), which would make different passwords equal. The trade-off:
    /// a full-width "Ａ" typed with some Asian input methods stays different from "A".
    /// </para>
    /// </remarks>
    internal static byte[] PasswordToBytes(ReadOnlySpan<char> password)
    {
        try
        {
            return NormalizeAndEncode(password);
        }
        catch (ArgumentException exception)
        {
            // The runtime names its own parameter ("source"); report ours, and keep the original for the log.
            throw new ArgumentException("The password contains invalid Unicode text.", nameof(password), exception);
        }
    }

    /// <summary>Runs PBKDF2 with HMAC-SHA256: the slow, password-strengthening step.</summary>
    /// <param name="passwordBytes">The password bytes.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="iterations">The number of rounds.</param>
    /// <param name="destination">Receives the derived bytes (32 for the master key).</param>
    /// <remarks>A separate method so the published RFC 7914 test vectors can check exactly this call.</remarks>
    internal static void DeriveMasterKey(
        ReadOnlySpan<byte> passwordBytes, ReadOnlySpan<byte> salt, int iterations, Span<byte> destination)
    {
        Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, destination, iterations, HashAlgorithmName.SHA256);
    }

    /// <summary>Runs HKDF-Expand with SHA-256: the fast step that turns the master key into one labelled key.</summary>
    /// <param name="masterKey">The PBKDF2 output.</param>
    /// <param name="label">The purpose of the key (HKDF "info"); a different label gives an unrelated key.</param>
    /// <param name="destination">Receives the key.</param>
    /// <remarks>
    /// <para>
    /// HKDF-Extract is skipped on purpose: the PBKDF2 output is already a uniformly random 32-byte key, and
    /// RFC 5869 §3.3 allows going straight to Expand in that case. NIST SP 800-132 §5.4 likewise derives further keys
    /// from the PBKDF2 output with a key derivation function.
    /// </para>
    /// <para>A separate method so the published RFC 5869 test vectors can check exactly this call.</para>
    /// </remarks>
    internal static void ExpandKey(ReadOnlySpan<byte> masterKey, ReadOnlySpan<byte> label, Span<byte> destination)
    {
        HKDF.Expand(HashAlgorithmName.SHA256, masterKey, destination, label);
    }

    /// <summary>Expands the master key into the two labelled keys, each in a new pinned array.</summary>
    /// <param name="masterKey">The 32-byte PBKDF2 output.</param>
    /// <returns>The encryption key and the authentication key.</returns>
    private static FileKeys SplitMasterKey(ReadOnlySpan<byte> masterKey)
    {
        byte[] encryptionKey = AllocatePinned<byte>(KeyLength);
        byte[] authenticationKey = AllocatePinned<byte>(KeyLength);
        ExpandKey(masterKey, EncryptionKeyLabel, encryptionKey);
        ExpandKey(masterKey, AuthenticationKeyLabel, authenticationKey);
        return new FileKeys(encryptionKey, authenticationKey);
    }

    /// <summary>Normalises the password to NFC in a pinned buffer, then encodes it as strict UTF-8.</summary>
    /// <param name="password">The password as typed.</param>
    /// <returns>A new pinned array with the password bytes.</returns>
    /// <exception cref="ArgumentException">The text is not valid Unicode (thrown by the runtime).</exception>
    /// <exception cref="CryptographicException">Normalisation did not fit its exactly-sized buffer (cannot happen).</exception>
    private static byte[] NormalizeAndEncode(ReadOnlySpan<char> password)
    {
        char[] normalized = AllocatePinned<char>(password.GetNormalizedLength(NormalizationForm.FormC));
        try
        {
            // The buffer has exactly the normalised length, so this cannot fail. If a broken runtime ever made it
            // fail, continuing would encrypt with an empty password, so it must stop (fail closed), not just assert.
            // Stryker disable once Block,Statement,String : unreachable by design; kept so the code fails closed
            if (!password.TryNormalize(normalized, out int charsWritten, NormalizationForm.FormC))
            {
                throw new CryptographicException("The password could not be normalised.");
            }

            ReadOnlySpan<char> normalizedPassword = normalized.AsSpan(0, charsWritten);
            byte[] passwordBytes = AllocatePinned<byte>(StrictUtf8.GetByteCount(normalizedPassword));
            StrictUtf8.GetBytes(normalizedPassword, passwordBytes);
            return passwordBytes;
        }
        // Stryker disable once Block : wiping a temporary is not observable by a test; checked by review (G9)
        finally
        {
            // Stryker disable once Statement : wiping a temporary is not observable by a test; checked by review (G9)
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(normalized.AsSpan()));
        }
    }

    /// <summary>
    /// Creates an array that the garbage collector will never move, so a secret written into it is never copied to
    /// another place in memory where it could not be wiped.
    /// </summary>
    /// <typeparam name="T">The element type (<see cref="byte"/> or <see cref="char"/>).</typeparam>
    /// <param name="length">The number of elements.</param>
    /// <returns>The pinned array. Its contents are not cleared, so the caller must fill all of it.</returns>
    private static T[] AllocatePinned<T>(int length)
        where T : unmanaged
    {
        // Stryker disable once Boolean : pinning is not observable by a test; checked by review (G9)
        return GC.AllocateUninitializedArray<T>(length, pinned: true);
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace SecureFile.Core.Cryptography;

/// <summary>
/// Creates the AES encryptor and decryptor exactly as format version 1 requires: AES-256 (32-byte key), CBC mode,
/// PKCS7 padding, and the file's own IV. This is the only place in the app that configures AES, so the settings are
/// written once and tested once against the NIST known answers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it returns a ready transform and not an <see cref="Aes"/> object:</b> a configured <see cref="Aes"/> can
/// still be changed, and it starts with a random IV of its own. A caller who forgot to set the file's IV would get
/// encryption that "works" but files that can never be decrypted. Taking the key <b>and</b> the IV in one call, both
/// checked, makes that mistake impossible. The <see cref="Aes"/> object is disposed at once (clearing its key copy);
/// the returned transform keeps working on its own.
/// </para>
/// <para>
/// <b>What AES-CBC does not do:</b> it hides the data but does not detect a wrong password or a changed file.
/// That is the job of the HMAC tag (encrypt-then-MAC, ADR-04), which is always checked before the last block is
/// finished and before any decrypted output is kept (ADR-07).
/// </para>
/// <para>
/// <b>Key copies:</b> the temporary <see cref="Aes"/> object keeps an ordinary (not pinned) copy of the key until it
/// is disposed at the end of each method, and the transform holds the key inside the operating system's crypto
/// library. Those copies are outside our control, like the ones described in <see cref="KeyDerivation"/>.
/// </para>
/// <para>Complexity: O(1) time and space to create.</para>
/// </remarks>
internal static class AesCbc
{
    /// <summary>Creates an AES-256-CBC encryptor with PKCS7 padding for one file.</summary>
    /// <param name="key">The 32-byte encryption key (<see cref="FileKeys.EncryptionKey"/>).</param>
    /// <param name="iv">The file's 16-byte IV from its header.</param>
    /// <returns>The encryptor. The caller must dispose it (<c>using</c>).</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not 32 bytes, or <paramref name="iv"/> is not 16 bytes.</exception>
    [SuppressMessage(
        "Security",
        "CA5401:Do not use CreateEncryptor with non-default IV",
        Justification = "The IV is 16 fresh bytes from RandomNumberGenerator for every file (EncryptedFileHeader.CreateNew), stored in the header and never reused. The analyzer cannot follow that data flow; a test proves two encryptions of the same data differ.")]
    public static ICryptoTransform CreateEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        using Aes aes = CreateConfigured(key, iv);
        return aes.CreateEncryptor();
    }

    /// <summary>Creates an AES-256-CBC decryptor with PKCS7 padding for one file.</summary>
    /// <param name="key">The 32-byte encryption key (<see cref="FileKeys.EncryptionKey"/>).</param>
    /// <param name="iv">The file's 16-byte IV from its header.</param>
    /// <returns>The decryptor. The caller must dispose it (<c>using</c>).</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not 32 bytes, or <paramref name="iv"/> is not 16 bytes.</exception>
    /// <remarks>
    /// The decryptor holds back the last block until <see cref="ICryptoTransform.TransformFinalBlock"/> is called,
    /// because only then is the padding checked and removed. Step 1.6 calls it only <b>after</b> the HMAC tag has been
    /// verified, so the padding of an unverified file is never examined.
    /// </remarks>
    public static ICryptoTransform CreateDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        using Aes aes = CreateConfigured(key, iv);
        return aes.CreateDecryptor();
    }

    /// <summary>Checks the key and IV, then creates an AES-256-CBC/PKCS7 cipher with them.</summary>
    /// <param name="key">Exactly 32 bytes.</param>
    /// <param name="iv">Exactly 16 bytes.</param>
    /// <returns>The configured cipher; the caller disposes it.</returns>
    /// <exception cref="ArgumentException">A length is wrong.</exception>
    /// <remarks>
    /// The lengths are checked <b>before</b> the cipher is created, so nothing needs cleaning up when they are wrong.
    /// A 16- or 24-byte key would otherwise silently select AES-128 or AES-192.
    /// </remarks>
    private static Aes CreateConfigured(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
    {
        RequireLength(key, KeyDerivation.KeyLength, "An AES-256 key", nameof(key));
        RequireLength(iv, EncryptedFileFormat.IvLength, "The IV", nameof(iv));

        var aes = Aes.Create(); // "var": the type is already visible on the right (style rule IDE0007)
        aes.SetKey(key);
        aes.IV = iv.ToArray(); // the IV is not secret, so an ordinary copy is fine

        // .NET's defaults are already CBC and PKCS7; they are set explicitly so the code states the format's rules and
        // cannot change if a default ever does.
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    /// <summary>Throws when a value does not have exactly the required length.</summary>
    /// <param name="value">The bytes to check.</param>
    /// <param name="requiredLength">The only accepted length.</param>
    /// <param name="description">What the value is, for the message.</param>
    /// <param name="parameterName">The caller's parameter name, reported in the exception.</param>
    private static void RequireLength(ReadOnlySpan<byte> value, int requiredLength, string description, string parameterName)
    {
        if (value.Length != requiredLength)
        {
            throw new ArgumentException($"{description} must be exactly {requiredLength} bytes long.", parameterName);
        }
    }
}

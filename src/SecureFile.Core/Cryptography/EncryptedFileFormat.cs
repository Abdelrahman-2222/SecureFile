namespace SecureFile.Core.Cryptography;

/// <summary>
/// The fixed layout of a SecureFile encrypted file, format version 1 (PROJECT_PLAN §5.1). Every size and position
/// is written here once, and all other code uses these names instead of bare numbers.
/// </summary>
/// <remarks>
/// <para>Layout (all numbers little-endian):</para>
/// <code>
/// offset  0   4 bytes   signature "SFEN"
/// offset  4   1 byte    format version (1)
/// offset  5   4 bytes   PBKDF2 iteration count
/// offset  9  16 bytes   salt
/// offset 25  16 bytes   IV (initialisation vector)
/// offset 41   n bytes   ciphertext (AES-256-CBC, n is a multiple of 16 and at least 16)
/// offset 41+n 32 bytes  authentication tag (HMAC-SHA256 over everything before it)
/// </code>
/// <para>
/// These values are a <b>promise to every file already written</b>: changing one would make old files unreadable.
/// A new layout gets a new version number instead. Tests pin every value.
/// </para>
/// <para>Complexity: every member is O(1) time and O(1) space.</para>
/// </remarks>
public static class EncryptedFileFormat
{
    /// <summary>The file name ending suggested for encrypted files (<c>report.pdf</c> → <c>report.pdf.enc</c>).</summary>
    public const string FileExtension = ".enc";

    /// <summary>The number of bytes of the signature that starts every encrypted file.</summary>
    public const int SignatureLength = 4;

    /// <summary>The format version this app writes and the only one it reads.</summary>
    internal const byte CurrentVersion = 1;

    /// <summary>The size of one AES block in bytes. The IV is one block; the ciphertext is whole blocks.</summary>
    internal const int AesBlockLength = 16;

    /// <summary>The number of bytes of random salt for the password-to-key function.</summary>
    internal const int SaltLength = 16;

    /// <summary>The number of bytes of the IV: exactly one AES block.</summary>
    internal const int IvLength = AesBlockLength;

    /// <summary>The number of bytes of the HMAC-SHA256 authentication tag at the end of the file.</summary>
    internal const int AuthenticationTagLength = 32;

    /// <summary>Where the version byte is: right after the signature.</summary>
    internal const int VersionOffset = SignatureLength;

    /// <summary>Where the 4-byte iteration count is.</summary>
    internal const int IterationsOffset = VersionOffset + sizeof(byte);

    /// <summary>Where the salt is.</summary>
    internal const int SaltOffset = IterationsOffset + sizeof(int);

    /// <summary>Where the IV is.</summary>
    internal const int IvOffset = SaltOffset + SaltLength;

    /// <summary>The total size of the header (41 bytes); the ciphertext starts here.</summary>
    internal const int HeaderLength = IvOffset + IvLength;

    /// <summary>
    /// The smallest possible encrypted file (89 bytes): the header, one block of ciphertext (an empty file still
    /// produces one block of padding) and the tag.
    /// </summary>
    internal const int MinimumFileLength = HeaderLength + AesBlockLength + AuthenticationTagLength;

    /// <summary>
    /// The PBKDF2-HMAC-SHA256 iteration count used for new files: 600,000, the OWASP Password Storage Cheat Sheet
    /// recommendation (checked 2026-11-24).
    /// </summary>
    internal const int DefaultIterations = 600_000;

    /// <summary>
    /// The lowest iteration count accepted when reading. This app never writes less, so a lower value means the
    /// file is damaged or was written by another program. It is a sanity check (defence in depth): a tampered value
    /// would also fail the authentication tag check.
    /// </summary>
    internal const int MinimumIterations = 100_000;

    /// <summary>
    /// The highest iteration count accepted when reading. Higher values are refused so that a crafted file cannot
    /// keep the app busy for minutes (denial of service).
    /// </summary>
    internal const int MaximumIterations = 10_000_000;

    /// <summary>Gets the signature bytes: the ASCII letters <c>SFEN</c> ("SecureFile ENcrypted").</summary>
    /// <remarks>
    /// A <see cref="ReadOnlySpan{T}"/> of a <c>u8</c> literal points straight at bytes stored in the program itself:
    /// nothing is allocated and nobody can change them.
    /// </remarks>
    internal static ReadOnlySpan<byte> Signature => "SFEN"u8;

    /// <summary>
    /// Checks whether data starts with the SecureFile signature, without throwing. Use it to suggest
    /// "Decrypt" or "Encrypt" for a selected file.
    /// </summary>
    /// <param name="fileStart">The first bytes of a file (at least <see cref="SignatureLength"/> to match).</param>
    /// <returns>
    /// <see langword="true"/> when the data starts with the signature. A <see langword="true"/> result does not prove
    /// the file is valid; reading the header checks the rest.
    /// </returns>
    public static bool HasSignature(ReadOnlySpan<byte> fileStart)
    {
        return fileStart.StartsWith(Signature);
    }

    /// <summary>Checks whether an iteration count is inside the accepted range (100,000 to 10,000,000).</summary>
    /// <param name="iterations">The iteration count to check.</param>
    /// <returns><see langword="true"/> when the count is accepted.</returns>
    internal static bool IsSupportedIterationCount(int iterations)
    {
        return iterations is >= MinimumIterations and <= MaximumIterations;
    }

    /// <summary>
    /// Works out how many ciphertext bytes an encrypted file of the given total length holds, and checks that the
    /// length is possible.
    /// </summary>
    /// <param name="encryptedFileLength">The total length of the encrypted file in bytes.</param>
    /// <returns>The ciphertext length: the total minus the header and the tag.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="encryptedFileLength"/> is negative.</exception>
    /// <exception cref="InvalidEncryptedFileException">
    /// The length is impossible for this format (<see cref="InvalidEncryptedFileReason.Damaged"/>): shorter than
    /// 89 bytes, or the ciphertext is not a whole number of 16-byte blocks. The file was cut short or extended.
    /// </exception>
    /// <remarks>
    /// Checked before any work is done, so a cut-short download is reported at once instead of after the slow key
    /// derivation.
    /// </remarks>
    internal static long GetCiphertextLength(long encryptedFileLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(encryptedFileLength);

        long ciphertextLength = encryptedFileLength - HeaderLength - AuthenticationTagLength;
        if (encryptedFileLength < MinimumFileLength || ciphertextLength % AesBlockLength != 0)
        {
            throw new InvalidEncryptedFileException(
                "The file length does not fit the encrypted file format: the file was cut short or extended.",
                InvalidEncryptedFileReason.Damaged);
        }

        return ciphertextLength;
    }
}

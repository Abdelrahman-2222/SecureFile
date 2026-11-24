using System.Buffers.Binary;
using System.Security.Cryptography;

namespace SecureFile.Core.Cryptography;

/// <summary>
/// The 41-byte header at the start of every encrypted file: the values needed to rebuild the keys (iteration count
/// and salt) and to start decryption (IV). See <see cref="EncryptedFileFormat"/> for the exact layout.
/// </summary>
/// <remarks>
/// <para>
/// <b>Immutable:</b> the salt and IV are copied in and only exposed read-only, so a header cannot change after it
/// is created or read. That matters because the same bytes must be written to the file, added to the
/// authentication tag, and used for the keys.
/// </para>
/// <para>
/// <b>Not secret:</b> the salt and IV only need to be random and unique per file; the password and the keys are the
/// secrets. That is why they can be stored in plain view.
/// </para>
/// <para>
/// <b>Untrusted on read:</b> <see cref="Read"/> treats the bytes as attacker-controlled and accepts only values this
/// version can use safely (signature, version, iteration range) — "validate at the boundary".
/// </para>
/// <para>
/// Internal: only the encryption service in this library reads and writes headers; the user interface never sees
/// them.
/// </para>
/// <para>Complexity: every member is O(1) time and O(1) space (the header has a fixed size).</para>
/// </remarks>
internal sealed class EncryptedFileHeader
{
    private readonly byte[] _salt;
    private readonly byte[] _iv;

    /// <summary>Creates a header from known values (used when reading a file and by tests).</summary>
    /// <param name="iterations">The PBKDF2 iteration count, from 100,000 to 10,000,000.</param>
    /// <param name="salt">Exactly 16 bytes of salt. The bytes are copied.</param>
    /// <param name="iv">Exactly 16 bytes of IV. The bytes are copied.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="iterations"/> is outside the accepted range.</exception>
    /// <exception cref="ArgumentException"><paramref name="salt"/> or <paramref name="iv"/> is not 16 bytes long.</exception>
    public EncryptedFileHeader(int iterations, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> iv)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, EncryptedFileFormat.MinimumIterations);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(iterations, EncryptedFileFormat.MaximumIterations);
        RequireLength(salt, EncryptedFileFormat.SaltLength, nameof(salt));
        RequireLength(iv, EncryptedFileFormat.IvLength, nameof(iv));

        Iterations = iterations;
        _salt = salt.ToArray();
        _iv = iv.ToArray();
    }

    /// <summary>Gets the PBKDF2 iteration count used to turn the password into the master key.</summary>
    public int Iterations { get; }

    /// <summary>Gets the 16-byte salt (read-only view).</summary>
    public ReadOnlyMemory<byte> Salt => _salt;

    /// <summary>Gets the 16-byte IV (read-only view).</summary>
    public ReadOnlyMemory<byte> Iv => _iv;

    /// <summary>
    /// Creates the header for a new encrypted file, with a fresh random salt and IV from the operating system's
    /// cryptographic random number generator.
    /// </summary>
    /// <param name="iterations">
    /// The PBKDF2 iteration count; the app passes <see cref="EncryptedFileFormat.DefaultIterations"/>.
    /// </param>
    /// <returns>A new header; two calls never share a salt or IV in practice (2¹²⁸ possible values each).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="iterations"/> is outside the accepted range.</exception>
    public static EncryptedFileHeader CreateNew(int iterations)
    {
        Span<byte> salt = stackalloc byte[EncryptedFileFormat.SaltLength];
        Span<byte> iv = stackalloc byte[EncryptedFileFormat.IvLength];
        RandomNumberGenerator.Fill(salt);
        RandomNumberGenerator.Fill(iv);

        return new EncryptedFileHeader(iterations, salt, iv);
    }

    /// <summary>
    /// Reads and checks a header from the first bytes of an encrypted file.
    /// </summary>
    /// <param name="source">
    /// The start of the file: at least 41 bytes for a valid header. Bytes after the header are ignored.
    /// </param>
    /// <returns>The header.</returns>
    /// <exception cref="InvalidEncryptedFileException">
    /// <see cref="InvalidEncryptedFileReason.NotEncryptedFile"/> when the signature is missing;
    /// <see cref="InvalidEncryptedFileReason.UnsupportedVersion"/> when the version is newer than this app;
    /// <see cref="InvalidEncryptedFileReason.Damaged"/> when the header is cut short, the version is impossible
    /// (0), or the iteration count is outside 100,000 to 10,000,000.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The checks run in the order that gives the most useful answer: first "is this our file at all?", then
    /// "can this version read it?", then "are the values possible?".
    /// </para>
    /// <para>
    /// <b>How to call it:</b> read the start of the file with
    /// <c>stream.ReadAtLeast(buffer, 41, throwOnEndOfStream: false)</c> and pass only the bytes actually read, so a
    /// 3-byte text file is reported as "not an encrypted file" instead of a disk error. Call it <b>before</b>
    /// <see cref="EncryptedFileFormat.GetCiphertextLength"/>, which would call any short file "damaged".
    /// </para>
    /// </remarks>
    public static EncryptedFileHeader Read(ReadOnlySpan<byte> source)
    {
        if (!EncryptedFileFormat.HasSignature(source))
        {
            throw new InvalidEncryptedFileException(
                "The data does not start with the SecureFile signature.", InvalidEncryptedFileReason.NotEncryptedFile);
        }

        if (source.Length > EncryptedFileFormat.VersionOffset)
        {
            ThrowIfVersionIsNotCurrent(source[EncryptedFileFormat.VersionOffset]);
        }

        if (source.Length < EncryptedFileFormat.HeaderLength)
        {
            throw new InvalidEncryptedFileException(
                "The header is cut short.", InvalidEncryptedFileReason.Damaged);
        }

        int iterations = BinaryPrimitives.ReadInt32LittleEndian(source[EncryptedFileFormat.IterationsOffset..]);
        if (!EncryptedFileFormat.IsSupportedIterationCount(iterations))
        {
            throw new InvalidEncryptedFileException(
                "The iteration count in the header is outside the accepted range.", InvalidEncryptedFileReason.Damaged);
        }

        return new EncryptedFileHeader(
            iterations,
            source.Slice(EncryptedFileFormat.SaltOffset, EncryptedFileFormat.SaltLength),
            source.Slice(EncryptedFileFormat.IvOffset, EncryptedFileFormat.IvLength));
    }

    /// <summary>Writes the 41 header bytes to the start of <paramref name="destination"/>.</summary>
    /// <param name="destination">Where to write; must have room for at least 41 bytes. Bytes after 41 are not touched.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than 41 bytes.</exception>
    /// <remarks>
    /// Writes into a caller's buffer instead of returning a new array, so the encryption service can use a small
    /// stack buffer: no allocation.
    /// </remarks>
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < EncryptedFileFormat.HeaderLength)
        {
            throw new ArgumentException("The destination is too short for the header (41 bytes).", nameof(destination));
        }

        EncryptedFileFormat.Signature.CopyTo(destination);
        destination[EncryptedFileFormat.VersionOffset] = EncryptedFileFormat.CurrentVersion;
        BinaryPrimitives.WriteInt32LittleEndian(destination[EncryptedFileFormat.IterationsOffset..], Iterations);
        _salt.CopyTo(destination[EncryptedFileFormat.SaltOffset..]);
        _iv.CopyTo(destination[EncryptedFileFormat.IvOffset..]);
    }

    /// <summary>
    /// Rejects every version except the current one: a higher number means a newer app wrote the file; a lower one
    /// (0) was never written by any version, so the file is damaged.
    /// </summary>
    /// <param name="version">The version byte read from the file.</param>
    /// <exception cref="InvalidEncryptedFileException">The version is not <see cref="EncryptedFileFormat.CurrentVersion"/>.</exception>
    private static void ThrowIfVersionIsNotCurrent(byte version)
    {
        if (version > EncryptedFileFormat.CurrentVersion)
        {
            throw new InvalidEncryptedFileException(
                "The file uses a newer format version than this app supports.", InvalidEncryptedFileReason.UnsupportedVersion);
        }

        if (version < EncryptedFileFormat.CurrentVersion)
        {
            throw new InvalidEncryptedFileException(
                "The format version in the header is impossible.", InvalidEncryptedFileReason.Damaged);
        }
    }

    /// <summary>Throws when a byte value does not have exactly the required length.</summary>
    /// <param name="value">The bytes to check.</param>
    /// <param name="requiredLength">The only accepted length.</param>
    /// <param name="parameterName">The caller's parameter name, reported in the exception.</param>
    /// <exception cref="ArgumentException">The length is wrong.</exception>
    private static void RequireLength(ReadOnlySpan<byte> value, int requiredLength, string parameterName)
    {
        if (value.Length != requiredLength)
        {
            throw new ArgumentException($"The value must be exactly {requiredLength} bytes long.", parameterName);
        }
    }
}

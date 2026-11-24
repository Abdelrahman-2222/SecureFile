using System.Buffers;
using System.Security.Cryptography;
using SecureFile.Core.Files;

namespace SecureFile.Core.Cryptography;

/// <summary>
/// Encrypts and decrypts files in SecureFile format version 1: PBKDF2 → HKDF keys, AES-256-CBC/PKCS7, HMAC-SHA256
/// over the header and the ciphertext (encrypt-then-MAC), written through a temp file and an atomic move.
/// </summary>
/// <remarks>
/// <para>
/// <b>Our own streaming loop (ADR-06):</b> the file is read in 1 MiB chunks into a pooled buffer; each chunk goes
/// through <see cref="ICryptoTransform.TransformBlock"/>, into the HMAC and to the temp file; the last 0–15 bytes go
/// through <see cref="ICryptoTransform.TransformFinalBlock"/>, which adds the padding. No <c>CryptoStream</c> (it hides
/// when the last block is processed, which decryption must control), and no one-shot <c>EncryptCbc</c> per chunk
/// (chaining the IV by hand is new risk for no gain).
/// </para>
/// <para>
/// <b>Decryption is verify-then-commit (§5.4):</b> the same loop in reverse, with the HMAC over the ciphertext; the
/// decryptor always holds back the last block, and <see cref="ICryptoTransform.TransformFinalBlock"/> runs only after
/// the tag matched. Unverified plaintext goes only to a hidden temp file, which is deleted on any failure.
/// </para>
/// <para>
/// <b>Key derivation runs on a thread-pool thread</b> (<c>Task.Run</c>): it is about 0.5 s of pure computing and
/// would freeze the window. Once started it cannot be interrupted; a cancel during it is seen at the first read.
/// </para>
/// <para>
/// <b>Same-file check:</b> the two full paths are compared ignoring case (Windows and macOS file systems usually ignore
/// case; on Linux this refuses a few valid pairs, which is the safe side). Links and short 8.3 names are not resolved;
/// even then the input is never damaged, because it is only read and the result is moved into place at the end.
/// </para>
/// <para>
/// Complexity: O(n) time for an n-byte file (plus the fixed PBKDF2 cost); O(1) memory — one 2 MiB pooled buffer,
/// whatever the file size.
/// </para>
/// </remarks>
public sealed class FileEncryptionService : IFileEncryptionService
{
    /// <summary>How many bytes are read, encrypted and written per step: 1 MiB, a whole number of AES blocks.</summary>
    internal const int ChunkLength = 1024 * 1024;

    private readonly Func<EncryptedFileHeader> _createHeader;

    /// <summary>Creates the service used by the app: a fresh random salt and IV and 600,000 iterations for every file.</summary>
    public FileEncryptionService()
        : this(static () => EncryptedFileHeader.CreateNew(EncryptedFileFormat.DefaultIterations))
    {
    }

    /// <summary>Creates the service with a custom way to make headers (tests: fewer iterations, or a fixed salt and IV).</summary>
    /// <param name="createHeader">Called once per file to make its header.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createHeader"/> is <see langword="null"/>.</exception>
    internal FileEncryptionService(Func<EncryptedFileHeader> createHeader)
    {
        ArgumentNullException.ThrowIfNull(createHeader);
        _createHeader = createHeader;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Not <see langword="async"/> on purpose: the arguments are checked at once and wrong ones throw at the call,
    /// not later when the task is awaited (ADR-13). The work itself is in <see cref="EncryptCoreAsync"/>.
    /// </remarks>
    public Task EncryptAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        (string fullInputPath, string fullOutputPath) = GetCheckedFullPaths(inputFilePath, outputFilePath, password, overwriteExisting);
        return EncryptCoreAsync(fullInputPath, fullOutputPath, password, overwriteExisting, progress, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Not <see langword="async"/> on purpose, like <see cref="EncryptAsync"/>: wrong arguments throw at the call. The
    /// work itself is in <see cref="DecryptCoreAsync"/>.
    /// </remarks>
    public Task DecryptAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        (string fullInputPath, string fullOutputPath) = GetCheckedFullPaths(inputFilePath, outputFilePath, password, overwriteExisting);
        return DecryptCoreAsync(fullInputPath, fullOutputPath, password, overwriteExisting, progress, cancellationToken);
    }

    /// <summary>
    /// Reads the first 41 bytes of an encrypted file and checks them as a header (§5.4 step 1).
    /// </summary>
    /// <param name="input">The encrypted file, at its start.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The checked header; the stream is left right after it, at the ciphertext.</returns>
    /// <exception cref="InvalidEncryptedFileException">The data is not a SecureFile header (see <see cref="EncryptedFileHeader.Read"/>).</exception>
    /// <remarks>
    /// <c>throwOnEndOfStream: false</c> and only the bytes actually read are passed on, so a 3-byte text file is
    /// reported as "not an encrypted file" instead of a disk error (Review Board 1.2, R1). Internal for the
    /// short-read test.
    /// </remarks>
    internal static async Task<EncryptedFileHeader> ReadHeaderAsync(Stream input, CancellationToken cancellationToken)
    {
        byte[] headerBytes = new byte[EncryptedFileFormat.HeaderLength];
        int bytesRead = await input.ReadAtLeastAsync(
            headerBytes, EncryptedFileFormat.HeaderLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        return EncryptedFileHeader.Read(headerBytes.AsSpan(0, bytesRead));
    }

    /// <summary>
    /// Decrypts the ciphertext and tag that follow the header in <paramref name="input"/> and writes the plaintext to
    /// <paramref name="output"/>, finishing the last block only after the tag matched (§5.4 steps 2–6).
    /// </summary>
    /// <param name="input">The encrypted file, positioned right after the header.</param>
    /// <param name="output">Where the plaintext is written (the temp file).</param>
    /// <param name="header">The file's checked header.</param>
    /// <param name="ciphertextLength">How many ciphertext bytes follow the header (checked: whole blocks, at least one).</param>
    /// <param name="password">The password.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when all plaintext is written.</returns>
    /// <exception cref="AuthenticationFailedException">The tag does not match: wrong password, or a changed file.</exception>
    /// <exception cref="EndOfStreamException">The file became shorter while it was read.</exception>
    /// <exception cref="CryptographicException">
    /// The tag matched but the padding is invalid: the file was made by another program that knows the password
    /// (cannot happen for files this app wrote).
    /// </exception>
    /// <remarks>Internal so a test can feed a stream that returns only a few bytes per read.</remarks>
    internal static async Task WriteDecryptedFileAsync(
        Stream input,
        Stream output,
        EncryptedFileHeader header,
        long ciphertextLength,
        string password,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using FileKeys keys = await Task.Run(() => KeyDerivation.DeriveKeys(password, header), cancellationToken).ConfigureAwait(false);
        using ICryptoTransform decryptor = AesCbc.CreateDecryptor(keys.EncryptionKey, header.Iv.Span);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, keys.AuthenticationKey);

        // Read accepted only values that WriteTo writes back unchanged, so these are exactly the 41 bytes in the file.
        byte[] headerBytes = new byte[EncryptedFileFormat.HeaderLength];
        header.WriteTo(headerBytes);
        hmac.AppendData(headerBytes);

        await DecryptContentAsync(input, output, decryptor, hmac, new PercentageProgress(progress, ciphertextLength), ciphertextLength, cancellationToken).ConfigureAwait(false);
        await VerifyTagAsync(input, hmac, cancellationToken).ConfigureAwait(false);

        await WriteLastBlockAsync(output, decryptor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks the arguments shared by <see cref="EncryptAsync"/> and <see cref="DecryptAsync"/> and makes the paths full.
    /// </summary>
    /// <param name="inputFilePath">The input path as given.</param>
    /// <param name="outputFilePath">The output path as given.</param>
    /// <param name="password">The password.</param>
    /// <param name="overwriteExisting">Whether an existing output file may be replaced.</param>
    /// <returns>The full input and output paths.</returns>
    /// <exception cref="ArgumentNullException">A path or the password is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A path or the password is empty, or the output path is unusable.</exception>
    /// <exception cref="IOException">The output file exists and may not be replaced.</exception>
    private static (string FullInputPath, string FullOutputPath) GetCheckedFullPaths(
        string inputFilePath, string outputFilePath, string password, bool overwriteExisting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        ArgumentException.ThrowIfNullOrEmpty(password);

        string fullInputPath = Path.GetFullPath(inputFilePath);
        string fullOutputPath = Path.GetFullPath(outputFilePath);
        ThrowIfOutputPathIsUnusable(fullInputPath, fullOutputPath, overwriteExisting);
        return (fullInputPath, fullOutputPath);
    }

    /// <summary>
    /// Encrypts everything <paramref name="input"/> holds and writes the complete encrypted file — header, ciphertext,
    /// tag (§5.1) — to <paramref name="output"/>.
    /// </summary>
    /// <param name="input">The plaintext, at its start. Any readable stream with a length (tests use a slow one).</param>
    /// <param name="output">Where the encrypted file is written.</param>
    /// <param name="header">This file's header (salt, IV, iterations).</param>
    /// <param name="password">The password.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when everything is written.</returns>
    /// <remarks>Internal so a test can feed a stream that returns only a few bytes per read (Review Board 1.5, F1).</remarks>
    internal static async Task WriteEncryptedFileAsync(
        Stream input,
        Stream output,
        EncryptedFileHeader header,
        string password,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        // A lambda cannot capture a span, so the string is passed; DeriveKeys turns it into bytes and wipes them.
        using FileKeys keys = await Task.Run(() => KeyDerivation.DeriveKeys(password, header), cancellationToken).ConfigureAwait(false);
        using ICryptoTransform encryptor = AesCbc.CreateEncryptor(keys.EncryptionKey, header.Iv.Span);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, keys.AuthenticationKey);

        byte[] headerBytes = new byte[EncryptedFileFormat.HeaderLength];
        header.WriteTo(headerBytes);
        await AuthenticateAndWriteAsync(output, hmac, headerBytes, cancellationToken).ConfigureAwait(false);

        await EncryptContentAsync(input, output, encryptor, hmac, new PercentageProgress(progress, input.Length), cancellationToken).ConfigureAwait(false);

        // The token reaches this write too, so a cancel after the last chunk still leaves no file.
        await output.WriteAsync(hmac.GetHashAndReset(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Rejects an output path that cannot work, before any slow work starts (fail fast).</summary>
    /// <param name="inputFilePath">The full input path.</param>
    /// <param name="outputFilePath">The full output path.</param>
    /// <param name="overwriteExisting">Whether an existing output file may be replaced.</param>
    /// <exception cref="ArgumentException">
    /// The output is a folder, has no file name, contains characters a file name cannot have, or is the input.
    /// </exception>
    /// <exception cref="IOException">The output file exists and may not be replaced.</exception>
    private static void ThrowIfOutputPathIsUnusable(string inputFilePath, string outputFilePath, bool overwriteExisting)
    {
        string fileName = Path.GetFileName(outputFilePath);

        // On Windows ':' is invalid in a name; "out.enc:x" would otherwise address a hidden NTFS data stream (Review Board 1.5, F4).
        if (fileName.Length == 0 || fileName.AsSpan().IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Directory.Exists(outputFilePath))
        {
            throw new ArgumentException("The output path must name a file, not a folder.", nameof(outputFilePath));
        }

        if (string.Equals(inputFilePath, outputFilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The output file must be different from the input file.", nameof(outputFilePath));
        }

        // The real guard is the final move, which refuses to overwrite; this only saves the user the wait.
        if (!overwriteExisting && File.Exists(outputFilePath))
        {
            throw new IOException("The output file already exists.");
        }
    }

    /// <summary>Opens the input, then writes the encrypted file through a temp file (ADR-07).</summary>
    /// <param name="inputFilePath">Full input path.</param>
    /// <param name="outputFilePath">Full output path.</param>
    /// <param name="password">The password (already checked as not empty).</param>
    /// <param name="overwriteExisting">Whether the final move may replace an existing file.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the output file is in place.</returns>
    /// <remarks>
    /// The input is opened first and the temp file second, both <b>before</b> the slow key derivation, so a missing
    /// file or a read-only folder is reported at once (design review 1.5, F6).
    /// </remarks>
    private async Task EncryptCoreAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        FileStream input = OpenInputFile(inputFilePath);
        await using (input.ConfigureAwait(false))
        {
            EncryptedFileHeader header = _createHeader();
            await AtomicFileWriter.WriteAsync(
                outputFilePath,
                overwriteExisting,
                isSensitive: false, // ciphertext only
                output => WriteEncryptedFileAsync(input, output, header, password, progress, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>Opens the encrypted input, checks its header and length, then decrypts it through a hidden temp file (ADR-07).</summary>
    /// <param name="inputFilePath">Full input path.</param>
    /// <param name="outputFilePath">Full output path.</param>
    /// <param name="password">The password (already checked as not empty).</param>
    /// <param name="overwriteExisting">Whether the final move may replace an existing file.</param>
    /// <param name="progress">Optional progress receiver.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the decrypted file is in place.</returns>
    /// <remarks>
    /// The header is checked <b>before</b> the length, so a short text file is "not an encrypted file" rather than
    /// "damaged"; and both are checked before the temp file and the slow key derivation, so a wrong file is reported at
    /// once and nothing is created.
    /// </remarks>
    private static async Task DecryptCoreAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        FileStream input = OpenInputFile(inputFilePath);
        await using (input.ConfigureAwait(false))
        {
            EncryptedFileHeader header = await ReadHeaderAsync(input, cancellationToken).ConfigureAwait(false);
            long ciphertextLength = EncryptedFileFormat.GetCiphertextLength(input.Length);
            await AtomicFileWriter.WriteAsync(
                outputFilePath,
                overwriteExisting,
                isSensitive: true, // not-yet-verified plaintext: hidden on Windows, owner-only on Linux/macOS
                output => WriteDecryptedFileAsync(input, output, header, ciphertextLength, password, progress, cancellationToken)).ConfigureAwait(false);
        }
    }

    /// <summary>Opens a file for reading in large sequential chunks.</summary>
    /// <param name="inputFilePath">Full input path.</param>
    /// <returns>The open stream; the caller disposes it.</returns>
    private static FileStream OpenInputFile(string inputFilePath)
    {
        // FileShare.Read: others may read the file meanwhile, but nobody may change it while we work on it.
        // Stryker disable once Initializer,Bitwise : the options only tune speed (no extra buffer, sequential-read hint); no test can observe them
        return new FileStream(inputFilePath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = 0, // we read 1 MiB at a time into our own buffer; a second buffer only copies
        });
    }

    /// <summary>
    /// The decrypt loop: read exactly the ciphertext in chunks, add each to the HMAC, decrypt its blocks (the decryptor
    /// holds back the last one), write the plaintext, report progress.
    /// </summary>
    /// <param name="input">The encrypted file, at the ciphertext.</param>
    /// <param name="output">The temp file.</param>
    /// <param name="decryptor">The AES-256-CBC decryptor for this file.</param>
    /// <param name="hmac">The running HMAC (already holds the header).</param>
    /// <param name="progress">Turns processed ciphertext bytes into percentages.</param>
    /// <param name="ciphertextLength">How many ciphertext bytes to read.</param>
    /// <param name="cancellationToken">Passed to every read and write.</param>
    /// <returns>A task that completes when all ciphertext is processed (the last block still held back).</returns>
    /// <remarks>
    /// The length is known from the header check, so each read asks for an exact amount (<c>ReadExactlyAsync</c>): a
    /// short read is waited out, and a file that shrank meanwhile throws <see cref="EndOfStreamException"/>. The HMAC
    /// gets the ciphertext <b>before</b> it is decrypted: encrypt-then-MAC authenticates what is stored.
    /// </remarks>
    private static async Task DecryptContentAsync(
        Stream input,
        Stream output,
        ICryptoTransform decryptor,
        IncrementalHash hmac,
        PercentageProgress progress,
        long ciphertextLength,
        CancellationToken cancellationToken)
    {
        // One pooled buffer: the first half receives the ciphertext, the second half the plaintext.
        byte[] buffer = ArrayPool<byte>.Shared.Rent(2 * ChunkLength);
        try
        {
            for (long remaining = ciphertextLength; remaining > 0; remaining -= ChunkLength)
            {
                int chunkLength = (int)Math.Min(ChunkLength, remaining);
                await input.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken).ConfigureAwait(false);
                hmac.AppendData(buffer, 0, chunkLength);
                int plaintextLength = decryptor.TransformBlock(buffer, 0, chunkLength, buffer, ChunkLength);
                await output.WriteAsync(buffer.AsMemory(ChunkLength, plaintextLength), cancellationToken).ConfigureAwait(false);
                progress.Advance(chunkLength);
            }
        }
        // Stryker disable once Block : returning and clearing a pooled buffer is not observable by a test; checked by review (G9)
        finally
        {
            // The buffer held the user's plaintext: clear it before the next borrower gets it.
            // Stryker disable once Statement,Boolean : returning and clearing a pooled buffer is not observable by a test; checked by review (G9)
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Decrypts the held-back last block, which checks and removes the padding, and writes it. Call it only after the
    /// tag matched (ADR-06), so the padding of an unverified file is never examined.
    /// </summary>
    /// <param name="output">The temp file.</param>
    /// <param name="decryptor">The decryptor holding the last block.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the last plaintext bytes are written.</returns>
    /// <exception cref="CryptographicException">The padding is invalid (only possible for a file another program wrote).</exception>
    private static async Task WriteLastBlockAsync(Stream output, ICryptoTransform decryptor, CancellationToken cancellationToken)
    {
        byte[] lastPlaintext = decryptor.TransformFinalBlock([], 0, 0);
        try
        {
            await output.WriteAsync(lastPlaintext, cancellationToken).ConfigureAwait(false);
        }
        // Stryker disable once Block : wiping a temporary is not observable by a test; checked by review (G9)
        finally
        {
            // Stryker disable once Statement : wiping a temporary is not observable by a test; checked by review (G9)
            CryptographicOperations.ZeroMemory(lastPlaintext);
        }
    }

    /// <summary>Reads the stored tag and compares it in constant time with the computed one.</summary>
    /// <param name="input">The encrypted file, at the tag.</param>
    /// <param name="hmac">The HMAC over the header and all ciphertext.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the tag matched.</returns>
    /// <exception cref="AuthenticationFailedException">The tags differ.</exception>
    /// <remarks>
    /// <see cref="CryptographicOperations.FixedTimeEquals"/> takes the same time wherever the first difference is, so
    /// timing cannot reveal how many leading bytes of a forged tag were right.
    /// </remarks>
    private static async Task VerifyTagAsync(Stream input, IncrementalHash hmac, CancellationToken cancellationToken)
    {
        byte[] storedTag = new byte[EncryptedFileFormat.AuthenticationTagLength];
        await input.ReadExactlyAsync(storedTag, cancellationToken).ConfigureAwait(false);

        if (!CryptographicOperations.FixedTimeEquals(hmac.GetHashAndReset(), storedTag))
        {
            throw new AuthenticationFailedException();
        }
    }

    /// <summary>The streaming loop: read a chunk, encrypt it, add it to the HMAC, write it, report progress.</summary>
    /// <param name="input">The plaintext stream.</param>
    /// <param name="output">The output stream.</param>
    /// <param name="encryptor">The AES-256-CBC encryptor for this file.</param>
    /// <param name="hmac">The running HMAC (already holds the header).</param>
    /// <param name="progress">Turns processed bytes into percentages.</param>
    /// <param name="cancellationToken">Passed to every read and write, so a cancel stops the next one.</param>
    /// <returns>A task that completes after the final, padded block is written.</returns>
    /// <remarks>
    /// <c>ReadAtLeastAsync</c> keeps reading until the chunk is full or the input ends, so a shorter result means the
    /// end of the input — even on a network drive that returns a few bytes per read. A file of exactly 1 MiB takes two
    /// rounds: one full chunk, then a 0-byte read whose final block is a whole block of padding.
    /// </remarks>
    private static async Task EncryptContentAsync(
        Stream input,
        Stream output,
        ICryptoTransform encryptor,
        IncrementalHash hmac,
        PercentageProgress progress,
        CancellationToken cancellationToken)
    {
        // One pooled buffer: the first half receives the plaintext, the second half the ciphertext.
        byte[] buffer = ArrayPool<byte>.Shared.Rent(2 * ChunkLength);
        try
        {
            int bytesRead;
            do
            {
                bytesRead = await input.ReadAtLeastAsync(
                    buffer.AsMemory(0, ChunkLength), ChunkLength, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                await EncryptOneChunkAsync(buffer, bytesRead, output, encryptor, hmac, cancellationToken).ConfigureAwait(false);
                progress.Advance(bytesRead);
            }
            while (bytesRead == ChunkLength);
        }
        // Stryker disable once Block : returning and clearing a pooled buffer is not observable by a test; checked by review (G9)
        finally
        {
            // The buffer held the user's data: clear it before the next borrower gets it.
            // Stryker disable once Statement,Boolean : returning and clearing a pooled buffer is not observable by a test; checked by review (G9)
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    /// <summary>
    /// Encrypts one chunk: its whole 16-byte blocks with <see cref="ICryptoTransform.TransformBlock"/>, and, for the
    /// last chunk, the remaining 0–15 bytes plus padding with <see cref="ICryptoTransform.TransformFinalBlock"/>.
    /// </summary>
    /// <param name="buffer">
    /// The shared buffer: the plaintext chunk is at offset 0, and the ciphertext is written at offset
    /// <see cref="ChunkLength"/>.
    /// </param>
    /// <param name="length">How many plaintext bytes the buffer holds; less than a full chunk means the last chunk.</param>
    /// <param name="output">The output stream.</param>
    /// <param name="encryptor">The encryptor; it carries the CBC chain from one chunk to the next.</param>
    /// <param name="hmac">The running HMAC.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the chunk's ciphertext is written.</returns>
    private static async Task EncryptOneChunkAsync(
        byte[] buffer,
        int length,
        Stream output,
        ICryptoTransform encryptor,
        IncrementalHash hmac,
        CancellationToken cancellationToken)
    {
        int wholeBlockLength = length - (length % EncryptedFileFormat.AesBlockLength);
        if (wholeBlockLength > 0) // TransformBlock refuses 0 bytes (ArgumentOutOfRangeException, pinned by the 0- and 1-byte tests)
        {
            int ciphertextLength = encryptor.TransformBlock(buffer, 0, wholeBlockLength, buffer, ChunkLength);
            await AuthenticateAndWriteAsync(output, hmac, buffer.AsMemory(ChunkLength, ciphertextLength), cancellationToken).ConfigureAwait(false);
        }

        if (length < ChunkLength)
        {
            byte[] finalBlock = encryptor.TransformFinalBlock(buffer, wholeBlockLength, length - wholeBlockLength);
            await AuthenticateAndWriteAsync(output, hmac, finalBlock, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Adds data to the HMAC, then writes it: every byte written before the tag is authenticated.</summary>
    /// <param name="output">Where to write.</param>
    /// <param name="hmac">The running HMAC.</param>
    /// <param name="data">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    private static ValueTask AuthenticateAndWriteAsync(
        Stream output, IncrementalHash hmac, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        hmac.AppendData(data.Span);
        return output.WriteAsync(data, cancellationToken);
    }
}

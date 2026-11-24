namespace SecureFile.Core.Cryptography;

/// <summary>
/// Encrypts files with a password into the SecureFile format (PROJECT_PLAN §5.1) and decrypts them again.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an interface:</b> the ViewModels depend on this abstraction, so their tests can use a fake that finishes
/// instantly or fails on purpose (Dependency Inversion). It is also the <b>Strategy</b> seam for a possible format v2
/// (AES-GCM): a new class behind the same interface, without editing v1.
/// </para>
/// </remarks>
public interface IFileEncryptionService
{
    /// <summary>
    /// Encrypts a file with AES-256-CBC and seals it with HMAC-SHA256, writing the result to a new file.
    /// </summary>
    /// <param name="inputFilePath">The file to encrypt. It is only read, never changed.</param>
    /// <param name="outputFilePath">Where to write the encrypted file (normally the input name + <c>.enc</c>). Must differ from the input.</param>
    /// <param name="password">The password; must not be empty. Spaces count as characters.</param>
    /// <param name="overwriteExisting">
    /// <see langword="true"/> to replace an existing output file; pass it only after the user confirmed.
    /// </param>
    /// <param name="progress">
    /// Optional: receives the whole percentage done (0–100) each time it goes up — at most 101 reports for any file.
    /// 100 means all data is encrypted; the task completing means the file is in place (show "Finishing…" between).
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the operation; no output file is left behind. The key derivation cannot be interrupted, so a cancel during
    /// it takes effect when it ends (about 0.5 s; up to about 8 s for a crafted file with 10,000,000 iterations).
    /// </param>
    /// <returns>A task that completes when the encrypted file is in place under <paramref name="outputFilePath"/>.</returns>
    /// <exception cref="ArgumentNullException">A path or the password is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, the output names a folder or contains characters a file name cannot have, both paths name the
    /// same file, or the password is empty. A password with invalid Unicode (a lone surrogate, which no keyboard can
    /// produce) also gives an <see cref="ArgumentException"/>, but only when the task is awaited.
    /// </exception>
    /// <exception cref="FileNotFoundException">The input file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The input file's folder or the output folder does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Reading the input or writing to the output folder is not allowed, the input is a folder, or the existing output
    /// file is read-only.
    /// </exception>
    /// <exception cref="NotSupportedException">The input is a device or a pipe (for example <c>CON</c>), not a regular file.</exception>
    /// <exception cref="PathTooLongException">A path is too long for the operating system.</exception>
    /// <exception cref="IOException">
    /// The output file already exists and <paramref name="overwriteExisting"/> is <see langword="false"/>, a file is in
    /// use by another program, the disk is full, or another disk error happened.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task EncryptAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks an encrypted file's authentication tag and decrypts it to a new file. The output appears only if the
    /// password is right and the file is unchanged.
    /// </summary>
    /// <param name="inputFilePath">The encrypted file. It is only read, never changed.</param>
    /// <param name="outputFilePath">Where to write the decrypted file (normally the input name without <c>.enc</c>). Must differ from the input.</param>
    /// <param name="password">The password used to encrypt the file; must not be empty.</param>
    /// <param name="overwriteExisting">
    /// <see langword="true"/> to replace an existing output file; pass it only after the user confirmed.
    /// </param>
    /// <param name="progress">
    /// Optional: receives the whole percentage done (0–100) each time it goes up. 100 means all data is read; the tag
    /// check, the last block and the move follow (show "Finishing…" between).
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels the operation; no output file is left behind. The key derivation cannot be interrupted, so a cancel during
    /// it takes effect when it ends (about 0.5 s; up to about 8 s for a crafted file with 10,000,000 iterations).
    /// </param>
    /// <returns>A task that completes when the decrypted file is in place under <paramref name="outputFilePath"/>.</returns>
    /// <exception cref="ArgumentNullException">A path or the password is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, the output names a folder or contains characters a file name cannot have, both paths name the
    /// same file, or the password is empty. A password with invalid Unicode (a lone surrogate, which no keyboard can
    /// produce) also gives an <see cref="ArgumentException"/>, but only when the task is awaited.
    /// </exception>
    /// <exception cref="InvalidEncryptedFileException">
    /// The input is not a SecureFile file, was made by a newer version, or is damaged or cut short
    /// (<see cref="InvalidEncryptedFileException.Reason"/>). Thrown before any output is created.
    /// </exception>
    /// <exception cref="AuthenticationFailedException">
    /// The password is wrong, or the file was changed or damaged after it was encrypted.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The tag matched but the padding is invalid: the file was written by another program that knows the password.
    /// </exception>
    /// <exception cref="FileNotFoundException">The input file does not exist.</exception>
    /// <exception cref="DirectoryNotFoundException">The input file's folder or the output folder does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Reading the input or writing to the output folder is not allowed, the input is a folder, or the existing output
    /// file is read-only.
    /// </exception>
    /// <exception cref="NotSupportedException">The input is a device or a pipe, not a regular file.</exception>
    /// <exception cref="PathTooLongException">A path is too long for the operating system.</exception>
    /// <exception cref="IOException">
    /// The output file already exists and <paramref name="overwriteExisting"/> is <see langword="false"/>, a file is in
    /// use, the input became shorter while it was read (<see cref="EndOfStreamException"/>), the disk is full, or
    /// another disk error happened.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task DecryptAsync(
        string inputFilePath,
        string outputFilePath,
        string password,
        bool overwriteExisting,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}

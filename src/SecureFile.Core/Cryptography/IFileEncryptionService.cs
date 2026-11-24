namespace SecureFile.Core.Cryptography;

/// <summary>
/// Encrypts files with a password into the SecureFile format (PROJECT_PLAN §5.1). Decryption is added in Step 1.6.
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
    /// <param name="cancellationToken">Cancels the operation; no output file is left behind.</param>
    /// <returns>A task that completes when the encrypted file is in place under <paramref name="outputFilePath"/>.</returns>
    /// <exception cref="ArgumentNullException">A path or the password is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A path is empty, the output names a folder or contains characters a file name cannot have, both paths name the
    /// same file, or the password is empty or contains invalid Unicode (a lone surrogate).
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
}

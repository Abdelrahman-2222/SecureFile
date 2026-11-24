using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace SecureFile.Core.Files;

/// <summary>
/// Writes a file so that it appears complete or not at all: the content goes into a temp file in the destination
/// folder, is flushed to the disk, and only then is renamed to the final name (ADR-07). On any error or cancel the
/// temp file is deleted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the temp file is in the destination folder and not in <c>%TEMP%</c>:</b> a move inside one folder is a
/// rename, which the file system does in one step (atomic). A move to another drive is a copy plus a delete, so a crash
/// in the middle could leave half a file under the final name.
/// </para>
/// <para>
/// <b>Why flush to the disk before the move:</b> without it, the rename can reach the disk before the data does, and a
/// power cut could leave a file with the final name but empty content.
/// </para>
/// <para>
/// Used by encryption (Step 1.5) and decryption (Step 1.6), so both share one tested implementation (DRY).
/// </para>
/// <para>Complexity: O(1) extra time and space on top of the caller's own writing.</para>
/// </remarks>
internal static class AtomicFileWriter
{
    /// <summary>The ending of every temp file, so a leftover after a crash is easy to recognise.</summary>
    internal const string TempFileSuffix = ".securefile-tmp";

    /// <summary>The start of every temp file name: a dot hides the file on Linux and macOS; "~" marks it as temporary.</summary>
    internal const string TempFilePrefix = ".~";

    /// <summary>How many random hexadecimal characters make each temp name unique.</summary>
    private const int RandomPartLength = 8;

    /// <summary>
    /// How many characters of the output name the temp name keeps. Most file systems allow 255 characters per name,
    /// and the temp name adds 26, so a long (but valid) output name must be shortened (design review 1.5, F11).
    /// </summary>
    internal const int MaximumNamePartLength = 100;

    /// <summary>
    /// Creates a temp file next to <paramref name="outputFilePath"/>, lets <paramref name="writeContent"/> fill it,
    /// flushes it to the disk and moves it to <paramref name="outputFilePath"/>.
    /// </summary>
    /// <param name="outputFilePath">The full path of the final file.</param>
    /// <param name="overwriteExisting"><see langword="true"/> to replace an existing file (the user confirmed it).</param>
    /// <param name="writeContent">
    /// Writes the content into the stream it receives. If it throws, the temp file is deleted and the exception passes
    /// through unchanged.
    /// </param>
    /// <returns>A task that completes when the final file is in place.</returns>
    /// <exception cref="ArgumentException"><paramref name="outputFilePath"/> has no folder part (for example a drive root).</exception>
    /// <exception cref="DirectoryNotFoundException">The destination folder does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Writing to the destination folder is not allowed.</exception>
    /// <exception cref="IOException">
    /// The file already exists and <paramref name="overwriteExisting"/> is <see langword="false"/>, the disk is full,
    /// or another disk error happened.
    /// </exception>
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "Flush(flushToDisk: true) forces the data onto the disk (fsync). FlushAsync only empties the .NET buffer and has no to-disk version. It runs once per file.")]
    public static async Task WriteAsync(string outputFilePath, bool overwriteExisting, Func<Stream, Task> writeContent)
    {
        string tempFilePath = CreateTempFilePath(outputFilePath);

        // Created before the try: if creating fails, there is nothing of ours to delete, and a file with the same
        // name that belongs to someone else (FileMode.CreateNew refuses it) is never touched.
        FileStream tempFile = CreateTempFile(tempFilePath);

        bool isMoved = false;
        try
        {
            await using (tempFile.ConfigureAwait(false))
            {
                await writeContent(tempFile).ConfigureAwait(false);
                // Stryker disable once Statement,Boolean : only a power cut right after the move could show the difference; checked by review (G9)
                tempFile.Flush(flushToDisk: true);
            }

            File.Move(tempFilePath, outputFilePath, overwriteExisting);
            // Stryker disable once Boolean : without it, finally deletes a temp path that no longer exists (a no-op); not observable
            isMoved = true;
        }
        finally
        {
            if (!isMoved)
            {
                // Best effort: a failure to delete must not replace the real error (or the cancel) the user should see.
                _ = TryDeleteFile(tempFilePath);
            }
        }
    }

    /// <summary>Creates the empty temp file, refusing to open a file that already exists.</summary>
    /// <param name="tempFilePath">The new file's path.</param>
    /// <returns>The open stream; the caller disposes it.</returns>
    private static FileStream CreateTempFile(string tempFilePath)
    {
        return new FileStream(tempFilePath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            BufferSize = 0, // the callers write in large chunks from their own buffer; a second buffer only copies
        });
    }

    /// <summary>Builds the temp file path: <c>.~&lt;output name&gt;.&lt;8 random hex&gt;.securefile-tmp</c> in the output folder.</summary>
    /// <param name="outputFilePath">The full path of the final file.</param>
    /// <returns>The temp file path.</returns>
    /// <exception cref="ArgumentException"><paramref name="outputFilePath"/> has no folder part.</exception>
    /// <remarks>
    /// The random part lets two operations write to the same folder at once, and means a temp file left behind by a
    /// crash never blocks the next attempt.
    /// </remarks>
    internal static string CreateTempFilePath(string outputFilePath)
    {
        string folder = Path.GetDirectoryName(outputFilePath)
            ?? throw new ArgumentException("The output path must name a file inside a folder.", nameof(outputFilePath));
        string fileName = Path.GetFileName(outputFilePath);
        // Stryker disable once Equality : at exactly 100 characters, cutting to 100 changes nothing (equivalent mutant)
        if (fileName.Length > MaximumNamePartLength)
        {
            // Never cut between the two halves of an emoji or other surrogate pair (Review Board 1.5, F9).
            int cutLength = char.IsHighSurrogate(fileName[MaximumNamePartLength - 1]) ? MaximumNamePartLength - 1 : MaximumNamePartLength;
            fileName = fileName[..cutLength];
        }

        string randomPart = RandomNumberGenerator.GetHexString(RandomPartLength, lowercase: true);
        return Path.Combine(folder, $"{TempFilePrefix}{fileName}.{randomPart}{TempFileSuffix}");
    }

    /// <summary>Deletes a file if possible, without throwing.</summary>
    /// <param name="path">The file to delete. A missing file counts as deleted.</param>
    /// <returns><see langword="true"/> when the file is gone; <see langword="false"/> when it could not be deleted.</returns>
    /// <remarks>
    /// On Windows a virus scanner may still hold the just-closed file for a moment, so deleting can fail. The file then
    /// stays behind with its recognisable name; it is never a valid result because it was never moved into place.
    /// </remarks>
    internal static bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

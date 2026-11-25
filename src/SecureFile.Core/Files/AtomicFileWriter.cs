using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

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

    /// <summary>How many times deleting a temp file is tried before giving up (see <see cref="TryDeleteFile(string)"/>).</summary>
    private const int DeleteAttempts = 5;

    /// <summary>The pause before the second try; each further pause is this much longer (40, 80, 120, 160 ms).</summary>
    private static readonly TimeSpan DeleteRetryStep = TimeSpan.FromMilliseconds(40);

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
    /// <param name="isSensitive">
    /// <see langword="true"/> when the content is sensitive (decryption: not-yet-verified plaintext, ADR-07). On Windows
    /// the temp file then gets the <i>hidden</i> attribute while it is written, removed again before the move so the
    /// result is never hidden. On Linux and macOS (where the leading dot already hides it) it is created readable and
    /// writable by its owner only (0600); on Windows it gets an owner-only ACL instead of the folder's (backlog C16).
    /// The result keeps either: a decrypted file stays private to the user.
    /// </param>
    /// <param name="writeContent">
    /// Writes the content into the stream it receives. If it throws, the temp file is emptied and deleted, and the
    /// exception passes through unchanged.
    /// </param>
    /// <returns>A task that completes when the final file is in place.</returns>
    /// <exception cref="ArgumentException"><paramref name="outputFilePath"/> has no folder part (for example a drive root).</exception>
    /// <exception cref="DirectoryNotFoundException">The destination folder does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Writing to the destination folder is not allowed.</exception>
    /// <exception cref="IOException">
    /// The file already exists and <paramref name="overwriteExisting"/> is <see langword="false"/>, the disk is full,
    /// or another disk error happened.
    /// </exception>
    public static async Task WriteAsync(string outputFilePath, bool overwriteExisting, bool isSensitive, Func<Stream, Task> writeContent)
    {
        string tempFilePath = CreateTempFilePath(outputFilePath);

        // Created before the try: if creating fails, there is nothing of ours to delete, and a file with the same
        // name that belongs to someone else (FileMode.CreateNew refuses it) is never touched.
        FileStream tempFile = CreateTempFile(tempFilePath, isSensitive);

        bool isMoved = false;
        try
        {
            await using (tempFile.ConfigureAwait(false))
            {
                await FillAsync(tempFile, isSensitive && OperatingSystem.IsWindows(), writeContent).ConfigureAwait(false);
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

    /// <summary>
    /// Lets <paramref name="writeContent"/> fill the temp file and flushes it to the disk, hiding it meanwhile when asked.
    /// If anything fails, the file is emptied through the still-open handle.
    /// </summary>
    /// <param name="tempFile">The open temp file (the caller closes it).</param>
    /// <param name="hide">Whether to hide the file while it is written (decryption on Windows).</param>
    /// <param name="writeContent">Writes the content.</param>
    /// <returns>A task that completes when the file is complete on the disk.</returns>
    /// <remarks>
    /// <b>Why empty it (Review Board 1.6, M1):</b> after a failed decryption the temp file is mostly genuine plaintext.
    /// If the delete afterwards fails (a virus scanner may hold the just-closed file), an emptied file is all that stays.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "Flush(flushToDisk: true) forces the data onto the disk (fsync). FlushAsync only empties the .NET buffer and has no to-disk version. It runs once per file.")]
    private static async Task FillAsync(FileStream tempFile, bool hide, Func<Stream, Task> writeContent)
    {
        bool isFilled = false;
        try
        {
            if (hide)
            {
                SetHidden(tempFile, hidden: true);
            }

            await writeContent(tempFile).ConfigureAwait(false);
            // Stryker disable once Statement,Boolean : only a power cut right after the move could show the difference; checked by review (G9)
            tempFile.Flush(flushToDisk: true);

            // A move keeps the attributes, so the hidden mark is taken off before the file gets its final name.
            if (hide)
            {
                SetHidden(tempFile, hidden: false);
            }

            isFilled = true;
        }
        finally
        {
            if (!isFilled)
            {
                // Best effort, like the delete: it must not replace the real error.
                _ = TryEmpty(tempFile);
            }
        }
    }

    /// <summary>Cuts an open file to 0 bytes, without throwing.</summary>
    /// <param name="file">The open temp file.</param>
    /// <returns><see langword="true"/> when the file is now empty.</returns>
    private static bool TryEmpty(FileStream file)
    {
        try
        {
            file.SetLength(0);
            // Stryker disable once Boolean : the caller discards the result (like TryDeleteFile's in WriteAsync); equivalent mutant
            return true;
        }
        // Stryker disable once Block : the caller discards the result; reaching it needs SetLength to fail on a healthy file (backlog C15)
        catch (IOException)
        {
            // Stryker disable once Boolean : same reason as the block above
            return false;
        }
    }

    /// <summary>Adds or removes the <i>hidden</i> attribute through the open handle (no second lookup by path).</summary>
    /// <param name="file">The open temp file.</param>
    /// <param name="hidden">Whether the file should be hidden.</param>
    /// <remarks>Only called on Windows, and only for a temp file that is to be hidden.</remarks>
    private static void SetHidden(FileStream file, bool hidden)
    {
        FileAttributes attributes = File.GetAttributes(file.SafeFileHandle);
        File.SetAttributes(file.SafeFileHandle, hidden ? attributes | FileAttributes.Hidden : attributes & ~FileAttributes.Hidden);
    }

    /// <summary>Creates the empty temp file, refusing to open a file that already exists.</summary>
    /// <param name="tempFilePath">The new file's path.</param>
    /// <param name="isSensitive">Create it readable and writable by its owner only (Unix mode 0600, or an owner-only ACL on Windows).</param>
    /// <returns>The open stream; the caller disposes it.</returns>
    /// <remarks>
    /// On Linux and macOS the default (usually 0644) lets every local user read the file, and <see cref="FileShare.None"/>
    /// is only an advisory lock there (Review Board 1.6, M2). On Windows the file would inherit the folder's ACL, which in
    /// a shared folder lets other accounts read the plaintext (backlog C16), so it gets its own ACL instead.
    /// </remarks>
    private static FileStream CreateTempFile(string tempFilePath, bool isSensitive)
    {
        if (isSensitive && OperatingSystem.IsWindows())
        {
            return CreateOwnerOnlyWindowsFile(tempFilePath);
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
            BufferSize = 0, // the callers write in large chunks from their own buffer; a second buffer only copies
        };

        // Stryker disable once all : only observable on Linux and macOS, where CI checks it (AtomicFileWriterTests)
        if (isSensitive && !OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(tempFilePath, options);
    }

    /// <summary>
    /// Creates the temp file on Windows with an ACL that grants full control to the current user only and inherits
    /// nothing from the folder (backlog C16).
    /// </summary>
    /// <param name="tempFilePath">The new file's path.</param>
    /// <returns>The open stream; the caller disposes it.</returns>
    /// <remarks>
    /// <para>
    /// The ACL is part of the create call, so there is no moment in which the file exists with the folder's rights.
    /// A rename inside one volume keeps the file's own ACL, so the result stays owner-only, like 0600 on Unix.
    /// </para>
    /// <para>
    /// Administrators can still take ownership, and backup or antivirus software running as SYSTEM reads files through
    /// its backup privilege, not through the ACL; those are outside the threat model (THREAT_MODEL §4).
    /// </para>
    /// </remarks>
    [SupportedOSPlatform("windows")]
    private static FileStream CreateOwnerOnlyWindowsFile(string tempFilePath)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(tempFilePath).Create(
            FileMode.CreateNew,
            FileSystemRights.Write | FileSystemRights.ReadAttributes,
            FileShare.None,
            bufferSize: 1, // this overload refuses 0; FileStream treats 1 as "no buffer" too
            FileOptions.Asynchronous,
            security);
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
    /// On Windows a virus scanner may still hold the just-closed file for a moment, so deleting can fail. It is tried up to
    /// <c>DeleteAttempts</c> times with growing pauses (about 0.4 seconds in all, only when it fails), which is normally
    /// enough for the scanner to let go. If it still fails, the file stays behind with its recognisable name; it is never
    /// a valid result because it was never moved into place.
    /// </remarks>
    internal static bool TryDeleteFile(string path)
    {
        return TryDeleteFile(path, TryDeleteOnce, Thread.Sleep);
    }

    /// <summary>The retry loop of <see cref="TryDeleteFile(string)"/>, with the delete and the pause passed in.</summary>
    /// <param name="path">The file to delete.</param>
    /// <param name="tryDeleteOnce">One delete attempt; <see langword="true"/> when the file is gone.</param>
    /// <param name="pause">Waits for the given time between attempts.</param>
    /// <returns><see langword="true"/> when an attempt succeeded.</returns>
    /// <remarks>
    /// Separate so a test can count the attempts and pauses exactly, instead of racing a real lock (backlog C15).
    /// </remarks>
    internal static bool TryDeleteFile(string path, Func<string, bool> tryDeleteOnce, Action<TimeSpan> pause)
    {
        for (int attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            if (tryDeleteOnce(path))
            {
                return true;
            }

            if (attempt < DeleteAttempts)
            {
                // Only on the failure path, and a few tenths of a second at most: a scanner usually lets go within that.
                pause(DeleteRetryStep * attempt);
            }
        }

        return false;
    }

    /// <summary>One try to delete a file, without throwing.</summary>
    /// <param name="path">The file to delete. A missing file counts as deleted.</param>
    /// <returns><see langword="true"/> when the file is gone.</returns>
    /// <remarks>Internal so a test can pair the real delete with a pause that releases a lock, without timing races.</remarks>
    internal static bool TryDeleteOnce(string path)
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

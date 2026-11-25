using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Files;

/// <summary>
/// Reads file details from the real file system using <see cref="FileInfo"/>.
/// </summary>
/// <remarks>
/// Complexity: O(p) time and space, where p is the length of the path (path normalisation), plus one metadata
/// request to the operating system (two when the path is a symbolic link). Independent of the file size.
/// </remarks>
public sealed class FileDetailsReader : IFileDetailsReader
{
    /// <inheritdoc/>
    public FileDetails Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FileInfo selectedFile = new(filePath);

        // FileInfo.Exists is false for folders as well as for missing files; a folder gets its own type, so the window
        // can say "this is a folder" instead of "can't be found" (review item 12).
        if (!selectedFile.Exists)
        {
            throw Directory.Exists(filePath)
                ? new PathIsFolderException(PathIsFolderException.DefaultMessage, filePath)
                : new FileNotFoundException("No file exists at the given path.", filePath);
        }

        // For a symbolic link, the size and change time must come from the file it points to
        // (on Windows the link itself reports 0 bytes). The name stays the one the user selected.
        FileInfo contentFile = ResolveFinalTarget(selectedFile, filePath);

        return new FileDetails(
            FullPath: selectedFile.FullName,
            Name: selectedFile.Name,
            Extension: selectedFile.Extension,
            SizeInBytes: contentFile.Length,
            LastModifiedUtc: contentFile.LastWriteTimeUtc,
            IsEncrypted: StartsWithSignature(contentFile));
    }

    /// <summary>Reads the first bytes of the file and checks for the SecureFile signature.</summary>
    /// <param name="contentFile">The file that holds the content.</param>
    /// <returns>
    /// <see langword="true"/> when the file starts with the signature; <see langword="false"/> when it does not, or
    /// when the start cannot be read (the file is locked or not readable: the real operation then reports why).
    /// </returns>
    /// <remarks>
    /// Only <see cref="EncryptedFileFormat.SignatureLength"/> bytes are read, so this is independent of the file size.
    /// Sharing is permissive so that a file another program has open for reading or writing can still be recognised.
    /// </remarks>
    private static bool StartsWithSignature(FileInfo contentFile)
    {
        try
        {
            using FileStream stream = new(contentFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> start = stackalloc byte[EncryptedFileFormat.SignatureLength];
            // Stryker disable once Boolean : equivalent mutant - EndOfStreamException is an IOException, caught below with the same result (false)
            int read = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
            return EncryptedFileFormat.HasSignature(start[..read]);
        }
        catch (IOException)
        {
            return false;
        }
        // Stryker disable once Block : needs a file the user may list but not read; no portable test can create one (the same branch is covered for IOException)
        catch (UnauthorizedAccessException)
        {
            // Stryker disable once Boolean : same reason as the block above
            return false;
        }
    }

    /// <summary>
    /// Returns the file that actually holds the content: the file itself, or the final target of a link chain.
    /// </summary>
    /// <param name="selectedFile">The file the user selected (already known to exist).</param>
    /// <param name="filePath">The original path, used in the error message.</param>
    /// <returns>The file whose size and change time describe the content.</returns>
    /// <exception cref="FileNotFoundException">The link points to a missing file or to a folder.</exception>
    private static FileInfo ResolveFinalTarget(FileInfo selectedFile, string filePath)
    {
        if (selectedFile.LinkTarget is null)
        {
            return selectedFile;
        }

        if (selectedFile.ResolveLinkTarget(returnFinalTarget: true) is not FileInfo target || !target.Exists)
        {
            throw new FileNotFoundException("The link points to a file that does not exist.", filePath);
        }

        return target;
    }
}

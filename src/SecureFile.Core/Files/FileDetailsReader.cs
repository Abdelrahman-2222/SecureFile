namespace SecureFile.Core.Files;

/// <summary>
/// Reads file details from the real file system using <see cref="FileInfo"/>.
/// </summary>
public sealed class FileDetailsReader : IFileDetailsReader
{
    /// <inheritdoc/>
    public FileDetails Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FileInfo fileInfo = new(filePath);

        // FileInfo.Exists is false for folders as well as for missing files,
        // so a folder path is rejected here too.
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("No file exists at the given path.", filePath);
        }

        return new FileDetails(
            FullPath: fileInfo.FullName,
            Name: fileInfo.Name,
            Extension: fileInfo.Extension,
            SizeInBytes: fileInfo.Length,
            LastModifiedUtc: fileInfo.LastWriteTimeUtc);
    }
}

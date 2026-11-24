namespace SecureFile.Core.Files;

/// <summary>
/// Reads the details (name, size, extension, last change) of a file on disk.
/// </summary>
/// <remarks>
/// This is an interface so that ViewModel tests can use a fake reader that returns prepared details
/// without touching the disk. The real implementation is <see cref="FileDetailsReader"/>.
/// </remarks>
public interface IFileDetailsReader
{
    /// <summary>
    /// Reads the details of the file at <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">The path of the file to describe. Relative paths are resolved from the current folder.</param>
    /// <returns>The details of the file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> is empty, contains only white space, or contains a null character.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// No file can be found at <paramref name="filePath"/>: it does not exist, it is a folder, the drive or network
    /// location is not available, the name contains characters the file system does not allow, or the file cannot
    /// be opened because access to it is denied (the file system reports all of these as "does not exist").
    /// </exception>
    /// <exception cref="PathTooLongException"><paramref name="filePath"/> is longer than the system allows.</exception>
    /// <exception cref="IOException">
    /// The file changed or became unavailable while its details were being read
    /// (for example it was deleted or the drive was removed at that moment).
    /// </exception>
    /// <remarks>
    /// <see cref="FileNotFoundException"/> and <see cref="PathTooLongException"/> both derive from
    /// <see cref="IOException"/>, so a caller can handle every file-system problem with one <c>catch (IOException)</c>.
    /// </remarks>
    FileDetails Read(string filePath);
}

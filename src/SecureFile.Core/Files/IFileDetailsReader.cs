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
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or contains only white space.</exception>
    /// <exception cref="FileNotFoundException">
    /// No file exists at <paramref name="filePath"/>. This is also thrown when the path points to a folder.
    /// </exception>
    FileDetails Read(string filePath);
}

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
    /// <returns>
    /// A <b>snapshot</b> of the file's details at the moment of the call. The file is only looked up, never opened,
    /// so the details can be out of date later and do not prove the file can be read.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> is empty, contains only white space, or contains a null character.
    /// </exception>
    /// <exception cref="PathIsFolderException">
    /// <paramref name="filePath"/> is a folder (a <see cref="FileNotFoundException"/>, so older callers still catch it).
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// No file can be found at <paramref name="filePath"/>: it does not exist, it is a link whose target
    /// is missing, its drive or network location is unavailable, its name contains characters the file system does not
    /// allow, or the folder that contains it cannot be accessed.
    /// </exception>
    /// <exception cref="PathTooLongException">
    /// <paramref name="filePath"/> is longer than the operating system allows (observed on Windows; other systems report
    /// <see cref="FileNotFoundException"/> instead).
    /// </exception>
    /// <exception cref="IOException">The path is a symbolic link whose chain is circular or too long to follow.</exception>
    /// <remarks>
    /// <para>
    /// Both exception types above derive from <see cref="IOException"/>, so a caller can handle every file-system
    /// problem with one <c>catch (IOException)</c>.
    /// </para>
    /// <para>
    /// The call asks the operating system for the file's metadata, which can take several seconds for an unavailable
    /// network location. User interface code must therefore call it off the UI thread (for example with <c>Task.Run</c>).
    /// </para>
    /// </remarks>
    FileDetails Read(string filePath);
}

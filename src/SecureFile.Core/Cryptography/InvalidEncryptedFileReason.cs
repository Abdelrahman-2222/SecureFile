namespace SecureFile.Core.Cryptography;

/// <summary>
/// Why a file could not be accepted as a SecureFile encrypted file. The user interface uses it to choose a message
/// that tells the user what to do next.
/// </summary>
public enum InvalidEncryptedFileReason
{
    /// <summary>
    /// The file does not start with the SecureFile signature: it was not made by SecureFile, or its start was
    /// destroyed. This is also the value when no specific reason is given.
    /// </summary>
    NotEncryptedFile = 0,

    /// <summary>
    /// The file was made by a newer version of SecureFile that uses a format this version does not know.
    /// The user can open it with an updated app.
    /// </summary>
    UnsupportedVersion = 1,

    /// <summary>
    /// The file starts like a SecureFile file, but a value in it is impossible or the file is cut short.
    /// The file was damaged or changed after it was encrypted.
    /// </summary>
    Damaged = 2,
}

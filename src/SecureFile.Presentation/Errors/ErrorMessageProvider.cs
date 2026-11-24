using SecureFile.Core.Cryptography;

namespace SecureFile.Presentation.Errors;

/// <summary>
/// Turns an expected exception into the fixed, plain-text message the user sees (ADR-13, Layer 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why fixed text and never <see cref="Exception.Message"/>:</b> the .NET message is technical, English-only and can
/// contain full paths with the user name. The technical details go to the <see cref="IErrorLog"/> instead. The messages
/// avoid operating-system names, because the same ViewModels run on macOS and Linux (bonus).
/// </para>
/// <para>
/// <b>Why one static class:</b> the mapping has no state, and keeping it in one place makes every message consistent and
/// testable without a window. Each method stays small on purpose (complexity limit, CA1502).
/// </para>
/// </remarks>
public static class ErrorMessageProvider
{
    /// <summary>The message for an exception this class does not know; the ViewModel never catches these (Layer 3 does).</summary>
    private const string UnexpectedMessage = "Something unexpected went wrong. Your files were not changed. Details were saved to the log.";

    /// <summary>Windows <c>ERROR_HANDLE_DISK_FULL</c> as an <see cref="int"/> HResult.</summary>
    private const int WindowsDiskFull = unchecked((int)0x80070070);

    /// <summary>Windows <c>ERROR_HANDLE_DISK_FULL</c> (the handle variant) as an <see cref="int"/> HResult.</summary>
    private const int WindowsHandleDiskFull = unchecked((int)0x80070027);

    /// <summary>Linux and macOS <c>ENOSPC</c>: no space left on the device.</summary>
    private const int NoSpaceErrno = 28;

    /// <summary>Windows <c>ERROR_FILE_EXISTS</c> as an <see cref="int"/> HResult.</summary>
    private const int WindowsFileExists = unchecked((int)0x80070050);

    /// <summary>Windows <c>ERROR_ALREADY_EXISTS</c> as an <see cref="int"/> HResult.</summary>
    private const int WindowsAlreadyExists = unchecked((int)0x800700B7);

    /// <summary>Linux and macOS <c>EEXIST</c>: the file exists.</summary>
    private const int ExistsErrno = 17;

    /// <summary>Windows <c>ERROR_SHARING_VIOLATION</c> as an <see cref="int"/> HResult.</summary>
    private const int WindowsSharingViolation = unchecked((int)0x80070020);

    /// <summary>Windows <c>ERROR_LOCK_VIOLATION</c> as an <see cref="int"/> HResult.</summary>
    private const int WindowsLockViolation = unchecked((int)0x80070021);

    /// <summary>Gets the message to show the user for an expected exception.</summary>
    /// <param name="exception">The exception that ended the user's action.</param>
    /// <returns>A fixed plain-text message; never the raw exception message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public static string GetMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => "Cancelled. No file was written.",
            AuthenticationFailedException => "Wrong password, or the file was changed or damaged.",
            InvalidEncryptedFileException invalidFile => GetInvalidFileMessage(invalidFile.Reason),
            _ => GetFileSystemMessage(exception),
        };
    }

    /// <summary>The message for a file that is not an acceptable SecureFile file, by reason.</summary>
    /// <param name="reason">Why the file was refused.</param>
    /// <returns>The message to show.</returns>
    private static string GetInvalidFileMessage(InvalidEncryptedFileReason reason)
    {
        return reason switch
        {
            InvalidEncryptedFileReason.UnsupportedVersion => "This file was made by a newer version of SecureFile. Update the app to open it.",
            InvalidEncryptedFileReason.Damaged => "This encrypted file is damaged or incomplete, so it can't be decrypted.",
            _ => "This isn't a file encrypted by SecureFile. Choose a file that ends in .enc.",
        };
    }

    /// <summary>The message for the file-system exceptions: missing, denied, unsupported, too long, or an I/O error.</summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>The message to show.</returns>
    /// <remarks>
    /// The order matters: <see cref="FileNotFoundException"/>, <see cref="DirectoryNotFoundException"/> and
    /// <see cref="PathTooLongException"/> derive from <see cref="IOException"/>, so they come before it.
    /// </remarks>
    private static string GetFileSystemMessage(Exception exception)
    {
        return exception switch
        {
            NotSupportedException => "This item can't be opened as a file. Choose a regular file.",
            FileNotFoundException or DirectoryNotFoundException => "The file or folder can't be found or opened.",
            UnauthorizedAccessException => "Access to this file or folder was denied. Choose another folder or check its permissions.",
            PathTooLongException => "The path is too long. Choose a shorter folder or name.",
            IOException ioException => GetIoMessage(ioException.HResult),
            _ => UnexpectedMessage,
        };
    }

    /// <summary>Splits an <see cref="IOException"/> by its error code into disk full, exists, in use, or any other.</summary>
    /// <param name="hResult">The exception's <see cref="Exception.HResult"/>.</param>
    /// <returns>The message to show.</returns>
    private static string GetIoMessage(int hResult)
    {
        if (hResult is WindowsDiskFull or WindowsHandleDiskFull or NoSpaceErrno)
        {
            return "There is not enough free space to save the file. Free up space or choose another drive.";
        }

        if (hResult is WindowsFileExists or WindowsAlreadyExists or ExistsErrno)
        {
            return "A file with this name already exists. Choose another name or confirm replacing it.";
        }

        if (hResult is WindowsSharingViolation or WindowsLockViolation)
        {
            return "The file is being used by another program. Close it and try again.";
        }

        return "The file couldn't be read or written. Check that the drive is still connected and try again.";
    }
}

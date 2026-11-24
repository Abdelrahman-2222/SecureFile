namespace SecureFile.Presentation.Dialogs;

/// <summary>Shows a file in the operating system's file manager (the "Open folder" button after success).</summary>
/// <remarks>
/// Its own small interface (Interface Segregation, ADR-09) because it is not a dialog and the Avalonia app will use a
/// different mechanism. It reports failure with a return value, not an exception: a file manager that cannot start is
/// not worth an error dialog, only a hint.
/// </remarks>
public interface IFolderOpener
{
    /// <summary>Opens the folder that contains a file, with the file selected when the system supports it.</summary>
    /// <param name="filePath">The full path of the file.</param>
    /// <returns><see langword="true"/> when the file manager was started; <see langword="false"/> otherwise.</returns>
    bool OpenContainingFolder(string filePath);
}

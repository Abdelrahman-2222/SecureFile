namespace SecureFile.Core.Files;

/// <summary>
/// The information about a selected file that the main window shows to the user
/// (task requirement R2: name, size and extension).
/// </summary>
/// <param name="FullPath">The complete path of the file, for example <c>C:\Reports\report.pdf</c>.</param>
/// <param name="Name">The file name with its extension, for example <c>report.pdf</c>.</param>
/// <param name="Extension">
/// The extension including the dot, for example <c>.pdf</c>; an empty string when the file has no extension.
/// </param>
/// <param name="SizeInBytes">The file size in bytes. Use <see cref="FileSizeFormatter"/> to display it.</param>
/// <param name="LastModifiedUtc">When the file was last changed, in Coordinated Universal Time (UTC).</param>
/// <remarks>
/// A <c>record</c> is a type made for holding data: two records with the same values are equal,
/// and the values cannot be changed after creation (immutable), so the details shown on screen
/// can never be modified by accident.
/// </remarks>
public sealed record FileDetails(
    string FullPath,
    string Name,
    string Extension,
    long SizeInBytes,
    DateTime LastModifiedUtc);

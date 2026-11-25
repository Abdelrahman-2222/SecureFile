using System.Globalization;
using SecureFile.Core.Files;

namespace SecureFile.Presentation.Main;

/// <summary>The file card's tooltip (review items 16 and 33).</summary>
/// <remarks>A second file for the same class, only to keep each file small.</remarks>
public sealed partial class MainViewModel
{
    /// <summary>
    /// Gets the file card's tooltip: the full name, the folder, the last-modified time and the exact size; or
    /// <see langword="null"/> (no tooltip) while no file is chosen.
    /// </summary>
    /// <remarks>
    /// The card shows one line that a long name cuts short, so the extension and size can disappear, and two files of
    /// the same name in different folders look the same. The tooltip shows what the card cannot (plan §11.1 (2)). The
    /// full folder is shown here on purpose: it is on the user's own screen, unlike the log, which never holds paths.
    /// </remarks>
    public string? FileCardToolTip => SelectedFile is null ? null : DescribeFile(SelectedFile, CultureInfo.CurrentCulture);

    /// <summary>Builds the four tooltip lines for a file.</summary>
    /// <param name="file">The chosen file.</param>
    /// <param name="culture">The culture for the date and the numbers.</param>
    /// <returns>The tooltip text.</returns>
    internal static string DescribeFile(FileDetails file, CultureInfo culture)
    {
        string folder = Path.GetDirectoryName(file.FullPath) ?? file.FullPath;
        return string.Create(
            culture,
            $"{file.Name}\nFolder: {folder}\nModified: {file.LastModifiedUtc.ToLocalTime():g}\nSize: {file.SizeInBytes:N0} bytes ({FileSizeFormatter.Format(file.SizeInBytes, culture)})");
    }
}

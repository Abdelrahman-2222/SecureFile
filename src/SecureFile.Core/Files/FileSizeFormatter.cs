namespace SecureFile.Core.Files;

/// <summary>
/// Turns a size in bytes into short, human-readable text such as <c>1.5 MB</c>.
/// </summary>
/// <remarks>
/// Uses binary units (1 KB = 1,024 bytes) with the labels KB, MB, GB…, the same convention as
/// Windows File Explorer, so the numbers match what Windows users see elsewhere.
/// </remarks>
public static class FileSizeFormatter
{
    /// <summary>How many of one unit make the next unit (1,024 bytes = 1 KB, 1,024 KB = 1 MB, ...).</summary>
    private const int BytesPerUnitStep = 1024;

    /// <summary>The largest number of decimal places shown, for example <c>1.25 MB</c>.</summary>
    private const int MaximumDecimalPlaces = 2;

    /// <summary>Unit labels from smallest to largest. EB (exabyte) is enough for the largest <see cref="long"/>.</summary>
    private static readonly string[] UnitNames = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB"];

    /// <summary>
    /// Formats <paramref name="sizeInBytes"/> as text with the largest fitting unit and at most two decimals.
    /// </summary>
    /// <param name="sizeInBytes">The size to format. Must be zero or more.</param>
    /// <param name="formatProvider">
    /// The culture rules for numbers (for example the decimal separator: <c>1.5</c> in English, <c>1,5</c> in German).
    /// Use <see cref="System.Globalization.CultureInfo.CurrentCulture"/> for text shown to the user.
    /// </param>
    /// <returns>Text such as <c>0 bytes</c>, <c>1 byte</c>, <c>1023 bytes</c>, <c>1.5 KB</c> or <c>4.2 GB</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sizeInBytes"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="formatProvider"/> is <see langword="null"/>.</exception>
    public static string Format(long sizeInBytes, IFormatProvider formatProvider)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeInBytes);
        ArgumentNullException.ThrowIfNull(formatProvider);

        if (sizeInBytes == 1)
        {
            return "1 byte";
        }

        double size = sizeInBytes;
        int unitIndex = 0;

        // Move to the next bigger unit while the ROUNDED number would still be 1,024 or more.
        // Checking the rounded value means 1,048,575 bytes shows as "1 MB" instead of "1024 KB".
        while (unitIndex < UnitNames.Length - 1
            && Math.Round(size, MaximumDecimalPlaces, MidpointRounding.AwayFromZero) >= BytesPerUnitStep)
        {
            size /= BytesPerUnitStep;
            unitIndex++;
        }

        // "0.##" = at most two decimals, and no trailing zeros ("1.5", not "1.50"; "2", not "2.00").
        return string.Create(formatProvider, $"{size:0.##} {UnitNames[unitIndex]}");
    }
}

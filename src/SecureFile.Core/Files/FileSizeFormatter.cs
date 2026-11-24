namespace SecureFile.Core.Files;

/// <summary>
/// Turns a size in bytes into short, human-readable text such as <c>1.5 MB</c>.
/// </summary>
/// <remarks>
/// <para>
/// Uses binary units (1 KB = 1,024 bytes) with the labels KB, MB, GB…, the convention Windows File Explorer uses,
/// and shows at most two decimals (Explorer rounds to three significant digits instead, so the last digit can differ).
/// </para>
/// <para>
/// Numbers follow the given culture; the unit words are English. If the user interface is translated, the unit
/// words move to the translation resources (recorded decision, Review Board Step 1.1).
/// </para>
/// <para>Complexity: O(1) time (at most six divisions) and O(1) space (only the result text is allocated).</para>
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
    /// <returns>Text such as <c>0 bytes</c>, <c>1 byte</c>, <c>1,023 bytes</c>, <c>1.5 KB</c> or <c>4.2 GB</c>.</returns>
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

        if (sizeInBytes < BytesPerUnitStep)
        {
            // Whole bytes, with the culture's thousands separator ("1,023 bytes" in English).
            return string.Create(formatProvider, $"{sizeInBytes:N0} {UnitNames[0]}");
        }

        (double size, int unitIndex) = ScaleToLargestFittingUnit(sizeInBytes);

        // "0.##" = at most two decimals, and no trailing zeros ("1.5", not "1.50"; "2", not "2.00").
        return string.Create(formatProvider, $"{size:0.##} {UnitNames[unitIndex]}");
    }

    /// <summary>
    /// Divides the size by 1,024 until the number, as it will be displayed, is below 1,024 (or the largest unit is reached).
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes; zero or more.</param>
    /// <returns>The scaled number and the index of its unit in <see cref="UnitNames"/>.</returns>
    /// <remarks>
    /// The check uses the ROUNDED number (two decimals, the same rounding the text uses), so 1,048,575 bytes
    /// becomes "1 MB" and never "1024 KB".
    /// </remarks>
    private static (double Size, int UnitIndex) ScaleToLargestFittingUnit(long sizeInBytes)
    {
        double size = sizeInBytes;
        int unitIndex = 0;
        int largestUnitIndex = UnitNames.Length - 1;

        while (unitIndex < largestUnitIndex && RoundForDisplay(size) >= BytesPerUnitStep)
        {
            size /= BytesPerUnitStep;
            unitIndex++;
        }

        return (size, unitIndex);
    }

    /// <summary>Rounds a number exactly the way the "0.##" text format will show it.</summary>
    /// <param name="value">The number to round.</param>
    /// <returns>The number rounded to two decimals, halves rounded away from zero.</returns>
    private static double RoundForDisplay(double value)
    {
        return Math.Round(value, MaximumDecimalPlaces, MidpointRounding.AwayFromZero);
    }
}

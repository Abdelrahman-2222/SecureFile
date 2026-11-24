using System.Globalization;
using CsCheck;
using SecureFile.Core.Files;

namespace SecureFile.Core.Tests.Files;

/// <summary>Tests for <see cref="FileSizeFormatter"/> (test catalog T-01).</summary>
public sealed class FileSizeFormatterTests
{
    /// <summary>Unit labels in order; the index is the power of 1,024 ("byte" and "bytes" are both index 0).</summary>
    private static readonly string[] UnitsInOrder = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB"];

    [Theory]
    [InlineData(0L, "0 bytes")]
    [InlineData(1L, "1 byte")]
    [InlineData(2L, "2 bytes")]
    [InlineData(1023L, "1,023 bytes")]                // thousands separator for whole bytes
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1_048_570L, "1023.99 KB")]            // largest value that still rounds below 1024 KB
    [InlineData(1_048_571L, "1 MB")]                  // first value whose rounded KB would be "1024" -> moves to MB
    [InlineData(1_048_575L, "1 MB")]
    [InlineData(1_048_576L, "1 MB")]
    [InlineData(1_572_864L, "1.5 MB")]
    [InlineData(1_073_741_824L, "1 GB")]
    [InlineData(1_610_612_736L, "1.5 GB")]
    [InlineData(5_368_709_120L, "5 GB")]              // larger than 4 GB (beyond the 32-bit limit)
    [InlineData(1_099_511_627_776L, "1 TB")]
    [InlineData(1_125_899_906_842_624L, "1 PB")]
    [InlineData(long.MaxValue, "8 EB")]               // the largest possible size
    public void Format_KnownSizes_ReturnsExpectedText(long sizeInBytes, string expectedText)
    {
        string text = FileSizeFormatter.Format(sizeInBytes, CultureInfo.InvariantCulture);

        Assert.Equal(expectedText, text); // the invariant culture uses ',' for thousands and '.' for decimals
    }

    [Theory]
    [InlineData("de-DE", 1536L, "1,5 KB")]
    [InlineData("de-DE", 1023L, "1.023 bytes")]
    [InlineData("en-US", 1023L, "1,023 bytes")]
    public void Format_SpecificCulture_UsesThatCulturesSeparators(string cultureName, long sizeInBytes, string expectedText)
    {
        string text = FileSizeFormatter.Format(sizeInBytes, CultureInfo.GetCultureInfo(cultureName));

        Assert.Equal(expectedText, text);
    }

    [Fact]
    public void Format_NegativeSize_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FileSizeFormatter.Format(-1L, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Format_NullFormatProvider_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => FileSizeFormatter.Format(1024L, null!));
    }

    /// <summary>
    /// Property-based test over 10,000 random sizes spread evenly across all magnitudes (bytes to EB): the text must
    /// convert back to the original size within the display rounding, use a number below 1024 (unless already EB),
    /// and never show a number below 1 for units above bytes.
    /// </summary>
    [Fact]
    public void Format_AnySize_TextConvertsBackToTheSizeWithinRounding()
    {
        // Pick a bit length first, then a value of that length, so small and large sizes are equally likely.
        Gen<long> sizesOfEveryMagnitude = Gen.Int[1, 63].SelectMany(bitLength =>
            Gen.Long[0L, bitLength == 63 ? long.MaxValue : (1L << bitLength) - 1]);

        sizesOfEveryMagnitude.Sample(sizeInBytes =>
        {
            string text = FileSizeFormatter.Format(sizeInBytes, CultureInfo.InvariantCulture);
            string[] parts = text.Split(' ');
            double shownNumber = double.Parse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture);
            int unitIndex = parts[1] == "byte" ? 0 : Array.IndexOf(UnitsInOrder, parts[1]);
            double exactNumber = sizeInBytes / Math.Pow(1024, unitIndex);

            Assert.True(unitIndex >= 0, $"'{text}' uses an unknown unit.");
            Assert.True(Math.Abs(shownNumber - exactNumber) <= 0.005 + (exactNumber * 1e-12), $"'{text}' does not match {sizeInBytes} bytes.");
            Assert.True(shownNumber < 1024 || unitIndex == UnitsInOrder.Length - 1, $"'{text}' should use a bigger unit.");
            Assert.True(unitIndex == 0 || shownNumber >= 1, $"'{text}' should use a smaller unit.");
        }, iter: 10_000);
    }
}

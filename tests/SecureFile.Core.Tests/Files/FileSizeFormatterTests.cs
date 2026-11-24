using System.Globalization;
using CsCheck;
using SecureFile.Core.Files;

namespace SecureFile.Core.Tests.Files;

/// <summary>Tests for <see cref="FileSizeFormatter"/> (test catalog T-01).</summary>
public sealed class FileSizeFormatterTests
{
    [Theory]
    [InlineData(0L, "0 bytes")]
    [InlineData(1L, "1 byte")]
    [InlineData(2L, "2 bytes")]
    [InlineData(1023L, "1023 bytes")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1_048_575L, "1 MB")]          // 1023.999 KB would round to "1024 KB"; must move up to MB
    [InlineData(1_048_576L, "1 MB")]
    [InlineData(1_572_864L, "1.5 MB")]
    [InlineData(1_073_741_824L, "1 GB")]
    [InlineData(5_368_709_120L, "5 GB")]      // larger than 4 GB (beyond the 32-bit limit)
    [InlineData(1_099_511_627_776L, "1 TB")]
    [InlineData(1_125_899_906_842_624L, "1 PB")]
    [InlineData(long.MaxValue, "8 EB")]       // the largest possible size
    public void Format_KnownSizes_ReturnsExpectedText(long sizeInBytes, string expectedText)
    {
        string text = FileSizeFormatter.Format(sizeInBytes, CultureInfo.InvariantCulture);

        Assert.Equal(expectedText, text);
    }

    [Fact]
    public void Format_GermanCulture_UsesCommaAsDecimalSeparator()
    {
        string text = FileSizeFormatter.Format(1536L, CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal("1,5 KB", text);
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
    /// Property-based test: for thousands of random sizes, the number shown is always below 1024
    /// (otherwise a bigger unit should have been used) and the text always ends with a known unit.
    /// </summary>
    [Fact]
    public void Format_AnyValidSize_ShowsNumberBelow1024WithKnownUnit()
    {
        string[] knownUnits = ["byte", "bytes", "KB", "MB", "GB", "TB", "PB", "EB"];

        Gen.Long[0L, long.MaxValue].Sample(sizeInBytes =>
        {
            string text = FileSizeFormatter.Format(sizeInBytes, CultureInfo.InvariantCulture);
            string[] parts = text.Split(' ');
            double number = double.Parse(parts[0], CultureInfo.InvariantCulture);

            Assert.True(number < 1024 || parts[1] == "EB", $"'{text}' should use a bigger unit.");
            Assert.Contains(parts[1], knownUnits);
        });
    }
}

using SecureFile.Core.Files;

namespace SecureFile.Core.Tests.Files;

/// <summary>
/// Tests for <see cref="FileDetailsReader"/> using real files in a temporary folder (test catalog T-01).
/// </summary>
/// <remarks>
/// xUnit creates a new instance of this class for every test, so every test gets its own empty folder,
/// and <see cref="Dispose"/> deletes it afterwards. Tests never share files.
/// </remarks>
public sealed class FileDetailsReaderTests : IDisposable
{
    private readonly DirectoryInfo _temporaryFolder = Directory.CreateTempSubdirectory("SecureFileTests_");
    private readonly FileDetailsReader _reader = new();

    public void Dispose()
    {
        _temporaryFolder.Delete(recursive: true);
    }

    [Fact]
    public void Read_ExistingFile_ReturnsNameExtensionAndSize()
    {
        string filePath = CreateFile("report.pdf", sizeInBytes: 1536);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal("report.pdf", details.Name);
        Assert.Equal(".pdf", details.Extension);
        Assert.Equal(1536L, details.SizeInBytes);
        Assert.Equal(Path.GetFullPath(filePath), details.FullPath);
    }

    [Fact]
    public void Read_EmptyFile_ReturnsZeroSize()
    {
        string filePath = CreateFile("empty.txt", sizeInBytes: 0);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(0L, details.SizeInBytes);
    }

    [Theory]
    [InlineData("README", "")]                    // no extension at all
    [InlineData("archive.tar.gz", ".gz")]         // only the LAST part counts as the extension
    [InlineData(".bashrc", ".bashrc")]            // .NET treats a leading-dot name as all extension
    [InlineData("PHOTO.JPG", ".JPG")]             // letter case is kept as it is on disk
    [InlineData("my report (final).docx", ".docx")]
    [InlineData("تقرير مالي.xlsx", ".xlsx")]      // Arabic name with a space
    public void Read_VariousFileNames_ReturnsExpectedExtension(string fileName, string expectedExtension)
    {
        string filePath = CreateFile(fileName, sizeInBytes: 10);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(fileName, details.Name);
        Assert.Equal(expectedExtension, details.Extension);
    }

    [Fact]
    public void Read_PathLongerThan260Characters_ReturnsDetails()
    {
        string deepFolder = Path.Combine(_temporaryFolder.FullName, new string('a', 100), new string('b', 100), new string('c', 100));
        Directory.CreateDirectory(deepFolder);
        string filePath = Path.Combine(deepFolder, "deep.txt");
        File.WriteAllBytes(filePath, new byte[5]);

        FileDetails details = _reader.Read(filePath);

        Assert.True(filePath.Length > 260);
        Assert.Equal(5L, details.SizeInBytes);
    }

    [Fact]
    public void Read_ExistingFile_ReturnsLastModifiedTimeInUtc()
    {
        string filePath = CreateFile("dated.txt", sizeInBytes: 1);
        DateTime knownTimeUtc = new(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(filePath, knownTimeUtc);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(knownTimeUtc, details.LastModifiedUtc);
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_temporaryFolder.FullName, "does-not-exist.txt");

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => _reader.Read(missingPath));

        Assert.Equal(missingPath, exception.FileName);
    }

    [Fact]
    public void Read_FolderInsteadOfFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _reader.Read(_temporaryFolder.FullName));
    }

    [Fact]
    public void Read_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _reader.Read(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_EmptyOrWhiteSpacePath_ThrowsArgumentException(string filePath)
    {
        Assert.Throws<ArgumentException>(() => _reader.Read(filePath));
    }

    /// <summary>Creates a file of the given size (filled with zeros) in this test's temporary folder.</summary>
    private string CreateFile(string fileName, int sizeInBytes)
    {
        string filePath = Path.Combine(_temporaryFolder.FullName, fileName);
        File.WriteAllBytes(filePath, new byte[sizeInBytes]);
        return filePath;
    }
}

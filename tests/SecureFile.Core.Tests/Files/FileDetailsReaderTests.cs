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

    [Theory]
    [InlineData("secret.enc", "SFEN-and-more", true)]  // our signature
    [InlineData("renamed.bak", "SFEN-and-more", true)] // a renamed encrypted file is still recognised: content, not name
    [InlineData("fake.enc", "not ours", false)]        // the name alone proves nothing
    [InlineData("tiny.bin", "SF", false)]              // shorter than the signature
    [InlineData("empty.bin", "", false)]
    public void Read_DecidesEncryptedByContentNotByName(string fileName, string content, bool expected)
    {
        string filePath = Path.Combine(_temporaryFolder.FullName, fileName);
        File.WriteAllText(filePath, content);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(expected, details.IsEncrypted);
    }

    [Fact]
    public void Read_FileOpenedExclusivelyByAnotherProgram_StillReadsDetailsAndSaysNotEncrypted()
    {
        string filePath = CreateFile("locked.bin", sizeInBytes: 10);
        using var holder = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(10L, details.SizeInBytes);
        Assert.False(details.IsEncrypted);
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
        Assert.Equal(DateTimeKind.Utc, details.LastModifiedUtc.Kind); // Equal() ignores Kind, so check it explicitly
    }

    [Fact]
    public void Read_RelativePath_ResolvesFromCurrentFolder()
    {
        string fileName = $"relative-{Guid.NewGuid():N}.txt";
        string expectedFullPath = Path.Combine(Environment.CurrentDirectory, fileName);
        File.WriteAllBytes(expectedFullPath, new byte[3]);
        try
        {
            FileDetails details = _reader.Read(fileName);

            Assert.Equal(expectedFullPath, details.FullPath);
            Assert.Equal(3L, details.SizeInBytes);
        }
        finally
        {
            File.Delete(expectedFullPath);
        }
    }

    [Fact]
    public void Read_FileLockedByAnotherProgram_StillReturnsDetails()
    {
        string filePath = CreateFile("locked.bin", sizeInBytes: 7);
        using FileStream exclusiveLock = new(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        FileDetails details = _reader.Read(filePath);

        Assert.Equal(7L, details.SizeInBytes);
    }

    [Theory]
    [InlineData(FileAttributes.ReadOnly)]
    [InlineData(FileAttributes.Hidden)]
    public void Read_FileWithSpecialAttribute_ReturnsDetails(FileAttributes attribute)
    {
        string filePath = CreateFile("special.txt", sizeInBytes: 4);
        File.SetAttributes(filePath, attribute);
        try
        {
            FileDetails details = _reader.Read(filePath);

            Assert.Equal(4L, details.SizeInBytes);
        }
        finally
        {
            File.SetAttributes(filePath, FileAttributes.Normal); // a read-only file could not be deleted afterwards
        }
    }

    [Fact]
    public void Read_SymbolicLinkToFile_ReturnsTargetSizeButLinkName()
    {
        string targetPath = CreateFile("target.dat", sizeInBytes: 2048);
        string linkPath = CreateSymbolicLinkOrSkip("shortcut.lnk.dat", targetPath);

        FileDetails details = _reader.Read(linkPath);

        Assert.Equal("shortcut.lnk.dat", details.Name);
        Assert.Equal(2048L, details.SizeInBytes);
    }

    [Fact]
    public void Read_ChainOfSymbolicLinks_ReturnsSizeOfFinalTarget()
    {
        // second-link -> first-link -> target: only following the chain to the END gives the real size.
        string targetPath = CreateFile("chain-target.dat", sizeInBytes: 4096);
        string firstLinkPath = CreateSymbolicLinkOrSkip("first-link.dat", targetPath);
        string secondLinkPath = CreateSymbolicLinkOrSkip("second-link.dat", firstLinkPath);

        FileDetails details = _reader.Read(secondLinkPath);

        Assert.Equal(4096L, details.SizeInBytes);
    }

    [Fact]
    public void Read_SymbolicLinkToMissingFile_ThrowsFileNotFoundException()
    {
        string missingTarget = Path.Combine(_temporaryFolder.FullName, "gone.dat");
        string linkPath = CreateSymbolicLinkOrSkip("broken-link.dat", missingTarget);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => _reader.Read(linkPath));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message), "The exception must explain the problem (it is logged).");
    }

    [Fact]
    public void Read_MissingFile_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_temporaryFolder.FullName, "does-not-exist.txt");

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => _reader.Read(missingPath));

        Assert.Equal(missingPath, exception.FileName);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), "The exception must explain the problem (it is logged).");
    }

    [Fact]
    public void Read_FolderInsteadOfFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _reader.Read(_temporaryFolder.FullName));
    }

    // The ParamName checks prove that OUR guard clause rejected the input (FileInfo would report "fileName"/"path").
    // This matters because on Linux and macOS "   " is a valid file name, so without the guard the behaviour
    // would differ between operating systems.
    [Fact]
    public void Read_NullPath_ThrowsArgumentNullExceptionNamingTheParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => _reader.Read(null!));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_EmptyOrWhiteSpacePath_ThrowsArgumentExceptionNamingTheParameter(string filePath)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => _reader.Read(filePath));

        Assert.Equal("filePath", exception.ParamName);
    }

    [Fact]
    public void Read_PathWithNullCharacter_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _reader.Read("file\0name.txt"));
    }

    [Theory]
    [InlineData("a<b>|?*.txt")]                   // invalid on Windows; on Linux/macOS a legal name that does not exist
    [InlineData("missing-folder/missing.txt")]    // folder that does not exist
    public void Read_InvalidOrUnreachablePath_ThrowsFileNotFoundException(string relativePath)
    {
        string path = Path.Combine(_temporaryFolder.FullName, relativePath);

        Assert.Throws<FileNotFoundException>(() => _reader.Read(path));
    }

    [Fact]
    public void Read_PathFarLongerThanSystemMaximum_ThrowsIOException()
    {
        // 40,000 characters: beyond every OS limit. Windows reports PathTooLongException, Linux and macOS may
        // report FileNotFoundException; both derive from IOException, which is what callers handle.
        string tooLongPath = Path.Combine(_temporaryFolder.FullName, new string('x', 40_000));

        Assert.ThrowsAny<IOException>(() => _reader.Read(tooLongPath));
    }

    /// <summary>
    /// Creates a symbolic link, or skips the test when this machine does not allow it
    /// (Windows needs Developer Mode or administrator rights to create symbolic links).
    /// </summary>
    private string CreateSymbolicLinkOrSkip(string linkName, string targetPath)
    {
        string linkPath = Path.Combine(_temporaryFolder.FullName, linkName);
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"Symbolic links cannot be created here: {exception.Message}");
        }

        return linkPath;
    }

    /// <summary>Creates a file of the given size (filled with zeros) in this test's temporary folder.</summary>
    private string CreateFile(string fileName, int sizeInBytes)
    {
        string filePath = Path.Combine(_temporaryFolder.FullName, fileName);
        File.WriteAllBytes(filePath, new byte[sizeInBytes]);
        return filePath;
    }
}

using SecureFile.Core.Files;

namespace SecureFile.Core.Tests.Files;

/// <summary>
/// Tests for <see cref="AtomicFileWriter"/>: the final file appears complete or not at all (ADR-07), and no temp file
/// is left behind after a failure (test catalog T-22).
/// </summary>
public sealed class AtomicFileWriterTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("SecureFileTests_");

    public void Dispose()
    {
        foreach (FileInfo file in _folder.GetFiles())
        {
            file.Attributes = FileAttributes.Normal; // a read-only test file would block the folder delete
        }

        _folder.Delete(recursive: true);
    }

    [Fact]
    public async Task WriteAsync_ContentWritten_AppearsUnderTheFinalNameWithNoTempFile()
    {
        string output = PathInFolder("result.enc");

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, stream => stream.WriteAsync(new byte[] { 1, 2, 3 }, Ct).AsTask());

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(output, Ct));
        Assert.Equal(["result.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task WriteAsync_WhileWriting_TheTempFileIsInTheOutputFolderAndTheOutputDoesNotExistYet()
    {
        string output = PathInFolder("result.enc");
        string[] namesWhileWriting = [];

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, _ =>
        {
            namesWhileWriting = FileNamesInFolder();
            return Task.CompletedTask;
        });

        string tempName = Assert.Single(namesWhileWriting);
        Assert.StartsWith(".~result.enc.", tempName, StringComparison.Ordinal);
        Assert.EndsWith(AtomicFileWriter.TempFileSuffix, tempName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_ContentWriterThrows_PassesTheExceptionOnAndLeavesNothing()
    {
        string output = PathInFolder("result.enc");

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1 }, Ct);
                throw new InvalidOperationException("writer failed");
            }));

        Assert.Equal("writer failed", thrown.Message);
        Assert.Empty(FileNamesInFolder());
    }

    [Fact]
    public async Task WriteAsync_ExistingFileWithoutOverwrite_ThrowsKeepsItAndDeletesTheTempFile()
    {
        string output = PathInFolder("result.enc");
        await File.WriteAllTextAsync(output, "keep", Ct);

        await Assert.ThrowsAsync<IOException>(
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, stream => stream.WriteAsync(new byte[] { 9 }, Ct).AsTask()));

        Assert.Equal("keep", await File.ReadAllTextAsync(output, Ct));
        Assert.Equal(["result.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task WriteAsync_ExistingFileWithOverwrite_ReplacesIt()
    {
        string output = PathInFolder("result.enc");
        await File.WriteAllTextAsync(output, "old", Ct);

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: true, stream => stream.WriteAsync(new byte[] { 9 }, Ct).AsTask());

        Assert.Equal([9], await File.ReadAllBytesAsync(output, Ct));
    }

    [Fact]
    public async Task WriteAsync_MissingFolder_ThrowsDirectoryNotFoundException()
    {
        string output = Path.Combine(_folder.FullName, "missing", "result.enc");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, _ => Task.CompletedTask));
    }

    [Fact]
    public void CreateTempFilePath_TwoCalls_GiveDifferentNamesInTheSameFolder()
    {
        string output = PathInFolder("report.pdf.enc");

        string first = AtomicFileWriter.CreateTempFilePath(output);
        string second = AtomicFileWriter.CreateTempFilePath(output);

        Assert.NotEqual(first, second);
        Assert.Equal(_folder.FullName, Path.GetDirectoryName(first));
        Assert.Matches(@"^\.~report\.pdf\.enc\.[0-9a-f]{8}\.securefile-tmp$", Path.GetFileName(first));
    }

    [Fact]
    public void CreateTempFilePath_LongName_KeepsOnlyTheFirst100Characters()
    {
        string longName = new('a', 200);

        string tempName = Path.GetFileName(AtomicFileWriter.CreateTempFilePath(PathInFolder(longName)));

        Assert.Equal(".~" + new string('a', AtomicFileWriter.MaximumNamePartLength) + ".", tempName[..103]);
        Assert.Equal(2 + 100 + 1 + 8 + AtomicFileWriter.TempFileSuffix.Length, tempName.Length);
    }

    [Fact]
    public void CreateTempFilePath_NameOfExactly100Characters_IsKeptWhole()
    {
        string name = new('b', AtomicFileWriter.MaximumNamePartLength);

        string tempName = Path.GetFileName(AtomicFileWriter.CreateTempFilePath(PathInFolder(name)));

        Assert.StartsWith(".~" + name + ".", tempName, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTempFilePath_CutWouldSplitAnEmoji_CutsBeforeIt()
    {
        string kept = new('c', AtomicFileWriter.MaximumNamePartLength - 1);
        string name = kept + "\U0001F600" + "tail"; // the emoji takes two chars, at positions 99 and 100

        string tempName = Path.GetFileName(AtomicFileWriter.CreateTempFilePath(PathInFolder(name)));

        Assert.StartsWith(".~" + kept + ".", tempName, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTempFilePath_DriveRoot_ThrowsArgumentException()
    {
        string root = Path.GetPathRoot(_folder.FullName)!;

        ArgumentException thrown = Assert.Throws<ArgumentException>("outputFilePath", () => AtomicFileWriter.CreateTempFilePath(root));
        Assert.StartsWith("The output path must name a file inside a folder.", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDeleteFile_ExistingFile_DeletesItAndReturnsTrue()
    {
        string path = PathInFolder("temp.bin");
        File.WriteAllText(path, "x");

        Assert.True(AtomicFileWriter.TryDeleteFile(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TryDeleteFile_PathInsideAFile_ReturnsFalseInsteadOfThrowing()
    {
        string file = PathInFolder("not-a-folder.txt");
        File.WriteAllText(file, "x");

        Assert.False(AtomicFileWriter.TryDeleteFile(Path.Combine(file, "child.bin"))); // an IOException inside
    }

    [Fact]
    public void TryDeleteFile_ReadOnlyFileOnWindows_ReturnsFalseInsteadOfThrowing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to delete a read-only file.");
        string path = PathInFolder("read-only.bin");
        File.WriteAllText(path, "x");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        Assert.False(AtomicFileWriter.TryDeleteFile(path)); // an UnauthorizedAccessException inside
        Assert.True(File.Exists(path));
    }

    /// <summary>The test run's token (xUnit1051).</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string PathInFolder(string name)
    {
        return Path.Combine(_folder.FullName, name);
    }

    private string[] FileNamesInFolder()
    {
        return [.. _folder.GetFiles().Select(file => file.Name).Order(StringComparer.Ordinal)];
    }
}

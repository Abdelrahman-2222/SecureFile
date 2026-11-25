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

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: false, stream => stream.WriteAsync(new byte[] { 1, 2, 3 }, Ct).AsTask());

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(output, Ct));
        Assert.Equal(["result.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task WriteAsync_WhileWriting_TheTempFileIsInTheOutputFolderAndTheOutputDoesNotExistYet()
    {
        string output = PathInFolder("result.enc");
        string[] namesWhileWriting = [];

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: false, _ =>
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
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: false, async stream =>
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
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: false, stream => stream.WriteAsync(new byte[] { 9 }, Ct).AsTask()));

        Assert.Equal("keep", await File.ReadAllTextAsync(output, Ct));
        Assert.Equal(["result.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task WriteAsync_ExistingFileWithOverwrite_ReplacesIt()
    {
        string output = PathInFolder("result.enc");
        await File.WriteAllTextAsync(output, "old", Ct);

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: true, isSensitive: false, stream => stream.WriteAsync(new byte[] { 9 }, Ct).AsTask());

        Assert.Equal([9], await File.ReadAllBytesAsync(output, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WriteAsync_Sensitive_HidesTheTempFileOnWindowsButNeverTheResult(bool isSensitive)
    {
        string output = PathInFolder("result.txt");
        bool tempWasHidden = false;

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive, _ =>
        {
            tempWasHidden = _folder.GetFiles().Single().Attributes.HasFlag(FileAttributes.Hidden);
            return Task.CompletedTask;
        });

        if (OperatingSystem.IsWindows()) // on Linux and macOS .NET reports every dot-name as hidden
        {
            Assert.Equal(isSensitive, tempWasHidden);
        }

        Assert.False(File.GetAttributes(output).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")] // for the analyzer (CA1416); the skip below keeps it off Windows
    public async Task WriteAsync_SensitiveOnLinuxOrMacOS_IsReadableByTheOwnerOnly()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no Unix file mode.");
        string output = PathInFolder("result.txt");
        UnixFileMode tempMode = default;

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: true, _ =>
        {
            tempMode = File.GetUnixFileMode(_folder.GetFiles().Single().FullName);
            return Task.CompletedTask;
        });

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, tempMode);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")] // for the analyzer (CA1416); the skip below keeps it on Windows
    public async Task WriteAsync_SensitiveOnWindows_TempAndResultHaveAnOwnerOnlyAcl(bool isSensitive)
    {
        // Backlog C16: a sensitive file must not inherit the folder's rights, so other accounts cannot read it.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Access control lists are checked on Windows; Unix uses the file mode.");
        string output = PathInFolder("result.txt");
        bool tempWasOwnerOnly = false;

        await AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive, _ =>
        {
            tempWasOwnerOnly = IsOwnerOnly(_folder.GetFiles().Single());
            return Task.CompletedTask;
        });

        Assert.Equal(isSensitive, tempWasOwnerOnly);
        Assert.Equal(isSensitive, IsOwnerOnly(new FileInfo(output)));
        Assert.Empty(await File.ReadAllBytesAsync(output, Ct)); // the owner can still read it
    }

    [Fact]
    public async Task WriteAsync_WriterFailsAndTheTempFileCannotBeDeleted_LeavesItEmpty()
    {
        // Windows refuses to delete a read-only file, standing in for a virus scanner that holds it (Review Board 1.6, M1).
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to delete a read-only file.");
        string output = PathInFolder("result.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: true, async stream =>
            {
                await stream.WriteAsync(new byte[] { 1, 2, 3 }, Ct);
                File.SetAttributes(_folder.GetFiles().Single().FullName, FileAttributes.ReadOnly | FileAttributes.Hidden);
                throw new InvalidOperationException("writer failed");
            }));

        FileInfo leftover = Assert.Single(_folder.GetFiles());
        Assert.Equal(0, leftover.Length);
    }

    [Fact]
    public async Task WriteAsync_MissingFolder_ThrowsDirectoryNotFoundException()
    {
        string output = Path.Combine(_folder.FullName, "missing", "result.enc");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => AtomicFileWriter.WriteAsync(output, overwriteExisting: false, isSensitive: false, _ => Task.CompletedTask));
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
    public void CreateTempFilePath_RandomPart_IsAlwaysLowercaseHex()
    {
        // 50 draws: one draw is all digits ~2 % of the time, which would hide an upper-case bug (found by Stryker, 1.6).
        for (int draw = 0; draw < 50; draw++)
        {
            Assert.Matches(@"\.[0-9a-f]{8}\.securefile-tmp$", AtomicFileWriter.CreateTempFilePath(PathInFolder("a.enc")));
        }
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

    [Fact]
    public async Task TryDeleteFile_FileHeldBrieflyByAnotherHandle_SucceedsOnALaterTry()
    {
        // A scanner that holds a just-closed file for a moment: Windows refuses the delete while the handle is open.
        // (On Linux and macOS an open file can be deleted at once, so the first try succeeds and the test still holds.)
        string path = PathInFolder("held.bin");
        await File.WriteAllTextAsync(path, "x", Ct);
        await using var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var release = Task.Run(async () =>
        {
            await Task.Delay(150, Ct);
            await holder.DisposeAsync();
        }, Ct);

        bool deleted = AtomicFileWriter.TryDeleteFile(path);
        await release;

        Assert.True(deleted);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TryDeleteFile_FileHeldForGoodOnWindows_GivesUpAndReturnsFalse()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to delete a file that is open.");
        string path = PathInFolder("held-forever.bin");
        File.WriteAllText(path, "x");
        using FileStream holder = new(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.False(AtomicFileWriter.TryDeleteFile(path)); // all tries refused, no exception
        Assert.True(File.Exists(path));
    }

    /// <summary>The test run's token (xUnit1051).</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Whether the file inherits nothing and grants rights to the current user only.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsOwnerOnly(FileInfo file)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        System.Security.AccessControl.FileSecurity security = file.GetAccessControl();
        var rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier))
            .Cast<System.Security.AccessControl.FileSystemAccessRule>()
            .ToList();
        return security.AreAccessRulesProtected && rules.Count > 0 && rules.TrueForAll(rule => rule.IdentityReference.Equals(identity.User));
    }

    private string PathInFolder(string name)
    {
        return Path.Combine(_folder.FullName, name);
    }

    private string[] FileNamesInFolder()
    {
        return [.. _folder.GetFiles().Select(file => file.Name).Order(StringComparer.Ordinal)];
    }
}

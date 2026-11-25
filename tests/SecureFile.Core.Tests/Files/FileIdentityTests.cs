using SecureFile.Core.Cryptography;
using SecureFile.Core.Files;

namespace SecureFile.Core.Tests.Files;

/// <summary>
/// Tests for <see cref="FileIdentity"/> and the same-file guard it gives <see cref="FileEncryptionService"/> (backlog C14).
/// </summary>
/// <remarks>
/// The link tests matter most on the Ubuntu and macOS CI runners, where the final rename would replace an open input.
/// Windows needs Developer Mode or administrator rights to create symbolic links, so there they skip when not allowed.
/// </remarks>
public sealed class FileIdentityTests : IDisposable
{
    private const string Password = "correct horse battery staple";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("SecureFileTests_");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Fact]
    public void AreSameFile_TwoDifferentNames_IsFalse()
    {
        Assert.False(FileIdentity.AreSameFile(PathIn("a.txt"), PathIn("b.txt")));
    }

    [Fact]
    public void AreSameFile_SameNameInAnotherCase_IsTrue()
    {
        Assert.True(FileIdentity.AreSameFile(PathIn("report.txt"), PathIn("REPORT.TXT")));
    }

    [Fact]
    public void AreSameFile_ComposedAndDecomposedAccent_IsTrue()
    {
        // macOS stores names decomposed (e + combining acute); a typed name is usually composed (é).
        Assert.True(FileIdentity.AreSameFile(PathIn("café.txt"), PathIn("café.txt")));
    }

    [Fact]
    public void AreSameFile_OutputThroughALinkToTheInputFolder_IsTrue()
    {
        string realFolder = Directory.CreateDirectory(PathIn("real")).FullName;
        string input = Path.Combine(realFolder, "secret.txt");
        File.WriteAllText(input, "plain");
        string aliasFolder = CreateFolderLinkOrSkip("alias", realFolder);

        Assert.True(FileIdentity.AreSameFile(input, Path.Combine(aliasFolder, "secret.txt")));
    }

    [Fact]
    public void AreSameFile_InputIsAFileLinkAndOutputItsTarget_IsTrue()
    {
        string target = PathIn("target.txt");
        File.WriteAllText(target, "plain");
        string link = CreateFileLinkOrSkip("link.txt", target);

        Assert.True(FileIdentity.AreSameFile(link, target));
    }

    [Fact]
    public void AreSameFile_OutputIsAFileLinkToTheInput_IsFalse()
    {
        // The rename replaces the link itself, not the file it points to, so the input is safe.
        string input = PathIn("input.txt");
        File.WriteAllText(input, "plain");
        string link = CreateFileLinkOrSkip("link.enc", input);

        Assert.False(FileIdentity.AreSameFile(input, link));
    }

    [Fact]
    public void AreSameFile_LinkWithARelativeTargetInsideALinkedFolder_IsTrue()
    {
        string realFolder = Directory.CreateDirectory(PathIn("real")).FullName;
        string input = Path.Combine(realFolder, "secret.txt");
        File.WriteAllText(input, "plain");
        string outer = CreateFolderLinkOrSkip("outer", realFolder);
        string inner = CreateFolderLinkOrSkip("inner", "outer"); // relative target, itself a link

        Assert.True(FileIdentity.AreSameFile(input, Path.Combine(inner, "secret.txt")));
        Assert.True(Directory.Exists(outer));
    }

    [Fact]
    public void AreSameFile_LinkLoop_ThrowsIOException()
    {
        CreateFolderLinkOrSkip("loop-a", PathIn("loop-b"));
        CreateFolderLinkOrSkip("loop-b", PathIn("loop-a"));

        Assert.Throws<IOException>(() => FileIdentity.AreSameFile(PathIn("input.txt"), Path.Combine(PathIn("loop-a"), "out.enc")));
    }

    [Fact]
    public async Task EncryptAsync_OutputThroughAFolderLinkToTheInput_ThrowsAndKeepsTheInput()
    {
        string realFolder = Directory.CreateDirectory(PathIn("real")).FullName;
        string input = Path.Combine(realFolder, "secret.txt");
        await File.WriteAllTextAsync(input, "plain", Ct);
        string aliasFolder = CreateFolderLinkOrSkip("alias", realFolder);
        var service = new FileEncryptionService(() => EncryptedFileHeader.CreateNew(100_000));

        ArgumentException thrown = Assert.Throws<ArgumentException>(
            "outputFilePath",
            () => { _ = service.EncryptAsync(input, Path.Combine(aliasFolder, "secret.txt"), Password, overwriteExisting: true, cancellationToken: Ct); });

        Assert.StartsWith("The output file must be different from the input file.", thrown.Message, StringComparison.Ordinal);
        Assert.Equal("plain", await File.ReadAllTextAsync(input, Ct));
    }

    /// <summary>The test run's token, so a stopped test run also stops the operation (xUnit1051).</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Returns the full path of <paramref name="name"/> in this test's folder.</summary>
    private string PathIn(string name)
    {
        return Path.Combine(_folder.FullName, name);
    }

    /// <summary>Creates a folder link, or skips the test when this machine does not allow it.</summary>
    private string CreateFolderLinkOrSkip(string linkName, string target)
    {
        string link = PathIn(linkName);
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"Symbolic links cannot be created here: {exception.Message}");
        }

        return link;
    }

    /// <summary>Creates a file link, or skips the test when this machine does not allow it.</summary>
    private string CreateFileLinkOrSkip(string linkName, string target)
    {
        string link = PathIn(linkName);
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip($"Symbolic links cannot be created here: {exception.Message}");
        }

        return link;
    }
}

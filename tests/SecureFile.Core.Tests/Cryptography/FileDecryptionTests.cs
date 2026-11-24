using System.Security.Cryptography;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="FileEncryptionService.DecryptAsync"/> (test catalog T-02 round trip, T-05–T-08 wrong password,
/// tampering and truncation, T-10 cancel, T-22 no partial output). Every failure must leave <b>no</b> output file and
/// <b>no</b> temp file, which each test checks by listing its own empty temp folder.
/// </summary>
/// <remarks>Files are encrypted with 100,000 iterations (the lowest accepted), so the tests run 6× faster.</remarks>
public sealed class FileDecryptionTests : IDisposable
{
    private const int TestIterations = 100_000;
    private const string Password = "correct horse battery staple";
    private const int Chunk = FileEncryptionService.ChunkLength;
    private const string EncryptedName = "secret.enc";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("SecureFileTests_");
    private readonly FileEncryptionService _service = new(() => EncryptedFileHeader.CreateNew(TestIterations));

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // ---- Round trip ------------------------------------------------------------------------------------------------

    public static TheoryData<int> Lengths => new()
    {
        0, 1, 15, 16, 17, Chunk - 1, Chunk, Chunk + 1, (2 * Chunk) + 17,
    };

    [Theory]
    [MemberData(nameof(Lengths))]
    public async Task DecryptAsync_FileMadeByEncryptAsync_GivesBackTheOriginalAndNoTempFile(int length)
    {
        byte[] plaintext = RandomNumberGenerator.GetBytes(length);
        string encrypted = await EncryptAsync(plaintext);

        await _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct);

        Assert.Equal(plaintext, await File.ReadAllBytesAsync(OutputPath(), Ct));
        Assert.Equal(["output.bin", EncryptedName], FileNamesInFolder());
        Assert.False(File.GetAttributes(OutputPath()).HasFlag(FileAttributes.Hidden));
    }

    [Fact]
    public async Task WriteDecryptedFileAsync_InputThatReturnsFewBytesPerRead_GivesTheOriginal()
    {
        byte[] plaintext = RandomNumberGenerator.GetBytes((2 * Chunk) + 17);
        byte[] file = await File.ReadAllBytesAsync(await EncryptAsync(plaintext), Ct);
        using var input = new TrickleStream(file);
        using var output = new MemoryStream();

        EncryptedFileHeader header = await FileEncryptionService.ReadHeaderAsync(input, Ct);
        long ciphertextLength = EncryptedFileFormat.GetCiphertextLength(input.Length);
        await FileEncryptionService.WriteDecryptedFileAsync(input, output, header, ciphertextLength, Password, progress: null, Ct);

        Assert.Equal(plaintext, output.ToArray());
        Assert.True(input.ReadCount > 3, "the stream must really have returned short reads");
    }

    [Fact]
    public async Task WriteDecryptedFileAsync_InputShorterThanItsCheckedLength_ThrowsEndOfStreamException()
    {
        // The length is checked once at the start; a file that shrinks while it is read must not be cut silently.
        byte[] file = await File.ReadAllBytesAsync(await EncryptAsync(new byte[100]), Ct);
        using var input = new MemoryStream(file[..^20]);
        using var output = new MemoryStream();
        EncryptedFileHeader header = await FileEncryptionService.ReadHeaderAsync(input, Ct);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => FileEncryptionService.WriteDecryptedFileAsync(input, output, header, EncryptedFileFormat.GetCiphertextLength(file.Length), Password, progress: null, Ct));
    }

    [Fact]
    public async Task DecryptAsync_Progress_RisesAfterEveryChunkAndEndsAt100()
    {
        // 2.5 chunks of plaintext → 2.5 chunks + 16 bytes of ciphertext, so each full chunk is just under 40 %.
        string encrypted = await EncryptAsync(new byte[(2 * Chunk) + (Chunk / 2)]);
        var progress = new RecordingProgress();

        await _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, progress, Ct);

        Assert.Equal([39, 79, 100], progress.Reports);
    }

    [Fact]
    public async Task DecryptAsync_WhileWriting_TheTempFileIsHiddenOnWindows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The hidden attribute is set on Windows only; elsewhere the dot-name hides it.");
        string encrypted = await EncryptAsync(new byte[100]);
        bool tempWasHidden = false;
        var progress = new RecordingProgress(_ => tempWasHidden = _folder.GetFiles(".~*").Single().Attributes.HasFlag(FileAttributes.Hidden));

        await _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, progress, Ct);

        Assert.True(tempWasHidden);
    }

    [Fact]
    public async Task DecryptAsync_ExistingOutputAndOverwriteAllowed_ReplacesIt()
    {
        string encrypted = await EncryptAsync([7, 8, 9]);
        await File.WriteAllTextAsync(OutputPath(), "old", Ct);

        await _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: true, cancellationToken: Ct);

        Assert.Equal([7, 8, 9], await File.ReadAllBytesAsync(OutputPath(), Ct));
    }

    // ---- Wrong password, tampering, truncation: refused, nothing left behind ------------------------------------------

    [Fact]
    public async Task DecryptAsync_WrongPassword_ThrowsAuthenticationFailedAndLeavesNothing()
    {
        string encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(5000));

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), "wrong password", overwriteExisting: false, cancellationToken: Ct));

        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_WrongPasswordWithOverwriteAllowed_KeepsTheExistingFile()
    {
        string encrypted = await EncryptAsync([1, 2, 3]);
        await File.WriteAllTextAsync(OutputPath(), "keep me", Ct);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), "wrong password", overwriteExisting: true, cancellationToken: Ct));

        Assert.Equal("keep me", await File.ReadAllTextAsync(OutputPath(), Ct));
        Assert.Equal(["output.bin", EncryptedName], FileNamesInFolder());
    }

    /// <summary>
    /// One flipped bit in each part of a 100-byte file (41 header + 112 ciphertext + 32 tag = 185 bytes). A negative
    /// offset counts from the end of the file.
    /// </summary>
    public static TheoryData<int, Type> FlippedBits => new()
    {
        { 0, typeof(InvalidEncryptedFileException) },    // signature
        { 4, typeof(InvalidEncryptedFileException) },    // version
        { 5, typeof(AuthenticationFailedException) },    // iterations, still in range (100,000 → 100,001)
        { 8, typeof(InvalidEncryptedFileException) },    // iterations, now negative
        { 9, typeof(AuthenticationFailedException) },    // salt
        { 25, typeof(AuthenticationFailedException) },   // IV
        { 41, typeof(AuthenticationFailedException) },   // first ciphertext byte
        { 89, typeof(AuthenticationFailedException) },   // middle ciphertext block (the 4th of 7)
        { -33, typeof(AuthenticationFailedException) },  // last ciphertext byte (the padding block)
        { -32, typeof(AuthenticationFailedException) },  // first tag byte
        { -1, typeof(AuthenticationFailedException) },   // last tag byte
    };

    [Theory]
    [MemberData(nameof(FlippedBits))]
    public async Task DecryptAsync_OneBitFlipped_ThrowsAndLeavesNothing(int offset, Type expectedException)
    {
        string encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        byte[] file = await File.ReadAllBytesAsync(encrypted, Ct);
        int index = offset >= 0 ? offset : file.Length + offset;
        file[index] ^= offset == 8 ? (byte)0x80 : (byte)0x01;
        await File.WriteAllBytesAsync(encrypted, file, Ct);

        Exception thrown = await Assert.ThrowsAnyAsync<SecureFileException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct));

        Assert.IsType(expectedException, thrown);
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_BitFlippedInTheFirstOfThreeChunks_ThrowsAfterStreamingAndDeletesThePlaintextTempFile()
    {
        string encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(2 * Chunk));
        byte[] file = await File.ReadAllBytesAsync(encrypted, Ct);
        file[100] ^= 0x01;
        await File.WriteAllBytesAsync(encrypted, file, Ct);
        var progress = new RecordingProgress();

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, progress, Ct));

        Assert.Equal(100, progress.Reports[^1]); // the whole file was read before the verdict
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Theory]
    [InlineData(-1, typeof(InvalidEncryptedFileException))]  // not whole blocks any more
    [InlineData(-16, typeof(AuthenticationFailedException))] // whole blocks, but the tag no longer fits
    [InlineData(-32, typeof(AuthenticationFailedException))] // the tag cut off completely
    [InlineData(16, typeof(AuthenticationFailedException))]  // a block appended
    public async Task DecryptAsync_FileCutShortOrExtended_ThrowsAndLeavesNothing(int lengthChange, Type expectedException)
    {
        string encrypted = await EncryptAsync(RandomNumberGenerator.GetBytes(100));
        byte[] file = await File.ReadAllBytesAsync(encrypted, Ct);
        Array.Resize(ref file, file.Length + lengthChange);
        await File.WriteAllBytesAsync(encrypted, file, Ct);

        Exception thrown = await Assert.ThrowsAnyAsync<SecureFileException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct));

        Assert.IsType(expectedException, thrown);
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_ThreeByteTextFile_IsNotAnEncryptedFile()
    {
        string input = WriteFile("notes.txt", "abc"u8.ToArray());

        InvalidEncryptedFileException thrown = await Assert.ThrowsAsync<InvalidEncryptedFileException>(
            () => _service.DecryptAsync(input, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct));

        Assert.Equal(InvalidEncryptedFileReason.NotEncryptedFile, thrown.Reason);
        Assert.Equal(["notes.txt"], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_EightyByteFileStartingWithTheSignature_IsDamaged()
    {
        // A valid header followed by too few bytes: the header check passes, the length check (≥ 89) does not.
        byte[] file = await File.ReadAllBytesAsync(await EncryptAsync([]), Ct);
        string input = WriteFile(EncryptedName, file[..80]);

        InvalidEncryptedFileException thrown = await Assert.ThrowsAsync<InvalidEncryptedFileException>(
            () => _service.DecryptAsync(input, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct));

        Assert.Equal(InvalidEncryptedFileReason.Damaged, thrown.Reason);
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_ValidTagButInvalidPadding_IsDamagedAndLeavesNothing()
    {
        // Only a program that knows the password can make this: a correct tag over a last block whose padding is wrong.
        var header = EncryptedFileHeader.CreateNew(TestIterations);
        using FileKeys keys = KeyDerivation.DeriveKeys(Password, header);
        using var aes = Aes.Create();
        aes.Key = keys.EncryptionKey.ToArray();
        byte[] ciphertext = aes.EncryptCbc(new byte[16], header.Iv.Span, PaddingMode.None); // last byte 0: invalid PKCS7
        byte[] headerBytes = new byte[EncryptedFileFormat.HeaderLength];
        header.WriteTo(headerBytes);
        byte[] authenticated = [.. headerBytes, .. ciphertext];
        string input = WriteFile(EncryptedName, [.. authenticated, .. HMACSHA256.HashData(keys.AuthenticationKey, authenticated)]);

        InvalidEncryptedFileException thrown = await Assert.ThrowsAsync<InvalidEncryptedFileException>(
            () => _service.DecryptAsync(input, OutputPath(), Password, overwriteExisting: false, cancellationToken: Ct));

        // Wrapped at the source (Step 1.7): one documented type for the caller, the runtime's error kept for the log.
        Assert.Equal(InvalidEncryptedFileReason.Damaged, thrown.Reason);
        Assert.IsAssignableFrom<CryptographicException>(thrown.InnerException);
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    // ---- No permission (T-22): a clear error, nothing left behind ---------------------------------------------------

    [Fact]
    public async Task DecryptAsync_ExistingOutputIsReadOnlyOnWindows_ThrowsUnauthorizedAccessAndKeepsIt()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a read-only file.");
        string encrypted = await EncryptAsync([1, 2, 3]);
        await File.WriteAllTextAsync(OutputPath(), "keep me", Ct);
        File.SetAttributes(OutputPath(), FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: true, cancellationToken: Ct));

            Assert.Equal("keep me", await File.ReadAllTextAsync(OutputPath(), Ct));
            Assert.Equal(["output.bin", EncryptedName], FileNamesInFolder()); // no temp file left
        }
        finally
        {
            File.SetAttributes(OutputPath(), FileAttributes.Normal); // so Dispose can delete the folder
        }
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")] // for the analyzer (CA1416); the skip below keeps it off Windows
    public async Task DecryptAsync_OutputFolderIsReadOnlyOnLinuxOrMacOS_ThrowsUnauthorizedAccessAndLeavesNothing()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no Unix folder mode; covered by the read-only file test.");
        string encrypted = await EncryptAsync([1, 2, 3]);
        DirectoryInfo readOnlyFolder = _folder.CreateSubdirectory("read-only");
        readOnlyFolder.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserExecute;
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _service.DecryptAsync(encrypted, Path.Combine(readOnlyFolder.FullName, "out.bin"), Password, false, cancellationToken: Ct));

            Assert.Empty(readOnlyFolder.GetFiles());
        }
        finally
        {
            readOnlyFolder.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        }
    }

    // ---- Cancel: nothing is left behind ----------------------------------------------------------------------------

    [Fact]
    public async Task DecryptAsync_AlreadyCancelled_ThrowsAndWritesNothing()
    {
        string encrypted = await EncryptAsync(new byte[100]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, cancellationToken: new CancellationToken(canceled: true)));

        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Theory]
    [InlineData(3 * Chunk)] // cancelled after the first of four chunks
    [InlineData(100)]       // cancelled after the only chunk, before the tag check and the last block
    public async Task DecryptAsync_CancelledFromTheProgressReport_ThrowsAndLeavesNothing(int length)
    {
        string encrypted = await EncryptAsync(new byte[length]);
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, progress, cancellation.Token));

        Assert.Single(progress.Reports);
        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    [Fact]
    public async Task DecryptAsync_CancelledDuringKeyDerivation_ThrowsAndLeavesNothing()
    {
        // 1,000,000 iterations take about 0.7 s, so a cancel after 50 ms lands while PBKDF2 is running.
        string encrypted = await EncryptAsync(new byte[100], iterations: 1_000_000);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.DecryptAsync(encrypted, OutputPath(), Password, overwriteExisting: false, cancellationToken: cancellation.Token));

        Assert.Equal([EncryptedName], FileNamesInFolder());
    }

    // ---- Invalid arguments: rejected at the call -------------------------------------------------------------------

    [Fact]
    public void DecryptAsync_InvalidArguments_ThrowAtOnce()
    {
        Assert.Throws<ArgumentNullException>("password", () => { _ = _service.DecryptAsync("a.enc", "b", null!, false, cancellationToken: Ct); });
        Assert.Throws<ArgumentException>("inputFilePath", () => { _ = _service.DecryptAsync(" ", "b", Password, false, cancellationToken: Ct); });

        string input = WriteFile(EncryptedName, [1]);
        Assert.Throws<ArgumentException>("outputFilePath", () => { _ = _service.DecryptAsync(input, input, Password, false, cancellationToken: Ct); });

        // A lone surrogate is refused at the call too, like every other bad argument (Step 1.7), not only when awaited.
        Assert.Throws<ArgumentException>("password", () => { _ = _service.DecryptAsync(input, "b", "abc\uD800", false, cancellationToken: Ct); });
    }

    [Fact]
    public void DecryptAsync_ExistingOutputWithoutOverwrite_ThrowsAtOnceAndKeepsTheFile()
    {
        string input = WriteFile(EncryptedName, [1]);
        File.WriteAllText(OutputPath(), "keep me");

        Assert.Throws<IOException>(() => { _ = _service.DecryptAsync(input, OutputPath(), Password, false, cancellationToken: Ct); });

        Assert.Equal("keep me", File.ReadAllText(OutputPath()));
    }

    [Fact]
    public async Task DecryptAsync_MissingInput_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _service.DecryptAsync(Path.Combine(_folder.FullName, "missing.enc"), OutputPath(), Password, false, cancellationToken: Ct));

        Assert.Empty(FileNamesInFolder());
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    /// <summary>The test run's token (xUnit1051).</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Encrypts <paramref name="plaintext"/> to <c>secret.enc</c> and deletes the plaintext file again.</summary>
    private async Task<string> EncryptAsync(byte[] plaintext, int iterations = TestIterations)
    {
        string plain = WriteFile("plain.bin", plaintext);
        string encrypted = Path.Combine(_folder.FullName, EncryptedName);
        await new FileEncryptionService(() => EncryptedFileHeader.CreateNew(iterations))
            .EncryptAsync(plain, encrypted, Password, overwriteExisting: false, cancellationToken: Ct);
        File.Delete(plain);
        return encrypted;
    }

    private string WriteFile(string name, byte[] content)
    {
        string path = Path.Combine(_folder.FullName, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private string OutputPath()
    {
        return Path.Combine(_folder.FullName, "output.bin");
    }

    private string[] FileNamesInFolder()
    {
        return [.. _folder.GetFiles().Select(file => file.Name).Order(StringComparer.Ordinal)];
    }
}

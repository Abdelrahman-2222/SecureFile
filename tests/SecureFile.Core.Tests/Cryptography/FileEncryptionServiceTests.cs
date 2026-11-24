using System.Security.Cryptography;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="FileEncryptionService.EncryptAsync"/> (test catalog T-02 round trip, T-03 sizes, T-10 cancel,
/// T-22 no partial output). Every test works in its own empty temp folder, so "no file was left behind" can be checked
/// by listing the folder.
/// </summary>
/// <remarks>
/// <para>
/// <b>How the output is checked without a decrypt method</b> (that comes in Step 1.6): a <i>reference model</i> builds
/// the expected file in the simplest possible way — the whole plaintext through one <c>TransformFinalBlock</c> call and
/// one <c>HMACSHA256.HashData</c> call. If our chunked loop gives the same bytes, chunking changed nothing (Review
/// Board 1.4, R5). The golden test checks against bytes computed with Python and OpenSSL, outside .NET.
/// </para>
/// <para>Most tests use 100,000 iterations (the lowest accepted) instead of 600,000, so they run 6× faster.</para>
/// </remarks>
public sealed class FileEncryptionServiceTests : IDisposable
{
    private const int TestIterations = 100_000;
    private const string Password = "correct horse battery staple";
    private const int Chunk = FileEncryptionService.ChunkLength;

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("SecureFileTests_");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    // ---- Correct output ------------------------------------------------------------------------------------------

    [Fact]
    public async Task EncryptAsync_FixedSaltAndIv_MatchesIndependentlyComputedGoldenFile()
    {
        // Computed with Python hashlib/hmac (PBKDF2, HKDF-Expand, HMAC) and OpenSSL 3.2.1 (AES-256-CBC), not with .NET.
        const string expectedHex =
            "5346454E01A0860100000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"
            + "4EE32DC64D84937B7111173551D09BB2968DC2A3BBCC1A1F6CA46241F72D8E1D696C7F6776325CB9365E7E13FF5F000F"
            + "E5BD136E4C3FF7D1998BE49B313BE73BF08D2A8D2DF1DE3578BFF220F2C783A1";
        var header = new EncryptedFileHeader(
            TestIterations,
            Convert.FromHexString("000102030405060708090A0B0C0D0E0F"),
            Convert.FromHexString("101112131415161718191A1B1C1D1E1F"));
        string input = WriteInput("The quick brown fox jumps over the lazy dog"u8.ToArray());
        string output = OutputPath();

        await new FileEncryptionService(() => header).EncryptAsync(input, output, Password, overwriteExisting: false, cancellationToken: Ct);

        Assert.Equal(expectedHex, Convert.ToHexString(await File.ReadAllBytesAsync(output, Ct)));
    }

    public static TheoryData<int> Lengths => new()
    {
        0, 1, 15, 16, 17, Chunk - 1, Chunk, Chunk + 1, (2 * Chunk) + 17,
    };

    [Theory]
    [MemberData(nameof(Lengths))]
    public async Task EncryptAsync_VariousLengths_ChunkedFileEqualsOneCallReference(int length)
    {
        byte[] plaintext = RandomNumberGenerator.GetBytes(length);
        EncryptedFileHeader? usedHeader = null;
        var service = new FileEncryptionService(() => usedHeader = EncryptedFileHeader.CreateNew(TestIterations));
        string output = OutputPath();

        await service.EncryptAsync(WriteInput(plaintext), output, Password, overwriteExisting: false, cancellationToken: Ct);

        byte[] expected = ReferenceEncrypt(plaintext, Password, usedHeader!);
        Assert.Equal(expected, await File.ReadAllBytesAsync(output, Ct));
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public async Task EncryptAsync_VariousLengths_FileLengthMatchesFormatFormula(int length)
    {
        string output = OutputPath();

        await CreateService().EncryptAsync(WriteInput(new byte[length]), output, Password, overwriteExisting: false, cancellationToken: Ct);

        long expectedLength = 41 + (((length / 16) + 1) * 16) + 32; // PROJECT_PLAN §5.1
        Assert.Equal(expectedLength, new FileInfo(output).Length);
    }

    [Theory]
    [MemberData(nameof(Lengths))]
    public async Task EncryptAsync_VariousLengths_RoundTripsWithReferenceDecryption(int length)
    {
        byte[] plaintext = RandomNumberGenerator.GetBytes(length);
        string output = OutputPath();

        await CreateService().EncryptAsync(WriteInput(plaintext), output, Password, overwriteExisting: false, cancellationToken: Ct);

        Assert.Equal(plaintext, ReferenceDecrypt(await File.ReadAllBytesAsync(output, Ct), Password));
    }

    [Fact]
    public async Task EncryptAsync_SameInputTwice_ProducesDifferentFiles()
    {
        string input = WriteInput(new byte[100]);
        string first = OutputPath("first.enc");
        string second = OutputPath("second.enc");

        await CreateService().EncryptAsync(input, first, Password, overwriteExisting: false, cancellationToken: Ct);
        await CreateService().EncryptAsync(input, second, Password, overwriteExisting: false, cancellationToken: Ct);

        Assert.NotEqual(await File.ReadAllBytesAsync(first, Ct), await File.ReadAllBytesAsync(second, Ct));
    }

    [Fact]
    public async Task EncryptAsync_Always_LeavesTheInputUnchangedAndNoTempFile()
    {
        byte[] plaintext = RandomNumberGenerator.GetBytes(5000);
        string input = WriteInput(plaintext);
        string output = OutputPath();

        await CreateService().EncryptAsync(input, output, Password, overwriteExisting: false, cancellationToken: Ct);

        Assert.Equal(plaintext, await File.ReadAllBytesAsync(input, Ct));
        Assert.Equal(["input.bin", "output.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_PasswordOfOnlySpaces_IsAValidPassword()
    {
        string output = OutputPath();

        await CreateService().EncryptAsync(WriteInput([1, 2, 3]), output, "   ", overwriteExisting: false, cancellationToken: Ct);

        Assert.Equal([1, 2, 3], ReferenceDecrypt(await File.ReadAllBytesAsync(output, Ct), "   "));
    }

    [Fact]
    public async Task PublicConstructor_WritesTheDefaultIterationCount()
    {
        string output = OutputPath();

        await new FileEncryptionService().EncryptAsync(WriteInput([42]), output, Password, overwriteExisting: false, cancellationToken: Ct);

        var header = EncryptedFileHeader.Read(await File.ReadAllBytesAsync(output, Ct));
        Assert.Equal(EncryptedFileFormat.DefaultIterations, header.Iterations);
    }

    // ---- Progress --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, new[] { 100 })]
    [InlineData(10, new[] { 100 })]
    [InlineData(Chunk, new[] { 100 })] // the second, 0-byte round adds the padding block but no new percent
    [InlineData((2 * Chunk) + (Chunk / 2), new[] { 40, 80, 100 })]
    public async Task EncryptAsync_Progress_RisesAfterEveryChunkAndEndsAt100(int length, int[] expectedReports)
    {
        var progress = new RecordingProgress();

        await CreateService().EncryptAsync(WriteInput(new byte[length]), OutputPath(), Password, overwriteExisting: false, progress, Ct);

        Assert.Equal(expectedReports, progress.Reports);
    }

    // ---- Cancel and failure: nothing is left behind ----------------------------------------------------------------

    [Fact]
    public async Task EncryptAsync_AlreadyCancelled_ThrowsAndWritesNothing()
    {
        string input = WriteInput(new byte[100]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().EncryptAsync(input, OutputPath(), Password, overwriteExisting: false, cancellationToken: new CancellationToken(canceled: true)));

        Assert.Equal(["input.bin"], FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_CancelledAfterTheFirstChunk_ThrowsAndLeavesNoOutputOrTempFile()
    {
        string input = WriteInput(new byte[3 * Chunk]);
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().EncryptAsync(input, OutputPath(), Password, overwriteExisting: false, progress, cancellation.Token));

        Assert.Single(progress.Reports); // stopped at the next chunk, not at the end
        Assert.Equal(["input.bin"], FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_CancelledAfterTheLastChunk_StillThrowsAndLeavesNothing()
    {
        string input = WriteInput(new byte[100]);
        using var cancellation = new CancellationTokenSource();
        var progress = new RecordingProgress(_ => cancellation.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService().EncryptAsync(input, OutputPath(), Password, overwriteExisting: false, progress, cancellation.Token));

        Assert.Equal(["input.bin"], FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_OutputAppearsDuringEncryption_ThrowsKeepsThatFileAndDeletesTheTempFile()
    {
        string input = WriteInput(new byte[10]);
        string output = OutputPath();
        var progress = new RecordingProgress(_ => File.WriteAllText(output, "someone else's file"));

        await Assert.ThrowsAsync<IOException>(
            () => CreateService().EncryptAsync(input, output, Password, overwriteExisting: false, progress, Ct));

        Assert.Equal("someone else's file", await File.ReadAllTextAsync(output, Ct));
        Assert.Equal(["input.bin", "output.enc"], FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_ExistingOutputAndOverwriteAllowed_ReplacesIt()
    {
        string output = OutputPath();
        await File.WriteAllTextAsync(output, "old", Ct);

        await CreateService().EncryptAsync(WriteInput([7]), output, Password, overwriteExisting: true, cancellationToken: Ct);

        Assert.Equal([7], ReferenceDecrypt(await File.ReadAllBytesAsync(output, Ct), Password));
    }

    // ---- Invalid input: rejected at the call, before any work ------------------------------------------------------

    [Fact]
    public void EncryptAsync_ExistingOutputWithoutOverwrite_ThrowsAtOnceAndKeepsTheFile()
    {
        string input = WriteInput([1]);
        string output = OutputPath();
        File.WriteAllText(output, "keep me");

        IOException thrown = Assert.Throws<IOException>(() => { _ = CreateService().EncryptAsync(input, output, Password, overwriteExisting: false, cancellationToken: Ct); });

        Assert.Equal("The output file already exists.", thrown.Message);

        Assert.Equal("keep me", File.ReadAllText(output));
    }

    [Fact]
    public void EncryptAsync_NullArguments_ThrowArgumentNullExceptionAtOnce()
    {
        FileEncryptionService service = CreateService();

        Assert.Throws<ArgumentNullException>("inputFilePath", () => { _ = service.EncryptAsync(null!, "b", Password, false, cancellationToken: Ct); });
        Assert.Throws<ArgumentNullException>("outputFilePath", () => { _ = service.EncryptAsync("a", null!, Password, false, cancellationToken: Ct); });
        Assert.Throws<ArgumentNullException>("password", () => { _ = service.EncryptAsync("a", "b", null!, false, cancellationToken: Ct); });
    }

    [Theory]
    [InlineData("", "out.enc", "pw", "inputFilePath")]
    [InlineData("  ", "out.enc", "pw", "inputFilePath")]
    [InlineData("in.bin", "", "pw", "outputFilePath")]
    [InlineData("in.bin", " ", "pw", "outputFilePath")]
    [InlineData("in.bin", "out.enc", "", "password")]
    public void EncryptAsync_EmptyArguments_ThrowArgumentExceptionAtOnce(string input, string output, string password, string parameterName)
    {
        Assert.Throws<ArgumentException>(parameterName, () => { _ = CreateService().EncryptAsync(input, output, password, false, cancellationToken: Ct); });
    }

    [Fact]
    public void EncryptAsync_PasswordWithLoneSurrogate_ThrowsArgumentExceptionAtOnce()
    {
        // At the call, like every other bad argument, not only when the task is awaited (Step 1.7). Not [InlineData]:
        // xUnit stores theory data as UTF-8, which would turn the lone surrogate into a valid character.
        Assert.Throws<ArgumentException>("password", () => { _ = CreateService().EncryptAsync("in.bin", "out.enc", "abc\uD800", false, cancellationToken: Ct); });
    }

    [Fact]
    public void EncryptAsync_OutputIsTheInputWithDifferentCase_ThrowsArgumentException()
    {
        string input = WriteInput([1]);

        ArgumentException thrown = Assert.Throws<ArgumentException>(
            "outputFilePath", () => { _ = CreateService().EncryptAsync(input, input.ToUpperInvariant(), Password, false, cancellationToken: Ct); });
        Assert.StartsWith("The output file must be different from the input file.", thrown.Message, StringComparison.Ordinal);
        Assert.Equal([1], File.ReadAllBytes(input));
    }

    [Fact]
    public void EncryptAsync_OutputEndsWithAFolderSeparator_ThrowsArgumentException()
    {
        string folderPath = _folder.FullName + Path.DirectorySeparatorChar;

        AssertRejectedAsNotAFile(folderPath);
    }

    [Fact]
    public void EncryptAsync_OutputIsAnExistingFolder_ThrowsArgumentExceptionBeforeAnyWork()
    {
        string folderPath = Directory.CreateDirectory(Path.Combine(_folder.FullName, "existing")).FullName;

        AssertRejectedAsNotAFile(folderPath);
    }

    [Theory]
    [InlineData("output.enc:hidden")] // an NTFS alternate data stream
    [InlineData("|output.enc")]       // an invalid character as the very first one
    public void EncryptAsync_OutputNameWithCharactersWindowsForbids_ThrowsArgumentExceptionOnWindows(string name)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "':' and '|' are valid file-name characters on Linux and macOS.");

        AssertRejectedAsNotAFile(OutputPath(name));
    }

    [Fact]
    public async Task EncryptAsync_MissingInput_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => CreateService().EncryptAsync(Path.Combine(_folder.FullName, "missing.bin"), OutputPath(), Password, false, cancellationToken: Ct));

        Assert.Empty(FileNamesInFolder());
    }

    [Fact]
    public async Task EncryptAsync_MissingOutputFolder_ThrowsDirectoryNotFoundException()
    {
        string output = Path.Combine(_folder.FullName, "no such folder", "output.enc");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => CreateService().EncryptAsync(WriteInput([1]), output, Password, false, cancellationToken: Ct));

        Assert.Equal(["input.bin"], FileNamesInFolder());
    }

    [Fact]
    public void Constructor_NullHeaderFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("createHeader", () => new FileEncryptionService(null!));
    }

    [Fact]
    public async Task EncryptAsync_CancelledDuringKeyDerivation_ThrowsAndLeavesNothing()
    {
        // 1,000,000 iterations take about 0.7 s, so a cancel after 50 ms lands while PBKDF2 is running.
        var service = new FileEncryptionService(() => EncryptedFileHeader.CreateNew(1_000_000));
        string input = WriteInput(new byte[100]);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.EncryptAsync(input, OutputPath(), Password, overwriteExisting: false, cancellationToken: cancellation.Token));

        Assert.Equal(["input.bin"], FileNamesInFolder());
    }

    [Fact]
    public async Task WriteEncryptedFileAsync_InputThatReturnsFewBytesPerRead_GivesTheSameFileAsTheReference()
    {
        // A network drive or pipe may return fewer bytes than asked for. Treating such a short read as the end of the
        // file would give a cut-short file with a VALID tag (Review Board 1.5, F1).
        byte[] plaintext = RandomNumberGenerator.GetBytes((2 * Chunk) + 17);
        var header = EncryptedFileHeader.CreateNew(TestIterations);
        using var input = new TrickleStream(plaintext);
        using var output = new MemoryStream();

        await FileEncryptionService.WriteEncryptedFileAsync(input, output, header, Password, progress: null, Ct);

        Assert.Equal(ReferenceEncrypt(plaintext, Password, header), output.ToArray());
        Assert.True(input.ReadCount > 3, "the stream must really have returned short reads");
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private void AssertRejectedAsNotAFile(string outputPath)
    {
        string input = WriteInput([1]);

        ArgumentException thrown = Assert.Throws<ArgumentException>(
            "outputFilePath", () => { _ = CreateService().EncryptAsync(input, outputPath, Password, false, cancellationToken: Ct); });
        Assert.StartsWith("The output path must name a file, not a folder.", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(FileNamesInFolder(), name => name.EndsWith(".securefile-tmp", StringComparison.Ordinal));
    }

    /// <summary>The test run's token, so a stopped test run also stops the operation (xUnit1051).</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FileEncryptionService CreateService()
    {
        return new FileEncryptionService(() => EncryptedFileHeader.CreateNew(TestIterations));
    }

    /// <summary>The expected file, built in one call each: header + AES of everything + HMAC of everything.</summary>
    private static byte[] ReferenceEncrypt(byte[] plaintext, string password, EncryptedFileHeader header)
    {
        using FileKeys keys = KeyDerivation.DeriveKeys(password, header);
        using ICryptoTransform encryptor = AesCbc.CreateEncryptor(keys.EncryptionKey, header.Iv.Span);
        byte[] headerBytes = new byte[EncryptedFileFormat.HeaderLength];
        header.WriteTo(headerBytes);
        byte[] authenticated = [.. headerBytes, .. encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length)];
        return [.. authenticated, .. HMACSHA256.HashData(keys.AuthenticationKey, authenticated)];
    }

    /// <summary>Checks the tag, then decrypts — the simplest possible reader of the format, for tests only.</summary>
    private static byte[] ReferenceDecrypt(byte[] file, string password)
    {
        var header = EncryptedFileHeader.Read(file);
        using FileKeys keys = KeyDerivation.DeriveKeys(password, header);
        int tagStart = file.Length - EncryptedFileFormat.AuthenticationTagLength;
        byte[] expectedTag = HMACSHA256.HashData(keys.AuthenticationKey, file.AsSpan(0, tagStart));
        Assert.True(CryptographicOperations.FixedTimeEquals(expectedTag, file.AsSpan(tagStart)), "authentication tag mismatch");

        using ICryptoTransform decryptor = AesCbc.CreateDecryptor(keys.EncryptionKey, header.Iv.Span);
        return decryptor.TransformFinalBlock(file, EncryptedFileFormat.HeaderLength, tagStart - EncryptedFileFormat.HeaderLength);
    }

    private string WriteInput(byte[] content)
    {
        string path = Path.Combine(_folder.FullName, "input.bin");
        File.WriteAllBytes(path, content);
        return path;
    }

    private string OutputPath(string name = "output.enc")
    {
        return Path.Combine(_folder.FullName, name);
    }

    private string[] FileNamesInFolder()
    {
        return [.. _folder.GetFiles().Select(file => file.Name).Order(StringComparer.Ordinal)];
    }
}

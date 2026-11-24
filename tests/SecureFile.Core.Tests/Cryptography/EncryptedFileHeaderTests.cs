using System.Buffers.Binary;
using CsCheck;
using SecureFile.Core.Cryptography;

namespace SecureFile.Core.Tests.Cryptography;

/// <summary>
/// Tests for <see cref="EncryptedFileHeader"/> (test catalog T-06 to T-08, header part): writing, reading, and
/// rejecting every kind of bad header, including random "fuzz" input.
/// </summary>
public sealed class EncryptedFileHeaderTests
{
    // A fixed, readable header used by many tests: 600,000 iterations, salt 00 01 … 0F, IV 10 11 … 1F.
    private const string KnownHeaderHex =
        "5346454E"                              // "SFEN"
        + "01"                                  // version 1
        + "C0270900"                            // 600,000 = 0x000927C0, stored little-endian
        + "000102030405060708090A0B0C0D0E0F"    // salt
        + "101112131415161718191A1B1C1D1E1F";   // IV

    private static readonly byte[] KnownSalt = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
    private static readonly byte[] KnownIv = Convert.FromHexString("101112131415161718191A1B1C1D1E1F");

    [Fact]
    public void WriteTo_KnownValues_ProducesExactlyTheDocumentedBytes()
    {
        // "Golden bytes": proves the byte order and positions on every OS, and that the format never drifts.
        EncryptedFileHeader header = new(600_000, KnownSalt, KnownIv);
        byte[] written = new byte[EncryptedFileFormat.HeaderLength];

        header.WriteTo(written);

        Assert.Equal(KnownHeaderHex, Convert.ToHexString(written));
    }

    [Fact]
    public void Read_KnownBytes_ReturnsTheDocumentedValues()
    {
        var header = EncryptedFileHeader.Read(Convert.FromHexString(KnownHeaderHex));

        Assert.Equal(600_000, header.Iterations);
        Assert.Equal(KnownSalt, header.Salt.ToArray());
        Assert.Equal(KnownIv, header.Iv.ToArray());
    }

    [Fact]
    public void Read_DataLongerThanHeader_ReadsOnlyTheHeader()
    {
        // The service passes the start of the file; ciphertext after the header must not disturb reading.
        byte[] fileStart = [.. Convert.FromHexString(KnownHeaderHex), 0xFF, 0xFF, 0xFF];

        var header = EncryptedFileHeader.Read(fileStart);

        Assert.Equal(600_000, header.Iterations);
        Assert.Equal(KnownIv, header.Iv.ToArray());
    }

    [Fact]
    public void WriteTo_LargerDestination_WritesOnlyTheFirst41Bytes()
    {
        EncryptedFileHeader header = new(600_000, KnownSalt, KnownIv);
        byte[] destination = new byte[50];
        Array.Fill(destination, (byte)0xAA);

        header.WriteTo(destination);

        Assert.Equal(KnownHeaderHex, Convert.ToHexString(destination, 0, EncryptedFileFormat.HeaderLength));
        Assert.All(destination[EncryptedFileFormat.HeaderLength..], value => Assert.Equal(0xAA, value));
    }

    [Fact]
    public void WriteTo_DestinationTooShort_ThrowsArgumentException()
    {
        EncryptedFileHeader header = new(600_000, KnownSalt, KnownIv);
        byte[] destination = new byte[EncryptedFileFormat.HeaderLength - 1];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => header.WriteTo(destination));

        Assert.Equal("destination", exception.ParamName);
        Assert.Contains("41 bytes", exception.Message, StringComparison.Ordinal); // the log says what was needed
        Assert.All(destination, value => Assert.Equal(0, value)); // nothing was half-written
    }

    [Fact]
    public void CreateNew_TwoHeaders_HaveDifferentRandomSaltsAndIvs()
    {
        var first = EncryptedFileHeader.CreateNew(EncryptedFileFormat.DefaultIterations);
        var second = EncryptedFileHeader.CreateNew(EncryptedFileFormat.DefaultIterations);

        Assert.Equal(EncryptedFileFormat.DefaultIterations, first.Iterations);
        Assert.Equal(EncryptedFileFormat.SaltLength, first.Salt.Length);
        Assert.Equal(EncryptedFileFormat.IvLength, first.Iv.Length);
        Assert.NotEqual(first.Salt.ToArray(), second.Salt.ToArray());
        Assert.NotEqual(first.Iv.ToArray(), second.Iv.ToArray());
        Assert.NotEqual(first.Salt.ToArray(), first.Iv.ToArray()); // salt and IV come from separate random draws
        Assert.Contains(first.Salt.ToArray(), value => value != 0); // filled, not left as zeros
        Assert.Contains(first.Iv.ToArray(), value => value != 0);
    }

    [Theory]
    [InlineData(99_999)]
    [InlineData(10_000_001)]
    public void CreateNew_IterationsOutOfRange_ThrowsArgumentOutOfRangeException(int iterations)
    {
        ArgumentOutOfRangeException exception =
            Assert.Throws<ArgumentOutOfRangeException>(() => EncryptedFileHeader.CreateNew(iterations));

        Assert.Equal("iterations", exception.ParamName);
    }

    [Theory]
    [InlineData(100_000)]
    [InlineData(10_000_000)]
    public void Constructor_IterationsAtLimits_IsAccepted(int iterations)
    {
        EncryptedFileHeader header = new(iterations, KnownSalt, KnownIv);

        Assert.Equal(iterations, header.Iterations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void Constructor_SaltWithWrongLength_ThrowsArgumentException(int saltLength)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => new EncryptedFileHeader(600_000, new byte[saltLength], KnownIv));

        Assert.Equal("salt", exception.ParamName);
        Assert.Contains("exactly 16 bytes", exception.Message, StringComparison.Ordinal); // the log says what was needed
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void Constructor_IvWithWrongLength_ThrowsArgumentException(int ivLength)
    {
        ArgumentException exception =
            Assert.Throws<ArgumentException>(() => new EncryptedFileHeader(600_000, KnownSalt, new byte[ivLength]));

        Assert.Equal("iv", exception.ParamName);

        Assert.Contains("exactly 16 bytes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_CallerChangesItsArrayLater_HeaderStaysUnchanged()
    {
        // Immutability: the header keeps its own copy, so the written bytes, the tag and the keys always agree.
        byte[] salt = [.. KnownSalt];
        byte[] iv = [.. KnownIv];
        EncryptedFileHeader header = new(600_000, salt, iv);

        Array.Clear(salt);
        Array.Clear(iv);

        Assert.Equal(KnownSalt, header.Salt.ToArray());
        Assert.Equal(KnownIv, header.Iv.ToArray());
    }

    [Theory]
    [InlineData("")]                           // empty file
    [InlineData("534645")]                     // "SFE": shorter than the signature
    [InlineData("25504446")]                   // "%PDF": a normal file
    [InlineData("73666E65")]                   // "sfen": wrong case
    [InlineData("0053464E45")]                 // signature not at the start
    public void Read_NoSignature_ThrowsNotEncryptedFile(string hex)
    {
        AssertRejected(Convert.FromHexString(hex), InvalidEncryptedFileReason.NotEncryptedFile);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(255)]
    public void Read_NewerVersion_ThrowsUnsupportedVersion(byte version)
    {
        byte[] bytes = KnownHeaderBytesWith(EncryptedFileFormat.VersionOffset, version);

        AssertRejected(bytes, InvalidEncryptedFileReason.UnsupportedVersion);
    }

    [Fact]
    public void Read_NewerVersionInShortData_StillThrowsUnsupportedVersion()
    {
        // A newer format may have a different header size, so the version is judged before the length.
        byte[] bytes = Convert.FromHexString("5346454E02");

        AssertRejected(bytes, InvalidEncryptedFileReason.UnsupportedVersion);
    }

    [Fact]
    public void Read_VersionZero_ThrowsDamaged()
    {
        byte[] bytes = KnownHeaderBytesWith(EncryptedFileFormat.VersionOffset, 0);

        AssertRejected(bytes, InvalidEncryptedFileReason.Damaged);
    }

    [Theory]
    [InlineData(4)]     // signature only
    [InlineData(5)]     // signature and version
    [InlineData(9)]     // up to the iteration count
    [InlineData(40)]    // one byte short
    public void Read_HeaderCutShort_ThrowsDamaged(int length)
    {
        byte[] bytes = Convert.FromHexString(KnownHeaderHex)[..length];

        AssertRejected(bytes, InvalidEncryptedFileReason.Damaged);
    }

    [Theory]
    [InlineData("9F860100")]    // 99,999: one below the minimum
    [InlineData("81969800")]    // 10,000,001: one above the maximum
    [InlineData("00000000")]    // 0
    [InlineData("FFFFFFFF")]    // -1 as a signed number (4,294,967,295 unsigned)
    [InlineData("00000080")]    // int.MinValue
    [InlineData("FFFFFF7F")]    // int.MaxValue: would freeze the app for hours
    public void Read_IterationsOutOfRange_ThrowsDamaged(string iterationsHexLittleEndian)
    {
        byte[] bytes = Convert.FromHexString(KnownHeaderHex);
        Convert.FromHexString(iterationsHexLittleEndian).CopyTo(bytes, EncryptedFileFormat.IterationsOffset);

        AssertRejected(bytes, InvalidEncryptedFileReason.Damaged);
    }

    [Theory]
    [InlineData("A0860100", 100_000)]       // the minimum
    [InlineData("80969800", 10_000_000)]    // the maximum
    public void Read_IterationsAtLimits_IsAccepted(string iterationsHexLittleEndian, int expected)
    {
        byte[] bytes = Convert.FromHexString(KnownHeaderHex);
        Convert.FromHexString(iterationsHexLittleEndian).CopyTo(bytes, EncryptedFileFormat.IterationsOffset);

        var header = EncryptedFileHeader.Read(bytes);

        Assert.Equal(expected, header.Iterations);
    }

    [Fact]
    public void WriteThenRead_AnyValidValues_GivesBackTheSameValues()
    {
        // Property: for every allowed iteration count and every salt and IV, reading what was written is lossless.
        Gen.Select(
                Gen.Int[EncryptedFileFormat.MinimumIterations, EncryptedFileFormat.MaximumIterations],
                Gen.Byte.Array[EncryptedFileFormat.SaltLength],
                Gen.Byte.Array[EncryptedFileFormat.IvLength])
            .Sample(
                values =>
                {
                    (int iterations, byte[] salt, byte[] iv) = values;
                    byte[] buffer = new byte[EncryptedFileFormat.HeaderLength];

                    new EncryptedFileHeader(iterations, salt, iv).WriteTo(buffer);
                    var read = EncryptedFileHeader.Read(buffer);

                    Assert.Equal(iterations, read.Iterations);
                    Assert.Equal(salt, read.Salt.ToArray());
                    Assert.Equal(iv, read.Iv.ToArray());
                },
                iter: 2_000);
    }

    [Fact]
    public void ReadThenWrite_AnyValidHeaderBytes_GivesBackTheSameBytes()
    {
        // Property: the format is canonical. The bytes added to the authentication tag (the bytes read from the file)
        // are exactly the bytes this header object stands for, so there is never a "second way" to write a header.
        Gen.Select(
                Gen.Int[EncryptedFileFormat.MinimumIterations, EncryptedFileFormat.MaximumIterations],
                Gen.Byte.Array[EncryptedFileFormat.SaltLength + EncryptedFileFormat.IvLength])
            .Sample(
                values =>
                {
                    (int iterations, byte[] saltAndIv) = values;
                    byte[] original =
                        [.. "SFEN"u8, EncryptedFileFormat.CurrentVersion, .. new byte[sizeof(int)], .. saltAndIv];
                    BinaryPrimitives.WriteInt32LittleEndian(
                        original.AsSpan(EncryptedFileFormat.IterationsOffset), iterations);
                    byte[] rewritten = new byte[EncryptedFileFormat.HeaderLength];

                    EncryptedFileHeader.Read(original).WriteTo(rewritten);

                    Assert.Equal(original, rewritten);
                },
                iter: 2_000);
    }

    [Fact]
    public void Read_RandomBytes_ReturnsHeaderOrThrowsOnlyInvalidEncryptedFile()
    {
        // Fuzz test: whatever a damaged or malicious file contains, Read must never fail in an unexpected way
        // (no IndexOutOfRangeException, no crash) — only a header or our one documented exception.
        Gen.Byte.Array[0, 64].Sample(AssertReadsOrRejectsCleanly, iter: 5_000);
    }

    [Fact]
    public void Read_SignatureFollowedByRandomBytes_ReturnsHeaderOrThrowsOnlyInvalidEncryptedFile()
    {
        // Random bytes almost never start with "SFEN", so this fuzz keeps the signature and randomises the rest,
        // reaching the version, length and iteration checks. Version 1 is forced in half of the cases.
        Gen.Select(Gen.Bool, Gen.Byte.Array[0, 60])
            .Sample(
                values =>
                {
                    (bool forceVersion1, byte[] rest) = values;
                    if (forceVersion1 && rest.Length > 0)
                    {
                        rest[0] = EncryptedFileFormat.CurrentVersion;
                    }

                    AssertReadsOrRejectsCleanly([.. "SFEN"u8, .. rest]);
                },
                iter: 5_000);
    }

    private static void AssertReadsOrRejectsCleanly(byte[] bytes)
    {
        Exception? exception = Record.Exception(() => EncryptedFileHeader.Read(bytes));

        Assert.True(
            exception is null or InvalidEncryptedFileException,
            $"Unexpected {exception?.GetType().Name} for input {Convert.ToHexString(bytes)}");
    }

    private static void AssertRejected(byte[] bytes, InvalidEncryptedFileReason expectedReason)
    {
        InvalidEncryptedFileException exception =
            Assert.Throws<InvalidEncryptedFileException>(() => EncryptedFileHeader.Read(bytes));

        Assert.Equal(expectedReason, exception.Reason);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), "The exception must explain the problem (it is logged).");
    }

    private static byte[] KnownHeaderBytesWith(int offset, byte value)
    {
        byte[] bytes = Convert.FromHexString(KnownHeaderHex);
        bytes[offset] = value;
        return bytes;
    }
}

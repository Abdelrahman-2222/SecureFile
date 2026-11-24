using System.Diagnostics;
using System.Security.Cryptography;
using SecureFile.Core.Cryptography;

namespace SecureFile.Integration.Tests;

/// <summary>
/// Round trips of very large files (test catalog T-27, plan Step 4.2): the result must equal the original and memory
/// must stay flat, which proves the file is streamed in chunks and never loaded whole.
/// </summary>
/// <remarks>
/// <para>
/// They run only with <c>SECUREFILE_LARGE_TESTS=1</c>: a 4.5 GB run takes minutes and needs about 14 GB of free disk
/// space, so it belongs to a manual or nightly run, not to every commit. <c>SECUREFILE_LARGE_DIR</c> picks the folder
/// (default: the system temp folder). Use <c>scripts/large-file-check.ps1</c> to run them.
/// </para>
/// <para>
/// Memory is read from the test process's peak working set. It includes the test runner itself, so the limit is
/// generous (400 MB): a version that loaded the file would need at least the file's size (1 GB and 4.5 GB).
/// </para>
/// </remarks>
public sealed class LargeFileTests : IDisposable
{
    private const long OneMegabyte = 1024L * 1024;
    private const long PeakMemoryLimitBytes = 400 * OneMegabyte;

    private readonly string _folder;

    public LargeFileTests()
    {
        string root = Environment.GetEnvironmentVariable("SECUREFILE_LARGE_DIR") ?? Path.GetTempPath();
        _folder = Path.Combine(root, "SecureFile-large-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [Theory]
    [InlineData(1024)] // 1 GiB
    [InlineData(4608)] // 4.5 GiB: more than 4 GB, so any 32-bit size or offset would break
    public async Task EncryptThenDecrypt_LargeFile_GivesTheSameBytesWithFlatMemory(int megabytes)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("SECUREFILE_LARGE_TESTS") == "1", "Set SECUREFILE_LARGE_TESTS=1 to run the large-file tests.");
        string original = Path.Combine(_folder, "large.bin");
        string encrypted = original + ".enc";
        string restored = Path.Combine(_folder, "restored.bin");
        CancellationToken token = TestContext.Current.CancellationToken;
        await WriteTestFileAsync(original, megabytes, token);
        string expectedHash = await HashAsync(original, token);
        var service = new FileEncryptionService();

        var encryptWatch = Stopwatch.StartNew();
        await service.EncryptAsync(original, encrypted, "large file passphrase", overwriteExisting: false, cancellationToken: token);
        encryptWatch.Stop();
        var decryptWatch = Stopwatch.StartNew();
        await service.DecryptAsync(encrypted, restored, "large file passphrase", overwriteExisting: false, cancellationToken: token);
        decryptWatch.Stop();

        Assert.Equal(expectedHash, await HashAsync(restored, token));
        long peak = Process.GetCurrentProcess().PeakWorkingSet64;
        TestContext.Current.SendDiagnosticMessage(
            $"{megabytes} MB: encrypt {encryptWatch.Elapsed.TotalSeconds:0.0} s ({megabytes / encryptWatch.Elapsed.TotalSeconds:0} MB/s), "
            + $"decrypt {decryptWatch.Elapsed.TotalSeconds:0.0} s ({megabytes / decryptWatch.Elapsed.TotalSeconds:0} MB/s), "
            + $"peak working set {peak / OneMegabyte} MB");
        Assert.True(peak < PeakMemoryLimitBytes, $"Peak working set was {peak / OneMegabyte} MB for a {megabytes} MB file: the file is probably being loaded whole.");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover multi-gigabyte folder is reported by the script, not by a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    /// <summary>Writes a file from one random megabyte repeated, with the block number mixed in, so blocks differ.</summary>
    private static async Task WriteTestFileAsync(string path, int megabytes, CancellationToken token)
    {
        byte[] block = RandomNumberGenerator.GetBytes((int)OneMegabyte);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
        for (int i = 0; i < megabytes; i++)
        {
            BitConverter.TryWriteBytes(block, i);
            await stream.WriteAsync(block, token);
        }
    }

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }
}

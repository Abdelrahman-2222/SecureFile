using BenchmarkDotNet.Attributes;
using SecureFile.Core.Cryptography;

namespace SecureFile.Benchmarks;

/// <summary>
/// Whole-file throughput of encrypt and decrypt against plain buffered copies on the same disk.
/// </summary>
/// <remarks>
/// The work folder is <c>SECUREFILE_BENCH_DIR</c> (default: the temp folder), so the copy and the encryption hit the
/// same disk. MB/s = file size / mean time. The 1 GB case runs only when <c>SECUREFILE_BENCH_1GB=1</c>.
/// </remarks>
[Config(typeof(BenchmarkConfig))]
public class ThroughputBenchmarks
{
    private const string Password = "correct horse battery staple";
    private const int CopyBufferSize = 81920;

    private string _folder = null!;
    private string _plain = null!;
    private string _encrypted = null!;
    private string _output = null!;
    private FileEncryptionService _service = null!;

    [ParamsSource(nameof(Sizes))]
    public int SizeMb { get; set; }

    public static IEnumerable<int> Sizes() => BenchmarkFiles.Sizes();

    [GlobalSetup]
    public void Setup()
    {
        _folder = BenchmarkFiles.CreateWorkFolder();
        _plain = Path.Combine(_folder, "plain.bin");
        _encrypted = Path.Combine(_folder, "plain.bin.enc");
        _output = Path.Combine(_folder, "output.bin");
        _service = new FileEncryptionService();

        BenchmarkFiles.WriteRandomFile(_plain, (long)SizeMb * 1024 * 1024);
        _service.EncryptAsync(_plain, _encrypted, Password, overwriteExisting: true).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Directory.Delete(_folder, recursive: true);
    }

    [Benchmark(Baseline = true)]
    public void PlainCopy() => Copy(flushToDisk: false);

    /// <summary>The fairer baseline: the same copy, forced onto the disk (fsync) like the encryptor's atomic writer.</summary>
    [Benchmark]
    public void DurableCopy() => Copy(flushToDisk: true);

    [Benchmark]
    public void Encrypt()
    {
        _service.EncryptAsync(_plain, _output, Password, overwriteExisting: true).GetAwaiter().GetResult();
    }

    [Benchmark]
    public void Decrypt()
    {
        _service.DecryptAsync(_encrypted, _output, Password, overwriteExisting: true).GetAwaiter().GetResult();
    }

    private void Copy(bool flushToDisk)
    {
        using var source = new FileStream(_plain, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize);
        using var target = new FileStream(_output, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize);
        source.CopyTo(target, CopyBufferSize);
        if (flushToDisk)
        {
            target.Flush(flushToDisk: true);
        }
    }
}

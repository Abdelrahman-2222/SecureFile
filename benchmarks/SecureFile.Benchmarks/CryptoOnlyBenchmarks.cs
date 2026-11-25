using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using SecureFile.Core.Cryptography;

namespace SecureFile.Benchmarks;

/// <summary>The CPU part alone: AES-256-CBC + HMAC-SHA256 over 1 MiB chunks in memory, no disk.</summary>
/// <remarks>It is the upper limit for encryption speed; the gap to the real encrypt time is disk, fsync and bookkeeping.</remarks>
[Config(typeof(BenchmarkConfig))]
public class CryptoOnlyBenchmarks
{
    private FileKeys _keys = null!;

    [ParamsSource(nameof(Sizes))]
    public int SizeMb { get; set; }

    public static IEnumerable<int> Sizes() => BenchmarkFiles.Sizes();

    [GlobalSetup]
    public void Setup()
    {
        var header = EncryptedFileHeader.CreateNew(EncryptedFileFormat.MinimumIterations);
        _keys = KeyDerivation.DeriveKeys("correct horse battery staple".AsSpan(), header);
    }

    [GlobalCleanup]
    public void Cleanup() => _keys.Dispose();

    [Benchmark]
    public long AesCbcPlusHmac()
    {
        using ICryptoTransform encryptor = AesCbc.CreateEncryptor(_keys.EncryptionKey, new byte[16]);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, _keys.AuthenticationKey);
        byte[] input = new byte[1024 * 1024];
        byte[] output = new byte[1024 * 1024];
        long total = 0;
        for (int i = 0; i < SizeMb; i++)
        {
            int written = encryptor.TransformBlock(input, 0, input.Length, output, 0);
            hmac.AppendData(output, 0, written);
            total += written;
        }

        return total;
    }
}

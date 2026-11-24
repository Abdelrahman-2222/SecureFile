using BenchmarkDotNet.Attributes;
using SecureFile.Core.Cryptography;

namespace SecureFile.Benchmarks;

/// <summary>Time of one key derivation (PBKDF2-SHA256 with the production iteration count, plus the HKDF split).</summary>
/// <remarks>The plan target is "reported, about 0.3-1 s": this delay is the security cost that slows password guessing.</remarks>
[Config(typeof(BenchmarkConfig))]
public class KeyDerivationBenchmarks
{
    private EncryptedFileHeader _header = null!;

    [GlobalSetup]
    public void Setup()
    {
        _header = EncryptedFileHeader.CreateNew(EncryptedFileFormat.DefaultIterations);
    }

    [Benchmark]
    public int DeriveKeys_600k()
    {
        using FileKeys keys = KeyDerivation.DeriveKeys("correct horse battery staple".AsSpan(), _header);
        return _header.Iterations;
    }
}

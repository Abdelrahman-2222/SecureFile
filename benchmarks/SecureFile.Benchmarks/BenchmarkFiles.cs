namespace SecureFile.Benchmarks;

/// <summary>Helpers shared by the file benchmarks: sizes, work folder and test data.</summary>
internal static class BenchmarkFiles
{
    /// <summary>The file sizes in MB: 1 and 100, plus 1024 when <c>SECUREFILE_BENCH_1GB=1</c>.</summary>
    public static IEnumerable<int> Sizes()
    {
        yield return 1;
        yield return 100;
        if (Environment.GetEnvironmentVariable("SECUREFILE_BENCH_1GB") == "1")
        {
            yield return 1024;
        }
    }

    /// <summary>Creates a fresh work folder inside <c>SECUREFILE_BENCH_DIR</c> (default: the temp folder).</summary>
    public static string CreateWorkFolder()
    {
        string root = Environment.GetEnvironmentVariable("SECUREFILE_BENCH_DIR") ?? Path.GetTempPath();
        string folder = Path.Combine(root, "SecureFile-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Writes <paramref name="length"/> pseudo-random bytes (fixed seed, so runs are comparable).</summary>
    public static void WriteRandomFile(string path, long length)
    {
        byte[] chunk = new byte[1024 * 1024];
        var random = new Random(12345);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        for (long written = 0; written < length; written += chunk.Length)
        {
            random.NextBytes(chunk);
            stream.Write(chunk, 0, (int)Math.Min(chunk.Length, length - written));
        }
    }
}

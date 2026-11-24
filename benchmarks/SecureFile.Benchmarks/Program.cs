using BenchmarkDotNet.Running;

namespace SecureFile.Benchmarks;

/// <summary>Entry point: lets BenchmarkDotNet pick the benchmark class from the command line (--filter).</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}

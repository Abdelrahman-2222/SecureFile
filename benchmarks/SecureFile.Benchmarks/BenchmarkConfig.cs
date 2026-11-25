using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;

namespace SecureFile.Benchmarks;

/// <summary>Shared settings: memory diagnoser, median/p95 columns and GitHub-markdown output for the report.</summary>
/// <remarks>
/// Whole-file benchmarks run one invocation per iteration (<c>InvocationCount = 1</c>) because a single call already
/// takes long; a handful of iterations is enough to see the disk speed and keeps the run short.
/// </remarks>
public sealed class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddJob(Job.Default
            .WithWarmupCount(1)
            .WithIterationCount(8)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.Median, StatisticColumn.P95);
        AddExporter(MarkdownExporter.GitHub, CsvExporter.Default);
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-8750H CPU 2.20GHz (Max: 2.21GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  Job-ZJPUIJ : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=8  UnrollFactor=1  
WarmupCount=1  

```
| Method          | Mean     | Error    | StdDev  | Median   | P95      | Allocated |
|---------------- |---------:|---------:|--------:|---------:|---------:|----------:|
| DeriveKeys_600k | 365.3 ms | 12.53 ms | 6.55 ms | 363.9 ms | 374.6 ms |   1.31 KB |

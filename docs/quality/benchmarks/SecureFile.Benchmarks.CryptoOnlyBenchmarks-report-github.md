```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core i7-8750H CPU 2.20GHz (Max: 2.21GHz) (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 10.0.404
  [Host]     : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3
  Job-ZJPUIJ : .NET 10.0.15 (10.0.15, 10.0.1526.53008), X64 RyuJIT x86-64-v3

InvocationCount=1  IterationCount=8  UnrollFactor=1  
WarmupCount=1  

```
| Method         | SizeMb | Mean       | Error     | StdDev    | Median     | P95        | Allocated |
|--------------- |------- |-----------:|----------:|----------:|-----------:|-----------:|----------:|
| **AesCbcPlusHmac** | **1**      |   **5.932 ms** | **0.0877 ms** | **0.0389 ms** |   **5.940 ms** |   **5.972 ms** |      **2 MB** |
| **AesCbcPlusHmac** | **100**    | **517.842 ms** | **6.5563 ms** | **3.4291 ms** | **517.354 ms** | **522.401 ms** |      **2 MB** |

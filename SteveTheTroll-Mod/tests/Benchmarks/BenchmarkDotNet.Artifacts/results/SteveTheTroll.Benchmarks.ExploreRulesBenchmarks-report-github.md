```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  ShortRun : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                     | Mean      | Error     | StdDev   | Ratio | RatioSD |
|--------------------------- |----------:|----------:|---------:|------:|--------:|
| Next_NothingBlocked        |  70.83 ns |  2.116 ns | 0.116 ns |  1.00 |    0.00 |
| Next_MostDirectionsBlocked | 465.38 ns | 52.180 ns | 2.860 ns |  6.57 |    0.04 |

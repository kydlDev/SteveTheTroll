```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  ShortRun : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method | Mean     | Error     | StdDev    |
|------- |---------:|----------:|----------:|
| Update | 2.415 ns | 0.1984 ns | 0.0109 ns |

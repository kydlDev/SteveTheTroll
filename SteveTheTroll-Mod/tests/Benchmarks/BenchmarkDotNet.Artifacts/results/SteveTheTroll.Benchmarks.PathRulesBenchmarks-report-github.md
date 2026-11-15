```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  ShortRun : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method      | Mean     | Error    | StdDev   |
|------------ |---------:|---------:|---------:|
| PushThrough | 53.66 ns | 2.780 ns | 0.152 ns |
| FeelerScore | 32.28 ns | 5.172 ns | 0.284 ns |

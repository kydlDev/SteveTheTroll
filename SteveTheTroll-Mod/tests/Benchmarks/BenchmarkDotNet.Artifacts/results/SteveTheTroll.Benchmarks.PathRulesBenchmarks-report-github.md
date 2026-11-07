```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method      | Mean     | Error    | StdDev   |
|------------ |---------:|---------:|---------:|
| PushThrough | 54.78 ns | 1.106 ns | 1.316 ns |
| FeelerScore | 33.96 ns | 0.628 ns | 0.723 ns |

```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method        | Mean      | Error     | StdDev    |
|-------------- |----------:|----------:|----------:|
| PickCandidate |  3.396 ns | 0.0874 ns | 0.1334 ns |
| Rotate        | 68.560 ns | 1.3545 ns | 2.0273 ns |

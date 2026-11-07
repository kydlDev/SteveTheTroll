```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method                     | Mean      | Error    | StdDev    | Ratio | RatioSD |
|--------------------------- |----------:|---------:|----------:|------:|--------:|
| Next_NothingBlocked        |  72.78 ns | 1.475 ns |  1.866 ns |  1.00 |    0.04 |
| Next_MostDirectionsBlocked | 468.64 ns | 9.335 ns | 14.533 ns |  6.44 |    0.25 |

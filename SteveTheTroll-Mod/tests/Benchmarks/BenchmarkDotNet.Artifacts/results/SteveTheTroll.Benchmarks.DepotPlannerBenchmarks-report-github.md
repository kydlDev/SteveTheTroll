```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method | Chests | Mean        | Error     | StdDev    | Median      | Allocated |
|------- |------- |------------:|----------:|----------:|------------:|----------:|
| **Choose** | **8**      |    **26.68 ns** |  **0.516 ns** |  **0.483 ns** |    **26.54 ns** |         **-** |
| **Choose** | **64**     |   **202.10 ns** |  **5.092 ns** | **15.013 ns** |   **195.85 ns** |         **-** |
| **Choose** | **512**    | **1,618.61 ns** | **17.716 ns** | **18.193 ns** | **1,612.48 ns** |         **-** |

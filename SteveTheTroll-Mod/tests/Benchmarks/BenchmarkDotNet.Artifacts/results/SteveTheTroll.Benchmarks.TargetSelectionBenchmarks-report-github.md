```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  DefaultJob : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256


```
| Method                 | Goals | Mean         | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------- |------ |-------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **FirstHit_LastGoalWins**  | **10**    |    **171.96 ns** |   **3.433 ns** |   **4.924 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 10    |     15.31 ns |   0.313 ns |   0.322 ns |  0.09 |    0.00 |         - |          NA |
|                        |       |              |            |            |       |         |           |             |
| **FirstHit_LastGoalWins**  | **100**   |  **1,624.06 ns** |  **32.380 ns** |  **49.448 ns** | **1.001** |    **0.04** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 100   |     15.18 ns |   0.264 ns |   0.283 ns | 0.009 |    0.00 |         - |          NA |
|                        |       |              |            |            |       |         |           |             |
| **FirstHit_LastGoalWins**  | **1000**  | **15,726.18 ns** | **279.195 ns** | **261.159 ns** | **1.000** |    **0.02** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 1000  |     15.22 ns |   0.239 ns |   0.211 ns | 0.001 |    0.00 |         - |          NA |

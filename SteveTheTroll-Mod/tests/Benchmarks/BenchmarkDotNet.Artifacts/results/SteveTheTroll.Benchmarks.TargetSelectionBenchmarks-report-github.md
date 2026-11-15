```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  ShortRun : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method                 | Goals | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|----------------------- |------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **FirstHit_LastGoalWins**  | **10**    |    **160.02 ns** |   **8.545 ns** |  **0.468 ns** |  **1.00** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 10    |     15.06 ns |   1.055 ns |  0.058 ns |  0.09 |         - |          NA |
|                        |       |              |            |           |       |           |             |
| **FirstHit_LastGoalWins**  | **100**   |  **1,499.32 ns** |  **43.561 ns** |  **2.388 ns** |  **1.00** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 100   |     15.04 ns |   0.562 ns |  0.031 ns |  0.01 |         - |          NA |
|                        |       |              |            |           |       |           |             |
| **FirstHit_LastGoalWins**  | **1000**  | **14,903.22 ns** | **795.034 ns** | **43.578 ns** | **1.000** |         **-** |          **NA** |
| FirstHit_FirstGoalWins | 1000  |     15.00 ns |   0.299 ns |  0.016 ns | 0.001 |         - |          NA |

```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6466/22H2/2022Update)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
  [Host]   : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256
  ShortRun : .NET Framework 4.8.1 (4.8.9310.0), X64 RyuJIT VectorSize=256

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method | Chests | Mean        | Error     | StdDev   | Allocated |
|------- |------- |------------:|----------:|---------:|----------:|
| **Choose** | **8**      |    **28.03 ns** |  **6.139 ns** | **0.337 ns** |         **-** |
| **Choose** | **64**     |   **202.93 ns** |  **7.416 ns** | **0.406 ns** |         **-** |
| **Choose** | **512**    | **1,726.91 ns** | **35.192 ns** | **1.929 ns** |         **-** |

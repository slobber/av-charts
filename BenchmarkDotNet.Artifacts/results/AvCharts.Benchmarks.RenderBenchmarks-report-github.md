```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26300.9457)
12th Gen Intel Core i9-12900H 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  InvocationCount=1  IterationCount=3  
LaunchCount=1  UnrollFactor=1  WarmupCount=3  

```
| Method        | Mean        | Error       | StdDev     | Allocated |
|-------------- |------------:|------------:|-----------:|----------:|
| FirstRender   | 30,008.4 μs | 60,249.4 μs | 3,302.5 μs |   4.14 MB |
| PanFrame      | 25,564.1 μs | 29,917.4 μs | 1,639.9 μs |   4.15 MB |
| PushFrame     |    842.8 μs |  1,850.7 μs |   101.4 μs |   1.85 MB |
| SwitchVariant | 28,224.3 μs | 26,154.6 μs | 1,433.6 μs |   4.19 MB |

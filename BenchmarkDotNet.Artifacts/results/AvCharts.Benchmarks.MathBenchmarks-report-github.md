```

BenchmarkDotNet v0.15.6, Windows 11 (10.0.26300.9457)
12th Gen Intel Core i9-12900H 2.50GHz, 1 CPU, 20 logical and 14 physical cores
.NET SDK 10.0.401
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method         | Mean           | Error           | StdDev        | Gen0   | Allocated |
|--------------- |---------------:|----------------:|--------------:|-------:|----------:|
| ReadValues     | 2,162,101.0 ns |   852,543.18 ns |  46,730.77 ns |      - |    3184 B |
| DescribeData   | 1,694,461.1 ns |   625,170.14 ns |  34,267.69 ns |      - |    3104 B |
| Map            | 9,170,388.3 ns | 4,781,739.53 ns | 262,103.28 ns |      - |      48 B |
| SampleMinMax   |   571,455.5 ns |   376,390.94 ns |  20,631.26 ns | 1.9531 |   28928 B |
| SampleLttb     |   544,398.3 ns |    97,232.60 ns |   5,329.65 ns | 0.4883 |    9656 B |
| ResolveAxis    |       382.1 ns |        40.67 ns |       2.23 ns | 0.1464 |    1840 B |
| FindDataPoints |    70,080.0 ns |    12,951.66 ns |     709.92 ns |      - |      88 B |

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4


```
| Method          | Mean      | Error     | StdDev    | Gen0      | Allocated |
|---------------- |----------:|----------:|----------:|----------:|----------:|
| StellaErgosfare | 31.637 ms | 0.2995 ms | 0.2655 ms | 2312.5000 | 112.92 MB |
| MediatR         |  6.209 ms | 0.1234 ms | 0.2630 ms |  375.0000 |  18.31 MB |

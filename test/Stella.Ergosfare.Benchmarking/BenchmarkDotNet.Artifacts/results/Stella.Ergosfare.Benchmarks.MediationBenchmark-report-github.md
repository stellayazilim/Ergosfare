```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8875/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4


```
| Method                    | Mean       | Error     | StdDev    | Gen0       | Allocated |
|-------------------------- |-----------:|----------:|----------:|-----------:|----------:|
| StellaErgosfare           |   4.676 ms | 0.0912 ms | 0.1050 ms |    62.5000 |   3.05 MB |
| StellaErgosfare_PublicApi |   5.329 ms | 0.0396 ms | 0.0331 ms |   187.5000 |   9.16 MB |
| MediatR                   |   5.881 ms | 0.0162 ms | 0.0136 ms |   375.0000 |  18.31 MB |
| LiteBus_PublicApi         | 156.421 ms | 3.0076 ms | 2.9538 ms | 14750.0000 | 714.87 MB |

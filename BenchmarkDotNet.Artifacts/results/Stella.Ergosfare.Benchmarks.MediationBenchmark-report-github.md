```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method          | Mean     | Error   | StdDev  | Gen0   | Allocated |
|---------------- |---------:|--------:|--------:|-------:|----------:|
| StellaErgosfare | 758.4 ns | 2.20 ns | 1.72 ns | 0.0439 |    1032 B |
| MediatR         | 141.2 ns | 0.29 ns | 0.26 ns | 0.0052 |     128 B |

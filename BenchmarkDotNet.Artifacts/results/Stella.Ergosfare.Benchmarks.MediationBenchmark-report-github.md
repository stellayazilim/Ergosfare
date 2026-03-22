```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|---------------- |-----------:|--------:|--------:|-------:|----------:|
| StellaErgosfare | 1,336.1 ns | 9.72 ns | 9.09 ns | 0.0801 |    1912 B |
| MediatR         |   146.9 ns | 0.31 ns | 0.26 ns | 0.0052 |     128 B |

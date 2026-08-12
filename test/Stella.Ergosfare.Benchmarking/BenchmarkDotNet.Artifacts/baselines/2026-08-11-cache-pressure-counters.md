# Cache-pressure counter baseline - 2026-08-11

The elevated capture the 2026-08-09 baseline asked for, taken on the frozen-dependencies
stack (PR #162 tip) from an administrator console - the first run with the counter columns
filled. Same machine caveat applies: the 7800X3D's 96 MB L3 hides misses a small-cache
server would feel.

Reading: CacheMisses/Op ~0 and BranchMispredictions/Op ~0 across every lane - the cost of
a dispatch on this host is purely instruction count. Command_Void's counters read NA
(first-process ETW session startup); Query_Result (677 instructions) is the same lane's
result shape.
```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8973/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4
  SingleCore : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4

Job=SingleCore  Affinity=0000000000000001  

```
| Method                 | Categories   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0   | CacheMisses/Op | InstructionRetired/Op | BranchMispredictions/Op | Allocated | Alloc Ratio |
|----------------------- |------------- |----------:|----------:|----------:|----------:|------:|--------:|-------:|---------------:|----------------------:|------------------------:|----------:|------------:|
| Command_Void           | Command_Void | 36.696 ns | 0.7698 ns | 2.1712 ns | 35.935 ns |  1.00 |    0.08 | 0.0005 |             NA |                    NA |                      NA |      24 B |        1.00 |
| Command_Void_Generated | Command_Void | 24.360 ns | 0.5035 ns | 0.9333 ns | 24.016 ns |  0.67 |    0.04 | 0.0005 |              0 |                   345 |                       0 |      24 B |        1.00 |
| Command_Void_Memoized  | Command_Void | 27.831 ns | 0.3739 ns | 0.3315 ns | 27.681 ns |  0.76 |    0.04 |      - |              0 |                   408 |                       0 |         - |        0.00 |
| MediatR_Send_Void      | Command_Void | 80.808 ns | 1.6411 ns | 3.2393 ns | 79.905 ns |  2.21 |    0.15 | 0.0038 |              1 |                 1,171 |                       0 |     192 B |        8.00 |
| MediatorSg_Send_Void   | Command_Void | 10.788 ns | 0.3491 ns | 0.9959 ns | 10.591 ns |  0.29 |    0.03 |      - |              0 |                   171 |                       0 |         - |        0.00 |
|                        |              |           |           |           |           |       |         |        |                |                       |                         |           |             |
| Query_Result           | Query_Result | 43.505 ns | 0.8950 ns | 2.0919 ns | 43.251 ns |  1.00 |    0.07 | 0.0005 |              0 |                   677 |                       0 |      24 B |        1.00 |
| Query_Result_Generated | Query_Result | 29.781 ns | 0.6247 ns | 1.8222 ns | 29.351 ns |  0.69 |    0.05 | 0.0005 |              0 |                   428 |                       0 |      24 B |        1.00 |
| Query_Result_Memoized  | Query_Result | 33.346 ns | 0.6633 ns | 1.7122 ns | 33.118 ns |  0.77 |    0.05 |      - |              0 |                   502 |                       0 |         - |        0.00 |
| MediatR_Send_Result    | Query_Result | 65.217 ns | 0.8982 ns | 0.7963 ns | 65.118 ns |  1.50 |    0.07 | 0.0038 |              1 |                 1,035 |                       0 |     192 B |        8.00 |
| MediatorSg_Send_Result | Query_Result |  9.219 ns | 0.2224 ns | 0.6418 ns |  9.008 ns |  0.21 |    0.02 |      - |              0 |                   161 |                       0 |         - |        0.00 |


# Cache-pressure baseline — 2026-08-09

The "before" picture for the core modernization, captured from `CachePressureBenchmark`
on the day Phase 0 wired the safety net, before any surgery. Re-run it after every large
merge and compare against this file.

```bash
dotnet run -c Release -f net9.0 --project test/Stella.Ergosfare.Benchmarking -- --filter '*CachePressure*'
```

## How to read it

- **Rows are lanes, not libraries.** Ergosfare appears three times — `Command_Void` /
  `Query_Result` is the runtime-registered default (transient handler per dispatch),
  `*_Generated` is the compile-time dispatch root and plan, `*_Memoized` resolves the
  handler graph once. MediatR and martinothamar/Mediator run their own defaults.
- **The baseline of each group is Ergosfare's default lane**, so `Ratio` reads as "what
  does this lane, or this library, cost relative to a plain runtime-registered dispatch".
- **Numbers here are not comparable to `MediationBenchmark`'s rows.** Everything below is
  pinned to one logical core; the mediation table runs free-floating. Compare this file
  only against later runs of this same benchmark.

## Hardware counters: NOT collected in this run

The counter columns (cache misses, branch mispredictions, retired instructions) are
absent because the capture ran from an **unelevated** console. BenchmarkDotNet reads them
through ETW kernel sessions, which need administrator rights on Windows; `CachePressureConfig`
attaches the counters only when the process can actually open that session, so an
unelevated run still yields the timing and allocation columns instead of failing
validation with nothing to show.

**To fill in the counter columns, run the same command from an administrator console.**
That capture is the one that answers the question this gate exists for — whether a
redesign paid for its speed with instruction-cache footprint or an extra unpredictable
branch — and it should be taken before the surgery starts.

## Machine caveat

The capture host is an AMD Ryzen 7 7800X3D: 96 MB of stacked L3. A working set that
thrashes on a typical single-core server can sit comfortably in this cache, so the
absolute miss rates measured here are optimistic. Deltas between rows on the same host
are still the signal; absolute numbers are not a claim about production hardware.

## Result

```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8973/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 7800X3D 4.20GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4
  SingleCore : .NET 9.0.11 (9.0.11, 9.0.1125.51716), X64 RyuJIT x86-64-v4

Job=SingleCore  Affinity=0000000000000001  

```
| Method                 | Categories   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------------------- |------------- |----------:|----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Command_Void           | Command_Void | 36.312 ns | 0.7547 ns | 2.0914 ns | 35.717 ns |  1.00 |    0.08 | 0.0005 |      24 B |        1.00 |
| Command_Void_Generated | Command_Void | 22.654 ns | 0.4759 ns | 1.1493 ns | 22.309 ns |  0.63 |    0.05 | 0.0005 |      24 B |        1.00 |
| Command_Void_Memoized  | Command_Void | 23.799 ns | 0.3128 ns | 0.2773 ns | 23.792 ns |  0.66 |    0.04 |      - |         - |        0.00 |
| MediatR_Send_Void      | Command_Void | 65.468 ns | 1.5189 ns | 4.3581 ns | 64.638 ns |  1.81 |    0.15 | 0.0038 |     192 B |        8.00 |
| MediatorSg_Send_Void   | Command_Void |  8.417 ns | 0.1882 ns | 0.2092 ns |  8.366 ns |  0.23 |    0.01 |      - |         - |        0.00 |
|                        |              |           |           |           |           |       |         |        |           |             |
| Query_Result           | Query_Result | 40.052 ns | 0.8225 ns | 2.3600 ns | 39.832 ns |  1.00 |    0.08 | 0.0005 |      24 B |        1.00 |
| Query_Result_Generated | Query_Result | 26.476 ns | 0.5733 ns | 1.6356 ns | 26.270 ns |  0.66 |    0.06 | 0.0005 |      24 B |        1.00 |
| Query_Result_Memoized  | Query_Result | 34.816 ns | 0.2926 ns | 0.2594 ns | 34.757 ns |  0.87 |    0.05 |      - |         - |        0.00 |
| MediatR_Send_Result    | Query_Result | 56.405 ns | 1.1104 ns | 0.9273 ns | 56.321 ns |  1.41 |    0.08 | 0.0038 |     192 B |        8.00 |
| MediatorSg_Send_Result | Query_Result |  8.789 ns | 0.2018 ns | 0.5888 ns |  8.573 ns |  0.22 |    0.02 |      - |         - |        0.00 |

## What the "before" picture says

- Both compiled lanes land ~35 % under the runtime-registered default on one core, which
  is the modernization's starting margin: whatever replaces the six dispatch generations
  has to keep at least this.
- `Query_Result_Memoized` (34.8 ns) is barely under the default (40.1 ns) and well over
  the generated lane (26.5 ns) — the memoized gate closing over the faster lane, the same
  inversion the five-participant pipeline rows show. It is a target, not noise.
- The gap to martinothamar/Mediator (~8.5 ns, both shapes) is unchanged by core pinning,
  so it is structural — execution context, registry and DI resolution — not a scheduling
  artefact.

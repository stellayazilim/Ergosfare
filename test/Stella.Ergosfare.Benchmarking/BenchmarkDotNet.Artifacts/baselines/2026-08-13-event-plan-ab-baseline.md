# Event plan A/B baseline — 2026-08-13

The measurement the compiled-plan surgery owed. Until this run the bench had no intercepted
event at all: its only event (`PingEvent`) carries no interceptor, so no plan was ever
emitted for it and the surgery's event work went entirely unmeasured.

Taken on `preview-dev` at `daf66fe`, one full `MediationBenchmark` table in a single run, so
every row below — the A/B pair and the control rows alike — comes from the same table and no
run-to-run drift separates them.

## The A/B

Two event types with the same pipeline shape: two handlers, one pass-through pre- and one
pass-through post-interceptor. They differ in exactly one thing — `UnplannedPingEvent`'s
handlers are nested types, and a nested participant disqualifies a staged plan
(`TryAssembleBroadcastHandlers`: the runtime orders segments by `Type.FullName`, whose `+`
nesting separator sorts differently from the display name's dot, so the generator refuses to
bake an order it cannot guarantee). Both are published through the *same* generated provider.

`SetupGenerated` asserts the premise rather than assuming it: `FindBroadcastPlan` must answer
for the planned twin and must not for the unplanned one. Without that assert the pair
degrades silently into two rows measuring the same lane — which would read as "the plan
bought nothing".

| Row | Lane | Mean | Allocated |
|---|---|---:|---:|
| `Event_Publish_Intercepted_Generated` | compiled broadcast plan | **75.48 ns** | 48 B |
| `Event_Publish_Intercepted_Unplanned` | runtime strategy | **200.75 ns** | 96 B |

**2.66× faster, allocation halved.** The plan lands an intercepted publish (75.5 ns) at
essentially the cost of an interceptor-*free* one (`Event_Publish`, 70.5 ns): the interceptor
stages stop costing a pipeline and start costing two calls.

The independently-registered strategy row cross-checks the unplanned twin: `Event_Publish_Intercepted`
(runtime provider, its own process) reads 192.16 ns against the twin's 200.75 ns — the same
lane within ~4%, the gap being the generated provider's larger registry.

## The rows

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| `Command_Void` | 32.70 ns | 1.00 | 24 B |
| `Command_Void_Generated` | 23.54 ns | 0.72 | 24 B |
| `Command_Void_Memoized` | 25.68 ns | 0.79 | – |
| `Command_Void_Intercepted` | 164.36 ns | 5.03 | 72 B |
| `Command_Void_Intercepted_Generated` | 52.68 ns | 1.61 | 24 B |
| `Query_Result` | 33.79 ns | 1.03 | 24 B |
| `Query_Result_Generated` | 22.13 ns | 0.68 | 24 B |
| `Query_Result_Memoized` | 25.27 ns | 0.77 | – |
| `Query_Result_Intercepted` | 182.81 ns | 5.59 | 120 B |
| `Query_Result_Intercepted_Generated` | 78.40 ns | 2.40 | 72 B |
| `Query_Result_Pipeline5` | 278.53 ns | 8.52 | 192 B |
| `Query_Result_Pipeline5_Generated` | 108.90 ns | 3.33 | 96 B |
| `Query_Result_Pipeline5_Memoized` | 204.59 ns | 6.26 | 72 B |
| `Command_Void_Grouped` | 36.11 ns | 1.10 | 24 B |
| `Command_Void_Grouped_GroupSet` | 34.14 ns | 1.04 | 24 B |
| **`Event_Publish`** | **70.55 ns** | 2.16 | 48 B |
| **`Event_Publish_Intercepted`** | **192.16 ns** | 5.88 | 96 B |
| **`Event_Publish_Intercepted_Generated`** | **75.48 ns** | 2.31 | 48 B |
| **`Event_Publish_Intercepted_Unplanned`** | **200.75 ns** | 6.14 | 96 B |
| `Event_Publish_Grouped` | 64.73 ns | 1.98 | 48 B |
| `Event_Publish_Grouped_GroupSet` | 66.02 ns | 2.02 | 48 B |

Competitor rows (`MediatR_*`, `MediatorSg_*`) and the `Scoped` category ran unchanged; see
the run's own report for them.

## Reading notes / traps

* **The broadcast participants increment a static counter (`BroadcastTouch`), and that is not
  incidental.** A plan bakes its handler calls straight-line and devirtualized, so an empty
  handler body would inline away entirely and the plan row would measure an empty publish.
  Both A/B arms pay the same four increments, so the ratio is unaffected — but
  `Event_Publish_Intercepted` is therefore a couple of nanoseconds off a strict comparison
  against the counter-free `Event_Publish`.
* **The generated provider now registers the event module too** (`AddEventModule(events =>
  events.RegisterGenerated())`), which it did not before this run. `Command_Void_Generated`
  and `Query_Result_Generated` may sit slightly differently from the 2026-08-11 capture for
  that reason; within this table they are consistent.
* **`Event_Publish_Grouped` (64.73 ns) is *faster* than `Event_Publish` (70.55 ns).** Not
  chased here. Worth remembering when grouped-keyed plans (the next-session item) are
  measured: the grouped publish is not the slow lane the "grouped dispatch sees no plan"
  framing might suggest.
* Separate BDN runs carry a ±2.7 ns systematic drift on this host (2026-08-09 methodology
  note). Nothing above should be compared across runs; the A/B is deliberately built so it
  never has to be.

## Addendum — what later runs in the same session showed about drift

Three more full runs followed on the same host that day, after the dispatch-table work. They
are not recorded as baselines, because they established something else: **the drift across
runs in that session was far larger than ±2.7 ns, and large enough to make sub-10% findings
unresolvable.**

`Event_Publish` — a row no change in the session touched — read **70.55 → 63.07 → 57.27 ns**
across the three runs. A monotonic 19% move on untouched code. Ratios do not rescue the
comparison either: the `Command_Void` row the ratios are computed against drifts too, and not
in step.

Two consequences, both worth keeping:

* A ~5% shift seen on the planned rows after the dispatch-table work could be neither
  confirmed nor refuted. It is recorded as an open question, not as a regression. A separately
  suspected grouped-lane regression *was* refuted: across runs the penalty simply moved
  between `Command_Void_Grouped` and `Command_Void_Grouped_GroupSet`, which is what noise
  looks like when two near-identical paths trade places.
* The A/B above survives all of it — 2.66× is 166%, an order of magnitude above the drift.
  That is the whole argument for building the pair into one table rather than comparing runs.

The methodology rule earns its place here: **run the two states in one script and carry an
untouched control row.** Comparing four separate runs, as was attempted, cannot resolve
anything smaller than the drift — and the drift is not a constant to subtract.

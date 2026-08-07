## v2.1.0 – '2026-08-07'

Stable release. Promotes the entire preview cycle since v2.0.0 to the stable channel — the
contents of the v2.2.0-preview, v2.3.0-preview and v2.4.0-preview entries below, exactly as
shipped there: the dispatch fast path (executor-level dependency caches, sync fast path,
transient facades), the shared dispatch engine with single-object facades and the broadcast
fast lane, compile-time pipeline plans — void and result — with direct handler construction,
the grouped and streaming fast lanes, the typed dispatch holders, and the NativeAOT smoke
gate in CI. (v2.1.0-preview carried repository chores only.) No API or behavior changes
beyond those entries; see them for the full details and benchmark tables.

## v2.4.0-preview – '2026-08-07'

Preview release. The theme: **the remaining lanes join the fast path.** v2.3.0-preview left three
entry points on the original `Mediate` machinery — grouped publishes, grouped dispatches paying
per-call key materialization, and streaming queries — and resolved every planned handler through
the container even when the container had nothing to add. This release moves all of them onto the
executors' cached-plan pattern, teaches generated plans to construct their handlers directly, and
puts the benchmark up against the fastest widely-used alternative instead of only MediatR. CI
gains an end-to-end NativeAOT gate. Behavior is preserved on every path (see Notes for the two
deliberate edges).

### Compile-time result plans and direct handler construction

* The generator now emits `GeneratedDispatchRoots.AddResultPlan<TMessage, TResult, THandler>()`
  for a dispatchable command/query with exactly one closed, non-stream result contract whose
  whole discovered pipeline is a single async result handler — the void plan's eligibility rules,
  applied to the result shape. The executor closes over all three types and invokes the handler
  devirtualized.
* Both plan shapes can additionally carry a **direct-construction factory**
  (`static () => new THandler()`). The compile-time gate is deliberately strict: the handler's
  *only* instance constructor must be public and parameterless (the container's greedy
  constructor selection would pick any richer one), no `required` members (an emitted `new()`
  would not compile), and no `IDisposable`/`IAsyncDisposable` (the container tracks transient
  disposables; direct construction would not).
* The factory is as advisory as the plan itself. At runtime it is used only while the handler's
  *effective* DI registration is the module's own plain transient self-registration — no user
  factory, no lifetime override, no forced memoization — re-validated with the same
  registry-version guard as the dependency cache. Any customization routes the dispatch back
  through the container.
* The lifetime capture behind that verdict (and behind the existing memoized fast path) now runs
  **at first resolution instead of inside `AddErgosfare`**, so registrations added between
  `AddErgosfare` and `BuildServiceProvider` are honored by both features.
* This reverses v2.3.0-preview's note that a result plan "showed no gain": devirtualization alone
  did not pay, but together with direct construction the planned query path drops from ~44 ns to
  ~25 ns per dispatch (see Benchmark).

### Grouped dispatch fast lane

* Grouped command/query dispatch used to materialize the caller's group sequence and build a
  joined string key on every call. A per-message-type **last-used group-set slot** (void and
  result alike) now answers the common shape — one message type, one stable group set — with an
  ordinal element-wise compare: no materialization, no key, no per-dispatch allocation. Misses
  fall back to the composite stores, which stay authoritative, so executor identity and its
  version-guarded dependency cache are preserved when group sets alternate. The slot snapshots
  the group contents, so mutating a reused settings instance reads as a new group set, never a
  stale hit.
* **Grouped publishes leave the `Mediate` fallback.** The broadcast invoker resolves the same
  group-filtered dependencies the old path built — same factory call, same descriptor semantics —
  from a last-used (factory, group set) plan slot, then runs the same strategy, including the
  straight-through loop for interceptor-free, unfiltered pipelines. A grouped publish now costs
  what a group-less one does (see Benchmark).

### Streaming joins the executor path

* Engine-backed facades no longer stream through `Mediate(options)`: no per-call
  `MediateOptions`, no per-call descriptor lookup, no resolving the scope's `IMessageMediator`.
  The stream invoker holds the query's pipeline plan (group-less and grouped slots, factory-keyed
  and version-guarded) and runs the same streaming strategy against it. Context construction is
  unchanged — one fresh, unpooled context per `StreamAsync` call, shared across re-enumerations
  exactly as before — and an unregistered query still throws at call time, not at enumeration.

### Typed void dispatch on the engine

* `MessageDispatchEngine.DispatchVoidAsync<TMessage>` resolves its executor from a
  static-generic holder — a field read and a cache-identity check instead of the type-keyed
  dictionary lookup — guarded by `message.GetType() == typeof(TMessage)`, so base-typed generic
  calls keep resolving by runtime type. It is deliberately **not** a `DispatchAsync` overload: a
  same-name generic would join the candidate set of every explicit `DispatchAsync<T>(msg, …)`
  call, and for echo-typed shapes (`ICommand<TResult> : ICommand : IMessage` makes them legal)
  the identity conversion would out-rank the result overload — silently rerouting result
  dispatches through the void pipeline or breaking compilation. The facade-level counterpart is
  blocked by the same C# overload-resolution fact and was not shipped.

### Benchmark: a real competitor

* [martinothamar/Mediator](https://github.com/martinothamar/Mediator) 3.0.2 — source-generated
  dispatch, singleton lifetime by default — joins the table as the `MediatorSg_*` rows, running
  its out-of-the-box defaults just as the MediatR rows run theirs.
* New Ergosfare rows make the comparison honest in both directions: `Command_Void_Memoized`
  (`ForceMemoizedHandlers()` — the zero-allocation shape that matches Mediator's singleton
  default), `Query_Result_Generated`, grouped rows for command dispatch and event publish, and
  the engine's typed void dispatch.

Per-operation numbers (BenchmarkDotNet v0.15.8, Windows 11, AMD Ryzen 7 7800X3D, .NET 9.0.11,
RyuJIT x86-64-v4), measured 2026-08-07:

| Method | Cat | Mean | Alloc |
|---|---|---:|---:|
| Engine_Void | Root | 35.16 ns | 24 B |
| Engine_Void_Typed | Root | 29.60 ns | 24 B |
| Command_Void | Root | 32.90 ns | 24 B |
| Command_Void_Generated | Root | **21.21 ns** | 24 B |
| Command_Void_Memoized | Root | 24.48 ns | **0 B** |
| Command_Void_Grouped | Root | 36.73 ns | 24 B |
| Query_Result | Root | 44.10 ns | 24 B |
| Query_Result_Generated | Root | **25.04 ns** | 24 B |
| Event_Publish | Root | 56.93 ns | 48 B |
| Event_Publish_Grouped | Root | 56.22 ns | 48 B |
| MediatR_Send_Void / _Result / _Publish | Root | 66.22 / 52.88 / 84.90 ns | 192 / 192 / 440 B |
| MediatorSg_Send_Void / _Result / _Publish | Root | 8.31 / 8.21 / 15.90 ns | 0 / 0 / 0 B |
| Engine_Void_Scoped | Scoped | 92.67 ns | 200 B |
| Command_Void_Scoped | Scoped | 89.80 ns | 192 B |
| Query_Result_Scoped | Scoped | 97.70 ns | 200 B |
| Event_Publish_Scoped | Scoped | 107.90 ns | 232 B |
| MediatR (void/result/publish, Scoped) | Scoped | 106.96 / 106.67 / 143.95 ns | 352 / 352 / 600 B |
| MediatorSg (void/result/publish, Scoped) | Scoped | 56.57 / 56.92 / 70.64 ns | 128 / 128 / 128 B |

The honest reading: Ergosfare leads MediatR on every row, and Mediator — which trades runtime
registration, group filtering, polymorphic dispatch and the execution-context model for a fully
static dispatch — leads Ergosfare. This cycle narrowed that gap on the planned void path from
~3.4× to ~2.3×; the grouped publish row shows the grouped lane reaching parity with the
group-less one; the remaining lifetime-honoring difference (24 B transient handler per dispatch
vs. Mediator's shared singletons) is a semantic choice, not an overhead — `Command_Void_Memoized`
is the like-for-like row.

### NativeAOT smoke in CI

* `examples/AotSmoke` publishes with `PublishAot=true` (trim/AOT analyzer warnings are errors)
  and runs every dispatch shape — void and result commands through their generated plans, a
  query, a two-handler class-event broadcast, and a struct event exercising the value-type
  generic instantiations only generated roots can anchor. The new `AotSmoke` workflow publishes
  and executes the binary on every push and PR.

### Notes

* **Public API additions:** `MessageDispatchEngine.DispatchVoidAsync<TMessage>`;
  `GeneratedDispatchRoots.AddResultPlan`/`FindResultPlan` with `ResultPlanRoot`/
  `IResultPlanRootVisitor`; `Func<THandler>`-taking overloads of `AddVoidPlan`/`AddResultPlan`.
  Nothing is removed or changed in shape.
* **Deliberate behavioral edges:** (1) a custom `IMessageMediator` registration decorating the
  scope's mediator no longer sees grouped publishes or streams on engine-backed facades — those
  lanes now run the engine directly, consistent with the group-less lane since v2.3.0-preview;
  wrapped-construction facades and foreign mediator implementations keep the original path.
  (2) Because the lifetime capture moved to first resolution, handler registrations added after
  `AddErgosfare` are now honored by the memoized fast path too — strictly closer to the
  container's own behavior.
* The static plan/executor holders root the last-serving container's executor graph past
  container disposal (at most one graph per message type — in a single-container process, the
  live one). A `WeakReference` would tax every hot-path read instead; called out for review.

## v2.3.0-preview – '2026-07-28'

Preview release. The theme: **event publishing joins the fast lane, and resolving a mediator stops
costing more than dispatching through it.** v2.2.0-preview put commands and queries on the
executor fast path; this release brings event broadcasting onto the same footing, collapses every
facade resolution to a single object over a shared dispatch engine, and ships the source
generator's first compile-time pipeline plans. Behavior is unchanged on every path; the public API
grows (see Notes) but nothing breaks.

### Broadcast fast lane

* **Pooled publish contexts and allocation-free default publishes.** A group-less `PublishAsync` without settings rents a pooled execution context and reuses a cached default broadcast strategy — no `EventMediationSettings`, no filter list, no strategy allocation. Caller-supplied settings items are adopted for the dispatch and detached untouched on return, so handler writes stay visible to the caller exactly as before.
* **Invoker-cached pipeline plan.** The broadcast invoker holds the event's resolved pipeline directly, re-validated against the registry version — the executors' pattern applied to publishing. The plan is keyed by the dependencies-factory reference, so one container's plan (which may pin that container's provider for memoized pipelines) is never served to another container.
* **Straight-through broadcast.** An unfiltered publish over an interceptor-free pipeline loops the handler arrays directly — synchronously while handlers complete synchronously, bailing to an awaiting helper on the first suspension, preserving strict sequential order. Exceptions propagate raw; `ThrowIfNoHandlerFound` is honored.
* **Fixed:** a broadcast now continues with the event instance a pre-interceptor returns, matching the documented pre-interceptor contract.
* Grouped publishes, handler-predicate filters, externally owned contexts and foreign `IMessageMediator` implementations keep the original `Mediate` path unchanged.

### Single-object facades: `MessageDispatchEngine`

* Resolving a facade used to build two transients per scope — the facade plus its `IMessageMediator` — while MediatR builds one. The executor dispatch bodies now live in **`MessageDispatchEngine`**, a process-wide singleton that takes the calling scope's provider per call; `IMessageMediator`'s executor overloads delegate to it unchanged.
* `CommandMediator`, `QueryMediator` and `EventMediator` gain a public engine constructor, and DI binds single-constructor engine-backed shapes — constructor injection compiles the engine into a constant callsite, avoiding both MS.DI's ambiguous-constructor rejection and the per-resolution service lookup a factory registration would pay. The original `IMessageMediator` constructors remain for direct construction and foreign mediator implementations; `EventMediator` is unsealed to admit its DI shape.
* Scoped handler resolution still binds to the calling scope (verified under `ValidateScopes = true`); the streaming and grouped paths resolve the scope's `IMessageMediator` on demand and are otherwise untouched.

### Cheaper result-executor lookup

* Group-less result dispatch paid a (message type, result type) composite-key hash per call while the void path got by on a single `Type`-keyed lookup. A per-message-type **last-used result-executor slot** closes the gap: one `Type` lookup plus a reference check on the recorded result type. Misses fall back to the composite store, which stays authoritative, so executor identity — and its registry-version-guarded dependency cache — is preserved.

### Typed publish without the dictionary

* The generic `PublishAsync<TEvent>` overload resolves its invoker from a static-generic holder (`Holder<TEvent>.Instance`), guarded by `@event.GetType() == typeof(TEvent)` — a base-typed generic call keeps resolving by runtime type, so polymorphic publishes dispatch exactly as before. The interface-erased overload keeps the dictionary path.

### Compile-time pipeline plans (source generator)

* When the generator can prove a dispatchable command's whole discovered pipeline is a single default-discovery, default-group async handler, it emits `GeneratedDispatchRoots.AddVoidPlan<TMessage, THandler>()` alongside the dispatch roots. The executor cache closes an executor over both types, so the fast path invokes the handler **devirtualized** — no contract pattern match, inlineable for sealed handlers.
* The plan is advisory, never authoritative: the registry-version-guarded dependency cache re-validates the pipeline, so runtime registrations invalidate generated plans exactly as they invalidate runtime executors; a plan whose handler no longer matches falls through to the ordinary contract switch, then the strategy — behavior identical, only the speedup is lost. Grouped pipelines and `RegisterFromAssembly`/manual-registration users never see a plan.
* Eligibility is conservative by design: one main-handler descriptor for the message across the compilation and scanned references, async void contract, unkeyed and ungrouped handler, no interceptor targeting the message, and a referenced package that exposes the plan surface (older packages keep the previous emission).

### Benchmark

Per-operation BenchmarkDotNet numbers (v0.15.8, Windows 11, AMD Ryzen 7 7800X3D, .NET 9.0.11,
RyuJIT x86-64-v4), measured 2026-07-28. *Root* resolves mediators once; *Scoped* creates a fresh
DI scope per dispatch and includes resolution in the measurement. MediatR columns are from the
same runs.

| Method | Cat | Mean | Alloc |
|---|---|---:|---:|
| Engine_Void | Root | 30.26 ns | 24 B |
| Command_Void | Root | 30.52 ns | 24 B |
| Command_Void_Generated | Root | **26.49 ns** | 24 B |
| Query_Result | Root | 34.43 ns | 24 B |
| Event_Publish | Root | 49.32 ns | 48 B |
| MediatR_Send_Void / _Result / _Publish | Root | 60.29 / 53.79 / 83.42 ns | 192 / 192 / 440 B |
| Engine_Void_Scoped | Scoped | 86.08 ns | 200 B |
| Command_Void_Scoped | Scoped | 81.72 ns | 192 B |
| Query_Result_Scoped | Scoped | 85.89 ns | 200 B |
| Event_Publish_Scoped | Scoped | 100.90 ns | 232 B |
| MediatR_Send_Void_Scoped / _Result_Scoped / _Publish_Scoped | Scoped | 101.48 / 115.76 / 130.32 ns | 352 / 352 / 600 B |

Across this release cycle the scoped rows moved: command 91.4 → 81.7 ns (224 → 192 B), query
109.7 → 85.9 ns (232 → 200 B), event publish 117.7 → 100.9 ns (264 → 232 B). The scoped query —
the one row that still trailed MediatR when the cycle started — now leads it comfortably. The
generated void-command plan beats the hand-written fast path by ~12%.

### Notes

* **Public API additions:** `MessageDispatchEngine` (constructed by DI only); engine-accepting public constructors on `CommandMediator`, `QueryMediator`, `EventMediator`; `GeneratedDispatchRoots.AddVoidPlan`/`FindVoidPlan` with `VoidPlanRoot`/`IVoidPlanRootVisitor`. `EventMediator` is no longer sealed. Nothing is removed or changed in shape; applications resolving mediators from DI see identical behavior with no migration steps.
* A result-producing counterpart of the void plan was implemented and measured during the cycle: it showed no gain (the runtime contract switch's first arm already matches the async contract, and tiered PGO devirtualizes it), so it was deliberately not shipped.
* Two runtime-registration test facts were shielded from assembly-scan pollution with `[ExcludeFromDiscovery]` — the registry is process-wide, and another test's `RegisterFromAssembly` sweep could slip a late-registered type into a pipeline before its fact warmed the cache. Test-only; no product change.

## v2.2.0-preview – '2026-07-28'

Preview release. The theme: **the dispatch path stops re-deriving what it already knows.** v2.0.0
moved dispatch-shape work off the per-call path and onto compile time or a once-per-message-type
plan; this release removes what was left — the per-dispatch lookup of that plan, the pipeline
machinery around a pipeline that has exactly one stage, and a DI lifetime that charged the
dispatch path for a scope entry it never used. No public API changes and no behavioral changes.

### Dispatch fast path

* **Executor-level dependency cache.** A pipeline executor is already per (message type, result type, group set), so it now holds its resolved `IMessageDependencies` directly, re-validated against the registry version on each dispatch. The per-dispatch factory call and its `ConcurrentDictionary` lookup collapse to a field read and an integer compare. Runtime registrations bump the version and the next dispatch rebuilds, so late registration keeps working; concurrent rebuilds are benign, since both writers publish equivalent state.
* **Straight-through dispatch.** When a message's plan resolves to exactly one main handler, no interceptors in any of the four stages, and no registered result adapters, the executor invokes the handler's typed member and returns its `ValueTask` unchanged — no mediation-strategy object, no async state machine, no interface-dispatched stage-count checks. The condition is precomputed once when the plan is built (`MessageDependencies.FastSingleHandler`), not evaluated per dispatch. Adding a single interceptor returns the message to the full staged pipeline; a handler contract the fast path does not recognize falls through to the strategy, which raises its canonical `NotSupportedException`.
* **Group-less executor lookup keyed by message type alone.** Dispatches that pass no groups — nearly all of them — skip group materialization and composite-key hashing entirely, hitting a `ConcurrentDictionary<Type, …>` instead.
* **Adapter check split by shape.** `ResultAdapterService` exposes an internal emptiness check, read live so a late `AddAdapter` is observed, letting the fast path skip adapter consultation when none are registered. A foreign `IResultAdapterService` implementation always routes through the strategy.
* **Which entry points this covers.** Both optimizations live in the pipeline executors, so they apply to `ICommandMediator.SendAsync`, `IQueryMediator.QueryAsync` and `IMessageMediator.DispatchAsync`. Event publishing (`IEventMediator`/`IPublisher.PublishAsync`, which broadcasts through `AsyncBroadcastMediationStrategy`) and streaming queries (`IQueryMediator.StreamAsync`) still dispatch through the options path, as does `IMessageMediator.Mediate` itself — they resolve dependencies per dispatch and build an unpooled context. Bringing those onto the executor path is future work.

### Mediator lifetime

* **The mediator facades are registered transient instead of scoped** — `ICommandMediator`, `IQueryMediator`, `IEventMediator`, `IPublisher` and `IMessageMediator`. A facade is stateless; the only thing it captures is the provider that resolved it, and DI hands a transient service the resolving scope's provider exactly as it does a scoped one, so handlers still resolve from the calling scope and scoped handler dependencies are honored unchanged (verified under `ValidateScopes = true`).
* What changes is cost. Resolving a scoped service takes the scope's lock and writes the instance into the scope's resolved-services dictionary. That amortizes across a long-lived scope and never amortizes at all when every dispatch creates its own scope — and because `ICommandMediator` → `IMessageMediator` were both scoped, the scope-per-dispatch path paid it twice. This is the single largest contributor to the web-server-shape numbers below.

### Benchmark

100k sequential no-op dispatches per operation. BenchmarkDotNet v0.15.8, Windows 11, AMD Ryzen 7
7800X3D, .NET 9.0.11 (RyuJIT x86-64-v4), measured 2026-07-28.

| Scenario | v2.1.0-preview | v2.2.0-preview | MediatR (same runs) |
|---|---:|---:|---:|
| Typical usage — mean | 6.76 ms | **2.97 ms** | 5.79 ms |
| Typical usage — allocated | 2.29 MB | 2.29 MB | 18.31 MB |
| Web-server shape (scope per dispatch) — mean | 20.11 ms | **8.46 ms** | 10.28 ms |
| Web-server shape — allocated | 38.91 MB | **21.36 MB** | 33.57 MB |

The two Ergosfare columns are back-to-back runs on the same machine. The web-server shape was the
one scenario v2.0.0 documented as a loss against MediatR; it is now a win on both axes, though the
time margin there (~18%) is narrower than the allocation margin (~36%) and that row is dominated by
DI scope creation for both libraries.

The internal engine path (`IMessageMediator.Mediate` with pre-built `MediateOptions`) is unchanged
at ~6.3 ms and now trails the public facade: it is a separate entry point that runs its own resolve
and mediation strategies, so it reaches neither the executor's cached plan nor the straight-through
path. It remains supported for custom mediation strategies.

### Notes

* No public API changes; no migration steps. Applications resolving mediators from DI see identical behavior.
* Custom modules registering their own facade should follow suit and use `TryAddTransient`; see the plugins guide.

## v2.0.0 – '2026-07-25'

First stable release of the v2 line. The theme: **all dispatch-shape work moves to compile time
or to a once-per-message-type plan** — registration is source-generated, dispatch generics
close at compile time, execution contexts are pooled, and the dispatch path carries no
reflection, no `MakeGenericType`, no registry scan and no `AsyncLocal`.

### Source-generated registration (`Stella.Ergosfare.SourceGenerator`)

* New incremental Roslyn generator discovers every Ergosfare construct in the compilation and emits `RegisterGenerated()` extensions on the module builders (plus a DI-agnostic `RegisterAll(IMessageRegistry)`) — drop-in replacements for `RegisterFromAssembly(...)`, with **pre-computed handler descriptors**: registration performs no reflection over handler types. Plain messages and open generics keep the runtime `Register(Type)` fallback; both paths are mutually idempotent, so generated and runtime registration can coexist.
* **Reference scanning:** the consuming project's generator also walks referenced assemblies — a library's handlers register through the app's generated code with zero registration code in the library. Internal types participate via `InternalsVisibleTo`; types generated code cannot name surface as **ERGOSG002** (marker types the runtime scan would have caught). Opt out per project with `<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>`. Assemblies named under the reserved `Stella.Ergosfare.*` prefix are skipped by default (their contract interfaces inherit the module markers); an app deliberately named under that prefix opts back in per-assembly with `<ErgosfareSourceGeneratorForceScanReferences>true</ErgosfareSourceGeneratorForceScanReferences>` — only assemblies that set it are scanned, so the library's own contracts are never registered.
* **Discovery keys:** `[DiscoveryKey("reporting.daily")]` gates a type out of default discovery until a registration call selects it — `RegisterGenerated()` takes untagged types, `RegisterGenerated("reporting.*")` cherry-picks by exact key or trailing-`*` prefix glob, and calls chain safely. `[assembly: DiscoveryKey]` tags a whole library; `[ExcludeFromDiscovery]` (type or assembly) removes a construct from discovery entirely. The reflection path (`RegisterFromAssembly`, now with a pattern overload) honors the same attributes.
* **Generated dispatch roots:** the generator emits compile-time generic closures (`GeneratedDispatchRoots.AddMessage<M>` / `AddResult<M, R>` / `AddStream<Q, R>`), letting the executor, event-broadcast and stream-invoker caches construct their pipelines **without `MakeGenericType`** — and giving Native AOT a static anchor for every instantiation, value-type messages included. The reflective path remains only as the fallback for open generics and runtime-only registrations.
* Diagnostics: `ERGOSG001` (inaccessible registrable type), `ERGOSG002` (invisible referenced type).

### Dispatch engine

* **Per-message-type pipeline executors.** Dispatch goes through a pipeline closed over the message's runtime type, built once per (message type, result type, group set) and cached process-wide; the facades resolve executors with a dictionary lookup. No per-call options object, no interface-erased strategy, no object-typed bridge — interface-erased dispatch throws `NotSupportedException` with guidance.
* **Independent sync/async contracts, typed end to end.** `IHandler` and the four interceptor roots are empty markers; typed synchronous and `ValueTask`-based asynchronous contracts are standalone hierarchies invoked exclusively through their typed members (generic invocation strategies, contravariant in message/result). All DIM bridges and object-typed root members are gone; synchronous typed interceptors actually work now (the old bridge crashed them with `InvalidCastException`).
* **`ValueTask`-first surface** across handlers, interceptors and facades; synchronously completing handlers allocate nothing.
* **Thread-safe registration.** Registration is serialized behind a gate with lock-free snapshot readers (immutable descriptor-array snapshots, copy-on-write stage arrays, version-stamped resolve caching — fixing registration races, mid-registration enumeration crashes, and a resolve-cache poisoning bug). Hot-path LINQ removed from event broadcast and pipeline-shape building.

### Execution context — scopes, pooling, nested dispatch

* **Nested dispatch as a first-class pattern:** `context.CreateScope()` returns a struct scope wrapping a clean, pooled child context — isolated items, inherited cancellation token. Facades accept the child (`SendAsync`/`QueryAsync`/`PublishAsync` overloads taking `IExecutionContext`; `MediateOptions.ExternalContext` for the options paths). An inner `Abort()` ends only the inner pipeline; parallel inner dispatches with separate scopes are safe.
* **Pooled execution contexts:** dispatches rent and return contexts through a `[ThreadStatic]`-first pool with a synchronous fast path (no async state machine when the pipeline completes synchronously). A context is valid only for the duration of its dispatch. Public-facade allocations drop **5.34 → 2.29 MB per 100k dispatches** with Gen0 pressure halved, at time parity.
* Context fixes: `Has`/`TryGet`/`Get` no longer allocate the items dictionary on an empty context; `Get` throws the documented `KeyNotFoundException`.

### Pipeline control

* **`[ExcludeFromPipeline]`** on a message type excludes covariantly matched interceptors — blanket or per group (`[ExcludeFromPipeline("logging")]`). Interceptors registered for the message type itself always run; main handlers are never affected.
* **Event broadcast delivers to covariantly matched handlers** (registered against a base type or interface of the event) — the event's own handlers first, then indirect ones. Opting out of broad delivery is a group concern.
* The two-parameter command/query post-/exception-interceptor contracts are now contravariant in their message parameter, matching the core contracts.
* The single-parameter pre-interceptor contracts (`ICommandPreInterceptor<TCommand>`, `IQueryPreInterceptor<TQuery>`, `IEventPreInterceptor<TEvent>`) now return the typed message (`ValueTask<TMessage>`) instead of `ValueTask<object>` — a pre-interceptor carries no result, so the message type is all there is to return. The non-generic contracts still return `object` (they intercept any message); the two-parameter variants still return the modified message type. `TMessage` is invariant because it is now returned.
* Fixed: the void-flavored `IEventPreInterceptor` default implementation returned `ValueTask.CompletedTask` as the pipeline's message-replacement value, crashing event pipelines running an event-wide pre-interceptor.

### Removed

* **`Stella.Ergosfare.Contracts` folded into Core.Abstractions** (`GroupAttribute`/`WeightAttribute` now in `Stella.Ergosfare.Core.Abstractions.Attributes`); the package is no longer produced.
* **`AmbientExecutionContext`** and `EnableAmbientExecutionContext()` (deprecated since v1.2.0): the context parameter is the only access path; no `AsyncLocal` remains on the dispatch path. `NoExecutionContextException` removed with it.
* The obsolete three-parameter `TModifiedResult` interceptor interfaces (deprecated in v1.4.0) — use the two-parameter typed variants.
* `IAsyncValueTaskHandler<TMessage, TResult>` (experimental) — `IAsyncHandler` itself now carries the `ValueTask` shape. The `[Experimental]` gate (`ERGOEXP`) stays in place for future APIs.
* Internal cleanups: `MainInvoker`, the `AbstractInvoker` layer, and the interceptor object roots.

### Benchmark (100k no-op dispatches, Ryzen 7 7800X3D, .NET 9, 2026-07-24)

| Scenario | Mean | Allocated |
|---|---:|---:|
| Ergosfare — typical usage (`SendAsync`, shared scope) | 6.87 ms | **2.29 MB** |
| MediatR — typical usage | 6.09 ms | 18.31 MB |
| Ergosfare — fresh DI scope per dispatch | 18.81 ms | 38.91 MB |
| MediatR — fresh DI scope per dispatch | 10.09 ms | 33.57 MB |

### Migration notes

* `using Stella.Ergosfare.Contracts.Attributes;` → `using Stella.Ergosfare.Core.Abstractions.Attributes;`; drop the `Stella.Ergosfare.Contracts` package reference.
* Handler/interceptor signatures: `Task`/`Task<T>` → `ValueTask`/`ValueTask<T>` (mechanical; `async` bodies need only the signature change).
* `AmbientExecutionContext.Current` → the `IExecutionContext` parameter your handler already receives.
* Prefer `RegisterGenerated()` (with the `Stella.Ergosfare.SourceGenerator` package) over `RegisterFromAssembly(...)`; the latter remains for runtime-loaded plugins.
* Do not hold an `IExecutionContext` reference beyond the dispatch it belongs to — contexts are pooled.

## v1.4.0 – '2026-07-22'

### Features & Improvements

#### Typed interceptors without the third type parameter

* The two-parameter typed interceptor interfaces — `ICommandExceptionInterceptor<TCommand, TResult>`, `ICommandPostInterceptor<TCommand, TResult>`, `IQueryExceptionInterceptor<TQuery, TResult>`, `IQueryPostInterceptor<TQuery, TResult>` — now declare the type-safe `HandleAsync` member returning the typed result directly.
* Their type parameters are deliberately **invariant** now: the pipeline invokes interceptors through the non-generic root interfaces, so interface variance bought nothing — while `in TResult` blocked typed returns, which was the only reason the three-parameter `TModifiedResult` variants existed.
* The three-parameter variants are marked `[Obsolete]` and will be removed in the next major version.
* Migration note: implementors of the previous marker-only two-parameter interfaces now implement the typed member (returning `Task<TResult>` / `Task<TResult?>`) instead of the base `Task<object>` member.

### CI

* Coverage and unit-test workflows run as a single job with both SDKs installed — the solution multi-targets net9.0/net10.0, so per-SDK matrix legs could not restore it, and the two parallel coverage jobs raced each other pushing the badges branch (the `is at … but expected …` non-fast-forward failure).
* The coverage badge is generated only on pushes to `main` (pull requests no longer overwrite the badge or push to the badges branch), and the badge action receives only its supported `coverage-file-name` input.

## v1.3.0 – '2026-07-22'

### Internal Surface Changes — Core / Core.Abstractions

These packages are public so the first-party modules (Commands, Queries, Events) can consume them across assembly boundaries; they are not a third-party plugin contract, so changes here are not considered breaking. Code that registers handlers/interceptors and dispatches through `ICommandMediator`/`IQueryMediator`/`IEventMediator` compiles and behaves unchanged.

* `IMessageDependencies` reshaped: ten lazy collections → six fixed `IReadOnlyList` stages. The four interceptor stages (pre/post/exception/final) merge direct and indirect registrations into a single list — direct entries first, then indirect, each segment ordered by weight and handler type name — matching the execution order the invokers previously implemented as two passes. Main handlers keep the direct/indirect split because single-handler validation applies to direct handlers only.
* `ILazyHandler`, `ILazyHandlerCollection`, `LazyHandler`, `LazyHandlerCollection`, and `ToLazyReadOnlyCollection` are removed. Their replacement is `IHandlerReference<THandler, TDescriptor>`: the descriptor, the pre-computed concrete `HandlerType`, and `Resolve(IServiceProvider)` which obtains the instance for the current dispatch.
* `IMessageMediationStrategy<TMessage, TResult>.Mediate` gained an `IServiceProvider` parameter: the mediator hands the dispatching scope's provider down the pipeline explicitly — resolution is the dispatcher's responsibility. The execution context stays a pure data carrier between handlers: it plays no part in handler resolution and deliberately exposes no service provider, so user-facing handler code gets no service-locator surface.
* `IMessageDependenciesFactory` is registered as a **singleton** (was scoped); its dependency graphs are provider-independent and cached process-wide. `IMessageMediator` stays scoped but is now a thin wrapper whose only per-scope job is carrying the scope's provider into the execution context.
* Implementation types in `Stella.Ergosfare.Core` that are not part of the public contract are now `internal`: the handler descriptor builders (`HandlerDescriptorBuilder`, `PreInterceptorDescriptionBuilder`, `PostHandlerDescriptorBuilder`, `ExceptionInterceptorDescriptorBuilder`, `FinalInterceptorDescriptorBuilder`), `HandlerDescriptorBuilderFactory`, and `MessageDependenciesFactory` (still consumable through the public `IMessageDependenciesFactory`). Default implementations of public abstractions — `ResultAdapterService`, `LruCacheStrategy` — remain public. First-party assemblies use `InternalsVisibleTo`; forks building custom modules can add their own grants.

### Features & Improvements

#### Provider-independent dependency graphs — scope-per-dispatch ~2.6x faster, ~2.7x less allocation

* The resolved pipeline of a message (ordered stages, closed generic handler types) is built once per message type and group set, and shared process-wide. A fresh DI scope no longer rebuilds factory state, per-scope caches, or lazy wrapper graphs — the per-scope cost is the scope itself plus two small mediator wrappers.
* Handler and interceptor instances resolve per invocation from the execution context's provider, so DI lifetimes are honored exactly: singleton → container-cached, scoped → one per scope, transient → one per dispatch. Fully singleton pipelines (and `ForceMemoizedHandlers()`) additionally cache instances inside their references, pinned to the root provider.
* Pipeline shapes pre-compute closed generic handler types: `MakeGenericType` runs once per closed message type instead of on every scope's first resolution. Closing is guarded by `IsGenericTypeDefinition`, so handlers registered against a constructed generic message resolve as-is (previously a latent `MakeGenericType` throw on first resolution).
* Interceptor invocation is a single indexed pass over the merged, pre-ordered array — the double direct/indirect passes and per-loop enumerator allocations are gone.
* Benchmark (100k dispatches/op, Ryzen 7 7800X3D, .NET 9, fresh scope per dispatch): 45.6 ms / 146.5 MB → **17.2 ms / 53.4 MB**; MediatR on the same shape: 10.3 ms / 33.6 MB.

### Notes

* Behavioral change: transient-registered handlers (the framework default) now resolve once per dispatch instead of being implicitly reused across dispatches within the same scope — i.e. a transient registration now behaves as declared, matching MediatR semantics. On dispatch-heavy single-scope loops this costs one handler instance per dispatch (raw mediator path: 3.05 → 6.1 MB per 100k dispatches — still well below MediatR's 18.3 MB on the same loop). To keep instance reuse, register those handlers as singletons (before `AddErgosfare`, so `TryAdd` respects it) or call `ForceMemoizedHandlers()`.
* Dispatching a message whose pipeline has no direct main handler now throws `InvalidOperationException` with an explicit "No handler is registered for …" message instead of a LINQ "Sequence contains no elements" error.

## v1.2.0 – '2026-07-22'

### Deprecations

#### `AmbientExecutionContext` (planned removal in the next major version)

* The entire `AmbientExecutionContext` API is now marked `[Obsolete]`: the execution context is passed to every handler and interceptor as a parameter, which is the supported way to access it.
* Ambient publication is now **opt-in and disabled by default**. Components that require constructor-injected `IExecutionContext` (the only feature that genuinely needs the ambient mechanism) must enable it during registration:

  ```csharp
  services.AddErgosfare(options =>
  {
      options.EnableAmbientExecutionContext(); // deprecated compatibility switch
      options.AddCoreModule(module => { /* ... */ });
  });
  ```

* When disabled (the default), no `AsyncLocal` write occurs on the dispatch path. Resolving `IExecutionContext` from DI without enabling the switch throws `NoExecutionContextException` with guidance.
* Migration: replace `AmbientExecutionContext.Current` usages with the `IExecutionContext` parameter your handler/interceptor already receives. The static API will be removed together with `EnableAmbientExecutionContext()` in the next major release.

### Features & Improvements

#### Dispatch hot-path overhaul (~5x faster, ~11x less allocation)

* Resolved message descriptors are cached per message `Type` in the resolve strategy; the previous full-registry LINQ scan per dispatch is gone.
* Zero-interceptor fast path: when a message has no pre/post/exception/final interceptors, the handler is invoked directly — no invoker objects or empty-collection async iterations are constructed.
* Resolved `MessageDependencies` are cached in `ConcurrentDictionary`s keyed by `Type` (plus a struct key for group filters); per-dispatch string-key construction, service-provider lookups, and LRU timestamp writes are eliminated.
* `CommandMediator`/`QueryMediator` reuse their stateless mediation strategies and no longer materialize default settings objects per call; mediators are now registered as singletons (`TryAdd`, so user overrides still apply).
* `MessageDescriptor` stage lists are allocated lazily, trimming startup allocations per registered message type.
* Benchmark (100k dispatches/op, Ryzen 7 7800X3D, .NET 9, ambient context disabled): raw mediator path 31.6 ms / 112.9 MB → 4.4 ms / 3.1 MB; public `SendAsync` path 5.3 ms / 9.2 MB; MediatR on the same benchmark: 6.1 ms / 18.3 MB.

#### Lifetime-aware handler resolution — scoped dependencies now supported (default on)

* Handler resolution now honors registered DI lifetimes. Messages whose handlers and interceptors are all **singleton-registered** keep the process-wide memoized fast path; everything else (the framework default is transient) is resolved from the **calling scope's provider** and cached per scope — so scoped constructor dependencies (`DbContext`, unit-of-work, current-user services) get one instance per scope, exactly like MediatR, and `IDisposable` handlers/dependencies are disposed with their scope.
* Previously (v1.1.0) handler instances were silently memoized process-wide: the first dispatch captured the first caller's scope — including its `DbContext` — and reused it for every subsequent dispatch, even after that scope was disposed. This was a latent correctness bug that did not surface as an exception (default `BuildServiceProvider()` does not validate scopes).
* Opting out: `ForceMemoizedHandlers()` on the registration builder restores the pre-v1.2 memoize-everything behavior for maximum dispatch throughput. Per-handler control needs no new API — register a handler as singleton (before `AddErgosfare`, so `TryAdd` respects it) to keep it on the fast path.
* Within a scope, resolution is memoized per message type, so apps dispatching several messages per request pay the per-dispatch resolution cost once per scope.
* Ordered, group-filtered pipeline shapes are cached process-wide (invalidated by the registry version); a fresh scope only materializes cheap lazy wrappers over the cached descriptor arrays.
* Cost (measured, fresh scope per dispatch — the per-request worst case): ~0.47 µs and ~1.55 KB per scope for a single-handler message, versus ~0.11 µs / ~0.35 KB for MediatR's equivalent shape; dispatches after the first within the same scope use the memoized fast path (~55-70 ns).

#### Runtime registration correctness

* `MessageRegistry` now tracks a monotonic `Version`, and dependency caches invalidate when the registry changes — runtime registrations (including handlers added to already-registered messages) become visible to subsequent dispatches. Previously the dependency cache was never invalidated.

### Bug Fixes

* `IAsyncPostInterceptor<TMessage, TResult>` default implementation now forwards the `IExecutionContext` parameter it receives instead of reading the ambient context.
* `SingleStreamHandlerMediationStrategy` no longer unconditionally overwrites the ambient context without restoring it; it re-publishes the context for stream enumeration only when ambient access is enabled.
* Final interceptor types (direct and indirect) are now registered with the DI container; previously `RegisterHandlersFromDescriptor` skipped them, so standalone final interceptors could not be resolved.

### API Changes

* `MediateOptions<TMessage, TResult>.Items` is now nullable and no longer allocates a default dictionary; the execution context creates its `Items` dictionary lazily on first access.
* `ILazyHandlerCollection<THandler, TDescriptor>` gained a `First()` default interface method (allocation-free in the built-in implementation).
* `ICommandMediator`, `IQueryMediator`, `IEventMediator`/`IPublisher`, `IMessageMediator`, and `IMessageDependenciesFactory` are registered as **scoped** (previously transient) so per-dispatch handler resolution binds to the calling scope; `ActualTypeOrFirstAssignableTypeMessageResolveStrategy` is a singleton. All registrations use `TryAdd`, so user overrides still win.
* New registration extension: `ForceMemoizedHandlers()` (opt-out of lifetime-aware resolution).

### Notes

* Behavioral change vs v1.1.0: scoped/transient-registered handlers (the default) are no longer silently memoized across scopes — they now behave as their registration declares. If you relied on the old implicit memoization for performance, either register those handlers as singletons or call `ForceMemoizedHandlers()`.
* With the ambient context disabled (the default), both the raw and public dispatch paths are faster than MediatR and allocate a fraction of its memory; enabling `EnableAmbientExecutionContext()` re-introduces the per-dispatch `AsyncLocal` cost.

## v1.1.0 – '2026-03-23'

### Features & Improvements

#### LRU Cache Strategy for MessageDescriptorCache

* Added pluggable `IDescriptorCacheStrategy` interface for flexible cache management.
* Implemented **LRU (Least Recently Used)** cache strategy with configurable size limit (default: 100 entries).
* `MessageDescriptorCache` now supports `TryGet<T>` and `Add` methods with thread-safe operations.
* Automatic eviction of least recently used entries to control memory usage.
* Optional periodic cleanup for stale entries (24-hour threshold).

#### Performance Optimizations

* Replaced LINQ chains with manual loops in `ResolveHandlers` to eliminate per-call delegate allocations.
* Optimized group filtering and sorting with single-pass array operations.
* Reduced allocation overhead in handler resolution hot path.

#### Cache Integration

* `MessageDependenciesFactory` now consumes `MessageDescriptorCache` for dependency caching.
* Cache keys are now generated from message type and group combinations.
* Improved cache hit rates for repeated message invocations with same parameters.

### Bug Fixes

* Fixed excessive memory allocation in handler resolution path.
* Corrected group intersection logic for better filtering accuracy.

### Internal Changes

* Refactored `ResolveHandlers` method to use array-based operations instead of LINQ.
* Simplified `MessageDescriptorCache` API with generic `TryGet<T>` method.
* Updated DI registration to include default LRU cache strategy.

### Notes

* Default cache size is set to 100 entries; can be configured via `LruCacheStrategy` constructor.
* Cache strategy can be overridden by implementing custom `IDescriptorCacheStrategy`.
* Existing consumers should see reduced memory allocation and improved performance without API changes.

## v1.0.1 – '2025-12-16'

### Features & Improvements

#### .NET 10 & Native AOT Support

* Full support for **.NET 10** across all projects and test fixtures.
* Native AOT builds now use **monetization strategy** instead of reflection for descriptor construction, improving startup performance and compatibility.
* Simplified handler descriptor building for AOT scenarios.

#### Pipeline & Handler Updates

* Obsolete **pre-handlers** have been removed.
* Post-handler invocation now correctly handles nullable results.
* Streaming and synchronous pipelines updated for improved compatibility with snapshot-less execution.
* Minor internal adjustments to handler and interceptor execution flow for stability.

#### Project & Namespace Updates

* Solution structure migrated to **.slnx** format for faster load times and improved IDE integration.
* Namespaces reorganized for better modularity and clarity.
* Snapshot mechanism fully removed; not part of compatibility policy.

### Bug Fixes

* Fixed bug where post-handlers incorrectly returned nullable results.
* Fixed minor issues in streaming and synchronous pipeline execution.
* Adjusted internal mediator strategies for improved consistency.

### Internal Changes

* Refactored mediation strategies to better handle .NET 10 and native AOT.
* Removed obsolete pipeline snapshot features.
* Updated test fixtures to align with current pipeline and handler changes.

### Notes

* This is a **patch release**, intended to improve compatibility and stability for v1.0.0 consumers.
* Pipeline and message mediation behavior remains fully backward compatible.
* Snapshot persistence and checkpoint features are now fully deprecated and removed.
* Consumers can continue using caching and manual persistence mechanisms where applicable.


## v0.2.0e – '2025-9-22'

### Features & Improvements

#### Snapshot & Checkpointing
* Introduced `PipelineCheckpoint` to track `Message`, `Result`, and `Success` for partial or full pipeline snapshotting.
* Pre- and post-interceptors updated to support snapshot-aware execution.
* Streaming and synchronous handlers can skip already completed checkpoints.
* Provides scoped caching within handlers; allows optional manual persistence by consumers.
#### Retry Mechanism

* Added `ErgosfareExecutionContext.RetryCount` to track retry attempts.
* `Retry()` publishes `PipelineRetrySignal` and throws `ExecutionRetryRequestedException`.
* `MessageMediator` refactored to catch retry requests and re-mediate with preserved context.
* MediateOptions support retry limits via `Retry` property.
#### Streaming Mediation Improvements

* `SingleStreamHandlerMediationStrategy` fixed to ensure **pre-interceptors execute before the main handler**.
* Partial snapshotting after streaming completes successfully.

#### Internal Changes

* Added `ExecutionRetryRequestedException` class.
* Added internal setters for `PipelineCheckpoint.Message` and `Result`.
* All mediation strategies refactored for snapshot support.
* Pre- and post-invokers refactored for snapshot compatibility.

#### Fixes & Minor Adjustments

* Streaming strategy bug fixed: pre-interceptors now run correctly before streaming handler execution.

#### Notes

* This will be the last **major/minor premature release** (`0.2.0e`).
* Only **patch releases** will follow in this branch.
* The next full release will be **stable v1.0.0**.
* Consumers can leverage checkpoints for caching or manual persistence in fire-and-forget pipelines.



## v0.1.3e '2025-9-20'
### Breaking Changes
- `IExecutionContext` and `AmbientExecutionContext` are now under `Ergosfare.Core.Abstractions` namespace.  
  Update any using statements and references in dependent projects.
### Changed
- Dropped `Ergosfare.Context` package.
- `Stella.Ergosfare.Context` package is no longer distributed.
- Moved `IExecutionContext` and `AmbientExecutionContext` to `Core.Abstractions.Context`.
- All execution context exceptions moved to `Ergosfare.Core.Abstractions/Exceptions`.
- Updated namespaces across the codebase to reflect context package removal.
- Added XML documentation for context-related types and handlers.
- CI workflows updated to skip building and distributing `Ergosfare.Context` package.
- Internal chore to support pipeline snapshot functionality.

### Notes
- This refactor is a chore to enable pipeline snapshot support.


## v0.1.2e '2025-9-20'

### **Added ambient data methods to `IExecutionContext`**

* `Set(string key, object item)` – Sets ambient data to share across the pipeline.
* `T Get<T>(string key)` – Retrieves ambient data by key.
* `bool Has(string key)` – Checks if a specific ambient data item exists.
* `bool TryGet<T>(string key, out T item)` – Attempts to retrieve ambient data; returns `true` if the item exists.

> **Note:** These methods are now part of the `IExecutionContext` API, allowing handlers and interceptors to store and access pipeline-level data.


## v0.1.1e  '2025-9-18'

### **Added Ergosfare.Test.Fixtures**
Includes various useful tools, helpers and stubs for test authors, mainly for Ergosfare internals and Plugin developers


## v0.1.0e – First minor release –  '2025-09-18'

### Final Interceptors

* Introduced new interfaces:

    * `IFinalInterceptor`, `IAsyncFinalInterceptor<TMessage, TResult>` for generic/non-generic messages.
    * `ICommandFinalInterceptor`, `IQueryFinalInterceptor`, `IEventFinalInterceptor` for higher-level modules.
* Final interceptors now run at the end of the pipeline (in `finally`), enabling cleanup and logging scenarios.
* Support for nullable message results and exceptions passed into final interceptors.

### Pre- & Post-Interceptors

* **Pre-Interceptors**:

    * Added support for both direct and indirect pre-interceptors.
    * Can mutate messages and return a new one.
    * Emit detailed events (`BeginPreIntercepting`, `FinishPreInterceptorInvocation`, etc.).

* **Post-Interceptors**:

    * Added support for both direct and indirect post-interceptors.
    * Can mutate results before returning to the caller.
    * Integrated `IResultAdapterService` to detect embedded exceptions in results.
    * Emit events for normal execution and exception-like results (`FinishPostInterceptingWithException`).

### ⚡ Exception Interceptors

* Support for direct and indirect exception interceptors.
* Emit begin/finish events for each interceptor.
* Full support for nullable `messageResult`.
* Rethrows exceptions if no interceptors are registered.

### Unified Pipeline Events

* Refactored **EventHub → SignalHub**, and renamed `HubEvent → Signal` for clarity.
* Introduced `PipelineEvent` base class (with `Message` and nullable `Result`).
* Standardized event naming:

    * Pre: `BeginPreInterceptingEvent`, `FinishPreInterceptorInvocationEvent`, …
    * Post: `BeginPostInterceptingEvent`, `FinishPostInterceptorInvocationEvent`, …
    * Exception: `BeginExceptionInterceptingEvent`, `FinishExceptionInterceptorInvocationEvent`
    * Final: `BeginFinalInterceptingEvent`, `FinishFinalInterceptorInvocationEvent`
* Events carry metadata (interceptor type, exception, total count).
* Unified equality comparison for better test coverage.

### Result Adapter

* Added `IResultAdapter` and `IResultAdapterService`:

    * Detect exceptions inside result objects (e.g., `ErrorOr`).
    * Allow exception interceptors to run without throwing original result types.
* Integrated with post-interceptors and mediators for consistent handling.

### Mediators

* `QueryMediator` and `EventMediator` updated to raise pipeline events.
* Integrated `IResultAdapterService` into mediation strategies (`SingleAsyncHandlerMediationStrategy`, `AsyncBroadcastMediationStrategy`).
* Improved async flow and nullability handling.

###  Execution Strategies & Handler Invokers

* Refactored pipeline execution strategies with final interceptor support.
* Introduced **Handler Invokers**:

    * Type-safe replacement for `MessageDependencyExtensions`.
    * Unified mechanism for invoking handlers and interceptors.
    * Integrated seamlessly with signals.

### Centralized Test Fixtures

* Added new `Ergosfare.Test.Fixtures` assembly.
* Common fixtures consolidated for reuse across test classes.
* Introduced categorized stubs and stub factories to reduce inline stubs.
* Improved maintainability, readability, and pluggability of test suite.
* Achieved **95%+ coverage** with fixture-based test design.


### Fixes & Refactors

* Cleaned up pipeline flow with consistent async/await handling.
* Removed `MessageDependencyExtensions` in favor of handler invokers.
* Unified naming across signals and interceptors.
* Simplified exception interception without reflection/dynamic.


### Benefits

* Final interceptors enable safe cleanup and logging.
* Pre- and post-interceptors can mutate messages and results consistently.
* Exception handling is safer, with result-based error detection via adapters.
* Unified event/signal system improves observability and debugging.
* Stronger modularity for command, query, and event pipelines.
* Centralized, reusable test infrastructure.
* Easier to maintain, extend, and test pipelines.

### Tests

* Fixture-based tests for:

    * `ErrorOr` adapter support.
    * Exception interception (direct/indirect).
    * Post-interceptor result exceptions.
* Updated tests for signal structure, handler invokers, and fixtures.
* Achieved consistent **95%+ coverage**.

___


## v0.0.16e – IHasProxyEvents '2025-09-11'
### **Added**
- IHasProxyEvents interface, contains all known proxy events
- EventHub now implements IHasProxyEvents
- Now known pipeline events subscrible with += and unsubscrible with -= syntax from EventHub

## v0.0.15e – Pipeline Event System Refactor & Coverage '2025-09-03'

### **Added**

* `PipelineEvent` abstract base class (formerly `PipelineEventBase`) with:
    * `Timestamp` auto-initialization
    * `RelatedEvents` support (`Add`, `AddRange`)
    * `GetEqualityComponents()` for value-based equality
* 20+ concrete pipeline events:
    * `BeginExceptionInterceptingEvent`, `BeginExceptionInterceptorInvocationEvent`, `BeginHandlerInvocationEvent`, `BeginHandlingEvent`, `BeginPipelineEvent`
    * `BeginPostInterceptingEvent`, `BeginPreInterceptorInvocationEvent`, `FinishExceptionInterceptingEvent`, `FinishExceptionInterceptorInvocationEvent`
    * `FinishHandlerInvocationEvent`, `FinishHandlingEvent`, `FinishHandlingWithExceptionEvent`, `FinishPipelineEvent`
    * `FinishPostInterceptingEvent`, `FinishPostInterceptingWithException`, `FinishPostInterceptorInvocationEvent`
    * `FinishPreInterceptingEvent`, `FinishPreInterceptingWithException`, `FinishPreInterceptorInvocationEvent`
* Factory methods (`Create`) for all pipeline events with null checks and default handling (`ResultType ?? typeof(void)`)
* Static subscription & publish support via `PipelineEvent.Subscribe<TEvent>`
* In-place instance invocation via `Invoke()` extension method

### **Changed**

* Renamed `PipelineEventBase` → `PipelineEvent`
* `HubEvent` updated:

    * `Timestamp` is instance-based, not static
    * `GetHashCode` and `Equals` use `GetEqualityComponents()`
    * `RelatedEvents` added to allow event chaining

### **Fixed / Improved**

* Full unit test coverage for:

    * All pipeline events (`Create`, `GetEqualityComponents`, equality, timestamp)
    * `RelatedEvents` behavior (add, add range, read-only enforcement)
    * Static subscription / publish mechanics
    * In-place `Invoke()` calls

### **Impact**

* Event pipeline fully type-safe and decoupled
* Subscribers can register without creating instances
* Improved consistency and maintainability of pipeline events

### **Testing**
- Unit tests updated to account for recent changes in HubEvent and PipelineEvent

- New unit tests added for all new pipeline events and related components

- RelatedEvents functionality fully tested (add, add range, read-only enforcement)

- Static subscription and in-place Invoke() methods tested for all pipeline events

- Value-based equality (GetEqualityComponents, Equals, GetHashCode) fully covered

- Maintained 100% test coverage for all event classes and base logic

---

## v0.0.14e – Event Hub Refines & Proxy Event System
### New Features
* Generic Event Hub (EventHub): Supports strongly-typed events using `HubEvent` base class, with strong and weak subscriptions.
* Proxy Events (`ProxyEvent<T>`)  
Subscribe to predefined events using += and unsubscribe using -= syntax for cleaner code.
* Predefined Event: `PreInterceptorBeingInvokeEvent` added as a foundational example for interceptors and handlers.
* Custom Events: Subscribe, publish, and unsubscribe custom HubEvent types independently of predefined proxies.
* Value Object Base for Events:
  HubEvent includes equality operators (`==`, `!=`) and value-based `Equals` / `GetHashCode` for future-proof event comparisons.

### Improvements
* Thread-safe subscriptions using ConcurrentDictionary and locking.
* Automatic cleanup of dead weak subscriptions during event publishing.

### Removals
* `ISubscription` and `IHubEvent` interfaces have been removed.
  * Replace `ISubscription` with `ISubscription<TEvent>`
  * Replace `IHubEvent` with the abstract `HubEvent` class
  
### Testing
#### Unit tests enhanced to cover:
* Strong/weak subscription invoke behavior
* Proxy `+=` / `-=` operators
* Subscription matching and unsubscription
* Base HubEvent equality and hash code computation

### Keynotes 
* This release lays the foundation for next-generation plug-ins and modules, allowing n-party decoupled event-driven integrations.

* Users can create their own events implementing HubEvent for custom plugin scenarios.
___
## v0.0.13e - Republish of v0.0.12e - '2025-09-03'
- no changes
## v0.0.12e - EventHub - '2025-09-03'

## **New Features**

* **Centralized EventHub**: A thread-safe, global hub for Pre, Post, Handler, and Exception stage events.
* **Weak and Strong Subscriptions**: Subscribers can be registered as strong or weak references.

    * `IsAlive` property allows automatic cleanup of dead weak references.
    * Subscriptions implement `IDisposable`.
* **DI Integration**: EventHub now resolves via DI as a singleton using `EventHubAccessor`.
* **Thread-safe Publishing**: Publishing events is safe across multiple threads, with automatic cleanup of dead weak subscriptions.
* **Extensible Plugin Support**: Modules can subscribe to events without modifying core components.

**Keynote**

- This EventHub forms the foundation for future plugins and modules that do not need to be directly coupled with main modules.

- It enables developers to write their own n-party plugins, extending the system safely and independently.

---

**Side Note:**

* This EventHub system is **separate from the message mediation events** (pre/post/interceptor) used in command, query, and event pipelines.
* It provides a **general-purpose, centralized event mechanism** for modules and plugins to subscribe to runtime events without coupling to the core pipeline.

---


## v0.0.11e - Refactor - '2025-09-01'

### Changed

* `ActualTypeOrFirstAssignableTypeMessageResolveStrategy`

    * Now constructor-injected with `IMessageRegistry`.
    * Simplified `Find` method signature (`Find(Type)` instead of `Find(Type, IMessageRegistry)`).
* `MessageMediator` updated to use the simplified strategy method.
* DI registration added for `ActualTypeOrFirstAssignableTypeMessageResolveStrategy`.
* Unit tests updated to reflect new DI-based message resolution.

### Notes

* Only the **message resolution part** of mediation is now resolved through DI.
* Other mediation internals (e.g., message dependencies creation, execution context) are still manually constructed and may be migrated to DI in future updates.

---



## v0.0.10e - '2025-09-01'

### Changed
- **Handlers & Interceptors**: Removed `CancellationToken` parameters from all contracts.  
  Execution context’s token is now used consistently instead.

### Breaking Changes
- Any custom handlers or interceptors that previously accepted a `CancellationToken`  
  must be updated to rely on the execution context for cancellation.

### Internal
- Refactored interface definitions to eliminate redundant token passing.
- Updated unit tests to use context-based cancellation.
- Coverage badge regenerated to reflect new code changes.



## v0.0.9e - '2025-8-31' -  Pipeline flow fixes


### Changed
- **Handlers**: Updated handler order grouping logic.
- **Interceptors**: Refined interceptor chaining based on group attributes.

### Fixed
- Resolved issue with default group assignment for ungrouped handlers.

### Internal
- Refactored handler registration process to streamline group assignment.

### Files Changed
- `HandlerRegistry.cs`
- `InterceptorChain.cs`

### Code Coverage
- Added tests for newly implemented functionality.



# v0.0.8e Pipeline flow control
## Introduced
- `public class GroupAttribute(params string[] groupNames)`
- `public class WeightAttribute(uint weight)`

## New Features: 
- Handler Grouping with GroupAttribute
- Handler Ordering with WeightAttribute

## Changes:
- IHandlerDescriptor has new two property `Weight` and `Groups`
- All handler descriptor builders updated internally to support grouping and ordering
- All internal mediator definitions updated to use grouping and ordering
- MessageDependencies and MessageDependenciesFactory updated internally
- TypeExtensions has new metohods `GetWeightFromAttribute()`, `GetGroupsFromAttribute()`


# 🌟 v0.0.6e 🛠️ Event, Command & Query module, unit tests & code coverage

 
#### 📊 New Features: Event module
* **🔹IEventExceptionInterceptor:** interface added, Event module now supports non generic ExceptionInterceptors.
* **🔹IEventExceptionInterceptor\<TEvent\>:** interface added, Event module now supports generic`<TEvent>` ExceptionInterceptors.
* **🔹IEventPreInterceptor:** interface added, Event module now supports non generic PreInterceptors.
* **🔹IEventPreInterceptor\<TEvent\> :** interface added, Event module now supports generic`<TEvent>` PreInterceptors.
* **🔹IEventPostInterceptor :** interface added, Event module now supports non generic PostInterceptors.
* **🔹IEventPostInterceptor\<TEvent\> :** interface added, Event module now supports generic`<TEvent>` PostInterceptors.


#### 📊 New Features: Command module
* **🔹ICommandExceptionInterceptor:** interface added, command module now supports non generic ExceptionInterceptors.
* **🔹ICommandExceptionInterceptor\<TEvent\>:** interface added, command module now supports generic`<TEvent>` ExceptionInterceptors.
* **🔹ICommandPreInterceptor:** interface added, command module now supports non generic PreInterceptors.
* **🔹ICommandPreInterceptor\<TEvent\> :** interface added, command module now supports generic`<TEvent>` PreInterceptors.
* **🔹ICommandPostInterceptor :** interface added, Command module now supports non generic PostInterceptors.
* **🔹ICommandPostInterceptor\<TEvent\> :** interface added, Command module now supports generic`<TEvent>` PostInterceptors.
* **🔹ICommandPostInterceptor\<TEvent,TResult\> :** interface added, Command module now supports generic`<TEvent, TResult>` PostInterceptors.


#### 📊 New Features: Query module
* **🔹IQueryExceptionInterceptor:** interface added, command module now supports non generic ExceptionInterceptors.
* **🔹IQueryExceptionInterceptor\<TQuery\>:** interface added, query module now supports generic`<TQuery>` ExceptionInterceptors.
* **🔹IQueryPreInterceptor:** interface added, query module now supports non generic PreInterceptors.
* **🔹IQueryPreInterceptor\<TQuery\> :** interface added, query module now supports generic`<TQuery>` PreInterceptors.
* **🔹IQueryPostInterceptor :** interface added, Query module now supports non generic PostInterceptors.
* **🔹IQueryPostInterceptor\<TQuery\> :** interface added, Query module now supports generic`<TQuery>` PostInterceptors.
* **🔹IQueryPostInterceptor\<TQuery,TResult\> :** interface added, query module now supports generic`<TQuery, TResult>` PostInterceptors.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Events`, `Ergosfare.Events.Abstractions`, `Ergosfare.Events.Extensions.MicrosfotDependencyInjection`.
* **💯 100% test coverage** for `Ergosfare.Queries`, `Ergosfare.Queries.Abstractions`, `Ergosfare.Queries.Extensions.MicrosfotDependencyInjection`.

* **📊 Total project coverage:** 99%.

___

# 🌟 v0.0.5e 

### 🛠️ CommandModule unit tests & code coverage

#### No breaking changes
* **🔹Refactor:** CommandModuleBuilder.Register<T>() now internally calls CommandModuleBuilder.Register(Type T).
* **🔹Refactor:** `MessageModule` renamed to `CoreModule`.
* **🔹Refactor:**`CoreModule`.Build(...) implemented.
* **🔹Chore:** Command module related tests and code coverage.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Command`, `Ergosfare.Command.Abstractions`, `Ergosfare.Command.Extensions.MicrosfotDependencyInjection`.
* **📊 Total project coverage:** 85%.




# 🌟 v0.0.4e – Core Contracts Refactor & Exception Interceptor

### 🛠️ Core Enhancements

* **🔹 Refactor:** Base contracts moved from `Contracts` package into their dedicated project **Abstractions**.
* **🔹 Refactor:** Streamlined `StreamAsyncMediationStrategy` for improved maintainability and clarity.
* **✨ Feature:** Introduced **`IExceptionInterceptor`** handler, descriptor, and variants — all pipeline types now support exception interceptors.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Core.Abstractions`.
* **📊 Total project coverage:** 67%.



# 🌟 v0.0.3e

### General

* chore: bump C# version to latest major to support C# 13+

### Ergosfare.Contracts

* feat: pre/post Interceptors interface definitions
* feat: handlers and interceptors now receive IExecutionContext as argument (Handlers.Handle receives IExecutionContext)
* fix: require IExecutionContext in IAsyncPreInterceptor.Handle
* feat: introduced IStreamHandler\<TMessage, TResult> in contracts

### Ergosfare.Core

* feat: pre/post Interceptor descriptor definitions
* feat: add pre/post interceptor collections to IMessageDependencies and implement in MessageDependencies
* feat: Post/Pre InterceptorDescriptors Interface and Class Implementation
* feat: MessageDescriptorBuilderFactory supports building new interceptor descriptors
* feat: Message handlers now support pre/post interceptors
* feat: Mediation strategies updated to support IExecutionContext
* fix: correct PreInterceptorDescriptorBuilder filtering
* chore: Ergosfare.Core 100% covered with unit tests


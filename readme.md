# Ergosfare

![Ergosfare Logo](./7101c7df-6cac-4b25-994a-60e2adbdc546.png)

[![NuGet](https://img.shields.io/nuget/v/Stella.Ergosfare.svg?label=nuget)](https://www.nuget.org/packages/Stella.Ergosfare)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
![Tests](https://img.shields.io/github/actions/workflow/status/stellayazilim/Ergosfare/coverage_tests.yml?branch=main&label=tests)
[![Coverage](https://raw.githubusercontent.com/stellayazilim/Ergosfare/badges/.badges/coverage.svg)](https://github.com/stellayazilim/Ergosfare/actions/workflows/coverage_tests.yml)

**Compile the pipeline. Dispatch the message.**

> **v2 is the stable line**, released from [`main`](https://github.com/stellayazilim/Ergosfare/tree/main).
> Work in progress lands on [`preview`](https://github.com/stellayazilim/Ergosfare/tree/preview)
> first and ships as `-preview` pre-releases; see [Versioning and releases](#versioning-and-releases).
> Coming from v1? See [Migrating from v1 to v2](https://stellayazilim.github.io/ergosfare.docs/migration/v1-to-v2).

Ergosfare is a source-generated mediator for modern .NET applications. Commands, queries,
events, interceptors and nested dispatch share one type-safe pipeline model whose shape is
decided at compile time—not rediscovered while your application is running.

The result is a mediator built for applications that want rich CQRS pipelines without
giving up predictable performance, dependency-injection lifetimes, Native AOT or explicit
control over execution state.

> **You are viewing the `preview` branch.** It contains the next generation of Ergosfare
> and may introduce breaking changes between preview releases. For the current stable line,
> see [`main`](https://github.com/stellayazilim/Ergosfare/tree/main).

[Preview documentation](https://stellayazilim.github.io/ergosfare.docs/preview/) ·
[Changelog](https://stellayazilim.github.io/ergosfare.changelog) ·
[Compatibility policy](COMPATIBILITY.md) ·
[NuGet](https://www.nuget.org/packages/Stella.Ergosfare/absoluteLatest)

## Start in 60 seconds

Install the complete stack and its source generator:

```bash
dotnet add package Stella.Ergosfare
dotnet add package Stella.Ergosfare.SourceGenerator
```

Declare a message and its handler:

```csharp
public sealed record CreateProduct(string Name) : ICommand<Guid>;

public sealed class CreateProductHandler
    : ICommandHandler<CreateProduct, Guid>
{
    public ValueTask<Guid> HandleAsync(
        CreateProduct command,
        ErgosfareContext context)
    {
        return ValueTask.FromResult(Guid.NewGuid());
    }
}
```

Register the generated application composition:

```csharp
builder.Services.AddErgosfare(ergosfare => ergosfare
    .AddCommandModule(commands => commands.RegisterGenerated())
    .AddQueryModule(queries => queries.RegisterGenerated())
    .AddEventModule(events => events.RegisterGenerated()));
```

Dispatch through the module facade:

```csharp
var id = await commandMediator.SendAsync(
    new CreateProduct("Mechanical keyboard"));
```

`RegisterGenerated()` is emitted into your project. The generator discovers the relevant
constructs in the compilation and its referenced assemblies, builds their pipeline
compositions and leaves runtime registration to select from that frozen table. There is no
assembly scan or mutable message registry behind the call.

## Why Ergosfare

| | What it means in an application |
|---|---|
| **Compile-time composition** | Handler and interceptor relationships become generated data. Missing or inaccessible constructs surface as build diagnostics instead of runtime discovery surprises. |
| **Fast, typed dispatch** | Generated roots close dispatch generics ahead of time. Hot paths avoid reflection, `MakeGenericType`, registry scans and object-typed handler bridges. |
| **Real DI semantics** | Participants resolve from the dispatching scope. Singleton, scoped, transient and keyed registrations retain the container semantics you chose. |
| **One pipeline model** | Commands, queries, streams, events and POCO notifications use the same ordered interceptor stages and execution context. |
| **Explicit execution state** | `ErgosfareContext` carries cancellation, shared items, abort signals and nested scopes without `AsyncLocal` or ambient service providers. |
| **Native AOT ready** | Generated dispatch roots statically anchor the required generic instantiations, including value-type messages and results. |
| **Modular by design** | Use the umbrella package or install Commands, Queries and Events independently over the shared core. |

## A pipeline you can shape

Ergosfare pipelines have a main-handler stage and four interceptor stages: pre, post,
exception and final. Interceptors can be broad cross-cutting policies or narrowly typed
participants that rewrite a specific message or result.

- **Ordering:** `[Weight(n)]` orders participants within a stage.
- **Groups:** `[Group("audit")]` and dispatch-time filters select a named slice of a
  pipeline.
- **Covariant matching:** an interceptor targeting a base contract can apply to every
  assignable message.
- **Opt-out:** `[ExcludeFromPipeline]` removes unwanted indirect interceptors globally or
  by group.
- **Typed exceptions:** `...ExceptionInterceptorFor<TException>` receives the matched
  exception directly, following normal catch semantics.
- **Intentional termination:** `context.Abort()` stops the remaining pipeline and reports
  `ExecutionAbortedException` to the caller. Reason and value overloads can carry context.

Cross-cutting policies can join every built-in module by carrying its marker interfaces:

```csharp
public sealed class LoggingInterceptor
    : IAsyncPreInterceptor<IMessage>, ICommand, IQuery, IEvent
{
    public ValueTask<IMessage> HandleAsync(
        IMessage message,
        ErgosfareContext context)
    {
        // Log, enrich or reject before the main handler.
        return ValueTask.FromResult<object>(message);
    }
}
```

The generator places this construct in the command, query and event partitions. Covariant
rows connect it to assignable messages; `[ExcludeFromPipeline]` remains the escape hatch
for messages that should not participate.

## Context without ambient state

Every handler and interceptor receives a concrete `ErgosfareContext`. It exposes the state
that belongs to one dispatch and nothing broader:

- a cancellation token;
- a lazily created items dictionary;
- explicit abort operations;
- isolated child scopes for nested mediator calls.

```csharp
public async ValueTask HandleAsync(
    PlaceOrder message,
    ErgosfareContext context)
{
    using var scope = context.CreateScope();

    await commandMediator.SendAsync(
        new ReserveStock(message.OrderId),
        scope.Context);

    await eventMediator.PublishAsync(
        new OrderPlaced(message.OrderId),
        scope.Context);
}
```

The child receives clean items and inherits cancellation. Disposing the struct scope
returns its context to the pool. A directly constructed `new ErgosfareContext(...)` is
caller-owned and can be passed to the engine-level dispatch overloads when the caller needs
to provide state explicitly.

## Events are the open message lane

The event module is not limited to marker-based domain events. Its generic publish surface
accepts any non-null type, making it the home for POCO notifications and other plain
messages with broadcast semantics:

```csharp
public sealed record CacheInvalidated(string Key);

public sealed class EvictLocalCache
    : IEventHandler<CacheInvalidated>
{
    public ValueTask HandleAsync(
        CacheInvalidated message,
        ErgosfareContext context)
    {
        // ...
        return ValueTask.CompletedTask;
    }
}

await eventMediator.PublishAsync(new CacheInvalidated("products"));
```

## Discovery that scales past one project

The generator walks the application compilation and referenced assemblies. A class library
can declare handlers without carrying application-specific registration code; the final
application emits the composition and registration entry point.

Discovery keys provide opt-in slices for modular monoliths, feature sets and environment
specific participants:

```csharp
[DiscoveryKey("reporting.daily")]
public sealed class DailyReportHandler
    : ICommandHandler<BuildDailyReport>
{
    // ...
}

builder.Services.AddErgosfare(ergosfare => ergosfare
    .AddCommandModule(commands => commands
        .RegisterGenerated()
        .RegisterGenerated("reporting.*")));
```

Untagged types join default discovery. A `[DiscoveryKey]` gates a construct until an exact
key or prefix glob selects it. `[assembly: DiscoveryKey("payments")]` can tag a complete
library, while `[ExcludeFromDiscovery]` removes a construct or assembly entirely.

Reference scanning can be disabled per project:

```xml
<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>
```

## How dispatch is built

```text
compile time                         container setup                     every dispatch
────────────                         ───────────────                     ──────────────
source generator                     module registration                 mediator facade
  discovers constructs        ───▶     selects generated rows    ───▶     rents context
  emits frozen compositions           registers participants             finds typed executor
  emits dispatch roots                 preserves DI lifetimes              runs selected pipeline
```

The source generator emits one frozen composition for each known message: the main-handler
rows, four interceptor stages and their covariant rows. Module registration performs a
set-like selection from this table for one container; repeated and overlapping selections
are harmless unions. Runtime code cannot append a new pipeline row.

For each dispatch, the mediator finds an executor closed over the message's concrete type,
rents a context, resolves participant instances from the calling scope and executes the
already ordered stages. Group filters and pipeline exclusions narrow that composition
without turning dispatch back into discovery.

This closed-world boundary is deliberate: dynamically loaded assemblies cannot introduce
new message shapes after compilation. Plugin contracts should bind their constructs to a
known command, query or event marker so the application generator can include them.

## Performance

Ergosfare is optimized around application semantics rather than a synthetic minimum API:
scoped handlers remain scoped, execution state remains explicit and interceptor-rich
pipelines remain first-class. The generated plan lanes remove infrastructure work around
those semantics.

BenchmarkDotNet v0.15.8, .NET 9.0.11, Windows 11, AMD Ryzen 7 7800X3D. Measured on the tree
released as v2.7.0-preview; each row is one dispatch through the public mediator surface.
Source: [`test/Stella.Ergosfare.Benchmarking`](test/Stella.Ergosfare.Benchmarking/Program.cs).

Mediators resolved once—typical of workers and message pumps:

| Per dispatch | Ergosfare | MediatR | Mediator |
|---|---:|---:|---:|
| Command, no result | **19.9 ns / 24 B** | 60.4 ns / 192 B | 8.5 ns / 0 B |
| Query with result | **24.0 ns / 24 B** | 54.9 ns / 192 B | 9.1 ns / 0 B |
| Query through five participants | **120.2 ns / 96 B** | 205.6 ns / 1008 B | 61.4 ns / 0 B |
| Event to two handlers | **50.5 ns / 48 B** | 89.7 ns / 440 B | 16.2 ns / 0 B |

One DI scope per dispatch—typical of request processing, including scope creation and
mediator resolution:

| Per dispatch | Ergosfare | MediatR | Mediator |
|---|---:|---:|---:|
| Command | **92.5 ns / 192 B** | 113.2 ns / 352 B | 54.8 ns / 128 B |
| Query | **90.6 ns / 200 B** | 110.0 ns / 352 B | 50.8 ns / 128 B |
| Event | **106.4 ns / 232 B** | 146.3 ns / 600 B | 57.1 ns / 128 B |

These tables need context. Mediator's default singleton lifetime does less scope-sensitive
work and therefore sets a lower raw-overhead floor. Ergosfare and MediatR resolve according
to the dispatching scope in these scenarios. Ergosfare's default transient participant is
the source of the small happy-path allocation; singleton registrations or
`ForceMemoizedHandlers()` can remove it when those semantics fit the application.

Run the full benchmark suite locally:

```bash
dotnet run -c Release -f net9.0 --project test/Stella.Ergosfare.Benchmarking
```

## Packages

`Stella.Ergosfare` is the convenience meta package. Applications can instead reference
only the modules they use:

- `Stella.Ergosfare.Commands`
- `Stella.Ergosfare.Queries`
- `Stella.Ergosfare.Events`
- their `.Abstractions` and `.Extensions.MicrosoftDependencyInjection` companions
- `Stella.Ergosfare.SourceGenerator` in the composing application

Libraries that only declare messages and participants normally need the relevant
`.Abstractions` package. The application owns source generation and DI composition.

## Preview notes

The current preview retires the mutable runtime message registry. The generated frozen
composition table is now the source of pipeline truth, and module registration selects the
parts a container runs. It also replaces `IExecutionContext` with the public sealed
`ErgosfareContext` and moves plain/POCO message handling to the event module.

Notable removals include `IMessageRegistry`, descriptor APIs, `RegisterFromAssembly`,
`RegisterDescriptors`, `MediateOptions`, the low-level `IMessageMediator.Mediate` surface,
`AddCoreModule` and the old core-module builders. See the
[v2.8.0-preview changelog](CHANGELOG.md) for the complete migration inventory.

APIs marked `[Experimental]` and diagnostic IDs beginning with `ERGOEXP` sit outside the
normal compatibility promise. Consuming one requires an explicit warning opt-in; see
[COMPATIBILITY.md](COMPATIBILITY.md).

## Versioning and releases

| Branch | Line | Tags | NuGet |
|---|---|---|---|
| `main` | stable | `vX.Y.Z` | stable packages |
| `preview` | next | `vX.Y.Z-preview` | prerelease packages |

Features mature on `preview` and graduate to stable through the project's rolling release
process. Stable fixes land on `main` first and are merged forward. Release workflows enforce
that stable and preview tags point to the correct branch history.

## Contributing

Preview development targets `preview`; stable fixes target `main`. Both flow through pull
requests.

```bash
dotnet test Stella.Ergosfare.slnx
```

The full suite runs on .NET 9 and .NET 10. Contributions that change dispatch semantics
should update the public contract suite and its lane-map baseline together.

## Acknowledgments

Ergosfare's public API and module architecture were originally inspired by
[LiteBus](https://github.com/litenova/LiteBus) by A. Shafie (MIT licensed).

## License

Ergosfare is licensed under the [MIT License](LICENSE.md).

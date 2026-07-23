
# Ergosfare — v2 preview line

![Ergosfare Logo](./7101c7df-6cac-4b25-994a-60e2adbdc546.png)

[![NuGet (preview)](https://img.shields.io/nuget/vpre/Stella.Ergosfare.svg?label=nuget%20preview)](https://www.nuget.org/packages/Stella.Ergosfare)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
![Tests](https://img.shields.io/github/actions/workflow/status/stellayazilim/ergosfare/coverage_tests.yml?branch=preview&label=tests)

> **You are on the `preview` branch — the Ergosfare v2 line.** Releases from this branch are
> `v2.0.0-preview.N` pre-releases. The stable v1.x line lives on [`main`](https://github.com/stellayazilim/Ergosfare/tree/main).
> APIs here may still change between previews; see [Versioning & release model](#versioning--release-model).

- **Docs** → https://stellayazilim.github.io/Ergosfare.Docs
- **Changelog** → https://stellayazilim.github.io/ergosfare.changelog
- **Compatibility policy** → [COMPATIBILITY.md](COMPATIBILITY.md)

## Overview

Ergosfare is a source-generated, high-performance mediator for CQRS-style messaging in
.NET — commands, queries and events over one pipeline model, built around a single design
premise: **all dispatch-shape work happens at compile time or once per message type, never
per dispatch**.

Registration is emitted by a Roslyn source generator; pipeline plans are computed once per
`(message type, group set)` and cached process-wide; dispatch generics are closed at
compile time; execution contexts are pooled. A dispatch resolves handler *instances* from
the calling DI scope and walks a pre-built plan — no reflection, no `MakeGenericType`, no
registry scan, no `AsyncLocal`, and no per-dispatch context allocation on the hot path.

| Property | Mechanism |
|----------|-----------|
| Compile-time registration | Source generator discovers handlers in your compilation **and referenced assemblies**, pre-computes descriptors, emits `RegisterGenerated()` |
| Reflection-free dispatch | Generated dispatch roots close executor generics at compile time; pipeline plans pre-close generic handler types |
| ~2.3 MB / 100k dispatches | Pooled execution contexts, `ValueTask`-first surface, synchronous fast paths (MediatR: 18.3 MB) |
| Nested dispatch | `context.CreateScope()` — isolated child context with inherited cancellation for mediator calls inside handlers |
| DI-lifetime correctness | Instances resolve per dispatch from the calling scope: singleton → container-cached, scoped → one per scope, transient → one per dispatch |
| Native AOT & trimming | Every construct referenced statically via `typeof`; dispatch roots anchor all generic instantiations, value-type messages included |
| No ambient state | The execution context is a parameter, never an `AsyncLocal` |
| Modular | Commands, Queries, Events are independent packages over a shared core |

## Quick start

```bash
dotnet add package Stella.Ergosfare --prerelease
dotnet add package Stella.Ergosfare.SourceGenerator --prerelease
```

```csharp
public sealed record CreateProduct(string Name) : ICommand<Guid>;

public sealed class CreateProductHandler : ICommandHandler<CreateProduct, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateProduct command, IExecutionContext context)
        => ValueTask.FromResult(Guid.NewGuid());
}

builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated())   // compile-time registration
    .AddQueryModule(q => q.RegisterGenerated())
    .AddEventModule(e => e.RegisterGenerated()));

var mediator = provider.GetRequiredService<ICommandMediator>();
var id = await mediator.SendAsync(new CreateProduct("Laptop"));
```

`RegisterGenerated()` is emitted into your project by the source generator: every message,
handler and interceptor in the compilation — and in referenced assemblies — registers with
pre-computed descriptors, no reflection involved. `RegisterFromAssembly(...)` remains
available as the runtime-scanning escape hatch (e.g. plugins loaded at runtime).

## Compile-time discovery

**Cross-assembly.** The generator also walks referenced assemblies: a library's handlers
register through the app's generated code with zero registration code in the library.
Internal types participate when the library grants `InternalsVisibleTo`; types generated
code cannot name surface as an `ERGOSG002` diagnostic instead of silently diverging from
runtime scanning. Opt out per project with
`<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>`.

**Discovery keys — registration-time cherry-picking.**

```csharp
[DiscoveryKey("reporting.daily")]
public sealed class DailyReportHandler : ICommandHandler<BuildDailyReport> { ... }

builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c
        .RegisterGenerated()                    // default discovery: untagged types
        .RegisterGenerated("reporting.*")));    // cherry-pick by exact key or prefix glob
```

A `[DiscoveryKey]` *gates* a type out of default discovery until a registration call
selects one of its keys — feature-flagged handlers, environment-specific interceptors,
modular-monolith slices. `[assembly: DiscoveryKey("payments")]` tags a whole library;
`[ExcludeFromDiscovery]` removes a type (or assembly) from discovery entirely. The
reflection path honors the same attributes, so generated and runtime registration stay in
lockstep.

## Pipeline control

Interceptors run in four stages — pre (may rewrite the message), post (may rewrite the
result), exception, final — each in non-generic, message-typed and fully typed forms.
Selection and ordering are declarative:

- **`[Weight(n)]`** orders interceptors within a stage (descending).
- **`[Group("audit")]`** scopes handlers/interceptors to dispatch-time group filters:
  `SendAsync(cmd, settings)` with a group filter runs only matching pipeline members.
- **`[ExcludeFromPipeline]`** opts a *message* out of covariantly matched interceptors —
  blanket or per group (`[ExcludeFromPipeline("logging")]`). Interceptors registered for
  the message type itself always run; main handlers are never affected.
- **Covariant matching.** An interceptor registered for a base type or interface
  (`IEventPreInterceptor`, `ICommandPreInterceptor<IAuditedCommand>`) applies to every
  assignable message. Event broadcast delivers to covariantly matched *handlers* too —
  the event's own handlers first, then base/interface registrations.

## Nested dispatch — scopes

A handler can mediate further messages under an isolated child context — the pattern
ambient-state mediators cannot offer safely:

```csharp
public async ValueTask HandleAsync(PlaceOrder msg, IExecutionContext ctx)
{
    using var scope = ctx.CreateScope();   // struct scope — allocation-free

    var reserved = await _commands.SendAsync(new ReserveStock(msg.OrderId), scope.Context);
    await _events.PublishAsync(new OrderPlaced(msg.OrderId), scope.Context);
}
```

- The child starts with **clean items** — inner state never pollutes the outer scope —
  and **inherits the parent's cancellation token**, so nested work can't escape the outer
  cancellation chain (and forgetting to thread the token is impossible).
- An inner `Abort()` ends only the inner pipeline; the outer handler decides what happens
  next. Parallel inner dispatches with separate scopes are safe.
- All facades accept the child: `SendAsync`, `QueryAsync`, `PublishAsync` overloads take
  an `IExecutionContext`; disposal returns the pooled child.

## Performance

Executors and invokers are closed over each message's concrete runtime type — handlers are
invoked through their typed members, with no object-typed bridge anywhere. Source-generated
**dispatch roots** provide those generic closures at compile time (`MakeGenericType`
remains only as a fallback for open generics and runtime-only registrations), which also
gives Native AOT a static anchor for every instantiation — including `record struct`
messages, which shared generic code cannot cover.

Execution contexts are **pooled**: a dispatch rents a context and returns it on
completion through a `[ThreadStatic]`-first pool (plain load/store on the common
same-thread cycle), with a synchronous fast path that skips the async state machine
entirely when the pipeline completes synchronously. A context is therefore only valid for
the duration of its dispatch.

### Benchmarks

BenchmarkDotNet, `[MemoryDiagnoser]`; each operation performs **100 000 sequential
dispatches** of a no-op handler. Source: [`test/Stella.Ergosfare.Benchmarking`](test/Stella.Ergosfare.Benchmarking/Program.cs).

```bash
dotnet run -c Release -f net9.0 --project test/Stella.Ergosfare.Benchmarking
```

Environment: BenchmarkDotNet v0.15.8 · Windows 11 · AMD Ryzen 7 7800X3D · .NET 9.0.11
(RyuJIT x86-64-v4). Measured on the `preview` branch, 2026-07-24.

Two shapes are measured, for three mediators:

- **Typical usage** — how an application normally sends messages: the library's public
  mediator interface, one shared DI scope for the whole loop.
- **Web-server shape** — a fresh DI scope for every dispatch, mimicking one scope per
  HTTP request (the shape ASP.NET Core gives you).

| Scenario (100k dispatches/op) | Mean | Allocated | Gen0/1k ops |
|---|---:|---:|---:|
| **Ergosfare** — typical usage | 6.87 ms | **2.29 MB** | 47 |
| **MediatR** — typical usage | 6.09 ms | 18.31 MB | 375 |
| **LiteBus** — typical usage | 146.96 ms | 714.87 MB | 14 750 |
| **Ergosfare** — web-server shape | 18.81 ms | 38.91 MB | 813 |
| **MediatR** — web-server shape | 10.09 ms | 33.57 MB | 688 |
| Ergosfare — internal engine path (reference row) | 6.49 ms | 5.34 MB | 109 |

Scenario notes:

- The typical-usage rows are the headline: at comparable latency, Ergosfare allocates
  **an eighth of MediatR's garbage**, with Gen0 collection pressure down accordingly —
  the effect of pooled execution contexts and the `ValueTask`-first pipeline.
- The *internal engine path* row drives `IMessageMediator` directly with pre-built
  options, bypassing the public facade and its pooled context. It exists to show the
  facade adds no hidden cost — you would not write application code this way.
- LiteBus is included as a reference point: Ergosfare's API surface was heavily inspired
  by it, but the runtime is an independent implementation.
- Transient handlers intentionally allocate one instance per dispatch (that is what a
  transient registration declares). Dispatch-heavy single-scope loops that want instance
  reuse should register handlers as singletons or call `ForceMemoizedHandlers()`.

Row ↔ BenchmarkDotNet method mapping, for matching against your own runs:
`StellaErgosfare_PublicApi` / `MediatR` / `LiteBus_PublicApi` (typical usage),
`StellaErgosfare_PublicApi_ScopePerDispatch` / `MediatR_ScopePerDispatch` (web-server
shape), `StellaErgosfare` (internal engine path).

## Dispatch architecture

```
compile time                     once per (message type, groups)          every dispatch
────────────                     ───────────────────────────────          ──────────────
source generator          ──►    MessageRegistry ──► descriptor ──► plan   ──►  pooled context rented
  descriptors pre-computed         │                                             │
  dispatch roots emitted           │  six fixed stages, pre-ordered              │  executor closed over runtime type
  RegisterGenerated()              │  (direct-first, weight-ordered)             │  IHandlerReference.Resolve(scopeProvider)
                                   └─ closed generic types pre-computed          └─ pre → main → post/exception → final
```

- **Registry & descriptors.** Generated registration supplies pre-computed descriptors;
  explicit `Register<T>()` and runtime scanning feed the same registry (idempotent, safe
  to combine).
- **Pipeline plan.** Six fixed stages per message: main handlers (direct/indirect split)
  and four interceptor stages, each a pre-ordered array — direct entries first, then
  covariant, ordered by weight. Group filters and `[ExcludeFromPipeline]` apply here,
  once.
- **Dispatch.** The mediator rents a pooled context, looks up the cached executor for the
  message's runtime type (constructed through a generated dispatch root), and walks the
  plan. Handler resolution belongs to the dispatcher — the execution context deliberately
  exposes **no** service provider.

## What changes on the v2 line

Tracked in [CHANGELOG.md](CHANGELOG.md) under *Unreleased — v2 preview line*. Highlights:

- **Source-generated registration** (`Stella.Ergosfare.SourceGenerator`): compile-time
  discovery with pre-computed descriptors, cross-assembly reference scanning, discovery
  keys, and generated dispatch roots for reflection-free, AOT-complete dispatch.
- **Nested dispatch scopes**: `IExecutionContext.CreateScope()` plus external-context
  facade overloads; pooled execution contexts (contexts are valid only during their
  dispatch).
- **Pipeline exclusion**: `[ExcludeFromPipeline]` on message types; event broadcast now
  delivers to covariantly matched handlers (direct-first).
- **`AmbientExecutionContext` removed** (deprecated since v1.2.0). The context parameter
  is the only access path; the dispatch path carries zero `AsyncLocal` state.
- **Three-parameter `TModifiedResult` interceptor interfaces removed** (deprecated in
  v1.4.0). The two-parameter typed interfaces expose the typed `HandleAsync` directly.
- **`ValueTask`-first surface.** Handlers, interceptors and the mediator facades return
  `ValueTask` / `ValueTask<TResult>`; synchronously completing handlers allocate nothing.
- **Typed dispatch everywhere.** Interceptor object roots and DIM bridges removed;
  invocation is generic end to end.
- **`[Experimental]` gate introduced.** Unstable APIs ship marked
  `[Experimental("ERGOEXPxxx")]` instead of blocking the release train — see below.

## Experimental APIs

APIs marked `[Experimental]` (diagnostic IDs prefixed `ERGOEXP`) are **outside the
compatibility promise** ([COMPATIBILITY.md §4](COMPATIBILITY.md)): they may change or
disappear in any release, including patch releases. Consuming one is a compile-time error
until you opt in explicitly:

```csharp
#pragma warning disable ERGOEXP001 // example: opting into an experimental API
```

or per project:

```xml
<NoWarn>$(NoWarn);ERGOEXP001</NoWarn>
```

Current experimental surface: none — the gate is in place for future experimental APIs.

## Installation

Preview packages are published to nuget.org (and GitHub Packages) as pre-releases:

```bash
dotnet add package Stella.Ergosfare --prerelease
```

Module packages (`Stella.Ergosfare.Commands`, `.Queries`, `.Events`, plus their
`.Abstractions` and `.Extensions.MicrosoftDependencyInjection` companions) can be
installed independently — the umbrella package is a convenience bundle. Libraries that
only declare messages or handlers need just the relevant `.Abstractions` package.

## Versioning & release model

| Branch | Line | Tags | NuGet |
|--------|------|------|-------|
| `main` | stable v1.x | `vX.Y.Z` | stable |
| `preview` | v2 | `v2.0.0-preview.N` | pre-release |

- **No release candidates.** Features mature on `preview` and graduate to stable
  individually (rolling graduation) — there is no big-bang RC freeze.
- Pre-release tags must point at `preview` commits, stable tags at `main` commits; the
  release workflows enforce this by ancestry check.
- Stable fixes land on `main` first; `main` is merged into `preview` regularly.
- Genuinely unstable APIs are not blocked on graduation — they ship `[Experimental]`.

## Contributing

v2 work targets `preview`, stable fixes target `main` — both through pull requests.

```bash
dotnet test Stella.Ergosfare.slnx            # full suite (net9.0 + net10.0)
```

## Acknowledgments

Ergosfare's design — including its public API surface, naming conventions, and module
architecture — is heavily inspired by [LiteBus](https://github.com/litenova/LiteBus)
by A. Shafie (MIT licensed).

## License

Ergosfare is licensed under the [MIT License](LICENSE.md).

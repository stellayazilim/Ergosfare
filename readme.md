
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

Ergosfare is a mediator for CQRS-style messaging pipelines in .NET, built around one design
premise: **all dispatch-shape work happens once per message type, not once per dispatch**.

The pipeline of a message — its handler, its pre/post/exception/final interceptors, their
order, and the closed generic types backing them — is computed a single time per
`(message type, group set)` and cached process-wide as an immutable plan. A dispatch then
only resolves handler *instances* against the calling DI scope and walks the plan. There is
no reflection, no `MakeGenericType`, no registry scan, and no `AsyncLocal` access anywhere
on the dispatch path.

| Property | Mechanism |
|----------|-----------|
| Reflection-free dispatch | Pipeline plans pre-close generic handler types at plan build |
| DI-lifetime correctness | Instances resolve per dispatch from the calling scope: singleton → container-cached, scoped → one per scope, transient → one per dispatch |
| Opt-in memoized fast path | All-singleton pipelines (or `ForceMemoizedHandlers()`) cache instances inside the plan, pinned to the root provider |
| No ambient state | The execution context is a parameter, never an `AsyncLocal`; v2 removed the ambient API entirely |
| Modular | Commands, Queries, Events are independent packages over a shared core |

## Dispatch architecture

```
registration                    once per (message type, groups)          every dispatch
─────────────                   ───────────────────────────────          ──────────────
Register<THandler>()   ──►   MessageRegistry ──► descriptor ──► plan   ──►   mediator (scoped, thin)
assembly scan                    │                                             │
                                 │  six fixed stages, pre-ordered              │  IHandlerReference.Resolve(scopeProvider)
                                 │  (direct-first, weight-ordered)             │  handler.HandleAsync(message, context)
                                 └─ closed generic types pre-computed          └─ interceptors: pre → main → post/exception → final
```

- **Registry & descriptors.** Handlers register explicitly (`module.Register<T>()`) or via
  assembly scanning. Each message type gets a descriptor describing its handler set.
- **Pipeline plan.** From the descriptor, a plan with six fixed stages is built: main
  handlers (direct/indirect kept split for single-handler validation) and four interceptor
  stages (pre, post, exception, final), each a pre-ordered `IReadOnlyList` — direct entries
  first, then indirect, ordered by weight. Generic handler definitions are closed against
  the concrete message type here, once.
- **Dispatch.** `IMessageMediator` is scoped but thin: its only per-scope job is carrying
  the calling scope's `IServiceProvider` into the pipeline. Each `IHandlerReference` in the
  plan resolves its instance from that provider, so registered DI lifetimes are honored
  exactly. Resolution belongs to the dispatcher — the execution context deliberately
  exposes **no** service provider.
- **Execution context.** `IExecutionContext` is a pure data carrier between pipeline
  members: `CancellationToken`, an `Items` bag, and `Abort()`. It is handed to every
  handler and interceptor as a parameter.
- **Interceptors.** Pre (may rewrite the message or stop the pipeline), post (may rewrite
  the result), exception (observe/replace/rethrow), final (always runs). All four exist in
  non-generic, message-typed, and fully typed result-safe forms; group and weight
  attributes control selection and ordering.

## Benchmarks

BenchmarkDotNet, `[MemoryDiagnoser]`; each operation performs **100 000 sequential
dispatches** of a no-op handler. Source: [`test/Stella.Ergosfare.Benchmarking`](test/Stella.Ergosfare.Benchmarking/Program.cs).
Run it yourself:

```bash
dotnet run -c Release -f net9.0 --project test/Stella.Ergosfare.Benchmarking
```

Environment: BenchmarkDotNet v0.15.8 · Windows 11 · AMD Ryzen 7 7800X3D · .NET 9.0.11
(RyuJIT x86-64-v4). Measured on the `preview` branch (executor dispatch), 2026-07-23.

| Scenario (100k dispatches/op) | Mean | Allocated |
|---|---:|---:|
| `StellaErgosfare` — raw `IMessageMediator` loop | 6.33 ms | 5.34 MB |
| `StellaErgosfare_PublicApi` — `ICommandMediator.SendAsync` | 7.19 ms | 5.34 MB |
| `MediatR` — `IMediator.Send` | 6.24 ms | 18.31 MB |
| `LiteBus_PublicApi` — `ICommandMediator.SendAsync` | 148.89 ms | 714.87 MB |
| `StellaErgosfare_PublicApi_ScopePerDispatch` — fresh scope each dispatch | 18.42 ms | 41.96 MB |
| `MediatR_ScopePerDispatch` — fresh scope each dispatch | 11.26 ms | 33.57 MB |

All rows dispatch through pipelines closed over the message's concrete type (cached
executors) — there is no object-typed bridge anywhere on the dispatch path, and the
public facade allocates exactly what the raw mediator does. Synchronously completing
handlers allocate nothing on the handler side, which a `Task`-based surface cannot do.

Scenario notes:

- *ScopePerDispatch* rows create a fresh DI scope per dispatch — the realistic
  per-request server shape, and the case the v2 plan/reference architecture targets.
- The raw `StellaErgosfare` row bypasses the public mediator facade and drives
  `IMessageMediator` directly with pre-built options.
- LiteBus is included as a reference point: Ergosfare's API surface was heavily inspired
  by it, but the runtime is an independent implementation.
- Transient handlers intentionally allocate one instance per dispatch (that is what a
  transient registration declares). Dispatch-heavy single-scope loops that want instance
  reuse should register handlers as singletons or call `ForceMemoizedHandlers()`.

## What changes on the v2 line

Tracked in [CHANGELOG.md](CHANGELOG.md) under *Unreleased — v2 preview line*. So far:

- **`AmbientExecutionContext` removed** (deprecated since v1.2.0), together with
  `EnableAmbientExecutionContext()` and the DI registration of `IExecutionContext`. The
  context parameter is the only access path; the dispatch path carries zero `AsyncLocal`
  state.
- **Three-parameter `TModifiedResult` interceptor interfaces removed** (deprecated in
  v1.4.0). The two-parameter typed interfaces expose the typed `HandleAsync` directly.
- **`ValueTask`-first surface.** Handlers, interceptors, and the mediator facades return
  `ValueTask` / `ValueTask<TResult>`. One surface serves both worlds: `async` bodies
  compile unchanged, existing `Task`-producing code wraps allocation-free via
  `new ValueTask<T>(task)`, and synchronously completing handlers allocate nothing.
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
installed independently — the umbrella package is a convenience bundle.

## Quick start

```csharp
public record CreateProduct(string Name) : ICommand<Guid>;

public sealed class CreateProductHandler : ICommandHandler<CreateProduct, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateProduct command, IExecutionContext context)
        => ValueTask.FromResult(Guid.NewGuid());
}

var services = new ServiceCollection()
    .AddErgosfare(o =>
    {
        o.AddCommandModule(cfg => cfg.RegisterFromAssembly(Assembly.GetExecutingAssembly()));
    })
    .BuildServiceProvider();

var mediator = services.GetRequiredService<ICommandMediator>();
var id = await mediator.SendAsync(new CreateProduct("Laptop"));
```

A typed post-interceptor (v1.4+ two-parameter form — the only form on the v2 line):

```csharp
public sealed class AuditInterceptor : ICommandPostInterceptor<CreateProduct, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateProduct command, Guid result, IExecutionContext context)
    {
        // observe or replace the typed result — no casts, no object round-trip
        return ValueTask.FromResult(result);
    }
}
```

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

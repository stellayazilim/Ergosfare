# Stella.Ergosfare.Plugins.Outbox

An opt-in outbox using ordinary Ergosfare command and event plans. The NuGet package
includes the runtime, an InMemory store, a bounded hosted worker, and its own source
generator under `analyzers/dotnet/cs`. No changes to the Ergosfare engine are required.

## Define a message and its handler

```csharp
using Stella.Ergosfare.Plugins.Outbox;
using Stella.Ergosfare.Core.Abstractions;

[OutboxMessage("product-created/v1")]
public sealed partial record ProductCreated(Guid ProductId, string Name);

public sealed class ProductCreatedHandler : IOutboxHandler<ProductCreated>
{
    public ValueTask HandleAsync(OutboxEvent<ProductCreated> delivery, ErgosfareContext context)
    {
        ProductCreated message = delivery.Message;
        // Background work. Make side effects safe to retry.
        return ValueTask.CompletedTask;
    }
}
```

The POCO implements no Ergosfare interface. `[OutboxMessage]` generates JSON reading,
writing and registration without reflection or a user-written `JsonSerializerContext`.
The optional contract defaults to the fully qualified type name plus `/v1`. For persisted
messages, choose an explicit stable contract before renaming types or namespaces.

`IOutboxHandler<T>` inherits `IEventHandler<OutboxEvent<T>>`. An ordinary
`IEventHandler<T>` does not receive this wrapper. Generic event interceptors still follow
normal Ergosfare composition rules. The handler's generic interface graph also names the
closed save participant, allowing the existing generator to discover both wrapper plans.

## Configure

```csharp
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

services.AddErgosfare(options =>
{
    options.AddCommandModule(commands => commands.AddGenerated());
    options.AddEventModule(events => events.AddGenerated());
    options.AddOutboxPlugin(outbox =>
    {
        outbox.UseInMemory();
        outbox.MaxConcurrency = 8;
    });
});
```

Keep the existing `Stella.Ergosfare.SourceGenerator` analyzer reference as well. Both
generators read original user source independently; neither consumes the other's output.
For repository ProjectReference consumers, reference the outbox generator project as an
analyzer explicitly (project analyzer references do not propagate). NuGet consumers get
it automatically from the outbox package.

The existing plugin surface requires opting into `ERGOEXP002`. This first-party assembly
uses `ErgosfareSourceGeneratorForceScanReferences` to opt into the existing reference scan.
Choose a store explicitly. Both command and event mediators are checked when the worker
starts, after module configuration, so registration order is immaterial. A .NET Generic
Host starts the registered worker; building a DI container alone does not run it.

## Enqueue

Inside an ordinary generated command or event handler:

```csharp
await context.EnqueueOutboxAsync(new ProductCreated(product.Id, product.Name));
```

The existing `Start` hook attaches the calling scope's command mediator. The extension
opens a child context and dispatches `OutboxEntry<T>`. Its save handler serializes a
snapshot and calls the scoped store. Neither request context nor live objects are stored.
The default discovery key is supported. A manually created context, or a child context
before entering a generated pipeline, has no binding and fails explicitly.

Enqueue completion means store acceptance, not background completion. Transactional
adapters must stage the write in the application's unit of work; enqueue does not commit
that transaction. Every enqueue generates a new ID and represents a distinct request.

## Worker

The worker claims records and publishes `OutboxEvent<T>` through a mediator resolved in
a new scope. It acknowledges only after the entire pipeline succeeds. A handled/swallowed
exception counts as success. Custom event participants can be composed normally.

- `MaxConcurrency` bounds hosted slots through dispatch, acknowledgment and disposal.
  `ProcessOneAsync` is available for manual hosts, which own their concurrency limit.
- Claims have fencing tokens; stale owners cannot acknowledge reclaimed records.
- Long deliveries renew leases using separate persistence scopes. Renewal failures cancel
  dispatch. The handler's DbContext is never used concurrently by the renewal loop.
- Failures wait `RetryDelay`, stopping at `MaxAttempts`. InMemory dead letters are retained;
  operator listing/requeue is not implemented yet. Completed InMemory records are removed.
- Shutdown uses a bounded independent token for cleanup. Crashes and acknowledgment failures
  leave reclaimable leases. Delivery is at least once; earlier broadcast handlers can run
  again if a later handler fails.

## JSON support and diagnostics

The first generator supports public, top-level, non-generic partial classes and records.
Properties must have public getters. Public constructors must map their parameters to
properties; remaining properties need public set/init accessors. Public fields, inheritance,
indexers and custom JSON attributes are rejected with `OUTBOX001`, never reflection fallback.

Supported values: string, bool, integer types, float, double, decimal, Guid, DateTime,
DateTimeOffset, nullable values, one-dimensional arrays and List<T> of supported values.
Nested object DTOs, enums, dictionaries and custom converters are not yet supported.
Property names are preserved. Reads require every declared field; extra JSON fields are
ignored. Schema changes therefore require explicit compatibility/version planning.
Duplicate contracts in one compilation produce `OUTBOX002`; cross-assembly collisions
are detected when constructing options.

Advanced unannotated types can still use `outbox.Register(contract, JsonTypeInfo<T>, groups)`
with an existing source-generated JSON context. Annotated types register automatically and
must not also be manually registered. There is no dynamic assembly scanning.

## Persistence adapters

`UseStore(Action<IServiceCollection>)` lets separate provider packages register one
`IOutboxStore`, optionally scoped. Append must join domain persistence for atomic writes.
Claims and state updates must check ownership atomically. Worker operations use independent
scopes/transactions; custom stores must honor cancellation.

InMemory is for tests/development: records are immediately visible, are not rolled back
with domain work, and disappear at process exit. It provides neither durability nor domain
transaction atomicity. EF Core/Redis adapters are not included in this first package.

The package follows the repository's common version and release workflow.
# Stella.Ergosfare

High-performance, source-generated mediator for CQRS-style messaging in .NET — commands,
queries and events over one reflection-free dispatch pipeline.

This is the **meta package**: referencing it brings in the full stack (core, commands,
queries, events and their DI extensions). Pair it with
[`Stella.Ergosfare.SourceGenerator`](https://www.nuget.org/packages/Stella.Ergosfare.SourceGenerator)
for compile-time registration.

```csharp
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated())
    .AddQueryModule(q => q.RegisterGenerated())
    .AddEventModule(e => e.RegisterGenerated()));

public sealed record Greet(string Name) : ICommand<string>;

public sealed class GreetHandler : ICommandHandler<Greet, string>
{
    public ValueTask<string> HandleAsync(Greet message, IExecutionContext context)
        => new($"hello, {message.Name}");
}

var greeting = await commandMediator.SendAsync(new Greet("world"));
```

**Why Ergosfare**

- All dispatch-shape work happens **once per message type** — no reflection, no
  `MakeGenericType`, no registry scan and no ambient state on the dispatch path.
- **Source-generated registration** with cross-assembly discovery: a library's handlers
  register through the app's generated code; cherry-pick with `[DiscoveryKey]` patterns.
- **Nested dispatch** as a first-class pattern: `context.CreateScope()` gives an isolated
  child context with inherited cancellation for mediator calls inside handlers.
- **Pooled execution contexts** and a `ValueTask`-first surface: ~2.3 MB total allocations
  per 100k dispatches (MediatR: 18.3 MB), Native AOT friendly via generated dispatch roots.

Docs, benchmarks and the full feature tour:
[github.com/stellayazilim/Ergosfare](https://github.com/stellayazilim/Ergosfare)

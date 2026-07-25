# Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection

Event module DI wiring for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
`AddEventModule(...)` and the `EventModuleBuilder` with explicit registration
(`Register<T>()`), discovery-aware assembly scanning (`RegisterFromAssembly`), and
source-generated registration (`RegisterGenerated()` /
`RegisterGenerated("discovery.key.*")`).

```csharp
builder.Services.AddErgosfare(o => o
    .AddEventModule(e => e.RegisterGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

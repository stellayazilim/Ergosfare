# Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection

Event module DI wiring for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
`AddEventModule(...)` and the `EventModuleBuilder` with explicit registration
(`Register<T>()`) and
source-generated registration (`AddGenerated()` /
`AddGenerated("discovery.key.*")`).

```csharp
builder.Services.AddErgosfare(o => o
    .AddEventModule(e => e.AddGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

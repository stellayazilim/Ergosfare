# Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection

Query module DI wiring for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
`AddQueryModule(...)` and the `QueryModuleBuilder` with explicit registration
(`Register<T>()`) and
source-generated registration (`AddGenerated()` /
`AddGenerated("discovery.key.*")`).

```csharp
builder.Services.AddErgosfare(o => o
    .AddQueryModule(q => q.AddGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

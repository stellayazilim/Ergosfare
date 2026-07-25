# Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection

Query module DI wiring for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
`AddQueryModule(...)` and the `QueryModuleBuilder` with explicit registration
(`Register<T>()`), discovery-aware assembly scanning (`RegisterFromAssembly`), and
source-generated registration (`RegisterGenerated()` /
`RegisterGenerated("discovery.key.*")`).

```csharp
builder.Services.AddErgosfare(o => o
    .AddQueryModule(q => q.RegisterGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

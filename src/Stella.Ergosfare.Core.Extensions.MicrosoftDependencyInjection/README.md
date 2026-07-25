# Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection

Microsoft.Extensions.DependencyInjection wiring for
[Ergosfare](https://github.com/stellayazilim/Ergosfare)'s core: `AddErgosfare(...)`, the
module registry, and the core module builder used to compose command/query/event modules.

```csharp
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

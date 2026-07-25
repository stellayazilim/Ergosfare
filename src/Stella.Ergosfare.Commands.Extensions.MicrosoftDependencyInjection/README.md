# Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection

Command module DI wiring for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
`AddCommandModule(...)` and the `CommandModuleBuilder` with explicit registration
(`Register<T>()`), discovery-aware assembly scanning (`RegisterFromAssembly`), and
source-generated registration (`RegisterGenerated()` /
`RegisterGenerated("discovery.key.*")`).

```csharp
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c.RegisterGenerated()));
```

Applications usually get this transitively through the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package.

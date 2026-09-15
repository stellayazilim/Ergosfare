# Stella.Ergosfare.SourceGenerator

Roslyn source generator for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
compile-time registration that replaces reflection-based assembly scanning.

The generator discovers every Ergosfare construct (messages, handlers, interceptors) in
your compilation **and its referenced assemblies**, pre-computes their handler descriptors,
and emits fixed selection tables, participant DI factories and executable plans.
`AddGenerated()` belongs to the public module builders; the generator supplies its selection
data. Startup validates and binds existing plans without reflection-based discovery or
runtime pipeline construction. Value-type messages are supported.

```csharp
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c
        .AddGenerated()                  // default discovery
        .AddGenerated("reporting.*")));  // cherry-pick [DiscoveryKey] gated types
```

- `[DiscoveryKey("key")]` gates a type behind explicit selection; `[ExcludeFromDiscovery]`
  removes it from discovery entirely.
- Reference scanning is on by default; opt out per project with
  `<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>`.
- Diagnostics: `ERGO001` (inaccessible registrable type), `ERGO002` (invisible type in
  a referenced assembly), `ERGO025` (explicit selection through the wrong module).
- Constructor shapes without a generated factory require an explicit DI factory registered
  before `AddErgosfare`; Ergosfare does not install a reflective activation fallback.

This is a development-time dependency (`analyzers/`); it adds no runtime assembly.

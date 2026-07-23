# Stella.Ergosfare.SourceGenerator

Roslyn source generator for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
compile-time registration that replaces reflection-based assembly scanning.

The generator discovers every Ergosfare construct (messages, handlers, interceptors) in
your compilation **and its referenced assemblies**, pre-computes their handler descriptors,
and emits `RegisterGenerated()` extensions plus dispatch roots that let the runtime build
its pipelines without reflection — trimming- and Native AOT-friendly, value-type messages
included.

```csharp
builder.Services.AddErgosfare(o => o
    .AddCommandModule(c => c
        .RegisterGenerated()                  // default discovery
        .RegisterGenerated("reporting.*")));  // cherry-pick [DiscoveryKey] gated types
```

- `[DiscoveryKey("key")]` gates a type behind explicit selection; `[ExcludeFromDiscovery]`
  removes it from discovery entirely.
- Reference scanning is on by default; opt out per project with
  `<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>`.
- Diagnostics: `ERGOSG001` (inaccessible registrable type), `ERGOSG002` (invisible type in
  a referenced assembly).

This is a development-time dependency (`analyzers/`); it adds no runtime assembly.

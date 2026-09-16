# Stella.Ergosfare.SourceGenerator

Roslyn source generator for [Ergosfare](https://github.com/stellayazilim/Ergosfare):
compile-time registration that replaces reflection-based assembly scanning.

The generator discovers every Ergosfare construct (messages, handlers, interceptors) in
your compilation **and its referenced assemblies**, pre-computes their handler descriptors,
and emits fixed selection tables, typed participant DI registrations and executable plans.
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
- The configured DI container selects constructors and activates injected participants.
  Existing registrations, service lifetimes and disposal ownership are preserved.
  NativeAOT support removes Ergosfare-owned runtime discovery and composition; it does
  not require the container or application dependency graph to be reflection-free.

This is a development-time dependency (`analyzers/`); it adds no runtime assembly.

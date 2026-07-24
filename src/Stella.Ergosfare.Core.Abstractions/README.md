# Stella.Ergosfare.Core.Abstractions

Core contracts of [Ergosfare](https://github.com/stellayazilim/Ergosfare): handler and
interceptor interfaces (`IHandler<,>`, `IAsyncHandler<>`, pre/post/exception/final
interceptors), the message registry and descriptor surfaces, `IExecutionContext` with
scoped nested dispatch (`CreateScope()`), discovery attributes (`[Weight]`, `[Group]`,
`[DiscoveryKey]`, `[ExcludeFromDiscovery]`, `[ExcludeFromPipeline]`) and the generated
dispatch roots store.

Reference this package from **libraries that declare messages, handlers or interceptors**
without pulling in the runtime. Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package instead.

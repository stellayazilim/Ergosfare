# Stella.Ergosfare.Core

Runtime core of [Ergosfare](https://github.com/stellayazilim/Ergosfare): scope-aware
mediators, direct execution of generated plans, and execution-context ownership.

The generated plan is itself the executor. Its immutable descriptor records the
participants and result adapter; its body contains the ordered handler/interceptor calls.
There is no runtime executor factory, frozen dispatch wrapper, handler-reference graph,
pipeline-shape construction, or group-composition cache on the dispatch path.

Code generation selects construction per participant. A safe public parameterless
constructor is emitted as `new Participant()`. Constructor dependencies are handled by
resolving the participant from the calling DI scope. Generated DI factories support
disposable participants and closed generic types. Constructor shapes the generator cannot
model require an explicit DI factory before `AddErgosfare`. A directly constructed participant bypasses
DI registrations, including factory and singleton overrides; injected participants retain
their DI lifetimes and overrides.

Known group sets get specialized plan bodies without participant filtering. Dynamic
group sets use a generated full plan with local guards, including missing/ambiguous
handler checks for commands and queries. `GroupSet` lookup does not sort or copy group names. Grouped
streaming queries remain unsupported.

`AddErgosfare` validates selected compositions and binds references to the original generated
plans before the service provider is built. Incompatible plans fail at startup without
building dependency graphs. Dispatch performs one plan lookup and
invokes that instance; there is no second admission lookup or adapter/options DI query.
Stateless result adapters are selected and validated by the generator and called directly
by the compiled plan. `UseDefaultResultAdapter(typeof(...))` is a compile-time declaration,
not an adapter service registration. Descriptor lookup is exact: inheritance and closed
generic coverage are generated, with no runtime ancestor walk or generic normalization.

Context pooling is unchanged. Caller-owned contexts remain caller-owned; streaming
queries retain their context for enumeration. This refactor removes infrastructure
allocations, not handler instances or asynchronous execution state.

This package is an implementation detail shared by the command/query/event modules.
Applications should reference the
[`Stella.Ergosfare`](https://www.nuget.org/packages/Stella.Ergosfare) meta package;
libraries that only declare handlers need
[`Stella.Ergosfare.Core.Abstractions`](https://www.nuget.org/packages/Stella.Ergosfare.Core.Abstractions).

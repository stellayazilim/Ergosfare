## v2.13.0-preview – '2026-08-17'

Preview release. The theme: **what the compiler already knows, the runtime stops asking
again.** The result adapter was answering at first dispatch a question the generator had
resolved at build time, over `Type` objects and a second unifier kept in agreement by hand.
It now reads a generated table, and the layer's reflection is gone.

On the other side, a message can now arrive in chunks. Streaming meant one thing — a query
whose result came over time — and the direction that matters for a converter or an upload was
missing: a handler that has to read four gigabytes cannot be handed it as a single value.

### The result adapter stops asking at run time what the generator already answered

* The result adapter binding is a generated table. Three tiers used to be answered by
  reflection at first dispatch: an annotated adapter was activated, the configured fallback was
  closed over the slot by a second unifier living beside the generator's, and both attributes
  were read off the message's base chain. The generator writes one entry per (message, result)
  slot an annotation binds, one per message that opts out, and one per result type the fallback
  serves — each passing its adapter as a type argument constrained to the slot's contract, so
  an adapter that does not serve the slot is a compile error in the generated file. What
  reaches the runtime is a dictionary read and one interface call.
* `DefaultResultAdapter`'s constructor is internal. **Breaking.** A carrier built by hand
  names an adapter no generated table answers for, which is the same hole `ERGO019` closes at
  the call site; the fallback is declared through `UseDefaultResultAdapter`, the call the
  generator reads. A container configured that way and reached by an application the generator
  never ran for now says so on the first dispatch instead of serving nobody in silence.
* This was reached even in a generated application: a staged plan bakes its adapter, but the
  executor still had to bind one reflectively to compare identity against it, so the plan's
  compile-time answer never removed the run-time one.
* Measured the way the reflective arms were measured before: with each replaced by a throw and
  the suite run, the shapes reaching the adapter layer drop from 18 to 0. Every trimming and
  AOT suppression in the layer is gone with them.

### A message can arrive in chunks

* `ErgosfareStream<TChunk>` is a message whose payload arrives a chunk at a time, with
  `ErgosfareCommandStream<TChunk, TMeta[, TResult]>` and `ErgosfareQueryStream<TChunk, TMeta,
  TResult>` over it. What makes a dispatch streaming is the message's own type — not the verb
  it is sent with, not the module it belongs to — so a command streams exactly the way a query
  does, and there is no `StreamAsync`, no streaming handler contract and no lane of its own.
* Streaming used to mean one thing only: a query whose *result* arrives over time. The other
  direction was missing, and it is the one that matters for a converter or an upload — a
  handler that must read four gigabytes cannot be handed it as a single value. The two are
  independent axes now: chunks in is a property of the message, chunks out is a property of
  the result (`TResult = IAsyncEnumerable<T>`), and either, neither or both may hold.
* The channel is bounded, so a producer faster than the handler waits instead of filling
  memory, and single-pass, so nothing is replayed — which is why a stream message has no retry
  and no resume: the handler runs once for the whole sequence. It is also an
  `IAsyncEnumerable<TChunk>`, so a handler writes `await foreach (var chunk in command)` and
  nothing stands between it and the payload.
* `AsStream()` and `Chunked()` bridge to and from `System.IO.Stream`, written against
  `IAsyncEnumerable<ReadOnlyMemory<byte>>` rather than against the message: they belong to the
  byte shape, and a sequence of frames should not carry a method claiming to be a stream. Each
  chunk owns its bytes — handing out slices of one reused buffer would be cheaper by an
  allocation and would leave a handler that kept a chunk reading memory the next read
  overwrote.
* `StreamInfo` is what a stream reports about itself: chunks taken, duration, and how it
  ended. It describes one stream, so an operation that takes chunks in and hands chunks out
  has two — the two directions start, end and fail independently.
* A pipeline that stops ends the stream with it. The two are separate synchronisation
  objects, so a stage refusing an upload or a handler failing said nothing to a caller
  waiting on a full buffer — it would have waited for a reader that was never coming, and the
  pump task would have leaked with the chunks it held. The executors now close the channel
  however the dispatch turns out, and the next write fails with a message naming what to do:
  await the dispatch to see why it ended.
* The metadata is the half of the message that exists before the payload moves, and that is
  what it is for: the stages that run before the handler see it and nothing else, so an upload
  can be refused without a byte of it arriving.
* The whole streaming surface is marked `[Experimental]` under `ERGOEXP003`. What a stream
  message *is* — bounded, single-pass, a chunk at a time — is settled; what the pipeline does
  around one is not: which stages it gets and what they are handed, and how a refused dispatch
  reaches a caller that is still writing. Experimental rather than obsolete because none of it
  has shipped, and in this repository experimental is an error by default — the honest default
  for a surface nobody depends on yet. Opt in with `<NoWarn>$(NoWarn);ERGOEXP003</NoWarn>`.
* Nothing on the dispatch path changed. A stream message is an ordinary `ICommand<TResult>`
  with an ordinary handler, so it gets the same compiled plan, the same interceptor stages and
  the same frozen composition every other message gets — measured rather than assumed: a
  stream message with a pre- and a post-interceptor emits `AddStagedPlan<…>` with both baked
  into it.

### Diagnostics

* `ERGO019`, `ERGO020` and `ERGO021` judge the `UseDefaultResultAdapter` call: the argument
  must be a literal `typeof` the compilation resolves, a compilation names one default adapter,
  and generated code must be able to name and construct it. **Breaking.** An unreadable or
  duplicated call used to turn compile-time baking off for the whole compilation and leave the
  answer to the runtime; there is no runtime answer any more, so what used to compile into a
  slower path now fails the build. An adapter serving several result families does so through
  several `IResultAdapter<TResult>` implementations.
* `ERGO011` now also reports an annotated adapter the generated registration cannot name —
  inaccessible from the compilation that declares the message. Same reason: the annotation's
  binding lives in the generated table or nowhere.
* `ERGO022` reports a published stream message. A publish delivers to every subscriber and a
  chunk channel is consumed once, so whichever subscriber reads first takes the payload and
  the rest see nothing — which one that is depending on registration order. An error: no
  arrangement of subscribers makes it work.

## v2.12.0-preview – '2026-08-17'

Preview release. The theme: **a pipeline stops failing in silence.** Four shapes compiled,
ran, and delivered the wrong thing without saying so — a publish nobody subscribes to, an
exception interceptor answering a dispatch with `null`, a stream dropping a `null` element, a
`Register` naming a type only run time knows. Each of them is now either a build error or a
correct result, and the settings that let a caller opt into the silence are gone.

Underneath it, the same work continues on the other side: what a dispatch closes at run time
keeps moving to compile time, so the difference between a shape that works in development and
one that fails at publish keeps shrinking.

### A publish that reaches nobody

* The dead-dispatch judgment no longer exempts event sites. Publishing an event no subscriber
  in the compilation serves fails the build with `ERGO005` in a composition root. **Breaking.**
* The exemption rested on "reaching zero subscribers is a legal no-op", which answers a
  different question: the runtime default decides what happens when no handler matched on a
  call, while the judgment asks whether any handler can ever match. Declaring the event and
  forgetting the subscriber is the ordinary way to arrive there, and it used to compile in
  silence.
* `throwIfNoHandlerFound` is removed from `PublishAsync`. **Breaking.** It asked the caller to
  decide at run time about something the compilation can prove. What remains at run time is a
  selection the container made — which is what the caller asked for — so reaching nobody
  returns, and it now does so unconditionally. Measured before removing: the flag was never
  passed `true` outside the tests that existed to exercise it.
* The `string[]` group overloads go with it. **Breaking.** The full call takes
  `IEnumerable<string>?`, which subsumes them, and keeping both made
  `PublishAsync(@event, ["a"], token)` ambiguous — a trap every caller would meet, rather than
  the two internal sites that had to be adjusted.

### A plain domain type is an event message once a subscriber names it

* A domain event declared without an Ergosfare reference used to compile, register its
  handler, and never dispatch: the handler was collected — its contract carries the marker —
  and the message had no pipeline to reach it with.
* Requiring `IEvent` on the message pushed a mediator reference into the layer that declares
  domain events, which runs the wrong way. Nothing in the publish path needed it:
  `IEventHandler<TEvent>`, `PublishAsync<TEvent>` and `FrozenBroadcastDispatch<TEvent>` are
  declared over `notnull`, because a broadcast carries no result and asks nothing of
  `IMessage`.
* What needed the marker was being *seen*. An `IEventHandler<T>` signature is not evidence
  pointing at a message — it is the classification itself, so the message is born from the
  subscriber's visit and its own declaration never has to be found. That is also why this
  works across assembly boundaries, where the symbol comes from the contract's type argument.
* `EventModuleBuilder.Register<TEvent>()` takes `notnull`. The module assertion on the `Type`
  overload now applies to participants only: a subscriber or interceptor belongs to a module
  and registering one in the wrong module is still reported, while a message belongs to
  nothing and has no marker to assert against.

### The result lane can take its message as a type argument

* `DispatchAsync<TMessage, TResult>` joins the lanes that name their message. Every other one
  already did; this one took the result and read the message's type back per call — not a
  decision but an inference accident, since the return type forces `TResult` to be a type
  parameter and C# does not infer type arguments through constraints.
* The command and query facades carry five typed shapes each — groups, context, cancellation
  token, `GroupSet`, `string[]` — mirroring the untyped surface rather than adding a second
  vocabulary. They are default interface methods, so an existing `ICommandMediator` or
  `IQueryMediator` implementation keeps compiling and forwards to its untyped call. Both
  facades pin that the shipped mediators override them, because an implementation that does
  not still returns the right answer and simply never reaches the typed engine path.
* Not a hot-path change, and checkable rather than remembered: three paired benchmark runs put
  the typed and untyped lanes within a nanosecond of each other, in both directions. What it
  buys is the construction path — the executor closes from arguments the compiler already
  resolved, so the `MakeGenericType` arm the untyped lane takes for a message with no
  generated root is never reached.
* The streaming members stay untyped on purpose: their shape is under revision, and adding a
  surface to something scheduled to change is work that has to be undone.

### Reflection keeps leaving the dispatch path

* The native result carriers name their own adapter. Binding a slot to `Result<T>` used to
  close `ResultExceptionAdapter<>` with `MakeGenericType` and read its `Instance` field
  reflectively — an answer only a JIT can give. The slot is a generic context and the carrier
  is the closed type in it, so the carrier answers for itself. 9 of the adapter layer's 18
  reflective bindings were this one, and the `IL2055`/`IL3050` suppressions are gone with it.
* Every hidden message the compilation declares gets a dispatch root, and its result contracts
  get theirs. `[ExcludeFromDiscovery]` keeps a type out of bulk collection; it says nothing
  about dispatch, and rooting is not registering — `AddMessage<T>()` selects nothing into any
  container. Rooting the message while skipping `AddResult`/`AddStream` left a hidden
  `ICommand<string>` closing one generic from the table and the other through
  `MakeGenericType`.
* Measured by making the reflective arms throw and running the suite: the shapes reaching them
  drop from 25 to 2. What remains is generic messages, whose closed forms are never rooted
  because an open definition is not a dispatchable message — tracked separately.

### The plan gates stop disqualifying legal pipelines

* The single-handler gate counted handler registrations per message and knew nothing about
  groups, so two handlers in different groups — the canonical use of groups, unambiguous at
  every set — read as a contested pipeline and lost every plan for the message, including the
  ungrouped default. The count is now taken per plan, within the (message, group set) pair a
  plan is built for.
* The covariant gate threw the plan away whenever any base type of the message also had a
  handler — a shape the priority ladder resolves without hesitation, and the very idiom the
  v2.3.0 announcement recommends for cross-cutting handlers. A message with exactly one direct
  handler is plannable regardless of what its bases carry; the staged plans now carry their
  covariant segment for the gate alone, since none of it is called but the gate compares the
  composition segment by segment.

### Removed: the two-parameter pre-interceptor contracts

* `ICommandPreInterceptor<TCommand, TModifiedCommand>`, `IQueryPreInterceptor<TQuery, TModifiedQuery>`
  and `IEventPreInterceptor<TEvent, TModifiedEvent>` are gone. **Breaking.**
* They existed to return a *narrower* message than the one that arrived, from a time when the
  single-parameter contracts still returned `object`. Since those started returning the typed
  message, the second parameter buys nothing: an interceptor declaring
  `ICommandPreInterceptor<PlaceOrder>` may already return any `PlaceOrder`, derived types
  included.
* Migration is a deletion: drop the second type argument. A
  `ICommandPreInterceptor<PlaceOrder, ValidatedPlaceOrder>` becomes
  `ICommandPreInterceptor<PlaceOrder>` and keeps returning its `ValidatedPlaceOrder`.
* The event variant was also the odd one out: alone among the pre-interceptor contracts it did
  not carry `IEvent`, so module discovery never found it and it had to be registered by hand.

### Diagnostics

* `ERGOSG001`–`ERGOSG018` become `ERGO001`–`ERGO018`. **Breaking.** Numbers are preserved, so
  the mapping is one rule: drop the `SG`. The scheme stays coherent — `ERGO###` for analyzer
  diagnostics, `ERGOEXP###` for the experimental-API markers, which are untouched.
* Anyone who suppressed a diagnostic by id is affected: an `.editorconfig` severity line, a
  `NoWarn` entry, a `#pragma` or a `SuppressMessage` naming `ERGOSG###` stops matching and the
  diagnostic comes back — as a build failure for the error-severity rules. This repository's
  own suppressions are the demonstration: two test projects carried
  `<NoWarn>ERGOSG005</NoWarn>` and their builds broke until updated.
* `ERGO018` reports a `Register` call naming its type at run time, rather than quietly
  suspending the reachability judgment. The closed world compiles a construct's pipeline,
  freezes its composition and bakes its plan from what the compilation can see, and a type only
  run time knows enters none of that. `Register(typeof(T))` and `Register<T>()` are unaffected.

### Corrections

* **Fix:** an exception interceptor that matched the failure and returned `null` made the
  dispatch return `null` — into a non-nullable slot, with the handler's exception gone, no
  diagnostic and no throw. A dispatch locks its result type at the call site and no stage
  downstream may downgrade that. The four typed-result exception facades now return
  `ValueTask<TResult>` rather than `ValueTask<TResult?>`. **Breaking.** The parameter stays
  nullable and the asymmetry is the point: the handler may have thrown before producing
  anything, so nullable in, non-nullable out. The erased core contracts keep their nullable
  return, because they serve the void lane too, where an empty slot legitimately reaches the
  exception and final stages as `null`.
* **Fix:** a stream handler yielding `null` for a reference-typed element had that element
  silently discarded, so the caller received a well-formed but shorter sequence. The null test
  stood in for "did `MoveNextAsync` produce an element", which the consumption already answers,
  and the two are not the same question — `null` is a legitimate element of an
  `IAsyncEnumerable<T?>`.

### Documentation

* Every comment under `src/` is rewritten from scratch, XML documentation and inline alike. The
  old text had drifted: it described legacy usage, named parameters the signatures no longer
  had, and more than once a documentation block sat above the wrong member entirely, leaving
  its real owner undocumented. Signature mismatches are corrected against the code rather than
  the prose. No behaviour change, and the compiler now validates the whole set — the generator
  builds with documentation generation on and zero warnings.
* The `<see cref="..."/>` targets the earlier surgery left dangling are corrected to the types
  that own the members now, along with the parameter docs left by removed parameters.

### Repository

* The generator body becomes wiring: `ErgosfareRegistrationGenerator.cs` goes from 4742 lines
  to 244, with `Initialize` registering providers and nothing else. The plan layer is one
  `PlanBuilder` split by responsibility, the symbol layer is readers per concern, and the
  constants that had a copy in each layer now have one declaration.
* Coverage is 86.5% → 91.1% line, with no project below 90%. The distribution was the real
  problem rather than the number: three assemblies carried almost every open line, and two had
  collapsed to near zero when the typed default interface methods arrived with nothing calling
  them. Four groups of tests, no source change — the default interface methods dispatched
  through a mediator that overrides nothing, suspended and failing pipelines that exercise the
  context's non-inline return, foreign dependency factories that freeze nothing, and the
  declarative surfaces the generator reads off symbols but never constructs.
* `RegistrableTypeModel`'s hand-written equality is swept by reflection rather than a list of
  cases, because it decides whether the incremental pipeline sees an edit at all: a property
  left out of it serves a stale file with no diagnostic, no wrong build and no failing test.
  All 36 properties participate, and a property added later is covered without anyone
  remembering to cover it.
* The e2e application layer owns its composition; the host only dispatches.

### Breaking changes

* `IEventMediator.PublishAsync` no longer takes `throwIfNoHandlerFound`, and its `string[]`
  group overloads are removed. A publish that reaches nobody returns.
* Publishing an event no subscriber in the compilation serves fails the build with `ERGO005`
  in a composition root.
* `ICommandExceptionInterceptor<TCommand, TResult>`, `IQueryExceptionInterceptor<TQuery, TResult>`
  and their `For<TException>` variants return `ValueTask<TResult>`. An implementation returning
  `null` no longer compiles without a warning, and no longer answers a dispatch at run time.
* The generator diagnostics are renamed `ERGOSG###` → `ERGO###`. Suppressions naming the old
  ids stop matching.
* The two-parameter pre-interceptor contracts are removed; drop the second type argument.

## v2.11.0-preview – '2026-08-15'

Preview release. The theme: **a plugin observes a pipeline, and cannot reshape one.** The
plugin surface shipped in v2.10.0-preview with six addressable stages, two of which let a
plugin change the emitted plan's structure. That is the wrong relationship: a plugin is a
third-party package, the consumer's pipeline is the consumer's, and neither is obliged to
fit the other's model. This release settles what a plugin can ask for, and gives it settings
without putting them in the container.

The whole release lives behind `ERGOEXP002`. Nothing outside the experimental plugin surface
changes shape.

### Four hooks, all of them free

* `Hook` replaces `Stage` and names four points: `Start`, `PreMain`, `PostMain`, `Finish`.
  Every one is a straight-line position that exists in every pipeline, so **no plan grows a
  `try`, a `catch` or a `finally` because a plugin was installed.** An interceptorless
  pipeline carrying calls at all four is still the straight line it is without them.
* The interceptor stages are deliberately not addressable. A plugin that must see the failure
  path, or run on every exit, writes an interceptor — which a plugin package ships just as
  easily, and which the consumer's composition already knows how to place.
* Points coincide rather than disappear: with no pre chain, `Start` and `PreMain` name the
  same instant and both calls run. In a broadcast, `PreMain` and `PostMain` name the seam
  around a *delivery*, so they run once per handler.

### One declaration, whatever the pipeline produces

* No hook carries a result, so `[PipelineInvokable]` and `[VoidPipelineInvokable]` collapse
  into one attribute with one signature — generic over the message alone. A void command, a
  result-producing query and an event broadcast call the same method the same way.
* With them go `PluginPipelineShape`, the arity check, the result parameter binding, and the
  per-plan selection that had to decide which family a method fitted.

### Settings the container never sees

* `[ErgosfarePlugin]` takes an optional options type. Naming one makes the generated
  `Add<Name>` take an instance, which the module holds and hands to the constructed service.
  **It is never registered**: the container carries nothing for it and no dispatch resolves
  it. The consumer constructs what they pass, so there is no builder in between and nothing
  configurable the call site cannot see.
* Two ways in, chosen by what the author writes. A constructor taking the options type is
  left alone, and its *other* parameters are resolved from the container — so a plugin
  service takes ordinary dependencies too. A `partial` service with no constructor gets the
  `readonly` field and the line that assigns it written for it.
* `ERGOSG017` reports a service that is neither, rather than letting it silently ignore
  settings its own plugin declared. Several public constructors is an ambiguity this emission
  will not guess at, and takes the same diagnostic.
* A plugin that declares no options is untouched by any of it: `Add<Name>` stays
  parameterless and the container activates the service as before.

### Corrections

* **Fix:** the void and result plan bodies emitted `finally { if (!aborted) { } }` — an empty
  guard and the abort flag that fed it — for any plan with an exception stage and no final
  stage. The `finally` existed to host the final stage and the now-retired final observer; it
  is emitted only when there is a final stage to run.
* Two documented claims were wrong on their own terms. The plugin selector still said events
  never reach it, though the broadcast plan family arrived in v2.10.0-preview and a test had
  been pinning the opposite since. And `Module.Query` claimed to cover streams, though the
  stream lane has no plan and a plugin call lives only in a plan body. Both now say what the
  code does; the stream gap is tracked separately.

### Breaking changes

Within the experimental plugin surface only, and a plugin is recompiled against the
abstractions it targets:

* `Stage` is replaced by `Hook`, and the exception and final stages have no successor. An
  observer that needs either becomes an interceptor.
* `VoidPipelineInvokableAttribute` is removed. `[PipelineInvokable]` serves every pipeline,
  and a hook method takes one type parameter — its message — where the result-bearing shape
  took two.

### Examples

* `examples/TimingPlugin`: a complete plugin small enough to read in one sitting — one
  assembly attribute, two hook methods, and a README walking the stages. The consumer side is
  the e2e app, which is also the NativeAOT gate, so installing it there tests the claim that
  baked hook calls survive trimming.

### Repository

* A `--artifacts` benchmark run writes to a suffixed sibling of `BenchmarkDotNet.Artifacts`,
  which the existing ignore pattern never matched — it excludes the directory's *contents* so
  committed baselines can be re-included. Those siblings are per-run and hold no baselines, so
  they are ignored whole.

## v2.10.0-preview – '2026-08-15'

Preview release. The theme: **a dispatch stops deciding and starts executing.** Six
generations of dispatch machinery had been living side by side — runtime strategies, fast
lanes, generated plans, staged plans, memoized gates — and an options object could still
change the pipeline at the call site. Both are gone. What a dispatch does is settled before
it runs: the compiled plan is the pipeline, and everything that used to be decided per
dispatch is now part of what keys it.

### The options objects are gone

* `SendAsync`, `QueryAsync`, `StreamAsync` and `PublishAsync` no longer take a settings
  object. Everything a dispatch can be told is a parameter: the group filter, the
  no-handler behaviour, the cancellation token. A settings object meant allocating one per
  dispatch and reading it at dispatch time — a shape nothing can be compiled from.
* The execution context is the only item channel. Items seeded through settings, and the
  parallel context-versus-settings paths that came with it, are removed.
* Six settings types go with them, without obsolete shims. The replacements are parameters
  on the same members, so a call site is edited once and the compiler finds every one.

### The group filter is a dispatch argument

* A filter no longer forms part of an executor's identity. One pipeline per message type
  serves every filter and selects its composition per call — which is what later lets a
  filter key a plan instead of disqualifying one.

### Events dispatch through compiled plans

* Broadcasts get their own plan family, so a publish looks in one store and a send in
  another, and nothing has to branch on the message.
* The broadcast lane becomes a table the container owns, and the streaming lane joins it.
  Closing a dispatch generic happens in one place for all three.

### The plugin surface

A plugin is a NuGet package that declares what it wants observed; the generator writes the
call. `Stella.Ergosfare.Plugins.Abstractions` carries the declaration surface —
`[ErgosfarePlugin]`, `[PipelineInvokable]`, `[VoidPipelineInvokable]`,
`[PluginServiceFilter]`, and the Stage/Module enums — and references nothing: the generator
matches attributes by metadata name, so a new stage ships without moving the core's version.
Emission covers both plan families, and a compilation that references no plugin gets exactly
the plan it would have had before this existed.

This is deliberately still experimental and not yet part of the supported public API;
`ERGOEXP002` marks it. It is recorded here because it works end to end and already shapes
the plan bodies visible in generated output.

### One dispatch shape

* `BroadcastDispatch`, `BroadcastDispatchTable` and `BroadcastMediation` are replaced by
  `FrozenBroadcastDispatch` and its table: plan or runtime body, bare loop or staged
  pipeline, direct construction or provider resolution — all decided once, in the
  constructor, from the container's settled composition.
* The four pipeline executors collapse into `FrozenVoidDispatch` and `FrozenResultDispatch`.
* `SingleAsyncHandlerMediationStrategy<TMessage>` and `<TMessage, TResult>` retire; their
  bodies are relocated as the plan family's N = 1 base case, so a single-handler pipeline
  and a broadcast become one body with a different handler count. The stream lane keeps its
  own strategy.
* Nothing on a dispatch path consults a gate, materializes a composition, or picks a
  strategy any more.

### Compiled plans load unconditionally

* Dispatch roots and every plan family enter the process-wide tables from a module
  initializer, as frozen compositions always have. A container that only calls
  `Register<T>()` dispatches through compiled plans for the first time; the tables used to
  be populated only by `RegisterGenerated()` or `RegisterAll()`.
* An interceptorless broadcast gets a plan too — its bare loop *is* the plan. A command in
  that shape falls back to the single-handler family; a publish has none, so the flagship
  "hooks cost nothing while not attached" lane was the one lane still resolving its handlers
  through the container on every dispatch.
* **Fix:** a plan with exactly one handler was filed under the sending store, where no
  publish ever looks — so every single-handler intercepted event plan was emitted, validated
  and never run. Which store a plan belongs to is a property of the message kind now.

### The group set keys the plan

* The generator reads the group sets its call sites prove — `GroupSet.Of(...)`, array and
  collection literals, and one hop through a named reference, since the documented idiom is
  a reused `static readonly GroupSet` — and bakes one plan per (message, set). Sets are
  normalized, because selection is an any-of test and two spellings of one set must key one
  plan.
* A participant outside a plan's set is skipped rather than disqualifying the plan. This
  lifts an older over-conservatism too: a grouped interceptor used to cost its message the
  default plan even though the default dispatch never runs it.
* A dispatch whose filter is a runtime value gets a plan as well: one body carrying every
  participant, each call behind a baked group test, validated against the composition over
  the groups it covers.
* Group sets travel to composition roots through `DispatchSiteAttribute.Groups`.

### Generic participants

* A participant that takes its message as a type parameter is monomorphized: the generator
  emits the closed forms, and registering the open definition selects the forms compiled
  from it.
* `ERGOSG016` reports the participant that registers and never runs.

### Lifetime correctness

* **Fix:** the all-participants-singleton verdict was cached per message type, but the
  answer is a property of the pipeline *shape*. A message whose handlers all live in a named
  group has an empty default shape, which is vacuously all-singleton — and that verdict
  leaked into the grouped shape, silently promoting its transient handlers to de-facto
  singletons. The cache is gone; a regression test pins the lifetime.

### Both facade names resolve

* `CommandMediator`, `QueryMediator` and `EventMediator` register under their concrete names
  alongside their interfaces, resolving one object graph. A dispatch through the interface
  pays a generic-virtual dispatch the JIT cannot devirtualize; through the class it is a
  direct call. Injecting the interface behaves exactly as before.

### Measured

Typical-user benchmark — one `AddErgosfare`, explicit `Register<T>()`, dispatch through the
public facades — against the v2.3.0 surface, medians:

| Row | v2.3.0 | this release |
| --- | --- | --- |
| Publish, no interceptors | 48.9 ns / 48 B | 28.4 ns / 0 B |
| Publish, intercepted | 205.2 ns | 84.1 ns |
| Publish, group-filtered | ~54 ns / 48 B | 32.8 ns / 0 B |
| Send, no interceptors | 29.5 ns | 23.5 ns |
| Send, intercepted | 150.0 ns | 59.4 ns |
| Send, group-filtered | ~42 ns / 24 B | 30.8 ns / 0 B |
| Query | 35.2 ns | 24.9 ns |
| Query, intercepted | 184.3 ns | 88.3 ns |

The zeroed allocations are dependency-free participants constructed with a visible `new`
through the existing construction gate, which the JIT can stack-allocate.

### Breaking changes

* The dispatch surfaces drop their options objects, and six settings types are removed with
  no obsolete shims.
* `SingleAsyncHandlerMediationStrategy<TMessage>` and `<TMessage, TResult>` are removed from
  `Core.Abstractions`. They were a LiteBus-era seam; the stream strategy is unaffected.
* The event exception and final interceptor facades no longer take a result parameter:
  `IEventExceptionInterceptor`, `IEventExceptionInterceptor<TEvent>`,
  `IEventFinalInterceptor` and `IEventFinalInterceptor<TEvent>` declare
  `(event, exception, context)`. A publish produces no result, so the parameter only ever
  carried a fixed placeholder.
* `CommandMediator(IMessageMediator)` is removed; the engine-backed constructor is the only
  construction shape, as it already was for the query and event facades.

### Repository

* An [AI policy](.github/AI_POLICY.md): an assistant is a co-developer and never the
  responsible party, architectural decisions stay with the developer, disclosure is
  recommended rather than required, and every line is reviewed by a human.
* A checked-in Codex MCP declaration for the documentation catalog, with `.codex/` admitting
  only that declaration.

## v2.3.0 – '2026-08-12'

Stable release. The theme: **the pipeline becomes a compiled artifact.** The preview cycle
from v2.2.0-preview through v2.9.0-preview moved Ergosfare from a source-assisted runtime
registry to a closed, source-generated dispatch model. The generator's frozen composition
table is now the authority for handler and interceptor relationships; module registration
selects which discovered constructs a container runs, and dispatch executes that selection
without reflective scanning or runtime pipeline mutation.

This release also finishes the public dispatch contract: concrete execution contexts,
explicit nested scopes, typed exception interception, predictable abort semantics,
covariant main-handler resolution, canonical resultless pipelines and one failure model for
missing handlers. The complete preview history is condensed here into the stable contract.

### Frozen composition replaces the runtime registry

* `FrozenCompositionCatalog` replaces `IMessageRegistry` end to end. The generator emits
  one immutable composition per message: direct and covariant handler rows plus the pre,
  post, exception and final interceptor stages in execution order.
* Registration is selection, not discovery. `RegisterGenerated()` and `Register<T>()`
  select constructs from the compiled table for one DI container; repeated and overlapping
  selections are harmless unions.
* Runtime mutation and reflective assembly scanning are gone. A dynamically loaded assembly
  cannot append a new message shape after compilation, and there is no registration window
  whose timing can change an already running application.
* Generated participant types preserve the constructor metadata required by Microsoft DI
  under trimming. The NativeAOT smoke covers command, query and event activation—including
  multi-subscriber broadcast and value-type messages—in a published native binary.

### Source-generated dispatch and NativeAOT

* Generated dispatch roots close executor and invoker generics over concrete message and
  result types at compile time. The hot path avoids registry scans, reflection,
  `MakeGenericType` and object-typed handler bridges.
* Single-handler command and query pipelines receive generated plans. Eligible transient
  handlers can also receive direct-construction factories, including provider-based
  factories for constructor dependencies, while runtime gates preserve the configured DI
  lifetime and user overrides.
* Interceptor-bearing pipelines receive generated staged plans: straight-line typed calls
  for pre, handler, post, exception and final stages. A composition mismatch gives up only
  the optimization and falls back to the general executor.
* The generator scans referenced assemblies, reports inaccessible constructs as diagnostics,
  respects nullable annotations in consumer compilations and can trim provably unreachable
  handlers at the composition root.

### A pinned pipeline contract

* A public-surface contract suite now runs the dispatch scenarios across the supported
  composition lanes on .NET 9, .NET 10 and .NET 11 — 194 scenarios on each. Its lane-map
  baseline records not only the result, but the dispatch path that produced it.
* `Abort()`, `Abort(reason)` and `Abort(reason, value)` stop the pipeline immediately and
  surface `ExecutionAbortedException` to the caller. Exception and final interceptors do
  not run after an abort; nested callers decide whether an inner abort ends the outer
  pipeline too.
* Resultless commands and events carry the single `Unit.Value` instance after successful
  execution. `null` retains the distinct meaning “no result was produced.”
* Main-handler resolution is a priority ladder over the direct and covariantly matched rows.
  A sole direct handler wins outright, however many covariant candidates exist; without one,
  a supertype handler serves the assignable message. Two claimants *at the same level* are a
  contest, counted before anything resolves, so the message runs neither and the caller gets
  `MultipleHandlerFoundException`. The generator reports the same condition as `ERGOSG010` at
  build time.
* New typed `...ExceptionInterceptorFor<TException>` contracts declare the exception type they
  accept and follow catch semantics, so the exception arrives typed and nothing has to
  remember to rethrow. They sit **beside** the untyped contracts, which are unchanged and stay
  the right shape for an interceptor handling several exception types or logging every
  failure. With either family, an exception is swallowed only when an interceptor that
  actually matched it returns a value; otherwise the original exception and stack leave the
  pipeline unchanged.
* Missing and filtered-out handlers consistently use `NoHandlerFoundException`, which now
  derives from `InvalidOperationException` and exposes `MessageType`. Event publication
  honors `ThrowIfNoHandlerFound` for every no-subscriber shape.
* A selected participant that the container cannot resolve fails before the first stage
  with `UnresolvableParticipantException`, naming both the message and participant.

### `ErgosfareContext` and nested dispatch

* The public sealed `ErgosfareContext` replaces `IExecutionContext`. Handler and
  interceptor contracts take the concrete type, eliminating an interface dispatch whose
  only implementation was internal.
* Its public constructor accepts optional items and cancellation, allowing callers to own
  a context for engine-level dispatch. Facade dispatches continue to rent contexts from the
  allocation-conscious pool.
* `ErgosfareContextScope` replaces `ExecutionContextScope`. `CreateScope()` is its only
  producer: a child starts with clean items, inherits cancellation and returns its pooled
  context on disposal. Disposing a default scope remains a no-op.
* Execution state stays explicit. There is no `AsyncLocal` context and no service provider
  exposed through the context.

### Modules, events and cross-cutting policies

* Commands, queries and events remain independent modules over the shared core. The event
  module is also the open lane for POCO messages: `IEventHandler<TEvent>` and
  `PublishAsync<TEvent>` accept any non-null event type and retain broadcast semantics.
* `AddCoreModule`, `CoreModule`, `CoreModuleBuilder` and `IModuleBuilder` are removed. Plain
  messages move to the event module; custom modules select from the frozen composition
  table and register their facade and services with DI.
* Cross-cutting interceptors join built-in module partitions by carrying their marker
  interfaces directly—for example an `IAsyncPreInterceptor<IMessage>` that also implements
  `ICommand`, `IQuery` and `IEvent`. Covariant rows apply it broadly and
  `[ExcludeFromPipeline]` provides blanket or group-specific opt-out.
* `GroupSet` provides a reusable canonical group filter, avoiding per-dispatch settings
  allocation while preserving grouped handler and interceptor selection.
* Discovery keys support exact and prefix-glob selection across the application and its
  references. `[ExcludeFromDiscovery]` remains the explicit exclusion boundary.

### Performance and DI semantics

* Mediator facades are transient, stateless wrappers over a shared dispatch engine. They
  capture the provider of the resolving scope without paying the scoped-service cache and
  lock cost on every request-shaped dispatch.
* Participant instances resolve from the dispatching scope, so singleton, scoped,
  transient and keyed registrations retain Microsoft DI semantics. The module default is
  transient; `ForceMemoizedHandlers()` remains available when process-wide reuse is the
  intended contract.
* Executors cache the frozen dependency shape, generated holders remove repeated composite
  lookups and pooled contexts avoid a per-dispatch context allocation. The `ValueTask`-first
  surface preserves synchronous completion through the public facades.
* On the v2.7.0-preview benchmark tree (.NET 9.0.11, Ryzen 7 7800X3D), a generated no-result
  command measured 19.9 ns / 24 B, a result query 24.0 ns / 24 B, a five-participant query
  120.2 ns / 96 B and a two-subscriber event 50.5 ns / 48 B. Request-shaped rows that
  create a DI scope and resolve a mediator remained ahead of the equivalent MediatR rows.

### Target frameworks

* Packages carry **`net11.0` beside `net10.0` and `net9.0`**. The addition buys no
  compatibility on its own — roll-forward already let a `net11.0` project bind
  `lib/net10.0/` — so what it carries is proof: the sources compile under the C# 15
  compiler, and the contract suite holds its lane map on .NET 11 on every run.
* **Nothing from .NET 11 or C# 15 is adopted**, and the reasons are worth recording. The
  runtime-side gains — JIT bounds-check elimination, GC work, NativeAOT's faster interface
  dispatch — need no target framework at all; a `net10.0` assembly already collects them
  running on .NET 11. The library additions have no call sites here. C# 15 union types
  store their payload in an `object?` field and box value types, which is the object-typed
  bridge the dispatch hot path was built to avoid, and the `closed` modifier applies to
  classes while the handler contracts are interfaces.
* **Runtime Async** is the one target-framework-gated win, deliberately deferred: it
  rewrites async codegen wholesale and needs the lane-map baseline re-validated against the
  abort semantics first. The target framework holds the slot; the feature waits.
* `LangVersion` stays `latest`, which now resolves to **C# 15 for the `net9.0` and
  `net10.0` compilations too**, since the language version follows the compiler rather than
  the target framework. Deliberate: one language version across all three, so a construct
  cannot compile on one target and fail on another.
* .NET 11 reaches GA on **2026-11-10**, the same day .NET 9 leaves support. Assets shipped
  for `net11.0` before that date are compiled against **preview reference packs** and want
  a re-pack against the GA reference pack once .NET 11 ships; the obligation is recorded in
  `.github/workflows/nuget_release.yml` rather than left to memory. Building the repository
  now requires the .NET 11 preview SDK — this affects contributors, not consumers, who need
  it only if they themselves target `net11.0`.

### Breaking changes and migration

* **Removed:** `IMessageRegistry` and descriptor APIs, `RegisterFromAssembly`,
  `RegisterDescriptors`, `MediateOptions`, message-resolve strategies, the low-level
  `IMessageMediator.Mediate` surface, `IExecutionContext`, `ExecutionContextScope`,
  `IPoolReturnable`, `AddCoreModule`, `CoreModule`, `CoreModuleBuilder` and
  `IModuleBuilder`.
* **Replace registry calls** with source-generated `RegisterGenerated()` selections or
  statically known `Register<T>()` selections. Runtime-computed participant and assembly
  registration is no longer supported.
* **Replace context types** with `ErgosfareContext` and create nested contexts only through
  `context.CreateScope()`.
* **Move plain messages** to `IEventHandler<T>` / `PublishAsync<T>`, and tag broad
  interceptors with the command, query and event marker interfaces they should join.
* **Audit abort callers:** abort now reaches the call site as `ExecutionAbortedException`
  and prevents all downstream stages, including final interceptors.
* **Audit contested handlers:** two main handlers on the same level — two direct, or two
  covariant with no direct one — now conflict instead of letting one win silently. A direct
  handler still beats covariant ones; that pairing is not a contest.
* The full solution, contract suite and NativeAOT smoke are green on the stable promotion
  commit with zero build warnings.

## v2.2.0 – '2026-08-10'

Stable release. Deprecates the reflection-based assembly-scanning surface: the
`RegisterFromAssembly` overloads on the four module builders, the `IModuleBuilder` seam
and `ResultAdapterBuilder`'s scan now carry `[Obsolete]` and warn at build time, one
release ahead of their removal. Compile-time discovery is the migration target —
`RegisterGenerated()` / `RegisterGenerated(pattern)` and `Register<T>()`, adapters via
`Register<TAdapter>()`. No API removals and no behavior changes in this release; the
builders' tests keep pinning the obsolete surface until the removal lands.

## v2.1.0 – '2026-08-07'

Stable maintenance release of the v2 line. Its preview carried repository and release-channel
chores only. The feature history that was previously repeated through the early preview
entries is consolidated into the v2.3.0 stable entry above.

## v2.0.0 – '2026-07-25'

First stable release of the v2 line. The theme: **all dispatch-shape work moves to compile time
or to a once-per-message-type plan** — registration is source-generated, dispatch generics
close at compile time, execution contexts are pooled, and the dispatch path carries no
reflection, no `MakeGenericType`, no registry scan and no `AsyncLocal`.

### Source-generated registration (`Stella.Ergosfare.SourceGenerator`)

* New incremental Roslyn generator discovers every Ergosfare construct in the compilation and emits `RegisterGenerated()` extensions on the module builders (plus a DI-agnostic `RegisterAll(IMessageRegistry)`) — drop-in replacements for `RegisterFromAssembly(...)`, with **pre-computed handler descriptors**: registration performs no reflection over handler types. Plain messages and open generics keep the runtime `Register(Type)` fallback; both paths are mutually idempotent, so generated and runtime registration can coexist.
* **Reference scanning:** the consuming project's generator also walks referenced assemblies — a library's handlers register through the app's generated code with zero registration code in the library. Internal types participate via `InternalsVisibleTo`; types generated code cannot name surface as **ERGOSG002** (marker types the runtime scan would have caught). Opt out per project with `<ErgosfareSourceGeneratorScanReferences>false</ErgosfareSourceGeneratorScanReferences>`. Assemblies named under the reserved `Stella.Ergosfare.*` prefix are skipped by default (their contract interfaces inherit the module markers); an app deliberately named under that prefix opts back in per-assembly with `<ErgosfareSourceGeneratorForceScanReferences>true</ErgosfareSourceGeneratorForceScanReferences>` — only assemblies that set it are scanned, so the library's own contracts are never registered.
* **Discovery keys:** `[DiscoveryKey("reporting.daily")]` gates a type out of default discovery until a registration call selects it — `RegisterGenerated()` takes untagged types, `RegisterGenerated("reporting.*")` cherry-picks by exact key or trailing-`*` prefix glob, and calls chain safely. `[assembly: DiscoveryKey]` tags a whole library; `[ExcludeFromDiscovery]` (type or assembly) removes a construct from discovery entirely. The reflection path (`RegisterFromAssembly`, now with a pattern overload) honors the same attributes.
* **Generated dispatch roots:** the generator emits compile-time generic closures (`GeneratedDispatchRoots.AddMessage<M>` / `AddResult<M, R>` / `AddStream<Q, R>`), letting the executor, event-broadcast and stream-invoker caches construct their pipelines **without `MakeGenericType`** — and giving Native AOT a static anchor for every instantiation, value-type messages included. The reflective path remains only as the fallback for open generics and runtime-only registrations.
* Diagnostics: `ERGOSG001` (inaccessible registrable type), `ERGOSG002` (invisible referenced type).

### Dispatch engine

* **Per-message-type pipeline executors.** Dispatch goes through a pipeline closed over the message's runtime type, built once per (message type, result type, group set) and cached process-wide; the facades resolve executors with a dictionary lookup. No per-call options object, no interface-erased strategy, no object-typed bridge — interface-erased dispatch throws `NotSupportedException` with guidance.
* **Independent sync/async contracts, typed end to end.** `IHandler` and the four interceptor roots are empty markers; typed synchronous and `ValueTask`-based asynchronous contracts are standalone hierarchies invoked exclusively through their typed members (generic invocation strategies, contravariant in message/result). All DIM bridges and object-typed root members are gone; synchronous typed interceptors actually work now (the old bridge crashed them with `InvalidCastException`).
* **`ValueTask`-first surface** across handlers, interceptors and facades; synchronously completing handlers allocate nothing.
* **Thread-safe registration.** Registration is serialized behind a gate with lock-free snapshot readers (immutable descriptor-array snapshots, copy-on-write stage arrays, version-stamped resolve caching — fixing registration races, mid-registration enumeration crashes, and a resolve-cache poisoning bug). Hot-path LINQ removed from event broadcast and pipeline-shape building.

### Execution context — scopes, pooling, nested dispatch

* **Nested dispatch as a first-class pattern:** `context.CreateScope()` returns a struct scope wrapping a clean, pooled child context — isolated items, inherited cancellation token. Facades accept the child (`SendAsync`/`QueryAsync`/`PublishAsync` overloads taking `IExecutionContext`; `MediateOptions.ExternalContext` for the options paths). An inner `Abort()` ends only the inner pipeline; parallel inner dispatches with separate scopes are safe.
* **Pooled execution contexts:** dispatches rent and return contexts through a `[ThreadStatic]`-first pool with a synchronous fast path (no async state machine when the pipeline completes synchronously). A context is valid only for the duration of its dispatch. Public-facade allocations drop **5.34 → 2.29 MB per 100k dispatches** with Gen0 pressure halved, at time parity.
* Context fixes: `Has`/`TryGet`/`Get` no longer allocate the items dictionary on an empty context; `Get` throws the documented `KeyNotFoundException`.

### Pipeline control

* **`[ExcludeFromPipeline]`** on a message type excludes covariantly matched interceptors — blanket or per group (`[ExcludeFromPipeline("logging")]`). Interceptors registered for the message type itself always run; main handlers are never affected.
* **Event broadcast delivers to covariantly matched handlers** (registered against a base type or interface of the event) — the event's own handlers first, then indirect ones. Opting out of broad delivery is a group concern.
* The two-parameter command/query post-/exception-interceptor contracts are now contravariant in their message parameter, matching the core contracts.
* The single-parameter pre-interceptor contracts (`ICommandPreInterceptor<TCommand>`, `IQueryPreInterceptor<TQuery>`, `IEventPreInterceptor<TEvent>`) now return the typed message (`ValueTask<TMessage>`) instead of `ValueTask<object>` — a pre-interceptor carries no result, so the message type is all there is to return. The non-generic contracts still return `object` (they intercept any message); the two-parameter variants still return the modified message type. `TMessage` is invariant because it is now returned.
* Fixed: the void-flavored `IEventPreInterceptor` default implementation returned `ValueTask.CompletedTask` as the pipeline's message-replacement value, crashing event pipelines running an event-wide pre-interceptor.

### Removed

* **`Stella.Ergosfare.Contracts` folded into Core.Abstractions** (`GroupAttribute`/`WeightAttribute` now in `Stella.Ergosfare.Core.Abstractions.Attributes`); the package is no longer produced.
* **`AmbientExecutionContext`** and `EnableAmbientExecutionContext()` (deprecated since v1.2.0): the context parameter is the only access path; no `AsyncLocal` remains on the dispatch path. `NoExecutionContextException` removed with it.
* The obsolete three-parameter `TModifiedResult` interceptor interfaces (deprecated in v1.4.0) — use the two-parameter typed variants.
* `IAsyncValueTaskHandler<TMessage, TResult>` (experimental) — `IAsyncHandler` itself now carries the `ValueTask` shape. The `[Experimental]` gate (`ERGOEXP`) stays in place for future APIs.
* Internal cleanups: `MainInvoker`, the `AbstractInvoker` layer, and the interceptor object roots.

### Benchmark (100k no-op dispatches, Ryzen 7 7800X3D, .NET 9, 2026-07-24)

| Scenario | Mean | Allocated |
|---|---:|---:|
| Ergosfare — typical usage (`SendAsync`, shared scope) | 6.87 ms | **2.29 MB** |
| MediatR — typical usage | 6.09 ms | 18.31 MB |
| Ergosfare — fresh DI scope per dispatch | 18.81 ms | 38.91 MB |
| MediatR — fresh DI scope per dispatch | 10.09 ms | 33.57 MB |

### Migration notes

* `using Stella.Ergosfare.Contracts.Attributes;` → `using Stella.Ergosfare.Core.Abstractions.Attributes;`; drop the `Stella.Ergosfare.Contracts` package reference.
* Handler/interceptor signatures: `Task`/`Task<T>` → `ValueTask`/`ValueTask<T>` (mechanical; `async` bodies need only the signature change).
* `AmbientExecutionContext.Current` → the `IExecutionContext` parameter your handler already receives.
* Prefer `RegisterGenerated()` (with the `Stella.Ergosfare.SourceGenerator` package) over `RegisterFromAssembly(...)`; the latter remains for runtime-loaded plugins.
* Do not hold an `IExecutionContext` reference beyond the dispatch it belongs to — contexts are pooled.

## v1.4.0 – '2026-07-22'

### Features & Improvements

#### Typed interceptors without the third type parameter

* The two-parameter typed interceptor interfaces — `ICommandExceptionInterceptor<TCommand, TResult>`, `ICommandPostInterceptor<TCommand, TResult>`, `IQueryExceptionInterceptor<TQuery, TResult>`, `IQueryPostInterceptor<TQuery, TResult>` — now declare the type-safe `HandleAsync` member returning the typed result directly.
* Their type parameters are deliberately **invariant** now: the pipeline invokes interceptors through the non-generic root interfaces, so interface variance bought nothing — while `in TResult` blocked typed returns, which was the only reason the three-parameter `TModifiedResult` variants existed.
* The three-parameter variants are marked `[Obsolete]` and will be removed in the next major version.
* Migration note: implementors of the previous marker-only two-parameter interfaces now implement the typed member (returning `Task<TResult>` / `Task<TResult?>`) instead of the base `Task<object>` member.

### CI

* Coverage and unit-test workflows run as a single job with both SDKs installed — the solution multi-targets net9.0/net10.0, so per-SDK matrix legs could not restore it, and the two parallel coverage jobs raced each other pushing the badges branch (the `is at … but expected …` non-fast-forward failure).
* The coverage badge is generated only on pushes to `main` (pull requests no longer overwrite the badge or push to the badges branch), and the badge action receives only its supported `coverage-file-name` input.

## v1.3.0 – '2026-07-22'

### Internal Surface Changes — Core / Core.Abstractions

These packages are public so the first-party modules (Commands, Queries, Events) can consume them across assembly boundaries; they are not a third-party plugin contract, so changes here are not considered breaking. Code that registers handlers/interceptors and dispatches through `ICommandMediator`/`IQueryMediator`/`IEventMediator` compiles and behaves unchanged.

* `IMessageDependencies` reshaped: ten lazy collections → six fixed `IReadOnlyList` stages. The four interceptor stages (pre/post/exception/final) merge direct and indirect registrations into a single list — direct entries first, then indirect, each segment ordered by weight and handler type name — matching the execution order the invokers previously implemented as two passes. Main handlers keep the direct/indirect split because single-handler validation applies to direct handlers only.
* `ILazyHandler`, `ILazyHandlerCollection`, `LazyHandler`, `LazyHandlerCollection`, and `ToLazyReadOnlyCollection` are removed. Their replacement is `IHandlerReference<THandler, TDescriptor>`: the descriptor, the pre-computed concrete `HandlerType`, and `Resolve(IServiceProvider)` which obtains the instance for the current dispatch.
* `IMessageMediationStrategy<TMessage, TResult>.Mediate` gained an `IServiceProvider` parameter: the mediator hands the dispatching scope's provider down the pipeline explicitly — resolution is the dispatcher's responsibility. The execution context stays a pure data carrier between handlers: it plays no part in handler resolution and deliberately exposes no service provider, so user-facing handler code gets no service-locator surface.
* `IMessageDependenciesFactory` is registered as a **singleton** (was scoped); its dependency graphs are provider-independent and cached process-wide. `IMessageMediator` stays scoped but is now a thin wrapper whose only per-scope job is carrying the scope's provider into the execution context.
* Implementation types in `Stella.Ergosfare.Core` that are not part of the public contract are now `internal`: the handler descriptor builders (`HandlerDescriptorBuilder`, `PreInterceptorDescriptionBuilder`, `PostHandlerDescriptorBuilder`, `ExceptionInterceptorDescriptorBuilder`, `FinalInterceptorDescriptorBuilder`), `HandlerDescriptorBuilderFactory`, and `MessageDependenciesFactory` (still consumable through the public `IMessageDependenciesFactory`). Default implementations of public abstractions — `ResultAdapterService`, `LruCacheStrategy` — remain public. First-party assemblies use `InternalsVisibleTo`; forks building custom modules can add their own grants.

### Features & Improvements

#### Provider-independent dependency graphs — scope-per-dispatch ~2.6x faster, ~2.7x less allocation

* The resolved pipeline of a message (ordered stages, closed generic handler types) is built once per message type and group set, and shared process-wide. A fresh DI scope no longer rebuilds factory state, per-scope caches, or lazy wrapper graphs — the per-scope cost is the scope itself plus two small mediator wrappers.
* Handler and interceptor instances resolve per invocation from the execution context's provider, so DI lifetimes are honored exactly: singleton → container-cached, scoped → one per scope, transient → one per dispatch. Fully singleton pipelines (and `ForceMemoizedHandlers()`) additionally cache instances inside their references, pinned to the root provider.
* Pipeline shapes pre-compute closed generic handler types: `MakeGenericType` runs once per closed message type instead of on every scope's first resolution. Closing is guarded by `IsGenericTypeDefinition`, so handlers registered against a constructed generic message resolve as-is (previously a latent `MakeGenericType` throw on first resolution).
* Interceptor invocation is a single indexed pass over the merged, pre-ordered array — the double direct/indirect passes and per-loop enumerator allocations are gone.
* Benchmark (100k dispatches/op, Ryzen 7 7800X3D, .NET 9, fresh scope per dispatch): 45.6 ms / 146.5 MB → **17.2 ms / 53.4 MB**; MediatR on the same shape: 10.3 ms / 33.6 MB.

### Notes

* Behavioral change: transient-registered handlers (the framework default) now resolve once per dispatch instead of being implicitly reused across dispatches within the same scope — i.e. a transient registration now behaves as declared, matching MediatR semantics. On dispatch-heavy single-scope loops this costs one handler instance per dispatch (raw mediator path: 3.05 → 6.1 MB per 100k dispatches — still well below MediatR's 18.3 MB on the same loop). To keep instance reuse, register those handlers as singletons (before `AddErgosfare`, so `TryAdd` respects it) or call `ForceMemoizedHandlers()`.
* Dispatching a message whose pipeline has no direct main handler now throws `InvalidOperationException` with an explicit "No handler is registered for …" message instead of a LINQ "Sequence contains no elements" error.

## v1.2.0 – '2026-07-22'

### Deprecations

#### `AmbientExecutionContext` (planned removal in the next major version)

* The entire `AmbientExecutionContext` API is now marked `[Obsolete]`: the execution context is passed to every handler and interceptor as a parameter, which is the supported way to access it.
* Ambient publication is now **opt-in and disabled by default**. Components that require constructor-injected `IExecutionContext` (the only feature that genuinely needs the ambient mechanism) must enable it during registration:

  ```csharp
  services.AddErgosfare(options =>
  {
      options.EnableAmbientExecutionContext(); // deprecated compatibility switch
      options.AddCoreModule(module => { /* ... */ });
  });
  ```

* When disabled (the default), no `AsyncLocal` write occurs on the dispatch path. Resolving `IExecutionContext` from DI without enabling the switch throws `NoExecutionContextException` with guidance.
* Migration: replace `AmbientExecutionContext.Current` usages with the `IExecutionContext` parameter your handler/interceptor already receives. The static API will be removed together with `EnableAmbientExecutionContext()` in the next major release.

### Features & Improvements

#### Dispatch hot-path overhaul (~5x faster, ~11x less allocation)

* Resolved message descriptors are cached per message `Type` in the resolve strategy; the previous full-registry LINQ scan per dispatch is gone.
* Zero-interceptor fast path: when a message has no pre/post/exception/final interceptors, the handler is invoked directly — no invoker objects or empty-collection async iterations are constructed.
* Resolved `MessageDependencies` are cached in `ConcurrentDictionary`s keyed by `Type` (plus a struct key for group filters); per-dispatch string-key construction, service-provider lookups, and LRU timestamp writes are eliminated.
* `CommandMediator`/`QueryMediator` reuse their stateless mediation strategies and no longer materialize default settings objects per call; mediators are now registered as singletons (`TryAdd`, so user overrides still apply).
* `MessageDescriptor` stage lists are allocated lazily, trimming startup allocations per registered message type.
* Benchmark (100k dispatches/op, Ryzen 7 7800X3D, .NET 9, ambient context disabled): raw mediator path 31.6 ms / 112.9 MB → 4.4 ms / 3.1 MB; public `SendAsync` path 5.3 ms / 9.2 MB; MediatR on the same benchmark: 6.1 ms / 18.3 MB.

#### Lifetime-aware handler resolution — scoped dependencies now supported (default on)

* Handler resolution now honors registered DI lifetimes. Messages whose handlers and interceptors are all **singleton-registered** keep the process-wide memoized fast path; everything else (the framework default is transient) is resolved from the **calling scope's provider** and cached per scope — so scoped constructor dependencies (`DbContext`, unit-of-work, current-user services) get one instance per scope, exactly like MediatR, and `IDisposable` handlers/dependencies are disposed with their scope.
* Previously (v1.1.0) handler instances were silently memoized process-wide: the first dispatch captured the first caller's scope — including its `DbContext` — and reused it for every subsequent dispatch, even after that scope was disposed. This was a latent correctness bug that did not surface as an exception (default `BuildServiceProvider()` does not validate scopes).
* Opting out: `ForceMemoizedHandlers()` on the registration builder restores the pre-v1.2 memoize-everything behavior for maximum dispatch throughput. Per-handler control needs no new API — register a handler as singleton (before `AddErgosfare`, so `TryAdd` respects it) to keep it on the fast path.
* Within a scope, resolution is memoized per message type, so apps dispatching several messages per request pay the per-dispatch resolution cost once per scope.
* Ordered, group-filtered pipeline shapes are cached process-wide (invalidated by the registry version); a fresh scope only materializes cheap lazy wrappers over the cached descriptor arrays.
* Cost (measured, fresh scope per dispatch — the per-request worst case): ~0.47 µs and ~1.55 KB per scope for a single-handler message, versus ~0.11 µs / ~0.35 KB for MediatR's equivalent shape; dispatches after the first within the same scope use the memoized fast path (~55-70 ns).

#### Runtime registration correctness

* `MessageRegistry` now tracks a monotonic `Version`, and dependency caches invalidate when the registry changes — runtime registrations (including handlers added to already-registered messages) become visible to subsequent dispatches. Previously the dependency cache was never invalidated.

### Bug Fixes

* `IAsyncPostInterceptor<TMessage, TResult>` default implementation now forwards the `IExecutionContext` parameter it receives instead of reading the ambient context.
* `SingleStreamHandlerMediationStrategy` no longer unconditionally overwrites the ambient context without restoring it; it re-publishes the context for stream enumeration only when ambient access is enabled.
* Final interceptor types (direct and indirect) are now registered with the DI container; previously `RegisterHandlersFromDescriptor` skipped them, so standalone final interceptors could not be resolved.

### API Changes

* `MediateOptions<TMessage, TResult>.Items` is now nullable and no longer allocates a default dictionary; the execution context creates its `Items` dictionary lazily on first access.
* `ILazyHandlerCollection<THandler, TDescriptor>` gained a `First()` default interface method (allocation-free in the built-in implementation).
* `ICommandMediator`, `IQueryMediator`, `IEventMediator`/`IPublisher`, `IMessageMediator`, and `IMessageDependenciesFactory` are registered as **scoped** (previously transient) so per-dispatch handler resolution binds to the calling scope; `ActualTypeOrFirstAssignableTypeMessageResolveStrategy` is a singleton. All registrations use `TryAdd`, so user overrides still win.
* New registration extension: `ForceMemoizedHandlers()` (opt-out of lifetime-aware resolution).

### Notes

* Behavioral change vs v1.1.0: scoped/transient-registered handlers (the default) are no longer silently memoized across scopes — they now behave as their registration declares. If you relied on the old implicit memoization for performance, either register those handlers as singletons or call `ForceMemoizedHandlers()`.
* With the ambient context disabled (the default), both the raw and public dispatch paths are faster than MediatR and allocate a fraction of its memory; enabling `EnableAmbientExecutionContext()` re-introduces the per-dispatch `AsyncLocal` cost.

## v1.1.0 – '2026-03-23'

### Features & Improvements

#### LRU Cache Strategy for MessageDescriptorCache

* Added pluggable `IDescriptorCacheStrategy` interface for flexible cache management.
* Implemented **LRU (Least Recently Used)** cache strategy with configurable size limit (default: 100 entries).
* `MessageDescriptorCache` now supports `TryGet<T>` and `Add` methods with thread-safe operations.
* Automatic eviction of least recently used entries to control memory usage.
* Optional periodic cleanup for stale entries (24-hour threshold).

#### Performance Optimizations

* Replaced LINQ chains with manual loops in `ResolveHandlers` to eliminate per-call delegate allocations.
* Optimized group filtering and sorting with single-pass array operations.
* Reduced allocation overhead in handler resolution hot path.

#### Cache Integration

* `MessageDependenciesFactory` now consumes `MessageDescriptorCache` for dependency caching.
* Cache keys are now generated from message type and group combinations.
* Improved cache hit rates for repeated message invocations with same parameters.

### Bug Fixes

* Fixed excessive memory allocation in handler resolution path.
* Corrected group intersection logic for better filtering accuracy.

### Internal Changes

* Refactored `ResolveHandlers` method to use array-based operations instead of LINQ.
* Simplified `MessageDescriptorCache` API with generic `TryGet<T>` method.
* Updated DI registration to include default LRU cache strategy.

### Notes

* Default cache size is set to 100 entries; can be configured via `LruCacheStrategy` constructor.
* Cache strategy can be overridden by implementing custom `IDescriptorCacheStrategy`.
* Existing consumers should see reduced memory allocation and improved performance without API changes.

## v1.0.1 – '2025-12-16'

### Features & Improvements

#### .NET 10 & Native AOT Support

* Full support for **.NET 10** across all projects and test fixtures.
* Native AOT builds now use **monetization strategy** instead of reflection for descriptor construction, improving startup performance and compatibility.
* Simplified handler descriptor building for AOT scenarios.

#### Pipeline & Handler Updates

* Obsolete **pre-handlers** have been removed.
* Post-handler invocation now correctly handles nullable results.
* Streaming and synchronous pipelines updated for improved compatibility with snapshot-less execution.
* Minor internal adjustments to handler and interceptor execution flow for stability.

#### Project & Namespace Updates

* Solution structure migrated to **.slnx** format for faster load times and improved IDE integration.
* Namespaces reorganized for better modularity and clarity.
* Snapshot mechanism fully removed; not part of compatibility policy.

### Bug Fixes

* Fixed bug where post-handlers incorrectly returned nullable results.
* Fixed minor issues in streaming and synchronous pipeline execution.
* Adjusted internal mediator strategies for improved consistency.

### Internal Changes

* Refactored mediation strategies to better handle .NET 10 and native AOT.
* Removed obsolete pipeline snapshot features.
* Updated test fixtures to align with current pipeline and handler changes.

### Notes

* This is a **patch release**, intended to improve compatibility and stability for v1.0.0 consumers.
* Pipeline and message mediation behavior remains fully backward compatible.
* Snapshot persistence and checkpoint features are now fully deprecated and removed.
* Consumers can continue using caching and manual persistence mechanisms where applicable.


## v0.2.0e – '2025-9-22'

### Features & Improvements

#### Snapshot & Checkpointing
* Introduced `PipelineCheckpoint` to track `Message`, `Result`, and `Success` for partial or full pipeline snapshotting.
* Pre- and post-interceptors updated to support snapshot-aware execution.
* Streaming and synchronous handlers can skip already completed checkpoints.
* Provides scoped caching within handlers; allows optional manual persistence by consumers.
#### Retry Mechanism

* Added `ErgosfareExecutionContext.RetryCount` to track retry attempts.
* `Retry()` publishes `PipelineRetrySignal` and throws `ExecutionRetryRequestedException`.
* `MessageMediator` refactored to catch retry requests and re-mediate with preserved context.
* MediateOptions support retry limits via `Retry` property.
#### Streaming Mediation Improvements

* `SingleStreamHandlerMediationStrategy` fixed to ensure **pre-interceptors execute before the main handler**.
* Partial snapshotting after streaming completes successfully.

#### Internal Changes

* Added `ExecutionRetryRequestedException` class.
* Added internal setters for `PipelineCheckpoint.Message` and `Result`.
* All mediation strategies refactored for snapshot support.
* Pre- and post-invokers refactored for snapshot compatibility.

#### Fixes & Minor Adjustments

* Streaming strategy bug fixed: pre-interceptors now run correctly before streaming handler execution.

#### Notes

* This will be the last **major/minor premature release** (`0.2.0e`).
* Only **patch releases** will follow in this branch.
* The next full release will be **stable v1.0.0**.
* Consumers can leverage checkpoints for caching or manual persistence in fire-and-forget pipelines.



## v0.1.3e '2025-9-20'
### Breaking Changes
- `IExecutionContext` and `AmbientExecutionContext` are now under `Ergosfare.Core.Abstractions` namespace.  
  Update any using statements and references in dependent projects.
### Changed
- Dropped `Ergosfare.Context` package.
- `Stella.Ergosfare.Context` package is no longer distributed.
- Moved `IExecutionContext` and `AmbientExecutionContext` to `Core.Abstractions.Context`.
- All execution context exceptions moved to `Ergosfare.Core.Abstractions/Exceptions`.
- Updated namespaces across the codebase to reflect context package removal.
- Added XML documentation for context-related types and handlers.
- CI workflows updated to skip building and distributing `Ergosfare.Context` package.
- Internal chore to support pipeline snapshot functionality.

### Notes
- This refactor is a chore to enable pipeline snapshot support.


## v0.1.2e '2025-9-20'

### **Added ambient data methods to `IExecutionContext`**

* `Set(string key, object item)` – Sets ambient data to share across the pipeline.
* `T Get<T>(string key)` – Retrieves ambient data by key.
* `bool Has(string key)` – Checks if a specific ambient data item exists.
* `bool TryGet<T>(string key, out T item)` – Attempts to retrieve ambient data; returns `true` if the item exists.

> **Note:** These methods are now part of the `IExecutionContext` API, allowing handlers and interceptors to store and access pipeline-level data.


## v0.1.1e  '2025-9-18'

### **Added Ergosfare.Test.Fixtures**
Includes various useful tools, helpers and stubs for test authors, mainly for Ergosfare internals and Plugin developers


## v0.1.0e – First minor release –  '2025-09-18'

### Final Interceptors

* Introduced new interfaces:

    * `IFinalInterceptor`, `IAsyncFinalInterceptor<TMessage, TResult>` for generic/non-generic messages.
    * `ICommandFinalInterceptor`, `IQueryFinalInterceptor`, `IEventFinalInterceptor` for higher-level modules.
* Final interceptors now run at the end of the pipeline (in `finally`), enabling cleanup and logging scenarios.
* Support for nullable message results and exceptions passed into final interceptors.

### Pre- & Post-Interceptors

* **Pre-Interceptors**:

    * Added support for both direct and indirect pre-interceptors.
    * Can mutate messages and return a new one.
    * Emit detailed events (`BeginPreIntercepting`, `FinishPreInterceptorInvocation`, etc.).

* **Post-Interceptors**:

    * Added support for both direct and indirect post-interceptors.
    * Can mutate results before returning to the caller.
    * Integrated `IResultAdapterService` to detect embedded exceptions in results.
    * Emit events for normal execution and exception-like results (`FinishPostInterceptingWithException`).

### ⚡ Exception Interceptors

* Support for direct and indirect exception interceptors.
* Emit begin/finish events for each interceptor.
* Full support for nullable `messageResult`.
* Rethrows exceptions if no interceptors are registered.

### Unified Pipeline Events

* Refactored **EventHub → SignalHub**, and renamed `HubEvent → Signal` for clarity.
* Introduced `PipelineEvent` base class (with `Message` and nullable `Result`).
* Standardized event naming:

    * Pre: `BeginPreInterceptingEvent`, `FinishPreInterceptorInvocationEvent`, …
    * Post: `BeginPostInterceptingEvent`, `FinishPostInterceptorInvocationEvent`, …
    * Exception: `BeginExceptionInterceptingEvent`, `FinishExceptionInterceptorInvocationEvent`
    * Final: `BeginFinalInterceptingEvent`, `FinishFinalInterceptorInvocationEvent`
* Events carry metadata (interceptor type, exception, total count).
* Unified equality comparison for better test coverage.

### Result Adapter

* Added `IResultAdapter` and `IResultAdapterService`:

    * Detect exceptions inside result objects (e.g., `ErrorOr`).
    * Allow exception interceptors to run without throwing original result types.
* Integrated with post-interceptors and mediators for consistent handling.

### Mediators

* `QueryMediator` and `EventMediator` updated to raise pipeline events.
* Integrated `IResultAdapterService` into mediation strategies (`SingleAsyncHandlerMediationStrategy`, `AsyncBroadcastMediationStrategy`).
* Improved async flow and nullability handling.

###  Execution Strategies & Handler Invokers

* Refactored pipeline execution strategies with final interceptor support.
* Introduced **Handler Invokers**:

    * Type-safe replacement for `MessageDependencyExtensions`.
    * Unified mechanism for invoking handlers and interceptors.
    * Integrated seamlessly with signals.

### Centralized Test Fixtures

* Added new `Ergosfare.Test.Fixtures` assembly.
* Common fixtures consolidated for reuse across test classes.
* Introduced categorized stubs and stub factories to reduce inline stubs.
* Improved maintainability, readability, and pluggability of test suite.
* Achieved **95%+ coverage** with fixture-based test design.


### Fixes & Refactors

* Cleaned up pipeline flow with consistent async/await handling.
* Removed `MessageDependencyExtensions` in favor of handler invokers.
* Unified naming across signals and interceptors.
* Simplified exception interception without reflection/dynamic.


### Benefits

* Final interceptors enable safe cleanup and logging.
* Pre- and post-interceptors can mutate messages and results consistently.
* Exception handling is safer, with result-based error detection via adapters.
* Unified event/signal system improves observability and debugging.
* Stronger modularity for command, query, and event pipelines.
* Centralized, reusable test infrastructure.
* Easier to maintain, extend, and test pipelines.

### Tests

* Fixture-based tests for:

    * `ErrorOr` adapter support.
    * Exception interception (direct/indirect).
    * Post-interceptor result exceptions.
* Updated tests for signal structure, handler invokers, and fixtures.
* Achieved consistent **95%+ coverage**.

___


## v0.0.16e – IHasProxyEvents '2025-09-11'
### **Added**
- IHasProxyEvents interface, contains all known proxy events
- EventHub now implements IHasProxyEvents
- Now known pipeline events subscrible with += and unsubscrible with -= syntax from EventHub

## v0.0.15e – Pipeline Event System Refactor & Coverage '2025-09-03'

### **Added**

* `PipelineEvent` abstract base class (formerly `PipelineEventBase`) with:
    * `Timestamp` auto-initialization
    * `RelatedEvents` support (`Add`, `AddRange`)
    * `GetEqualityComponents()` for value-based equality
* 20+ concrete pipeline events:
    * `BeginExceptionInterceptingEvent`, `BeginExceptionInterceptorInvocationEvent`, `BeginHandlerInvocationEvent`, `BeginHandlingEvent`, `BeginPipelineEvent`
    * `BeginPostInterceptingEvent`, `BeginPreInterceptorInvocationEvent`, `FinishExceptionInterceptingEvent`, `FinishExceptionInterceptorInvocationEvent`
    * `FinishHandlerInvocationEvent`, `FinishHandlingEvent`, `FinishHandlingWithExceptionEvent`, `FinishPipelineEvent`
    * `FinishPostInterceptingEvent`, `FinishPostInterceptingWithException`, `FinishPostInterceptorInvocationEvent`
    * `FinishPreInterceptingEvent`, `FinishPreInterceptingWithException`, `FinishPreInterceptorInvocationEvent`
* Factory methods (`Create`) for all pipeline events with null checks and default handling (`ResultType ?? typeof(void)`)
* Static subscription & publish support via `PipelineEvent.Subscribe<TEvent>`
* In-place instance invocation via `Invoke()` extension method

### **Changed**

* Renamed `PipelineEventBase` → `PipelineEvent`
* `HubEvent` updated:

    * `Timestamp` is instance-based, not static
    * `GetHashCode` and `Equals` use `GetEqualityComponents()`
    * `RelatedEvents` added to allow event chaining

### **Fixed / Improved**

* Full unit test coverage for:

    * All pipeline events (`Create`, `GetEqualityComponents`, equality, timestamp)
    * `RelatedEvents` behavior (add, add range, read-only enforcement)
    * Static subscription / publish mechanics
    * In-place `Invoke()` calls

### **Impact**

* Event pipeline fully type-safe and decoupled
* Subscribers can register without creating instances
* Improved consistency and maintainability of pipeline events

### **Testing**
- Unit tests updated to account for recent changes in HubEvent and PipelineEvent

- New unit tests added for all new pipeline events and related components

- RelatedEvents functionality fully tested (add, add range, read-only enforcement)

- Static subscription and in-place Invoke() methods tested for all pipeline events

- Value-based equality (GetEqualityComponents, Equals, GetHashCode) fully covered

- Maintained 100% test coverage for all event classes and base logic

---

## v0.0.14e – Event Hub Refines & Proxy Event System
### New Features
* Generic Event Hub (EventHub): Supports strongly-typed events using `HubEvent` base class, with strong and weak subscriptions.
* Proxy Events (`ProxyEvent<T>`)  
Subscribe to predefined events using += and unsubscribe using -= syntax for cleaner code.
* Predefined Event: `PreInterceptorBeingInvokeEvent` added as a foundational example for interceptors and handlers.
* Custom Events: Subscribe, publish, and unsubscribe custom HubEvent types independently of predefined proxies.
* Value Object Base for Events:
  HubEvent includes equality operators (`==`, `!=`) and value-based `Equals` / `GetHashCode` for future-proof event comparisons.

### Improvements
* Thread-safe subscriptions using ConcurrentDictionary and locking.
* Automatic cleanup of dead weak subscriptions during event publishing.

### Removals
* `ISubscription` and `IHubEvent` interfaces have been removed.
  * Replace `ISubscription` with `ISubscription<TEvent>`
  * Replace `IHubEvent` with the abstract `HubEvent` class
  
### Testing
#### Unit tests enhanced to cover:
* Strong/weak subscription invoke behavior
* Proxy `+=` / `-=` operators
* Subscription matching and unsubscription
* Base HubEvent equality and hash code computation

### Keynotes 
* This release lays the foundation for next-generation plug-ins and modules, allowing n-party decoupled event-driven integrations.

* Users can create their own events implementing HubEvent for custom plugin scenarios.
___
## v0.0.13e - Republish of v0.0.12e - '2025-09-03'
- no changes
## v0.0.12e - EventHub - '2025-09-03'

## **New Features**

* **Centralized EventHub**: A thread-safe, global hub for Pre, Post, Handler, and Exception stage events.
* **Weak and Strong Subscriptions**: Subscribers can be registered as strong or weak references.

    * `IsAlive` property allows automatic cleanup of dead weak references.
    * Subscriptions implement `IDisposable`.
* **DI Integration**: EventHub now resolves via DI as a singleton using `EventHubAccessor`.
* **Thread-safe Publishing**: Publishing events is safe across multiple threads, with automatic cleanup of dead weak subscriptions.
* **Extensible Plugin Support**: Modules can subscribe to events without modifying core components.

**Keynote**

- This EventHub forms the foundation for future plugins and modules that do not need to be directly coupled with main modules.

- It enables developers to write their own n-party plugins, extending the system safely and independently.

---

**Side Note:**

* This EventHub system is **separate from the message mediation events** (pre/post/interceptor) used in command, query, and event pipelines.
* It provides a **general-purpose, centralized event mechanism** for modules and plugins to subscribe to runtime events without coupling to the core pipeline.

---


## v0.0.11e - Refactor - '2025-09-01'

### Changed

* `ActualTypeOrFirstAssignableTypeMessageResolveStrategy`

    * Now constructor-injected with `IMessageRegistry`.
    * Simplified `Find` method signature (`Find(Type)` instead of `Find(Type, IMessageRegistry)`).
* `MessageMediator` updated to use the simplified strategy method.
* DI registration added for `ActualTypeOrFirstAssignableTypeMessageResolveStrategy`.
* Unit tests updated to reflect new DI-based message resolution.

### Notes

* Only the **message resolution part** of mediation is now resolved through DI.
* Other mediation internals (e.g., message dependencies creation, execution context) are still manually constructed and may be migrated to DI in future updates.

---



## v0.0.10e - '2025-09-01'

### Changed
- **Handlers & Interceptors**: Removed `CancellationToken` parameters from all contracts.  
  Execution context’s token is now used consistently instead.

### Breaking Changes
- Any custom handlers or interceptors that previously accepted a `CancellationToken`  
  must be updated to rely on the execution context for cancellation.

### Internal
- Refactored interface definitions to eliminate redundant token passing.
- Updated unit tests to use context-based cancellation.
- Coverage badge regenerated to reflect new code changes.



## v0.0.9e - '2025-8-31' -  Pipeline flow fixes


### Changed
- **Handlers**: Updated handler order grouping logic.
- **Interceptors**: Refined interceptor chaining based on group attributes.

### Fixed
- Resolved issue with default group assignment for ungrouped handlers.

### Internal
- Refactored handler registration process to streamline group assignment.

### Files Changed
- `HandlerRegistry.cs`
- `InterceptorChain.cs`

### Code Coverage
- Added tests for newly implemented functionality.



# v0.0.8e Pipeline flow control
## Introduced
- `public class GroupAttribute(params string[] groupNames)`
- `public class WeightAttribute(uint weight)`

## New Features: 
- Handler Grouping with GroupAttribute
- Handler Ordering with WeightAttribute

## Changes:
- IHandlerDescriptor has new two property `Weight` and `Groups`
- All handler descriptor builders updated internally to support grouping and ordering
- All internal mediator definitions updated to use grouping and ordering
- MessageDependencies and MessageDependenciesFactory updated internally
- TypeExtensions has new metohods `GetWeightFromAttribute()`, `GetGroupsFromAttribute()`


# 🌟 v0.0.6e 🛠️ Event, Command & Query module, unit tests & code coverage

 
#### 📊 New Features: Event module
* **🔹IEventExceptionInterceptor:** interface added, Event module now supports non generic ExceptionInterceptors.
* **🔹IEventExceptionInterceptor\<TEvent\>:** interface added, Event module now supports generic`<TEvent>` ExceptionInterceptors.
* **🔹IEventPreInterceptor:** interface added, Event module now supports non generic PreInterceptors.
* **🔹IEventPreInterceptor\<TEvent\> :** interface added, Event module now supports generic`<TEvent>` PreInterceptors.
* **🔹IEventPostInterceptor :** interface added, Event module now supports non generic PostInterceptors.
* **🔹IEventPostInterceptor\<TEvent\> :** interface added, Event module now supports generic`<TEvent>` PostInterceptors.


#### 📊 New Features: Command module
* **🔹ICommandExceptionInterceptor:** interface added, command module now supports non generic ExceptionInterceptors.
* **🔹ICommandExceptionInterceptor\<TEvent\>:** interface added, command module now supports generic`<TEvent>` ExceptionInterceptors.
* **🔹ICommandPreInterceptor:** interface added, command module now supports non generic PreInterceptors.
* **🔹ICommandPreInterceptor\<TEvent\> :** interface added, command module now supports generic`<TEvent>` PreInterceptors.
* **🔹ICommandPostInterceptor :** interface added, Command module now supports non generic PostInterceptors.
* **🔹ICommandPostInterceptor\<TEvent\> :** interface added, Command module now supports generic`<TEvent>` PostInterceptors.
* **🔹ICommandPostInterceptor\<TEvent,TResult\> :** interface added, Command module now supports generic`<TEvent, TResult>` PostInterceptors.


#### 📊 New Features: Query module
* **🔹IQueryExceptionInterceptor:** interface added, command module now supports non generic ExceptionInterceptors.
* **🔹IQueryExceptionInterceptor\<TQuery\>:** interface added, query module now supports generic`<TQuery>` ExceptionInterceptors.
* **🔹IQueryPreInterceptor:** interface added, query module now supports non generic PreInterceptors.
* **🔹IQueryPreInterceptor\<TQuery\> :** interface added, query module now supports generic`<TQuery>` PreInterceptors.
* **🔹IQueryPostInterceptor :** interface added, Query module now supports non generic PostInterceptors.
* **🔹IQueryPostInterceptor\<TQuery\> :** interface added, Query module now supports generic`<TQuery>` PostInterceptors.
* **🔹IQueryPostInterceptor\<TQuery,TResult\> :** interface added, query module now supports generic`<TQuery, TResult>` PostInterceptors.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Events`, `Ergosfare.Events.Abstractions`, `Ergosfare.Events.Extensions.MicrosfotDependencyInjection`.
* **💯 100% test coverage** for `Ergosfare.Queries`, `Ergosfare.Queries.Abstractions`, `Ergosfare.Queries.Extensions.MicrosfotDependencyInjection`.

* **📊 Total project coverage:** 99%.

___

# 🌟 v0.0.5e 

### 🛠️ CommandModule unit tests & code coverage

#### No breaking changes
* **🔹Refactor:** CommandModuleBuilder.Register<T>() now internally calls CommandModuleBuilder.Register(Type T).
* **🔹Refactor:** `MessageModule` renamed to `CoreModule`.
* **🔹Refactor:**`CoreModule`.Build(...) implemented.
* **🔹Chore:** Command module related tests and code coverage.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Command`, `Ergosfare.Command.Abstractions`, `Ergosfare.Command.Extensions.MicrosfotDependencyInjection`.
* **📊 Total project coverage:** 85%.




# 🌟 v0.0.4e – Core Contracts Refactor & Exception Interceptor

### 🛠️ Core Enhancements

* **🔹 Refactor:** Base contracts moved from `Contracts` package into their dedicated project **Abstractions**.
* **🔹 Refactor:** Streamlined `StreamAsyncMediationStrategy` for improved maintainability and clarity.
* **✨ Feature:** Introduced **`IExceptionInterceptor`** handler, descriptor, and variants — all pipeline types now support exception interceptors.

### ✅ Test Coverage Milestone

* **💯 100% test coverage** for `Ergosfare.Core.Abstractions`.
* **📊 Total project coverage:** 67%.



# 🌟 v0.0.3e

### General

* chore: bump C# version to latest major to support C# 13+

### Ergosfare.Contracts

* feat: pre/post Interceptors interface definitions
* feat: handlers and interceptors now receive IExecutionContext as argument (Handlers.Handle receives IExecutionContext)
* fix: require IExecutionContext in IAsyncPreInterceptor.Handle
* feat: introduced IStreamHandler\<TMessage, TResult> in contracts

### Ergosfare.Core

* feat: pre/post Interceptor descriptor definitions
* feat: add pre/post interceptor collections to IMessageDependencies and implement in MessageDependencies
* feat: Post/Pre InterceptorDescriptors Interface and Class Implementation
* feat: MessageDescriptorBuilderFactory supports building new interceptor descriptors
* feat: Message handlers now support pre/post interceptors
* feat: Mediation strategies updated to support IExecutionContext
* fix: correct PreInterceptorDescriptorBuilder filtering
* chore: Ergosfare.Core 100% covered with unit tests

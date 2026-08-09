# Stella.Ergosfare.Contract.Test

An executable specification of Ergosfare's **currently observable** public behavior.

This suite exists for one reason: the core is about to be redesigned. Internals may change
freely; this suite — and only this suite — defines what "unchanged behavior" means. It is
written as characterization tests, so **the current behavior is correct by definition**.
Where a behavior looked wrong, it was still pinned as-is and written down under
[Suspicious behaviors observed](#suspicious-behaviors-observed). Nothing in this project
fixes or works around anything.

```bash
dotnet test test/Stella.Ergosfare.Contract.Test
```

## What it may touch

Public types from the `Stella.Ergosfare.*` packages only: `AddErgosfare`, the module
builders, `ICommandMediator` / `IQueryMediator` / `IEventMediator`, the mediation settings
and `GroupSet`, `IExecutionContext`, `IMessageRegistry`, and the handler/interceptor
contract interfaces. No `InternalsVisibleTo`, no `*.Internal` namespaces, no reflection
into non-public members. A behavior that cannot be observed through the public surface is
out of scope by design — if the suite could see internals, the modernization could not keep
it green and the whole point would collapse.

## Registration axes

Source-generated registration is the primary axis: the generator is referenced as an
analyzer, and containers are built with `RegisterGenerated(<discovery key>)`. Explicit
`Register<T>()` is the fallback axis — the path the generated one degrades to.

Three areas run under **both** axes, each sharing its scenarios through an abstract
contract class and closing it over a per-axis type set:

| Area | Contract | Generated types | Fallback types |
| --- | --- | --- | --- |
| Pipeline semantics | `Pipeline/PipelineSemanticsContract.cs` | `Pipeline/GeneratedPipelineTypes.cs` | `Pipeline/FallbackPipelineTypes.cs` |
| Synchronous interceptors | `Sync/SyncSemanticsContract.cs` | `Sync/GeneratedSyncTypes.cs` | `Sync/FallbackSyncTypes.cs` |
| Post-interceptor abort | `Abort/PostAbortSemanticsContract.cs` | `Abort/GeneratedPostAbortTypes.cs` | `Abort/FallbackPostAbortTypes.cs` |

| Axis | Types | Registration | Executors actually reached |
| --- | --- | --- | --- |
| `GeneratedRegistration*Tests` | top-level, unkeyed, ungrouped | `RegisterGenerated()` | `StagedVoidPipelineExecutor`, `StagedResultPipelineExecutor`, `GeneratedVoidPipelineExecutor` + the emitted `StagedPlanN` classes |
| `RuntimeRegistration*Tests` | `[ExcludeFromDiscovery]` | `Register<T>()` | `VoidPipelineExecutor`, `ResultPipelineExecutor` + `SingleAsyncHandlerMediationStrategy` |

Both halves of that table are load-bearing, and both are easy to break by accident:

- **The generated axis must stay top-level, unkeyed and ungrouped.** The generator
  disqualifies keyed, grouped and nested types from its compile-time plans
  (`TryGetSolePlannableHandler` requires `DiscoveryKeys.IsEmpty`; staged-plan participants
  additionally must not be nested). Adding a `[DiscoveryKey]` here still registers through
  generated descriptors — and silently dispatches on the reflective executors, testing
  nothing the fallback axis does not already cover. The pattern-less `RegisterGenerated()`
  is therefore **reserved for the three areas above**, and every type they declare is local
  to its own area: a message type shared with another area, or an interceptor registered
  against something outside the area, would cross-contaminate the shared unkeyed pool.
- **The fallback axis must stay `[ExcludeFromDiscovery]`.** The generator's descriptor
  catalog is populated by a module initializer for *every* type it models, so a type the
  generator has seen gets pre-computed descriptors even when registered with
  `Register<T>()`. Excluding them is the only way to make the reflective path run.

Every other area registers by its own discovery key (`contract.dispatch`,
`contract.lifetime`, `contract.scope`, `contract.groups`, `contract.polymorphism`,
`contract.events`, `contract.broadcast`, `contract.mutation`, `contract.context`,
`contract.stream`, `contract.sync`, `contract.multi`, `contract.exclude`), which is also
what keeps the pattern-less call above selecting only the three axes. Types that must never be
auto-registered — never-registered messages, late-registered interceptors — carry
`[ExcludeFromDiscovery]`.

Four of the keyed areas are keyed *because* no plan can serve them, so both of their axes
reach the reflective path by construction and the key costs nothing:

- **`contract.sync`** (`Sync/SyncMainHandlerTests.cs`) — the bare-result contracts
  (`IHandler<T, object>`, `IHandler<T, string>`) put the bare result type in their
  descriptor rather than the `ValueTask` carrier every plan computation matches against
  (`ErgosfareRegistrationGenerator.cs:1668`), so they are disqualified before discovery
  keys are even considered. The `ValueTask`-shaped ones must stay keyed for a different and
  worse reason — see [suspicious behavior 10](#suspicious-behaviors-observed). Synchronous
  *interceptors* are neither: they do reach the emitted plans, which is why they live on
  the unkeyed axis above.
- **`contract.broadcast`** (`Events/SyncEventHandlerTests.cs`) — a publish never enters a
  compile-time plan: the generator emits plans for sole-handler command and query
  dispatches only, so every event fans out through the reflective broadcast strategy and
  its own synchronous pattern-match arms. The fan-out twin of `contract.sync`.
- **`contract.multi`** (`Handlers/MultipleMainHandlerTests.cs`) — the sole-handler gate
  drops any message with more than one main handler.
- **`contract.exclude`** (`Exclusion/PipelineExclusionTests.cs`) — the generator skips
  messages carrying `[ExcludeFromPipeline]` rather than modeling the exclusion.

## Rules for anyone adding tests here

**The `MessageRegistry` is process-wide.** It is a singleton shared by every container in
the test process, and it never forgets. Everything below follows from that.

1. **Give every test class its own message, handler and interceptor types.** Never share a
   message type across classes. Cross-class isolation is type isolation; there is nothing
   else.
2. **Give your area its own discovery key, and never call the pattern-less
   `RegisterGenerated()`.** That overload registers every default-discovery construct in
   the assembly, and it belongs to the three both-axis areas alone (see
   [Registration axes](#registration-axes)). An unkeyed message anywhere else joins those
   containers and can break their plan expectations. Reach for it only when the scenario
   genuinely needs to run inside an emitted plan — and then keep every type the scenario
   declares local to the new area.
3. **Never assert on registry contents or counts, and never assume an empty registry.** By
   the time your test runs, other classes have registered their types into the same
   registry.
4. **Do not register interceptors against a module marker** (`ICommand`, `IQuery`,
   `IEvent`). Such an interceptor attaches to every pipeline in the process, permanently,
   and breaks unrelated classes — including ones whose containers were built before it
   appeared and therefore cannot resolve it. Declare your own supertype instead, as
   `PolymorphicDispatchTests` does. If a scenario ever genuinely needs a marker-wide
   registration, it belongs in its own collection with `DisableParallelization = true`.
5. **Mutating the registry after a container is live goes in
   `RegistryMutationCollection`** (`DisableParallelization = true`). The version bump
   invalidates every container's cached pipeline.
6. **Do not pin internals.** Instance identity is contract only where lifetime says so
   (transient = new per dispatch, memoized = reused). No assertions on pooling, caching,
   plan or strategy selection, or timing.
7. **No sleeps.** Use `TaskCompletionSource` if async coordination is ever needed. Every
   scenario here completes synchronously today.
8. **Hand-written stubs, no Moq.** The repository is phasing mocks out.

## How a scenario observes a dispatch

`PipelineRecorder` travels into a dispatch on the mediation settings' `Items` dictionary —
the public way to hand data to a pipeline — and every participant marks its own stage
through it. An expectation is therefore one ordered list of stage names, and a failure
prints the whole trace.

## Diagnostic stack dump

Ordering alone does not say *which* internal lane ran. With capture switched on, every
mark also records the managed frames between the mediator entry point and the stage, and
the traces are written to one file at process exit:

```bash
ERGOSFARE_CONTRACT_STACKDUMP=1 ERGOSFARE_CONTRACT_DUMP_PATH=./contract-stackdump.txt dotnet test test/Stella.Ergosfare.Contract.Test -f net9.0
```

Each section is labelled with the scenario and the axis, so a redesigned core can be
diffed against the current one frame by frame. The same scenario under the two axes is
what the [Registration axes](#registration-axes) table asserts in prose — the compiled
plan on one side, the strategy-and-invoker stack on the other (both excerpts are the
`pre` stage of `A_post_interceptor_rewrite_is_the_result_the_caller_receives`, trimmed of
their test-side frames):

```
GeneratedRegistrationPipelineTests — stages: [pre, handler, post, final]
  1. pre  <- ok
     | ...
         | Stella.Ergosfare.Commands.CommandMediator.SendAsync
           | Stella.Ergosfare.Core.MessageDispatchEngine.DispatchAsync
             | Stella.Ergosfare.Core.Internal.Mediator.StagedResultPipelineExecutor`2.Execute
               | Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations+StagedPlan2.ExecuteDirect
                 | ...
                   | ...IAsyncPreInterceptor<TCommand>.HandleAsync
                     | ...
                       | Stella.Ergosfare.Contract.Test.Pipeline.PayloadResultPreBase`1.HandleAsync
                         | Stella.Ergosfare.Contract.Test.Harness.Recording.Mark

RuntimeRegistrationPipelineTests — stages: [pre, handler, post, final]
  1. pre  <- ok
     | ...
         | Stella.Ergosfare.Commands.CommandMediator.SendAsync
           | Stella.Ergosfare.Core.MessageDispatchEngine.DispatchAsync
             | Stella.Ergosfare.Core.Internal.Mediator.ResultPipelineExecutor`2.Execute
               | ...SingleAsyncHandlerMediationStrategy`2.Mediate
                 | ...
                   | ...PreInterceptorInvocationStrategy`1.Invoke
                     | ...
                       | ...IAsyncPreInterceptor<TCommand>.HandleAsync
```

Capture is **off by default and never asserted on**. It changes nothing the tests observe;
the pinned contract remains the stage ordering alone.

Hardware counters (cache misses, branch mispredictions, cycles) are deliberately *not*
here: measurement is neither deterministic nor a behavioral contract, and rules 6 and 7
above rule it out. That work lives in `test/Stella.Ergosfare.Benchmarking` as
`CachePressureBenchmark`, whose baseline was captured alongside this one.

## Lane-map baseline

[`baselines/lane-map.txt`](baselines/lane-map.txt) is that dump, captured and committed
before the core modernization started. Sections are written in a stable order, so two
dumps of an unchanged core are byte-identical — as are the two TFMs.

**Every modernization phase re-captures it and diffs against the stored file:**

```bash
ERGOSFARE_CONTRACT_STACKDUMP=1 ERGOSFARE_CONTRACT_DUMP_PATH=./lane-map.txt dotnet test test/Stella.Ergosfare.Contract.Test -f net9.0
diff test/Stella.Ergosfare.Contract.Test/baselines/lane-map.txt ./lane-map.txt
```

Green tests say the *behavior* survived. They cannot say which lane produced it: a
compiled plan that quietly stops qualifying falls back to the reflective executors and
every assertion in this suite still passes. **A changed lane map under green tests is the
only early warning that the plan lanes silently degraded.** So a diff is not a failure —
it is a question that must be answered in the PR:

- Expected (the phase moved a lane on purpose): re-capture and commit the new baseline in
  the same PR, and say in the body which frames moved and why.
- Unexpected: the lanes diverged. Stop and report; do not update the baseline to make the
  diff go away.

One diff shape is mechanical rather than behavioral: the emitted plan classes are numbered
positionally (`StagedPlan0`, `StagedPlan1`, …), so adding an unkeyed message that qualifies
for a plan renumbers the existing ones. That shows up as changed frames inside untouched
sections. It is only benign when every removed line is a `StagedPlanN` frame and the shift
is uniform — check that before waving it through.

Frames are captured from a `Debug` build; a `Release` capture inlines differently and is
not comparable to this file.

## Suspicious behaviors observed

Pinned as-is. Each is a candidate for the modernization to decide on deliberately rather
than change by accident.

1. **`IExecutionContext.Abort(object? messageResult)` ignores its argument.** The
   implementation throws `ExecutionAbortedException` unconditionally; the documented
   "result to abort with" never reaches the caller, and the final interceptor is handed
   `null` (or the result type's default). Pinned by
   `Aborting_with_a_result_value_still_throws_and_does_not_deliver_the_value`.

2. ~~**Two different exceptions mean "nothing will handle this."**~~ *Fixed.* A message
   type absent from the registry used to produce `NoHandlerFoundException` while a
   registered message whose handlers were all filtered out produced a plain
   `InvalidOperationException("No handler is registered for X.")`, so no single `catch`
   covered both. Both now raise `NoHandlerFoundException` — which derives from
   `InvalidOperationException`, so callers who were catching the second case keep catching
   it — and the two stay told apart by the message alone. Pinned by the group-filtering
   scenarios and `A_base_typed_handler_does_not_serve_a_derived_message_registered_in_its_own_right`.

3. ~~**Events invert the default for "no handler."**~~ *Fixed.* Publishing an
   *unregistered* event type used to throw `NoHandlerFoundException` whatever the caller
   asked for, while a *registered* event nobody handles was a silent no-op unless
   `ThrowIfNoHandlerFound` was set — so the flag governed one of the two ways a publish
   reaches nobody. Both obey it now, and the default for both is the silent no-op that
   fire-and-forget implies. Pinned by
   `Publishing_an_unregistered_event_type_is_a_no_op_like_a_registered_one` and
   `Publishing_an_unregistered_event_type_throws_when_the_caller_asks_it_to`.

   The cost is real and was taken deliberately: a misspelled or never-registered event
   type used to announce itself and now goes quietly. `ThrowIfNoHandlerFound` is the way
   to get that back for a publisher that must know someone listened.

   What the old behavior really depended on is worth recording, because it is a trap for
   anyone writing event tests anywhere: **"unregistered" is not a property of the event
   type, it is a property of the whole process.** The resolve strategy falls back to the
   first *assignable* descriptor, so a single participant registered against the `IEvent`
   marker — a non-generic `IEventPreInterceptor`, say — gives every event type in the
   process a descriptor, and nothing is unregistered from then on. The old throw therefore
   fired or did not fire depending on whether such a participant existed anywhere in the
   app. This area keeps its own `[ExcludeFromDiscovery]` types and registers nothing at
   marker level (rule 3 above), which is what makes `UnknownEvent` mean what it says here.

4. **Covariance applies to interceptors but not to main handlers.** An interceptor
   registered against a supertype joins a derived message's pipeline. A *handler*
   registered against a supertype only serves a derived message that has no descriptor of
   its own — register the derived type (which `RegisterGenerated` does for every discovered
   message) and the base handler becomes an indirect handler that single-handler mediation
   never considers, so the dispatch fails. Pinned by the two
   `A_base_typed_handler_*` scenarios.

5. **Runtime registry mutation is half-supported.** Registering an interceptor type after
   the container is built does change the next dispatch — but only if that type was already
   in DI. Otherwise the next dispatch throws `InvalidOperationException: No service for
   type ...`, and because the registry has no removal, that message type stays broken for
   the rest of the process. Pinned by
   `A_late_registered_interceptor_the_container_cannot_resolve_fails_the_next_dispatch`.

6. **A void pipeline's "result" is a `ValueTask` sentinel, except on abort.** Post, final
   and exception interceptors of a void command are handed a non-null `ValueTask` as the
   result argument even though the handler produced nothing — but on an aborted dispatch
   the final interceptor gets `null`. Three different values (`ValueTask`, `null` from the
   exception stage, `null` on abort) for "there is no result."

7. ~~**The generated staged-plan code emits nullable warnings into the consumer's
   build.**~~ *Fixed.* Building this project used to surface `CS8604` eight times per TFM
   in `ErgosfareRegistrations.g.cs`: the emitted staged result plan cast its post chain to
   `TResult?` and handed that to the post interceptor's non-nullable `messageResult`
   parameter, which broke consumers building with `TreatWarningsAsErrors`. The emitter now
   picks the cast from the stage contract — post takes `TResult`, exception and final take
   `TResult?` — so **this project builds with zero warnings**, and a warning appearing here
   again is a regression rather than a curiosity.

   The count used to double as evidence that the staged-plan lane was being exercised.
   That job belongs to the [lane-map baseline](#lane-map-baseline) now, which names the
   lane that ran instead of inferring it from a warning.

8. **A synchronous exception or final interceptor throws `NullReferenceException` on a
   void pipeline.** The void pipeline carries a `ValueTask` in its result slot, but the
   slot is still empty before the handler completes — and the synchronous contracts have no
   result-agnostic flavor, so `FinalInterceptorInvocationStrategy.cs:59` (and its
   exception-stage twin) unboxes that `null` into a `ValueTask` parameter. The emitted plan
   does the same, through `ResultCast`'s `(ValueTask)result!`. Every failure path of such a
   pipeline therefore ends in a `NullReferenceException`, which — thrown from a `finally` —
   replaces both the handler's own exception and `ExecutionAbortedException`. Pinned by the
   two `A_void_pipelines_synchronous_*` scenarios on both axes. Result-typed pipelines are
   unaffected: `null` casts to `string?` and `default` boxes for value types.

9. **A synchronous participant cannot be registered without a module marker.** The module
   builders reject any type that is not assignable to `ICommand` / `IQuery` / `IEvent`
   (`CommandModuleBuilder.Register`), and the module-flavored interceptor facades inherit
   that marker themselves — `ICommandPreInterceptor<T> : ICommand`. The synchronous
   contracts in `Core.Abstractions.Handlers` have no such facade, so a bare
   `IPreInterceptor<T>` is silently skipped by `RegisterGenerated`, and the dispatch fails
   later with `NoHandlerFoundException: No handler is registered for X`. The silence is
   the scan path's alone: handing the same bare type to the explicit `Register<T>()` throws
   `NotSupportedException` at registration instead. Every synchronous participant in this
   suite therefore declares `: ICommand, IPreInterceptor<T>` — see `Sync/SyncContracts.cs`
   (and `: IEvent, IHandler<T, ·>` on the broadcast side, `Events/SyncEventHandlerTests.cs`).

10. **A `ValueTask`-shaped synchronous main handler breaks the consumer's build.** The plan
    computations gate on the descriptor's result type, and `IHandler<T, ValueTask>` /
    `IHandler<T, ValueTask<TResult>>` record exactly the carrier they look for — so an
    unkeyed one qualifies and the generator emits `AddVoidPlan<TMessage, THandler>` /
    `AddResultPlan<…>` for it. Those methods constrain `THandler` to `IAsyncHandler<…>`,
    which a synchronous handler does not implement, and the emitted file fails to compile
    with `CS0311`. Both shapes were verified by temporarily un-keying them; both fail. This
    is why `Sync/SyncMainHandlerTests.cs` keeps its types keyed — a compile error cannot be
    pinned by a test, so this entry is the record. The runtime is not at fault:
    `VoidPipelineExecutor` and the mediation strategies both have a working arm for these
    handlers, and the keyed axis exercises it.

11. **A memoized handler instance survives only until the next registration anywhere in the
    process.** `ForceMemoizedHandlers` caches the instance inside the handler reference held
    by a `MessageDependencies` object, and that object is cached against the registry
    version (`MessageDependenciesFactory.Create` →
    `MessageDescriptorCache.InvalidateIfRegistryChanged`). Registering a *new* type — in any
    container, for any unrelated message — bumps `MessageRegistry.Version`, drops the cache
    and rebuilds the references, so the next dispatch constructs a fresh "memoized"
    instance. Duplicate registrations are free; only genuinely new types bump.
    <br />This one was found the hard way. `ForceMemoizedHandlers_reuses_one_instance_across_dispatches`
    had been passing since the suite was written, but the phase-1 areas added six more
    classes whose first container build registers new types, widening the window enough to
    fail roughly one run in ten. It is not a test defect and not a race in the registry: a
    registration between two dispatches genuinely resets memoization. The scenario is now in
    `RegistryMutationCollection` so nothing registers beside it — the only change this phase
    made to an existing test file, and the reason is this entry. Whether memoized instances
    should survive a refresh is a live question for the plan lifecycle
    (freeze → refresh → re-freeze), not something to paper over here.

## Where it runs

The project is listed in `Stella.Ergosfare.slnx`, so a solution-wide `dotnet test` picks
it up on both TFMs. CI runs it in the `UnitTests` workflow, whose filter names this
suite's category explicitly (`Category=Unit|Category=Contract`) — a contract test without
`[Trait("Category", "Contract")]` builds and passes locally but never runs there. The
`Coverage` workflow has no filter and runs it too.

Triggers are unchanged: pull requests into `preview` and `main` run CI; the `preview-dev`
integration branch keeps its no-CI-on-dev-PRs policy, so the local run before a dev PR is
still mandatory.

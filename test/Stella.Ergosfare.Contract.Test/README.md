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
| Typed exception filters | `ExceptionFilters/ExceptionFilterSemanticsContract.cs` | `ExceptionFilters/GeneratedExceptionFilterTypes.cs` | `ExceptionFilters/FallbackExceptionFilterTypes.cs` |

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
  is therefore **reserved for the four areas above**, and every type they declare is local
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

One gate is deliberately left unpinned, because no assertion can see it: an exception
interceptor that implements the non-generic `IExceptionInterceptorFilter` by hand — rather
than declaring one `IExceptionInterceptorFilter<TException>` — has no compile-time exception
type, so the generator disqualifies the whole plan and the dispatch falls back to the
reflective stage, which asks the instance. Both paths then produce the same observable
behavior; only the lane map would show the difference. Do not write such an interceptor in
the unkeyed pool: it would take an area's plans down without failing a single test.

## Rules for anyone adding tests here

**The `MessageRegistry` is process-wide.** It is a singleton shared by every container in
the test process, and it never forgets. Everything below follows from that.

1. **Give every test class its own message, handler and interceptor types.** Never share a
   message type across classes. Cross-class isolation is type isolation; there is nothing
   else.
2. **Give your area its own discovery key, and never call the pattern-less
   `RegisterGenerated()`.** That overload registers every default-discovery construct in
   the assembly, and it belongs to the four both-axis areas alone (see
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
9. **Give your area its own exception types.** Since typed exception interceptors match on
   assignability, an exception type is now a selector as much as a message type is: an
   interceptor declaring a widely-used base type accepts faults thrown by scenarios that
   have never heard of it. Declare the faults your area throws inside the area, and never
   filter on a framework exception type.

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

1. ~~**`IExecutionContext.Abort(object? messageResult)` ignores its argument.**~~ *Fixed.*
   The implementation threw `ExecutionAbortedException` unconditionally and never read the
   argument, so the documented "result to abort with" reached nobody — and the exception
   itself surfaced to the caller, which the XML doc did not mention either.

   Aborting is a short circuit now. The parameter is gone from the signature (it never
   worked, and no shim pretends otherwise), and the caller receives **what the pipeline had
   already produced**: the handler's result when the abort came after it, the result type's
   default when it came before. No exception reaches the caller on any path, the
   exception-interceptor stage never sees the abort, and final interceptors run as they
   always do — handed the same value the caller gets, with no exception.

   `ExecutionAbortedException` still exists and is still what travels: it unwinds the
   participant up to the mediation strategy or the baked plan, which catches it. It is an
   implementation detail of the unwind, not a signal to catch — a `catch` for it in
   participant code defeats the abort rather than observing it.

   Pinned across the areas that reach each arm: `Abort/` for "already produced" on all three
   result shapes, the `Aborting_*` and `A_*_abort_*` scenarios in `Pipeline/` and `Sync/`
   for "nothing produced yet",
   `A_handler_aborting_a_pipeline_with_no_interceptors_completes_the_dispatch` and its
   result twin for the interceptor-free lane the executors serve without ever entering a
   strategy, `Aborting_a_publish_completes_it_without_an_exception` for the fan-out, and
   `A_nested_dispatchs_abort_stops_at_its_own_dispatch` for the scoped-child case.

2. ~~**Two different exceptions mean "nothing will handle this."**~~ *Fixed.* A message
   type absent from the registry used to produce `NoHandlerFoundException` while a
   registered message whose handlers were all filtered out produced a plain
   `InvalidOperationException("No handler is registered for X.")`, so no single `catch`
   covered both. Both now raise `NoHandlerFoundException` — which derives from
   `InvalidOperationException`, so callers who were catching the second case keep catching
   it — and the two stay told apart by the message alone. Pinned by the group-filtering
   scenarios.

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

4. ~~**Covariance applies to interceptors but not to main handlers.**~~ *Fixed.* An
   interceptor registered against a supertype joins a derived message's pipeline; a
   *handler* registered against a supertype used to serve a derived message only while that
   message had no descriptor of its own. Registering the derived type — which
   `RegisterGenerated` does for every discovered message — filed the base handler as an
   indirect one, and single-handler mediation never looked there, so the dispatch failed.

   Direct and indirect handlers are one candidate set now, so whether a message has a
   descriptor of its own no longer decides who serves it. Two candidates are a contest and
   fail the dispatch with `MultipleHandlerFoundException` — the same outcome two direct
   handlers produce, counted before anything resolves so neither claimant runs. Pinned by
   the two `A_base_typed_handler_*` scenarios and
   `A_direct_and_a_base_typed_handler_claiming_one_message_fail_the_dispatch`.

   Two consequences worth knowing:

   - **A group filter that empties the direct set now falls through to a covariantly
     matched handler** rather than failing with `NoHandlerFoundException`. The filter is
     applied to both sets while the shape is built, so an indirect handler that survives it
     is a candidate like any other. No scenario pins this corner yet.
   - **The compiled plans give such a message up.** A message with any base-typed main
     handler is disqualified from every staged and single-handler plan: a plan bakes one
     handler in and cannot express "this is contested", and the model cannot prove the
     supertype registration is the only one the runtime will see. Those dispatches take the
     reflective path, and the lane map is what says so.

5. ~~**Runtime registry mutation is half-supported.**~~ *Partly fixed — the diagnosis, not
   the constraint.* Registering an interceptor type after the container is built changes
   the next dispatch only if that type is also in DI. It used to fail with an opaque
   `InvalidOperationException: No service for type ...`, raised part-way through the
   dispatch by whichever stage first asked for the participant. Pipeline construction now
   checks every planned participant against the container up front and raises
   `UnresolvableParticipantException`, which names the message, names the participant and
   states the remedy, before any stage runs.

   **The underlying constraint stands and cannot be fixed here:** the registry is
   process-wide, has no removal, and a container is per-application, so a participant
   resolvable in one container may be absent from another. That is also why the check
   cannot live in `Register` — only a pipeline being built in a container's context can
   answer the question. What the fix does guarantee is that the failure is not sticky:
   nothing is cached for a failed build, so a container that does register the participant
   builds the pipeline the broken one could not. Pinned by
   `A_late_registered_interceptor_the_container_cannot_resolve_fails_the_next_dispatch`
   and `A_container_that_can_resolve_the_late_participant_builds_the_pipeline_the_broken_one_could_not`.

   The check covers indirect main handlers too, which was the one corner where it could
   have broken a dispatch that always worked: single-handler mediation never read that slot,
   so an unresolvable handler sitting in it was harmless. Entry 4 closed that gap from the
   other side — the slot is a candidate set now, so anything in it genuinely has to be
   resolvable, and checking it is no longer eager. Only the events fan-out ever read it
   before, and it always resolved what it read.

6. ~~**A void pipeline's "result" is a `ValueTask` sentinel, except on abort.**~~ *Fixed.*
   Post, final and exception interceptors of a void command used to be handed a non-null
   `ValueTask` even though the handler produced nothing, while an aborted dispatch handed
   the final interceptor `null` — three values for "there is no result." A resultless
   pipeline now carries one: `Unit.Value`, the single instance of
   `Stella.Ergosfare.Core.Abstractions.Unit`, from the moment the handler completes.

   `null` is no longer a synonym for it; it kept its own meaning. It is the slot *before*
   anything was produced, which is what the exception stage of a failed dispatch and the
   final stage of a pre-interceptor abort see. A publish has nothing to produce and fills
   the slot up front, so all three of its stages see `Unit.Value`. Pinned by
   `A_void_pipelines_result_agnostic_stages_are_handed_the_shared_unit_instance`,
   `A_void_pipelines_result_slot_is_empty_until_the_handler_has_run`, and the two publish
   scenarios in `Events/EventPublishTests.cs`.

   **`Unit` is a class, not a struct**, and that is load-bearing rather than a taste call —
   see entry 8. Two consequences worth knowing:

   - The result key of a resultless pipeline changed from `ValueTask` to `Unit`, and no
     compiler can see it. An interceptor still written against
     `IPostInterceptor<T, ValueTask>` registers exactly as before and then matches no arm,
     so the dispatch fails with `NotSupportedException` instead of quietly skipping the
     stage. The noise is deliberate. Pinned by
     `A_void_interceptor_keyed_on_the_old_result_type_fails_the_dispatch_loudly`.
   - The event facades moved with the key: `IEventExceptionInterceptor<T>` and
     `IEventFinalInterceptor<T>` close over `Unit` now. `IEventPostInterceptor<T>` did not
     change shape — it was already result-agnostic underneath, and its default
     implementation stopped boxing on the way through.

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

8. ~~**A synchronous exception or final interceptor throws `NullReferenceException` on a
   void pipeline.**~~ *Fixed by entry 6, and the reason `Unit` is a class.* The result slot
   travels as `object?` and is cast back to the pipeline's result type at every stage.
   While that type was `ValueTask`, an empty slot meant unboxing `null` into a struct —
   `FinalInterceptorInvocationStrategy.cs:59`, its exception-stage twin, and the emitted
   plans' `ResultCast` — and the synchronous contracts have no result-agnostic flavor to
   escape into. Every failure path of such a pipeline ended in a `NullReferenceException`
   thrown from a `finally`, replacing both the handler's own exception and
   `ExecutionAbortedException`.

   `Unit` is a reference type, so the same cast on an empty slot yields `null` and the
   stage simply observes that nothing was produced. **No cast expression changed** — the
   crash was a property of the type in the slot, not of the code reading it. Pinned by
   `A_void_pipelines_synchronous_stages_run_with_an_empty_result_when_the_handler_throws`
   and `A_void_pipelines_synchronous_final_interceptor_runs_on_abort_with_no_result`, the
   two scenarios that used to assert the crash. Result-typed pipelines were never affected:
   `null` casts to `string?` and `default` boxes for value types.

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

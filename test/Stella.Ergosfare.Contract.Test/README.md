# Stella.Ergosfare.Contract.Test

An executable specification of Ergosfare's **currently observable** public behavior.

This suite exists for one reason: the core is being redesigned. Internals may change
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
and `GroupSet`, `ErgosfareContext`, the public exception types, and the
handler/interceptor contract interfaces. No `InternalsVisibleTo`, no `*.Internal`
namespaces, no reflection into non-public members. A behavior that cannot be observed
through the public surface is out of scope by design — if the suite could see internals,
the modernization could not keep it green and the whole point would collapse.

## Registration axes

There is one dispatch lane now: the compiled plan. The source generator is referenced as
an analyzer, models every discoverable construct in this assembly at compile time, and
emits a plan for every pipeline it can prove; plans load through module initializers. At
run time the executor resolves a container's live participants once, verifies them
against the plan's baked composition, and runs the plan — or refuses the dispatch.
Nothing degrades into a runtime lane anymore, because there is none.

A dispatch the plans cannot serve fails precisely:

- a message nobody serves still raises `NoHandlerFoundException`, and a contested level
  still raises `MultipleHandlerFoundException` — the throw path inspects the live
  participants, so both work without any plan;
- everything else raises `UnplannedDispatchException`, whose `Reason` says why without
  parsing: `NoCompiledPlan`, `NoDispatchRoot`, `CompositionDiverged`,
  `MemoizedInstances`, `UnplannedResultAdapter`, `ForeignDependenciesFactory` or
  `UnplannedGroupSet`.

Events keep their fire-and-forget half: a publish that reaches nobody is a silent no-op
(`ThrowIfNoHandlerFound` unchanged), while a publish that would reach somebody without a
plan throws — a subscriber must never be skipped silently. Broadcast plans include
covariant subscribers and interceptor-free events.

Two registration styles remain, and both feed the same lane:

- **The pattern-less `RegisterGenerated()`** — reserved for the four contract areas
  below. It registers every default-discovery construct in the assembly, including every
  other area's unkeyed types. That is safe only while each unkeyed message stays scoped
  to its own test class and no interceptor targets a shared or marker type: a
  registered-but-never-dispatched pipeline is inert.
- **Explicit `Register<T>()`** — the norm everywhere else. The plan gate demands each
  dispatched message's full compiled pipeline per container: register the handler and
  every discoverable participant of that message, or the dispatch fails with
  `CompositionDiverged` naming the diverged stage (pinned in
  `Unplanned/UnplannedDispatchTests.cs`).

The four contract areas run their scenarios through an abstract contract class closed
over one type set each. They used to close it a second time, over
`[ExcludeFromDiscovery]` twins, for a runtime-registration axis; that axis was deleted
with the lane it exercised — and it had already stopped testing that lane before the
removal (see [suspicious behavior 12](#suspicious-behaviors-observed)).

| Area | Contract | Types |
| --- | --- | --- |
| Pipeline semantics | `Pipeline/PipelineSemanticsContract.cs` | `Pipeline/GeneratedPipelineTypes.cs` |
| Synchronous interceptors | `Sync/SyncSemanticsContract.cs` | `Sync/GeneratedSyncTypes.cs` |
| Post-interceptor abort | `Abort/PostAbortSemanticsContract.cs` | `Abort/GeneratedPostAbortTypes.cs` |
| Typed exception filters | `ExceptionFilters/ExceptionFilterSemanticsContract.cs` | `ExceptionFilters/GeneratedExceptionFilterTypes.cs` |

The formerly keyed areas — dispatch, context, groups, lifetime, scope, events,
polymorphism — are unkeyed and plan-served now: their fixture types were hoisted to
top level, their `[DiscoveryKey]`s dropped, and their containers register each dispatched
message's pipeline explicitly. Where the old behavior survives on the plan lane, the
assertions are unchanged; where it cannot, the scenario pins the loud failure instead.

### What is unplannable today

These shapes have no compiled plan, so dispatching them fails loudly — each is pinned
where listed:

- **Nested types** — anything declared inside a class. Every fixture in the migrated
  areas is top-level for exactly this reason.
- **`[DiscoveryKey]`-keyed and `[ExcludeFromDiscovery]` types.** A hand-registered
  excluded handler serves nothing, void, result and publish alike:
  `Unplanned/UnplannedDispatchTests.cs`.
- **Synchronous main handlers**, all four shapes: `Sync/UnplannedSyncMainHandlerTests.cs`
  for sends, `Events/UnplannedSyncEventHandlerTests.cs` for publishes. The
  ValueTask-shaped pair cannot even be pinned unkeyed — unkeying them breaks the build;
  see [suspicious behavior 10](#suspicious-behaviors-observed).
- **`[ExcludeFromPipeline]` messages that would need a staged plan** —
  `Exclusion/PipelineExclusionTests.cs`. Only the degenerate bare-handler case is
  planned, and there the covariant suppression genuinely works.
- **Sends claimed only covariantly** — `Polymorphism/PolymorphicDispatchTests.cs`. A
  direct handler beside covariant claims is planned and wins its ladder; publishes keep
  covariance outright.
- **Open-generic pipelines** — `Dispatch/GenericHandlerDispatchTests.cs`. The dispatch
  sites record the open definition and no closed form gets a plan (engine suspect; see
  entry 12).
- **Sends mixing grouped and ungrouped participants** — `Groups/GroupFilteringTests.cs`
  (engine suspect; the broadcast side models the same mix fine).
- **Memoized pipelines** — `ForceMemoizedHandlers`, and any pipeline whose participants
  are all singletons, which a user's `AddSingleton` of the only handler produces:
  `Lifetime/HandlerLifetimeTests.cs`.

One area keeps a discovery key, because being unkeyed is impossible for it:

- **`contract.multi`** (`Handlers/MultipleMainHandlerTests.cs`) — two discoverable
  handlers claiming one message is a build error now (ERGO010/ERGO023), so the contested
  pairs must stay out of the generator's model for the dispatch-time contest to be
  observable at all.

The streaming area (`Streaming/StreamQueryTests.cs`) is plan-served like everything
else now: the generator compiles a stream plan per (query, item) pair — sole direct
handler through the stream contract, default set only — so its fixtures are top-level
and unkeyed like the other areas', and its behavioral assertions are unchanged. Grouped
streams have no per-set plans yet; naming a set on a stream fails as
`UnplannedGroupSet`.

One historical gate note: an exception interceptor implementing the non-generic
`IExceptionInterceptorFilter` by hand has no compile-time exception type. It used to
demote a plan silently; today an unplannable pipeline cannot run at all, so writing one
beside an unkeyed message takes that message's dispatches down loudly. Do not.

## Rules for anyone adding tests here

**The `MessageRegistry` is process-wide.** It is a singleton shared by every container in
the test process, and it never forgets. Everything below follows from that.

1. **Give every test class its own message, handler and interceptor types.** Never share a
   message type across classes. Cross-class isolation is type isolation; there is nothing
   else.
2. **Register your area's participants explicitly, and never call the pattern-less
   `RegisterGenerated()`.** That overload registers every default-discovery construct in
   the assembly, and it belongs to the four contract areas alone (see
   [Registration axes](#registration-axes)). A new area declares top-level, unkeyed,
   message-scoped types and lists each dispatched message's full pipeline with
   `Register<T>()` — the plan gate insists on the full pipeline, and an explicit list is
   the honest spelling of it. A discovery key is not an isolation tool anymore: keyed
   types are unplannable, so a key buys a loud failure, not a lane.
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
   `RegistryMutationCollection`** (`DisableParallelization = true`). The engine surgery
   removed the last scenarios that did; the collection definition stays for the next
   one, because the registry is still process-wide and still never forgets.
6. **Do not pin internals.** Instance identity is contract only where lifetime says so
   (transient = new per dispatch). No assertions on pooling, caching, plan or strategy
   selection, or timing.
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
diffed against the current one frame by frame. The excerpt pair below is historical —
captured while both lanes existed — and is kept because it shows exactly what the
runtime-lane removal deleted: the same scenario served by the compiled plan on one side
and by the strategy-and-invoker stack on the other (both excerpts are the `pre` stage of
`A_post_interceptor_rewrite_is_the_result_the_caller_receives`, trimmed of their
test-side frames):

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

Green tests say the *behavior* survived. They cannot say which lane produced it — which
is what made the map the early warning while a fallback lane still existed, and what
makes it the proof now that only the plan frames remain. A diff is not a failure — it is
a question that must be answered in the PR:

- Expected (the phase moved a lane on purpose): re-capture and commit the new baseline in
  the same PR, and say in the body which frames moved and why.
- Unexpected: the lanes diverged. Stop and report; do not update the baseline to make the
  diff go away.

The stored baseline predates the runtime-lane removal and this suite's migration onto the
plan lane, so the next deliberate re-capture will be a wholesale rewrite: every
`RuntimeRegistration*` section drops out, and the renamed areas re-key their sections.
That diff is expected; [suspicious behavior 12](#suspicious-behaviors-observed) is its
explanation.

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

   **Abort is a signal now, and it stops the pipeline.** Nothing downstream of the aborting
   participant runs: not the rest of its own stage, not the exception stage (an abort is not
   a failure), not the final stage. There is no result to deliver either — a stopped
   pipeline did not produce one — so the caller is told rather than handed a default it
   would have to interpret. `ExecutionAbortedException` is that signal, and it is part of
   the contract: a dispatch whose participants can abort is one the caller wraps in a
   `try`. Applications that would rather carry outcomes as values have the result-adapter
   surface for that.

   The overloads say what the participant wants said: `Abort()`, `Abort(reason)` and
   `Abort(reason, value)`, arriving on the exception as `Reason` and `Value`. The old
   parameter's mistake was claiming to set the pipeline's *result*; a stopped pipeline has
   none, and what a caller actually needs is why.

   The mechanism no longer varies by pipeline shape, which is the other half of the fix:
   with interceptors or without, the signal travels straight out. The strategies and the
   emitted plans only mark themselves aborted so their own final stage is skipped; the
   executors and the engine have no abort code at all, and the `AbortShortCircuit` helper
   that carried the old semantics is gone.

   Pinned across the areas that reach each arm: `Abort/` for aborting after a result was
   produced, on all three result shapes; the `Aborting_*` and `A_*_abort_*` scenarios in
   `Pipeline/` and `Sync/` for aborting before the handler;
   `A_handler_aborting_a_pipeline_with_no_interceptors_reaches_the_caller` and its result
   twin for the interceptor-free lane the executors serve without ever entering a strategy;
   `Aborting_a_publish_reaches_the_publisher` for the fan-out; and, for the scoped-child
   case, `A_nested_dispatchs_abort_surfaces_to_the_handler_that_nested_it` with
   `A_handler_that_catches_a_nested_abort_carries_on` beside it — the outer handler is the
   inner dispatch's call site, so it is who hears it, and catching is how it says the inner
   step was optional.

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

4. ~~**Covariance applies to interceptors but not to main handlers.**~~ *Fixed, then
   narrowed by the plan lane — see entry 12.* An interceptor registered against a
   supertype joins a derived message's pipeline; a *handler* registered against a
   supertype used to serve a derived message only while that message had no descriptor of
   its own. Registering the derived type — which discovery does for every discovered
   message — filed the base handler as an indirect one, and single-handler mediation never
   looked there, so the dispatch failed.

   Direct and indirect handlers are read as one priority ladder now, so whether a message
   has a descriptor of its own no longer decides who serves it. A sole direct handler wins
   the ladder outright — a covariant handler is a fallback for messages nobody claims
   directly, not a competitor — and without one, the covariant level serves. The ladder has
   no tiebreaker *within* a level: two claimants on the same level are a contest and fail
   the dispatch with `MultipleHandlerFoundException`, counted before anything resolves so
   neither claimant runs.

   On the plan lane the ladder's direct level survives — a message with its own handler is
   planned, covariant claims and all, and the direct handler wins — but the covariant
   *fallback* level does not: a message claimed only covariantly has no plan, and its
   dispatch fails unplanned rather than running the base handler. Pinned by
   `A_direct_handler_beats_a_base_typed_one_claiming_the_same_message`,
   `Two_covariant_claimants_with_no_direct_handler_fail_the_dispatch`, and the two
   `*_claimed_only_through_its_base_*` scenarios that used to pin the fallback serving.

5. ~~**Runtime registry mutation is half-supported.**~~ *Partly fixed — the diagnosis, not
   the constraint; since the dependency freeze, resolved by contract; the mutation
   scenarios themselves were removed with the runtime lane.* Registering an interceptor
   type after the container is built changed the next dispatch only if that type was also
   in DI, and used to fail with an opaque `InvalidOperationException` part-way through the
   dispatch. Pipeline construction later checked every planned participant against the
   container up front (`UnresolvableParticipantException`), and the dependency freeze then
   retired registration-after-first-dispatch observability altogether. On the plan-only
   engine the question has moved: a participant the generator saw but the container did
   not register fails the dispatch as `CompositionDiverged`, named per stage — pinned in
   `Unplanned/UnplannedDispatchTests.cs`.

   **The underlying constraint stands and cannot be fixed here:** the registry is
   process-wide, has no removal, and a container is per-application, so a participant
   resolvable in one container may be absent from another. That is also why the check
   cannot live in `Register` — only a pipeline being built in a container's context can
   answer the question.

6. ~~**A void pipeline's "result" is a `ValueTask` sentinel, except on abort.**~~ *Fixed;
   the publish half superseded on the plan lane — see entry 12.* Post, final and exception
   interceptors of a void command used to be handed a non-null `ValueTask` even though the
   handler produced nothing, while an aborted dispatch handed the final interceptor `null`
   — three values for "there is no result." A resultless pipeline now carries one:
   `Unit.Value`, the single instance of `Stella.Ergosfare.Core.Abstractions.Unit`, from
   the moment the handler completes.

   `null` is no longer a synonym for it; it kept its own meaning. It is the slot *before*
   anything was produced, which is what the exception stage of a failed dispatch and the
   final stage of a pre-interceptor abort see. A successful publish fills the slot before
   its post stage runs, so post and final see `Unit.Value`; a **failed** publish's
   exception and final stages see the empty slot now — the compiled broadcast plan fills
   the slot only after each handler completes, where the retired runtime fan-out filled it
   up front. Pinned by
   `A_void_pipelines_result_agnostic_stages_are_handed_the_shared_unit_instance`,
   `A_void_pipelines_result_slot_is_empty_until_the_handler_has_run`, and the publish
   scenarios in `Events/EventPublishTests.cs`.

   **`Unit` is a class, not a struct**, and that is load-bearing rather than a taste call —
   see entry 8. Two consequences worth knowing:

   - The result key of a resultless pipeline changed from `ValueTask` to `Unit`, and no
     compiler can see it. An interceptor still written against
     `IPostInterceptor<T, ValueTask>` registers exactly as before and then matches no arm.
     On the plan lane that leaves its whole message without a compiled plan, so the
     dispatch fails with `UnplannedDispatchException` (`NoCompiledPlan`) instead of
     quietly skipping the stage. The noise is deliberate; only the messenger changed —
     it used to be the stage's own `NotSupportedException`. Pinned by
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
   `A_void_pipelines_synchronous_stages_run_with_an_empty_result_when_the_handler_throws`,
   the scenario that used to assert the crash; its abort twin now pins that the synchronous
   final stage does not run at all when the pipeline is cut (entry 1). Result-typed pipelines were never affected:
   `null` casts to `string?` and `default` boxes for value types.

9. **A synchronous participant cannot be registered without a module marker.** The module
   builders reject any type that is not assignable to `ICommand` / `IQuery` / `IEvent`
   (`CommandModuleBuilder.Register`), and the module-flavored interceptor facades inherit
   that marker themselves — `ICommandPreInterceptor<T> : ICommand`. The synchronous
   contracts in `Core.Abstractions.Handlers` have no such facade, so a bare
   `IPreInterceptor<T>` is silently skipped by discovery, and handing the same bare type
   to the explicit `Register<T>()` throws `NotSupportedException` at registration.
   Every synchronous participant in this suite therefore declares
   `: ICommand, IPreInterceptor<T>` — see `Sync/SyncContracts.cs` (and `: IEvent,
   IHandler<T, ·>` on the broadcast side, `Events/UnplannedSyncEventHandlerTests.cs`).

10. **A `ValueTask`-shaped synchronous main handler breaks the consumer's build.**
    Re-verified on the plan-only engine (2026-08-18), and it grew a broadcast flavor. The
    plan computations gate on the descriptor's result type, and `IHandler<T, ValueTask>` /
    `IHandler<T, ValueTask<TResult>>` record exactly the carrier they look for — so an
    unkeyed one qualifies and the generator emits `AddVoidPlan<TMessage, THandler>` /
    `AddResultPlan<…>` for it. Those methods constrain `THandler` to `IAsyncHandler<…>`,
    which a synchronous handler does not implement, and the emitted file fails to compile
    with `CS0311`. An unkeyed `IHandler<TEvent, ValueTask>` *subscriber* is likewise baked
    into the emitted broadcast plan as a `HandleAsync` call it does not have, and fails
    with `CS1061`. A compile error cannot be pinned by a test, so this entry is the
    record, and the `Unplanned*` pin classes keep those shapes `[ExcludeFromDiscovery]`
    and reach the runtime throw through hand registration instead. The bare-result shapes
    (`IHandler<T, object>`, `IHandler<T, string>`) are declined cleanly and are pinned
    unkeyed.

11. ~~**A memoized handler instance survives only until the next registration anywhere in the
    process.**~~ *Closed by the dependency freeze, then mooted by the runtime-lane
    removal: memoized pipelines no longer dispatch at all — see entry 12.*
    `ForceMemoizedHandlers` cached the instance inside the handler reference held by a
    `MessageDependencies` object, and that object was cached against the registry version,
    so registering a new type anywhere in the process rebuilt the references and reset the
    "memoized" instance — a one-in-ten flake found the hard way. The dependency freeze
    closed the flake (executors stopped consulting the registry version after their first
    dispatch), and the plan-only engine then retired the contract itself: a compiled plan
    resolves or constructs its participants fresh, so a memoized pipeline is refused, on
    every dispatch, with `UnplannedDispatchException` (`MemoizedInstances`). Pinned by
    `ForceMemoizedHandlers_fails_the_dispatch`.

12. **The runtime lane is gone (2026-08-18), and this suite now describes the plan-only
    engine.** A deliberate contract change, not a regression: nothing is dispatched at run
    time that was not produced at compile time by the source generator. A pipeline without
    a compiled plan fails loudly with `UnplannedDispatchException` — its `Reason` naming
    the cause — instead of degrading into a reflective lane; a message nobody serves still
    raises `NoHandlerFoundException` and a contested one still raises
    `MultipleHandlerFoundException`, both computed from the live participants without any
    plan. The casualties, all failing loudly pending generator coverage: synchronous main
    handlers (sends and publishes; entries 9 and 10), `[ExcludeFromPipeline]` messages
    that need a staged plan, sends claimed only covariantly (entry 4), open-generic
    pipelines, sends mixing grouped and ungrouped participants, and memoized pipelines —
    `ForceMemoizedHandlers`, and the all-singleton pipeline a user's `AddSingleton` of the
    only handler produces, so the "user's own singleton wins" idiom now fails where a
    user's transient factory still wins. A failed publish's exception and final stages now
    see the empty result slot (`null`), superseding the publish half of entry 6.

    Three of these look like engine gaps rather than doctrine, and are pinned as observed
    with engine-suspect comments at the pin: **open-generic sends** (the dispatch sites
    record the open definition — `Wrap`1`, no type arguments — and no closed form gets a
    plan, though the compilation names `Wrap<int>` and `Wrap<string>` at the call sites),
    **the mixed grouped/ungrouped send** (`MixedAudience` gets no plan at all, default or
    per-set, while the broadcast side bakes both a default and a per-set plan for the
    same mix), and **the all-singleton memoization refusal** (the plan's resolving
    variant would return the same instance per dispatch anyway).

    Two things were true before the removal and belong in the record. The fallback axis
    had already stopped testing the fallback: commit `ae21f35` dropped the
    `using …Fallback` imports from the Pipeline and Sync runtime-axis classes, so their
    `Register<T>()` calls silently bound to the generated twin types in the same
    namespace and their scenarios rode the compiled plans — green tests, wrong lane, on
    the axis whose whole purpose was the other lane. And the Abort and ExceptionFilters
    runtime axes, which did still use their excluded twins, failed every dispatch the
    moment the lane went — which is the removal working as designed.

## Where it runs

The project is listed in `Stella.Ergosfare.slnx`, so a solution-wide `dotnet test` picks
it up on both TFMs. CI runs it in the `UnitTests` workflow, whose filter names this
suite's category explicitly (`Category=Unit|Category=Contract`) — a contract test without
`[Trait("Category", "Contract")]` builds and passes locally but never runs there. The
`Coverage` workflow has no filter and runs it too.

Triggers are unchanged: pull requests into `preview` and `main` run CI; the `preview-dev`
integration branch keeps its no-CI-on-dev-PRs policy, so the local run before a dev PR is
still mandatory.

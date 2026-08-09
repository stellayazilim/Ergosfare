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

The pipeline scenarios in [`Pipeline/`](Pipeline) run under **both**, sharing their code
through `PipelineSemanticsContract`:

| Axis | Types | Registration | Executors actually reached |
| --- | --- | --- | --- |
| `GeneratedRegistrationPipelineTests` | `Pipeline/GeneratedPipelineTypes.cs` — top-level, unkeyed, ungrouped | `RegisterGenerated()` | `StagedVoidPipelineExecutor`, `StagedResultPipelineExecutor`, `GeneratedVoidPipelineExecutor` + the emitted `StagedPlanN` classes |
| `RuntimeRegistrationPipelineTests` | `Pipeline/FallbackPipelineTypes.cs` — `[ExcludeFromDiscovery]` | `Register<T>()` | `VoidPipelineExecutor`, `ResultPipelineExecutor` + `SingleAsyncHandlerMediationStrategy` |

Both halves of that table are load-bearing, and both are easy to break by accident:

- **The generated axis must stay top-level, unkeyed and ungrouped.** The generator
  disqualifies keyed, grouped and nested types from its compile-time plans
  (`TryGetSolePlannableHandler` requires `DiscoveryKeys.IsEmpty`; staged-plan participants
  additionally must not be nested). Adding a `[DiscoveryKey]` here still registers through
  generated descriptors — and silently dispatches on the reflective executors, testing
  nothing the fallback axis does not already cover. The pattern-less `RegisterGenerated()`
  is therefore **reserved for this axis**.
- **The fallback axis must stay `[ExcludeFromDiscovery]`.** The generator's descriptor
  catalog is populated by a module initializer for *every* type it models, so a type the
  generator has seen gets pre-computed descriptors even when registered with
  `Register<T>()`. Excluding them is the only way to make the reflective path run.

Every other area registers by its own discovery key (`contract.dispatch`,
`contract.lifetime`, `contract.scope`, `contract.groups`, `contract.polymorphism`,
`contract.events`, `contract.mutation`, `contract.context`, `contract.stream`), which is
also what keeps the pattern-less call above selecting only the pipeline axis. Types that
must never be auto-registered — never-registered messages, late-registered interceptors —
carry `[ExcludeFromDiscovery]`.

## Rules for anyone adding tests here

**The `MessageRegistry` is process-wide.** It is a singleton shared by every container in
the test process, and it never forgets. Everything below follows from that.

1. **Give every test class its own message, handler and interceptor types.** Never share a
   message type across classes. Cross-class isolation is type isolation; there is nothing
   else.
2. **Give your area its own discovery key, and never call the pattern-less
   `RegisterGenerated()`.** That overload registers every default-discovery construct in
   the assembly, and it belongs to `GeneratedRegistrationPipelineTests` alone (see
   [Registration axes](#registration-axes)). An unkeyed message anywhere else joins that
   axis' containers and breaks its plan expectations.
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

2. **Two different exceptions mean "nothing will handle this."** A message type that is not
   in the registry produces `NoHandlerFoundException`; a registered message whose handlers
   are all filtered out by a group filter produces a plain
   `InvalidOperationException("No handler is registered for X.")`. Callers cannot catch
   both with one type.

3. **Events invert the default for "no handler."** Publishing a *registered* event nobody
   handles is a silent no-op unless `ThrowIfNoHandlerFound` is set; publishing an
   *unregistered* event type throws `NoHandlerFoundException` regardless of that flag. The
   setting only controls the first case.

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

7. **The generated staged-plan code emits nullable warnings into the consumer's build.**
   Building this project surfaces `CS8604` twice in
   `ErgosfareRegistrations.g.cs` — the emitted staged result plan passes a
   possibly-null `string` into `IAsyncPostInterceptor<TMessage, TResult>.HandleAsync`,
   whose `messageResult` parameter is non-nullable. It appears for any reference-typed
   result whose pipeline qualifies for a staged plan. Left unsuppressed on purpose: it is
   the only warning in this project and it is evidence the staged-plan lane is being
   exercised.

## Where it runs

The project is listed in `Stella.Ergosfare.slnx`, so a solution-wide `dotnet test` picks
it up on both TFMs. CI runs it in the `UnitTests` workflow, whose filter names this
suite's category explicitly (`Category=Unit|Category=Contract`) — a contract test without
`[Trait("Category", "Contract")]` builds and passes locally but never runs there. The
`Coverage` workflow has no filter and runs it too.

Triggers are unchanged: pull requests into `preview` and `main` run CI; the `preview-dev`
integration branch keeps its no-CI-on-dev-PRs policy, so the local run before a dev PR is
still mandatory.

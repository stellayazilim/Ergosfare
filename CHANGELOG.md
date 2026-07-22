## v1.2.0 – 'Unreleased'

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


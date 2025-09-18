
## v0.1.0e – First minor release –  2025-18-09

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


## v0.0.16e – IHasProxyEvents 2025-11-09
### **Added**
- IHasProxyEvents interface, contains all known proxy events
- EventHub now implements IHasProxyEvents
- Now known pipeline events subscrible with += and unsubscrible with -= syntax from EventHub

## v0.0.15e – Pipeline Event System Refactor & Coverage 2025-03-09

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
## v0.0.13e - Republish of v0.0.12e - 2025-09-03
- no changes
## v0.0.12e - EventHub - 2025-09-03

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


## v0.0.11e - Refactor - 2025-09-01

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



## v0.0.10e - 2025-09-01

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



## v0.0.9e - 2025-8-31 -  Pipeline flow fixes


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


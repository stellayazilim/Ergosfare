Perfect! Here’s a **changelog entry** in your style, minimal emoji, ready for `ergosfare.changelog`:

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


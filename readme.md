
# Ergosfare

![Ergosfare Logo](./7101c7df-6cac-4b25-994a-60e2adbdc546.png)

[![NuGet](https://img.shields.io/nuget/v/Stella.Ergosfare.svg)](https://www.nuget.org/packages/Stella.Ergosfare)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/stellayazilim/ergosfare/coverage_tests.yml?label=tests)
![Coverage](https://github.com/stellayazilim/Ergosfare/blob/badges/.badges/main/coverage.svg)

---

## 📖 Documentation & 📜 Changelog

- [Documentation](https://stellayazilim.github.io/Ergosfare.Docs)  
- [Changelog](https://stellayazilim.github.io/ergosfare.changelog)

---

## Overview

**Ergosfare** is a lightweight, reflection-free, high-performance mediation library for implementing **CQRS** and messaging patterns in the .NET ecosystem.  

Unlike other mediator libraries, Ergosfare is:

- ⚡ **Fast & AOT-friendly** — No runtime reflection, compile-time registration.  
- ✅ **Fully open-source** — MIT licensed, no restrictions.  
- 🧩 **Modular** — Commands, Queries, Events can be used independently.  
- 🔄 **Flexible** — Supports covariance & contravariance for more natural type matching.  
- 🛠 **Extensible** — Interceptor pipeline for cross-cutting concerns.  
- 🔗 **DI-friendly** — Works out of the box with `Microsoft.Extensions.DependencyInjection`.  

---

## Features

- Command, Query, Event, and Stream modules with unified execution model.  
- Group-based handler execution  (default group for undecorated handlers).  
- Weight-based handler ordering
- Cancellation propagation via **execution context** (no need to pass `CancellationToken` manually).  
- Interceptors:
  - Pre / Post / Exception / Final stages  
  - Sync & Async variants  
- No external dependencies — works with your DI container of choice.  

---

## Modules

Ergosfare is structured into independent modules:

- **Core**  
- **Core.Abstractions**  
- **Contracts** (only shared contracts like attributes)  
- **Commands**, **Commands.Abstractions**  
- **Queries**, **Queries.Abstractions**  
- **Events**, **Events.Abstractions**  
- 
👉 *Use only what you need — modules are designed to be composable and independent.*  

---

## 💿 Installation

Ergosfare packages are available on **NuGet.org**.  

```bash
dotnet add package Stella.Ergosfare
````

> Replace with module-specific package if needed (`Stella.Ergosfare.Commands`, `Stella.Ergosfare.Queries`, etc.)
> **Tip:** If using GitHub Packages, make sure your `nuget.config` includes the GitHub feed.

---

## Quick Start

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

public record CreateProduct(string Name) : ICommand<Guid>;

public class CreateProductHandler : ICommandHandler<CreateProduct, Guid>
{
    public Task<Guid> HandleAsync(CreateProduct command, IExecutionContext context)
    {
        var id = Guid.NewGuid();
        return Task.FromResult(id);
    }
}
```
```cs
var services = new ServiceCollection()
    .AddErgosfare(cfg =>
    {
        cfg.AddCommandModule(b => b.RegisterFromAssembly(Assembly.GetExecutingAssembly()));
    })
    .BuildServiceProvider();
```
```cs
var mediator = services.GetRequiredService<ICommandMediator>();
var productId = await mediator.SendAsync(new CreateProduct("Laptop"));
Console.WriteLine($"Created product: {productId}");
```

---

## Interceptors

Cross-cutting concerns are handled via interceptors:

```csharp
public class LoggingInterceptor : IAsyncPostInterceptor<ICommand>
{
    public async Task HandleAsync(ICommand message, object? result, IExecutionContext context)
    {
        Console.WriteLine($"Executed {message.GetType().Name} with result {result}");
        await Task.CompletedTask;
    }
}
```

Available interceptor types:
- Pre, always run before main handlers, allow to modify/override the message or short-circuit the pipeline before reaching main handler
- Post, always run before successful main handlers, allow modify/override the result of handler produced. 
- Exception, runs if an pre/main/post handler throw an exception, can shallow exception, rethrow exception, retry pipeline again or modify result if applicable 
- Final, always run at very end of pipeline wheater is success or fail
---


## Pipeline snapshots
Ergosfare can snapshot an handler state partially or entirely 

```cs
public class JoinUserToRoleCommandHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ISnapshotService snapshotService
) : ICommandHandler<JoinUserToRoleCommand>
{
    public async Task HandleAsync(JoinUserToRoleCommand command, IExecutionContext context)
    {
        // handlers body snapshotted entirely by default
        
        // if you want snapshat a scope partially, use ISnapshotService
        // to take scoped snapshot
        // Snapshot a database call
        var role = await snapshotService.Snapshot(
            "snapshot-role-exist",
            new Snapshot<RoleEntity>(async () =>
            {
                return await roleRepository
                    .GetRoleByNameAsync(command.Role.Name, context.CancellationToken);
            })
        );

        // if this call throws, snapshot ensures previous GetRoleByName() expensive DB call is not repeated
        command.User.AddRole(role);

        await userRepository.UpdateUserAsync(command.User, context.CancellationToken);
    }
}
```
- The Snapshot wraps the GetRoleByNameAsync call.
- If an exception occurs later in the handler (e.g., AddRole or UpdateUserAsync), the pipeline can safely retry without re-running the expensive database query.
-  subsequent retries or reruns of the handler use the cached value.




---
```cs
class QueryCachePreInterceptor(ICacheService cacheService) : IQueryPreInterceptor
{
    public async Task<object> Handle(object message, IExecutionContext context)
    {
        var key = $"{message.GetType().Name}:{message.GetHashCode()}";
        if (cacheService.TryGet(key, out var snapshot))
        {
            context.Checkpoints.Add(snapshot); // use cached snapshot
        }
        return message;
    }
}
```
```cs
class QueryCacheFinalInterceptor(ICacheService cacheService) : IQueryFinalInterceptor
{
    public async Task Handle(object message, IExecutionContext context)
    {
        var key = $"{message.GetType().Name}:{message.GetHashCode()}";
        var snapshot = context.Checkpoints.ToArray();
        cacheService.Set(key, snapshot); // save checkpoint for future reuse
    }
}
```
This two snippets demonstrates real-time caching of expensive operations using snapshots and interceptors. Instead of running entire query pipeline we can return cached result



## Result Adapters

Ergosfare supports **railway-style result handling** without throwing exceptions. This allows you to capture and handle success/failure results inside your handler.

You can integrate any result library (e.g., **FluentResults**, **ErrorOr**) with Ergosfare.

**Example interface:**

```csharp
public interface IResultAdapter
{
    bool CanAdapt(object result); 
    bool TryGetException(object result, out Exception? exception);
}
```

**Example adapter:**

```csharp
public class ResultAdapter : IResultAdapter
{
    public bool CanAdapt(object result) => result is Result<int>;

    public bool TryGetException(object result, out Exception? exception)
    {
        if (result is Result<int> r)
        {
            exception = r.Error;
            return exception != null;
        }

        exception = null;
        return false;
    }
}
```

**Integration with the pipeline:**

```csharp
builder.Services.AddErgosfare(options =>
{
    options.ConfigureResultAdapters(adapterBuilder =>
        adapterBuilder.Register<ResultAdapter>()
    );
});
```

**Highlights:**

* Handler results can be checked without throwing exceptions.
* Works with any compatible result type or library.
* Enables consistent success/failure handling in commands, queries, and streams.


## Plugin Example

Ergosfare modules (e.g., `QueryModule`) are plugins built on `IModule`. You can create your own modular, independent, and thread-safe plugins.

### Example Service

```csharp
internal sealed class ExampleService(ISignaltHub signalHub) : IDisposable
{
    public ExampleService() => signalHub.BeginPipelineSignal += OnPipelineBegin;

    private void OnPipelineBegin(@event) =>
        Console.WriteLine($"Pipeline Event occurred: {@event.MessageType}");

    public void Dispose() => signalHub.BeginPipelineSignal -= OnPipelineBegin;
}
```

> Ergosfare designed to registering its apis concurrently. Wrapping the service as a plugin ensures thread-safety and there is no race-condition.

### Plugin Module

```csharp
public sealed class ExamplePlugin : IModule
{
    private readonly Action<ExamplePluginBuilder> _builder;
    public ExamplePlugin(Action<ExamplePluginBuilder> builder) => _builder = builder;

    public void Build(IModuleConfiguration configuration) =>
        _builder(new ExamplePluginBuilder(configuration.Services));
}
```

### Plugin Builder

```csharp
public class ExamplePluginBuilder
{
    private readonly IServiceCollection _services;
    public ExamplePluginBuilder(IServiceCollection services) => _services = services;

    public ExamplePluginBuilder AddLogConsoleOnPipeline()
    {
        _services.AddSingleton<ExampleService>();
        return this;
    }
}
```

### Module Registry Extension

```csharp
public static class ModuleRegistryExtensions
{
    public static IModuleRegistry AddExamplePlugin(
        this IModuleRegistry registry,
        Action<ExamplePluginBuilder> builder)
        => registry.Register(new ExamplePlugin(builder));
}
```

### Usage with Ergosfare

```csharp
services.AddErgosfare(options =>
{
    options
        .AddExamplePlugin(builder => builder.AddLogConsoleOnPipeline())
        .AddCommandModule(_ => { })
        .AddQueryModule(_ => { });
});
```


## Signals & SignalHub

**Signals** are framework-level notifications that report **pipeline execution stages**, such as when a handler or interceptor is about to run. They are **not domain events**—they exist purely to observe or hook into the Ergosfare message pipeline.

**SignalHub** is the central dispatcher for these signals:

* Subscribe to receive notifications about **handler execution**, **pre/post interceptors**, or **pipeline retries**.
* Publish framework-level signals to notify other components about pipeline state.
* Multiple subscribers are supported, and signal dispatching is **non-blocking**.

**Example:**

```csharp
// Subscribe to a pipeline retry signal
SignalHubAccessor.Instance.Subscribe<PipelineRetrySignal>(signal =>
{
    Console.WriteLine($"Handler retry requested for {signal.Message.GetType().Name}");
});

// Publish a signal
var retrySignal = new PipelineRetrySignal(message, result, checkpoints);
SignalHubAccessor.Instance.Publish(retrySignal);
```


**Key Points:**

* Designed for **plugins** to interact with the Ergosfare system at a **framework level**.
* Useful for logging, metrics, monitoring, or custom pipeline behaviors.
* Works seamlessly with **snapshots, result adapters, and interceptors**.
* Enables **decoupled observation and interaction** with pipeline execution stages without modifying handlers.


## Testing

Ergosfare provides **built-in testing tools and fixtures** to help you validate your handlers, interceptors, and plugins efficiently:

* **Handler & Pipeline Testing**
  Easily simulate command/query/event execution with **pre-configured execution contexts**.
* **Snapshot Testing**
  Validate that your **snapshot and retry logic** works as expected.
* **Plugin Integration Tests**
  Load your plugin in a controlled test environment to ensure **thread-safe behavior** and proper interaction with signals, interceptors, and services.
* **Result Adapter Verification**
  Test custom result adapters to ensure **consistent exception and result handling**.

**Example:**

```csharp
// Create a new fixture instance
var fixture = new ExecutionContextFixture();

// Use default context
var defaultCtx = fixture.Ctx;

// Create a completely isolated context
var isolatedCtx = fixture.CreateContext();

// Run a scoped block with a custom context
await using (var scope = fixture.CreateScope(isolatedCtx))
{
    var ctx = AmbientExecutionContext.Current;
    // ctx is the isolated context
}

// Optionally propagate default context
fixture.PropagateAmbientContext();
Assert.Same(fixture.Ctx, AmbientExecutionContext.Current);

```
```cs
var pipelineFixture = new AspPipelineFixture();

// Simulate an HTTP GET request
var response = await pipelineFixture.SendAsync(
    new HttpRequestMessage(HttpMethod.Get, "/api/products/42")
);

// Assert that the HTTP response is successful
Assert.Equal(HttpStatusCode.OK, response.StatusCode);

// Assert that the correct handler was invoked in the pipeline
Assert.True(pipelineFixture.IsInvoked(typeof(GetProductByIdQueryHandler)));

// Assert that the response contains expected content
var content = await response.Content.ReadAsStringAsync();
Assert.Contains("Laptop", content);

```
> These tools are primarily intended for **plugin developers** but can also help test **core command/query/event logic**.


## Roadmap

- Text fixtures for plugin developers
- Project templates
- Example projects using Ergosfare

---

## Project Status

The core functionality is fully working with support for commands, queries, and events.
Handler & Interceptor contracts are **stable** and unlikely to change further.

## Current Focus

* Unit & Test coverage
* Documentation
* Preparation for first stable v1.0.0 release
---

## Contributing

Contributions, discussions, and feedback are welcome!
Feel free to open issues, submit PRs, or suggest improvements.

---

## License

Ergosfare licensed under the [MIT License](LICENSE).


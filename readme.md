
# Ergosfare

![Ergosfare Logo](./7101c7df-6cac-4b25-994a-60e2adbdc546.png)

[![NuGet](https://img.shields.io/nuget/v/Stella.Ergosfare.svg)](https://www.nuget.org/packages/Stella.Ergosfare)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/stellayazilim/ergosfare/coverage_tests.yml?label=tests)
[![Coverage](./.badges/main/coverage.svg)](./coverage/coverage.cobertura.xml)

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
- Group-based handler execution ordering (default group for undecorated handlers).  
- Cancellation propagation via **execution context** (no need to pass `CancellationToken` manually).  
- Interceptors:
  - Pre / Post / Exception stages  
  - Sync & Async variants  
- No external dependencies — works with your DI container of choice.  

---

## Modules

Ergosfare is structured into independent modules:

- **Core**  
- **Core.Abstractions**  
- **Context** (execution context: cancellation, metadata, ambient data)  
- **Contracts** (common message contracts)  
- **Commands**, **Commands.Abstractions**  
- **Queries**, **Queries.Abstractions**  
- **Events**, **Events.Abstractions**  
- **Streams**, **Streams.Abstractions**  

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

var services = new ServiceCollection()
    .AddErgosfare(cfg =>
    {
        cfg.AddCommandModule(b => b.RegisterFromAssembly(Assembly.GetExecutingAssembly()));
        cfg.AddInterceptors(); // enables interceptor pipeline
    })
    .BuildServiceProvider();

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

* `IPreInterceptor<TMessage, TResult>`
* `IPostInterceptor<TMessage, TResult>` / `IAsyncPostInterceptor<TMessage, TResult>`
* `IExceptionInterceptor<TMessage, TResult>` / `IAsyncExceptionInterceptor<TMessage>`

---

## Roadmap

* Built-in interceptors: Validation, Unit of Work, etc.
* Built-in error handling policies.
* Built-in caching mechanism for query module.
* Railway style Result adapters (FluentResults-style).
* Execution-Snapshotting:

    * Resume execution from the failed handler instead of restarting the pipeline.
    * Pause/Continue execution programmatically via snapshot support.

---

## Project Status

The core functionality is fully working with support for commands, queries, and events.
Handler contracts are **stable** and unlikely to change further.
Interceptor contracts (especially **post- and exception-interceptors**) are still being refined, so **minor API changes** may occur in future releases.

---

## Current Focus

* Refining interceptor contracts.
* Strengthening the Execution Context.

---

## Contributing

Contributions, discussions, and feedback are welcome!
Feel free to open issues, submit PRs, or suggest improvements.

---

## License

Licensed under the [MIT License](LICENSE).


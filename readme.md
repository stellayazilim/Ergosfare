
# Ergosfare
![7101c7df-6cac-4b25-994a-60e2adbdc546.png](7101c7df-6cac-4b25-994a-60e2adbdc546.png)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![coveragebadge](./.badges/main/coverage.svg)](./coverage/coverage.covertura.xml)
## Description

Ergosfare is a lightweight, flexible, and high-performance mediation library for implementing CQRS and messaging patterns in the .NET ecosystem.

It was created as an open-source alternative to MediatR's commercial licensing and avoids runtime reflection by leveraging compile-time type safety.
Additionally, it offers built-in support for covariance and contravariance, enabling more flexible type relationships.

---

## Features

* ✅ Fully open-source — no licensing restrictions.
* ⚡ Reflection-free design — compile-time registration for AOT compatibility.
* 🔄 Covariance & Contravariance support — enables more flexible handler assignments.
* 🧩 Modular architecture — Command, Query, Event, Stream modules can be used independently.
* 🛠 Interceptor pipeline — pre/post/exception interceptors (sync & async).
* 🔗 Fully compatible with Microsoft.Extensions.DependencyInjection — easily integrates with your existing DI container.
* 🔗 No external dependencies required — you can choose any DI infrastructure you prefer.

---

## Modular Structure

Ergosfare is organized into modular components, such as:

* **Core**
* **Core.Abstractions**
* **Context**
* **Contracts**
* **Core.Extensions.DependencyInjection**
* **Commands**
* **Commands.Abstractions**
* **Queries**
* **Queries.Abstractions**
* **Events**
* **Events.Abstractions**
* **Streams**
* **Streams.Abstractions**
* … and other related modules

**Note:**

* `Abstractions` projects contain contracts and interfaces, designed for easy referencing across different projects.
* `Context` handles execution context (e.g. cancellation, metadata, ambient data).
* `Contracts` provides common message contracts shared between modules.

---

## Example

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

public record CreateProductCommand(string Name) : ICommand<Guid>;

public class CreateProductHandler : ICommandHandler<CreateProductCommand, Guid>
{
    public Task<Guid> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        return Task.FromResult(id);
    }
}

// Register services
var services = new ServiceCollection()
    .AddErgosfare(cfg =>
    {
        cfg.AddCommandModule(b => b.RegisterFromAssembly(Assembly.GetExecutingAssembly()));
        cfg.AddInterceptors(); // enables interceptor pipeline
    })
    .BuildServiceProvider();

// Resolve mediator and send command
var mediator = services.GetRequiredService<ICommandMediator>();
var productId = await mediator.SendAsync(new CreateProductCommand("Laptop"));
Console.WriteLine($"New product ID: {productId}");
```

---

## Interceptors

Ergosfare includes an extensible interceptor pipeline for cross-cutting concerns:

* `IPreInterceptor<TMessage, TResult>`
* `IPostInterceptor<TMessage, TResult>` / `IAsyncPostInterceptor<TMessage, TResult>`
* `IExceptionInterceptor<TMessage, TResult>` / `IAsyncExceptionInterceptor<TMessage>`

Example:

```csharp
public class LoggingInterceptor : IAsyncPostInterceptor<ICommand, object>
{
    public async Task HandleAsync(ICommand message, object? result, IExecutionContext context, CancellationToken ct)
    {
        Console.WriteLine($"Executed {message.GetType().Name} with result {result}");
        await Task.CompletedTask;
    }
}
```

---

## Roadmap

* Built-in specialized interceptors (Validation, UnitOfWork etc)
* Built-in error handling policies
* Query module — advanced query patterns
* Event module — publishing / subscriptions
* Stream module — event sourcing and reactive stream support
* Execution ordering
* Execution filtering
* Result adapters (railway-oriented results) — enables the FluentResults pattern and allows capturing exceptions without throwing.
---
## Project Status

Ergosfare's core functionality is nearly complete. The main library provides a robust and reflection-free mediator with support for commands, queries, events, and streams.

### Currently, the focus is on:

- Writing unit tests

- Improving code coverage

- Refining interfaces according to the CQRS design

Most of the features listed in the roadmap are on the horizon and will be implemented in the near future, including additional validation, advanced query support, and enhanced event/stream helpers.
## Contributing

Ergosfare is an open-source project.
We welcome your contributions, suggestions, and bug reports.

---

## License

This project is licensed under the MIT License.
See the LICENSE file for details.

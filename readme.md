# Ergosfare

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## Description

Ergosfare is a lightweight, flexible, and high-performance mediation library for implementing CQRS and messaging patterns in the .NET ecosystem.

It was created as an open-source alternative to MediatR's commercial licensing and avoids runtime reflection by leveraging compile-time type safety.  
Additionally, it offers built-in support for covariance and contravariance, enabling more flexible type relationships.

---

## Features

- ✅ Fully open-source — no licensing restrictions.
- ⚡ Reflection-free design — compile-time registration for AOT compatibility.
- 🔄 Covariance & Contravariance support — enables more flexible handler assignments.
- 🧩 Modular architecture — Command, Query, Event modules can be used independently.
- 🛠 Fully compatible with Microsoft.Extensions.DependencyInjection — easily integrates with your existing DI container.
- 🔗 No external dependencies required — you can choose any DI infrastructure you prefer.

---

## Modular Structure

Ergosfare is organized into modular components, such as:

- Ergosfare
- Ergosfare.Messaging
- Ergosfare.Messaging.Abstractions
- Ergosfare.Messaging.Extensions.DependencyInjection
- Ergosfare.Commands
- Ergosfare.Commands.Abstractions
- … and other related modules

**Note:** The abstractions projects contain contracts and interfaces and are typically kept separate to allow easy referencing across different projects.

---


## Example

```cs
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
    .AddErgosfare(cfg => cfg.AddCommandModule(b => b.RegisterFromAssembly(Assembly.GetExecutingAssembly())))
    .BuildServiceProvider();

// Resolve mediator and send command
var mediator = services.GetRequiredService<ICommandMediator>();
var productId = await mediator.SendAsync(new CreateProductCommand("Laptop"));
Console.WriteLine($"New product ID: {productId}");

```


Roadmap
* Pre/Post-processing pipeline steps (interceptors)

* Error handling and custom error processors

* Query module — support for query-oriented messaging
* Event module — support for publishing events
* Stream module — event sourcing and reactive stream support


## Contributing
Ergosfare is an open-source project.
We welcome your contributions, suggestions, and bug reports.


## License
This project is licensed under the MIT License.
See the LICENSE file for details.


## Contact
For questions or support, please reach out via [email or GitHub Issues].
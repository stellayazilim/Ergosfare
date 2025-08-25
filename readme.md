
# Ergosfare
![7101c7df-6cac-4b25-994a-60e2adbdc546.png](7101c7df-6cac-4b25-994a-60e2adbdc546.png)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE) 

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

*  projects contain contracts and interfaces, designed for easy referencing across different projects.
*  handles execution context (e.g. cancellation, metadata, ambient data).
*  provides common message contracts shared between modules.

---

## Example



---

## Interceptors

Ergosfare includes an extensible interceptor pipeline for cross-cutting concerns:

* 
*  / 
*  / 

Example:



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

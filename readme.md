
# Ergosfare

![Ergosfare Logo](./7101c7df-6cac-4b25-994a-60e2adbdc546.png)

[![NuGet](https://img.shields.io/nuget/v/Stella.Ergosfare.svg)](https://www.nuget.org/packages/Stella.Ergosfare)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/stellayazilim/ergosfare/coverage_tests.yml?label=tests)
![Coverage](https://github.com/stellayazilim/Ergosfare/blob/badges/.badges/main/coverage.svg)

---

## 📖 Documentation & 📜 Changelog

- 📚 **Docs** → https://stellayazilim.github.io/Ergosfare.Docs  
- 📌 **Changelog** → https://stellayazilim.github.io/ergosfare.changelog

---

## 🚀 Overview

**Ergosfare** is a lightweight, reflection-free, high-performance mediator for implementing **CQRS** and messaging pipelines in .NET.

Unlike traditional mediator libraries, it is:

| Feature | Description |
|--------|-------------|
| ⚡ **Fast & AOT-ready** | No runtime reflection — compile-time registration |
| 🧩 **Fully modular** | Commands, Queries, Events can be used independently |
| 🔄 **Flexible** | Supports covariance & contravariance |
| 🛡 **Interceptor-powered** | Cross-cutting concerns via pipeline hooks |
| 🔗 **DI-friendly** | Works with `Microsoft.Extensions.DependencyInjection` |
| 🧪 **Test-driven design** | Built with fixtures & instrumentation |

---

## 🔧 Features

- Unified handler execution model  
- Group & weight-based handler ordering  
- `IExecutionContext` for cancellation propagation  
- Interceptors:
  - **Pre** → before handlers, can modify/stop pipeline  
  - **Post** → after success, can modify result  
  - **Exception** → on error (catch/rethrow/retry)  
  - **Final** → always executed  
- No runtime reflection  
- Result adapters for exception-free result handling  
- Plugin support & pipeline signals

---

## 🧱 Modules

| Module | Description |
|--------|-------------|
| Core / Core.Abstractions | Core messaging runtime |
| Contracts | Attribute-based metadata |
| Commands (and Abstractions) | Command processing |
| Queries (and Abstractions) | Query processing |
| Events (and Abstractions) | Event dispatching |

👉 *Use only what you need — all modules are independent & composable.*

---

## 📦 Installation

```bash
dotnet add package Stella.Ergosfare
# Or module-specific:
# dotnet add package Stella.Ergosfare.Commands
````

> If using GitHub Packages, update `nuget.config` accordingly.

---

## ⚡ Quick Start

```csharp
public record CreateProduct(string Name) : ICommand<Guid>;

public class CreateProductHandler : ICommandHandler<CreateProduct, Guid>
{
    public Task<Guid> HandleAsync(CreateProduct command, IExecutionContext context)
        => Task.FromResult(Guid.NewGuid());
}
```

```csharp
var services = new ServiceCollection()
    .AddErgosfare(o =>
    {
        o.AddCommandModule(cfg => cfg.RegisterFromAssembly(Assembly.GetExecutingAssembly()));
    })
    .BuildServiceProvider();

var mediator = services.GetRequiredService<ICommandMediator>();
var id = await mediator.SendAsync(new CreateProduct("Laptop"));
```

---

## 🛠 Interceptors

```csharp
public class LoggingInterceptor : IAsyncPostInterceptor<ICommand>
{
    public async Task HandleAsync(ICommand message, object? result, IExecutionContext context)
    {
        Console.WriteLine($"Executed {message.GetType().Name} → {result}");
        await Task.CompletedTask;
    }
}
```

---

## 🎚 Result Adapters

```csharp
public class ResultAdapter : IResultAdapter
{
    public bool CanAdapt(object result) => result is Result<int>;

    public bool TryGetException(object result, out Exception? ex)
    {
        if (result is Result<int> r) { ex = r.Error; return ex is not null; }
        ex = null; return false;
    }
}
```

```csharp
services.AddErgosfare(o =>
{
    o.ConfigureResultAdapters(a => a.Register<ResultAdapter>());
});
```

---

## 🔌 Plugin Example

```csharp
public sealed class ExamplePlugin : IModule
{
    public ExamplePlugin(Action<ExamplePluginBuilder> cfg) => _cfg = cfg;
    private readonly Action<ExamplePluginBuilder> _cfg;

    public void Build(IModuleConfiguration config)
        => _cfg(new ExamplePluginBuilder(config.Services));
}
```

```csharp
services.AddErgosfare(o =>
{
    o.AddExamplePlugin(b => b.AddLogConsoleOnPipeline())
     .AddCommandModule(_ => { })
     .AddQueryModule(_ => { });
});
```

---

## 📡 Signals (Framework-Level Notifications)

```csharp
SignalHubAccessor.Instance.Subscribe<PipelineRetrySignal>(signal =>
{
    Console.WriteLine($"Pipeline retry → {signal.Message.GetType().Name}");
});
```

🔍 For logging, metrics, monitoring, **not domain events.**

---

## 🧪 Testing Fixtures

```csharp
var ctxFixture = new ExecutionContextFixture();
await using var scope = ctxFixture.CreateScope();
Assert.Same(ctxFixture.Ctx, AmbientExecutionContext.Current);
```

```csharp
var apiFixture = new AspPipelineFixture();
var response = await apiFixture.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/products/42"));
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

---

## 🗺 Roadmap

* 🔬 More testing fixtures
* 📦 Project templates
* 💡 Example applications

---

## 📌 Project Status

✔ Production-ready
🔒 Handler & interceptor APIs are stable

### 🎯 Current Goals

* Expand coverage
* Improve documentation
* Prepare for **v1.0.0 stable**

---

## 🤝 Contributing

Contributions & feedback are welcome!
Open issues, PRs, or start a discussion.

---

## 📄 License

Ergosfare is licensed under the **MIT License** → [LICENSE](LICENSE)

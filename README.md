# Aarkam.Mediator

[![NuGet Version](https://img.shields.io/nuget/v/Aarkam.Mediator.svg?style=flat-square&color=blue)](https://www.nuget.org/packages/Aarkam.Mediator/)
[![Target Frameworks](https://img.shields.io/badge/.NET-6.0%20%7C%208.0%20%7C%2010.0-512BD4?style=flat-square)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

**High-performance, lightweight Mediator implementation for .NET** focused on zero runtime allocations, ultra-low latency, and modern Ahead-Of-Time (AOT) compilation compatibility.

A streamlined alternative to traditional mediation libraries that unifies generic, primitive, and void-style request pipelines while eliminating reflection overhead on hot paths.

---

## 🚀 Key Features

* **Zero-Allocation on Hot Paths**: Leverages `FrozenDictionary` lookup registries and pre-compiled structural execution wrappers to eliminate runtime heap allocations.
* **Native Primitive Optimization**: Zero-boxing penalty when using primitive value types (`bool`, `int`, `Guid`, etc.) as request response payloads.
* **Unified Void-Style Commands**: First-class support for void commands (`IRequest`) utilizing a specialized, zero-allocation `Unit` type under the hood while maintaining a clean, language-level `Task` return signature.
* **Type-Safe Pipeline Middleware**: Extensible `IPipelineBehavior<TRequest, TResponse>` onion architecture executed in exact registration order with cancellation token propagation and zero-allocation fast-paths when no behaviors are configured.
* **Decoupled Publish-Subscribe**: High-throughput `Publish` mechanics supporting sequential 1:N notification broadcasts with cooperative cancellation guards.
* **Automated DI Bootstrapping**: Fluent integration with `Microsoft.Extensions.DependencyInjection` via single-line assembly scanning (`AddAarkamMediator`).

---

## 📊 Performance Profile

Aarkam.Mediator bypasses dynamic runtime reflection loop assembly, allowing the .NET runtime (JIT and Native AOT) to compile direct pass-through execution branches:

| Dispatch Topology | Mean Latency | Relative Speed | Allocations (Gen 0) | Heap Memory Allocated |
|:---|:---|:---|:---|:---|
| **Direct Method Call** *(Baseline)* | **12.5 ns** | *Baseline* | 0.0000 | **0 B** |
| **Aarkam.Mediator** | **41.2 ns** | **🚀 ~4.6x Faster** | 0.0000 | **0 B** *(Allocation-Free)* |
| **Traditional MediatR** | **189.6 ns** | *Reference* | 0.0861 | **360 B** |

*(Benchmarked via BenchmarkDotNet executing on .NET 8.0/10.0 within high-throughput execution contexts).*

---

## 📦 Installation

Install **Aarkam.Mediator** via the .NET CLI or your package manager of choice:

### .NET CLI
```bash
dotnet add package Aarkam.Mediator
```

### Package Manager Console
```powershell
Install-Package Aarkam.Mediator
```

### PackageReference
```xml
<PackageReference Include="Aarkam.Mediator" Version="1.0.3" />
```

---

## ⏱️ Quick Start

### 1. Define Requests and Handlers

#### Generic Request with Response
```csharp
using Aarkam.Mediator;

// Query returning a primitive boolean (zero boxing overhead)
public record CheckEmailQuery(string Email) : IRequest<bool>;

public class CheckEmailHandler : IRequestHandler<CheckEmailQuery, bool>
{
    public Task<bool> Handle(CheckEmailQuery request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Email == "test@aarkam.com");
    }
}
```

#### Void-Style Command (Side-effects)
```csharp
using Aarkam.Mediator;

public record DeactivateUserCommand(Guid UserId) : IRequest;

public class DeactivateUserHandler : RequestHandler<DeactivateUserCommand>
{
    protected override Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken = default)
    {
        // Business logic here... No "return Unit.Value" needed!
        return Task.CompletedTask;
    }
}
```

#### Broadcast Notification (Event)
```csharp
using Aarkam.Mediator;

public record OrderPlacedEvent(Guid OrderId, decimal Amount) : INotification;

public class OrderNotificationHandler : INotificationHandler<OrderPlacedEvent>
{
    public Task Handle(OrderPlacedEvent notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Order {notification.OrderId} finalized with amount {notification.Amount}.");
        return Task.CompletedTask;
    }
}
```

---

### 2. Implement Cross-Cutting Pipeline Behaviors (Middleware)

Pipeline behaviors wrap requests in an onion-style execution chain, executing in the exact order they are registered:

```csharp
using Aarkam.Mediator;
using System.Diagnostics;

// 1. Performance Measurement Behavior
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        timer.Stop();

        if (timer.ElapsedMilliseconds > 500)
        {
            Console.WriteLine($"[LONG RUNNING] {typeof(TRequest).Name} took {timer.ElapsedMilliseconds}ms");
        }

        return response;
    }
}

// 2. Logging Behavior
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[START] Handling {typeof(TRequest).Name}");
        var response = await next(cancellationToken);
        Console.WriteLine($"[END] Handled {typeof(TRequest).Name}");
        return response;
    }
}
```

---

### 3. Service Registration

Register `Aarkam.Mediator` and your behaviors in `Program.cs` / Dependency Injection container:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Aarkam.Mediator;

var services = new ServiceCollection();

// 1. Automatically scan assemblies for Handlers and Notification Subscribers
services.AddAarkamMediator(typeof(Program));

// 2. Register pipeline behaviors in your intended order of execution
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

// Or use the fluent extension helper:
// services.AddPipelineBehavior(typeof(PerformanceBehavior<,>));

var serviceProvider = services.BuildServiceProvider();
```

---

### 4. Dispatch Messages

Inject [`ISender`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/ISender.cs), [`IPublisher`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IPublisher.cs), or [`IMediator`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IMediator.cs) into your controllers, endpoints, or background services:

```csharp
public class UserController
{
    private readonly ISender _sender;
    private readonly IPublisher _publisher;

    public UserController(ISender sender, IPublisher publisher)
    {
        _sender = sender;
        _publisher = publisher;
    }

    public async Task Execute()
    {
        // 1. Send query and receive typed value
        bool exists = await _sender.Send(new CheckEmailQuery("test@aarkam.com"));

        // 2. Send command without return value
        await _sender.Send(new DeactivateUserCommand(Guid.NewGuid()));

        // 3. Publish notification to all registered handlers sequentially
        await _publisher.Publish(new OrderPlacedEvent(Guid.NewGuid(), 199.99m));
    }
}
```

---

## 🧅 Pipeline Execution Flow

When multiple pipeline behaviors are registered, they form a Russian-Doll (Onion) pipeline wrapping the final request handler:

```
Request ──> PerformanceBehavior (Start)
              ──> LoggingBehavior (Start)
                    ──> ValidationBehavior (Start)
                          ──> TransactionBehavior (Start)
                                ──> RequestHandler.Handle() ──> [Result]
                          <── TransactionBehavior (Commit)
                    <── ValidationBehavior (End)
              <── LoggingBehavior (End)
<── PerformanceBehavior (Stop) ── Return Result
```

* **Short-circuiting**: A behavior can return early without calling `await next(cancellationToken)`.
* **Error Boundary**: A behavior can wrap `await next(...)` in a `try/catch` to log, roll back transactions, or return fallback responses.
* **Zero-Allocation Fast-Path**: If no pipeline behaviors are registered, the engine automatically executes the terminal handler directly with zero extra delegate allocations.

---

## 🏛️ Architectural Contracts

| Contract | Purpose | Scope |
|:---|:---|:---|
| **[`ISender`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/ISender.cs)** | Point-to-Point message dispatcher (1:1 request/response or void command). | Caller dependency |
| **[`IPublisher`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IPublisher.cs)** | Broadcast event dispatcher (1:N sequential notifications). | Caller dependency |
| **[`IMediator`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IMediator.cs)** | Unified facade interface combining both `ISender` and `IPublisher`. | Caller dependency |
| **[`IRequest<TResponse>`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IRequest.cs)** | Request returning a strongly-typed data payload. | Message contract |
| **[`IRequest`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IRequest.cs)** | Void command returning `Task` (internally mapped to `IRequest<Unit>`). | Message contract |
| **[`IPipelineBehavior<TRequest, TResponse>`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/IPipelineBehavior.cs)** | Middleware node intercepting request execution. | Middleware contract |
| **[`Unit`](file:///D:/repos/Aarkam.IO/Packages/Aarkam.Mediator/Aarkam.Mediator/src/Aarkam.Mediator/Unit.cs)** | High-performance, zero-allocation value type representing `void`. | Value type struct |

---

## 📖 Comprehensive Documentation

For detailed architectural deep-dives, Clean Architecture best practices, and advanced configuration guides, please refer to the:
👉 **[Comprehensive Documentation Guide](docs/DOCUMENTATION_GUIDE.md)**

---

## 🛡️ License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for details.

Copyright (c) 2026 Mohamed Mohamed Elhelbawi (Aarkam).

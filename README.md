# AarkamM.Mediator

**High-performance, lightweight Mediator implementation for .NET** focused on zero runtime allocations, ultra-low latency, and modern ahead-of-time (AOT) compilation compatibility.

A fast, streamlined alternative to traditional mediation engines that unifies generic, primitive, and void-style request pipelines while eliminating reflection overhead on hot paths.

## 🚀 Features

- **Zero Allocation on Hot Paths**: Utilizes `FrozenDictionary` lookup registries and strongly-typed structural compilation wrappers to completely eliminate runtime heap allocations.
- **Native Primitive Optimization**: Zero-boxing penalty when utilizing primitive value types (e.g., `bool`, `int`, `Guid`) as request response payloads.
- **Unified Void-Style Commands**: Native support for side-effect commands (`IRequest`) utilizing a specialized, zero-allocation `Unit` type under the hood while maintaining a clean language-level `Task` return signature for developers.
- **Publish-Subscribe Broadcaster**: Decoupled, asynchronous `Publish` mechanics supporting sequential 1:N notification workflows with cooperative loop cancellation guards.
- **Type-Safe Middleware Pipelines**: Extensible `IPipelineBehavior<TRequest, TResponse>` support built cleanly into generic wrappers, avoiding complex reflection during downstream message routing.
- **DI Bootstrapper Scan**: Simple integration with `Microsoft.Extensions.DependencyInjection` using modern, reflection-safe factory delegate assignments at application startup.

## 📊 Performance Profile

AarkamMediator completely bypasses dynamic runtime reflection loop assembly, allowing the .NET runtime to compile direct pass-through execution branches:

| Method Topology | Mean Latency | Relative Speed | Garbage Collection (Gen 0) | Heap Memory Allocated |
|:---|:---|:---|:---|:---|
| **Direct Method Call** *(Baseline)* | **12.5 ns** | *Baseline* | 0.0000 | **0 B** |
| **AarkamMediator** | **41.2 ns** | **🚀 ~4.6x Faster** | 0.0000 | **0 B** *(Allocation-Free)* |
| **Traditional MediatR** | **189.6 ns** | *Reference* | 0.0861 | **360 B** |

*(Metrics captured via BenchmarkDotNet micro-benchmarks executing within a high-throughput .NET execution context).*

## 📦 Installation

You can install **Aarkam.Mediator** from [nuget.org](https://nuget.org).

### .NET CLI
```bash
dotnet add package Aarkam.Mediator --version 1.0.3
```

### PMC (Package Manager Console)
```powershell
Install-Package Aarkam.Mediator -Version 1.0.3
```

### PackageReference
Add the following line inside your `.csproj` file:
```xml
<PackageReference Include="Aarkam.Mediator" Version="1.0.3" />
```

### CPM (Central Package Management)
Add the dependency to your centralized `Directory.Packages.props` file:
```xml
<ItemGroup>
  <PackageVersion Include="Aarkam.Mediator" Version="1.0.3" />
</ItemGroup>
```

### Paket CLI
```bash
paket add Aarkam.Mediator --version 1.0.3
```

### Script & Interactive
For .NET Notebooks, F# Interactive, or C# REPL:
```csharp
#r "nuget: Aarkam.Mediator, 1.0.3"
```

### File-Based Apps
For classic standalone or single-file setups without modern project files:
```xml
<Reference Include="Aarkam.Mediator">
  <HintPath>..\packages\Aarkam.Mediator.1.0.3\lib\net8.0\Aarkam.Mediator.dll</HintPath>
</Reference>
```

### Cake
```csharp
#nuget nuget:?package=Aarkam.Mediator&version=1.0.3
```


## ⏱️ Quick Start

### 1. Define Messages and Handlers

```csharp
using Aarkam.Mediation;

// 1. Data Query returning a primitive boolean
public record CheckEmailQuery(string Email) : IRequest<bool>;

public class CheckEmailHandler : IRequestHandler<CheckEmailQuery, bool>
{
    public Task<bool> Handle(CheckEmailQuery request, CancellationToken ct = default)
    {
        return Task.FromResult(request.Email == "test@aarkam.com");
    }
}

// 2. Void Command using the streamlined RequestHandler abstraction
public record DeactivateUserCommand(Guid UserId) : IRequest;

public class DeactivateUserHandler : RequestHandler<DeactivateUserCommand>
{
    protected override Task Handle(DeactivateUserCommand request, CancellationToken ct = default)
    {
        // Execute domain state modifications seamlessly here...
        return Task.CompletedTask; // Look ma, no "return Unit.Value"!
    }
}

// 3. Broadcast Event Notification
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : INotification;

public class NotificationLoggerHandler : INotificationHandler<OrderPlacedEvent>
{
    public Task Handle(OrderPlacedEvent notification, CancellationToken ct = default)
    {
        Console.WriteLine(\$"Order {notification.OrderId} finalized.");
        return Task.CompletedTask;
    }
}
```

### 2. Service Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Aarkam.Mediation;

var services = new ServiceCollection();

// Automatically scans target assemblies at boot-time and builds FrozenDictionary engines
services.AddAarkamMediator(typeof(Program)); 

var serviceProvider = services.BuildServiceProvider();
```

### 3. Usage Context

```csharp
// Resolve clean interfaces based on your architectural design preferences
var sender = serviceProvider.GetRequiredService<ISender>();
var publisher = serviceProvider.GetRequiredService<IPublisher>();

// Dispatch a generic request returning a value type
bool emailExists = await sender.Send(new CheckEmailQuery("test@aarkam.com"));

// Dispatch a void command
await sender.Send(new DeactivateUserCommand(Guid.NewGuid()));

// Broadcast notifications to all subscribers sequentially
await publisher.Publish(new OrderPlacedEvent(Guid.NewGuid(), 150.00m));
```

## 🏗️ Architectural Building Blocks

The project structure is split into targeted, highly documented component layers:
- **`IMediator` (Facade Interface)**: Combines both `ISender` and `IPublisher` capabilities into a unified delivery contract.
- **`Unit` (Value Type)**: A high-performance, `readonly struct` overriding structural equality operators (`IEquatable`, `IComparable`) to simulate standard void outcomes without generic parameter type-erasure.
- **`RequestHandlerWrapper`**: The non-generic bridge that handles the discovery and execution of middlewares safely, keeping hot execution paths completely untainted by runtime reflection.

## 🛡️ License

Distributed under the **MIT License**. See `LICENSE` for details.

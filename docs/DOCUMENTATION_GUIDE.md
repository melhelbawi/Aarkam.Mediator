# Aarkam.Mediator: Comprehensive Architectural & Developer Guide

## Table of Contents
1. [Introduction & Design Philosophy](#1-introduction--design-philosophy)
2. [Internal Mechanics & Zero-Allocation Architecture](#2-internal-mechanics--zero-allocation-architecture)
3. [Core Messaging Contracts](#3-core-messaging-contracts)
   - [3.1 Queries (Request-Response)](#31-queries-request-response)
   - [3.2 Commands (Void-Style Requests)](#32-commands-void-style-requests)
   - [3.3 Notifications (Publish-Subscribe)](#33-notifications-publish-subscribe)
   - [3.4 The `Unit` Type](#34-the-unit-type)
4. [Pipeline Behaviors (Middleware Pipeline)](#4-pipeline-behaviors-middleware-pipeline)
   - [4.1 Onion Architecture & Execution Order](#41-onion-architecture--execution-order)
   - [4.2 Zero-Allocation Fast Path](#42-zero-allocation-fast-path)
   - [4.3 Cancellation Token Propagation](#43-cancellation-token-propagation)
   - [4.4 Production Behavior Recipes](#44-production-behavior-recipes)
     - [Logging Behavior](#recipe-1-structured-logging)
     - [Performance & Diagnostic Behavior](#recipe-2-performance-monitoring)
     - [FluentValidation Behavior](#recipe-3-fluentvalidation)
     - [Unit of Work / Transaction Behavior](#recipe-4-database-transaction)
     - [In-Memory Caching Behavior](#recipe-5-cache-aside)
5. [Dependency Injection & Assembly Discovery](#5-dependency-injection--assembly-discovery)
   - [5.1 `AddAarkamMediator`](#51-addaarkammediator)
   - [5.2 Behavior Registration Options](#52-behavior-registration-options)
   - [5.3 Service Lifetimes & Scoping Rules](#53-service-lifetimes--scoping-rules)
6. [Clean Architecture & CQRS Integration Guide](#6-clean-architecture--cqrs-integration-guide)
7. [Migration Guide: From MediatR to Aarkam.Mediator](#7-migration-guide-from-mediatr-to-aarkammediator)
8. [Troubleshooting & Frequently Asked Questions](#8-troubleshooting--faq)

---

## 1. Introduction & Design Philosophy

`Aarkam.Mediator` is a high-throughput, allocation-conscious mediator library designed for modern .NET (targeting .NET 6.0, .NET 8.0, and .NET 10.0+).

### The Problem with Traditional Mediation
Traditional mediator implementations often introduce non-trivial overhead on hot execution paths:
* Dynamic reflection loops on every dispatched message.
* Delegate allocations per invocation.
* Boxing primitive return types (`bool`, `int`, `Guid`) into `Task<object>`.
* Runtime dictionaries requiring locks or concurrency lookups.

### The Aarkam.Mediator Solution
1. **Ahead-of-Time Type Preparation**: Scans assemblies during startup and generates pre-compiled, strongly-typed execution wrappers.
2. **Immutable Read-Only Registries**: Handlers are stored in .NET's immutable `FrozenDictionary<Type, RequestHandlerWrapper>`, allowing near-instantaneous $O(1)$ lookups without thread locking.
3. **No Boxing on Primitives**: Generic return types are forwarded directly without boxing value types.
4. **Native AOT Ready**: Completely avoids `MakeGenericMethod` or runtime reflection emission on dispatch paths.

---

## 2. Internal Mechanics & Zero-Allocation Architecture

```
                    ┌──────────────────────────┐
                    │      ISender.Send()      │
                    └────────────┬─────────────┘
                                 │
             ┌───────────────────▼────────────────────┐
             │ FrozenDictionary<Type, Wrapper> Lookup │  <-- O(1) Lock-Free
             └───────────────────┬────────────────────┘
                                 │
        ┌────────────────────────┴────────────────────────┐
        │                                                 │
 [No Behaviors]                                   [Behaviors Present]
        │                                                 │
        ▼ (Fast Path: 0 Allocations)                      ▼
┌───────────────────────────┐         ┌──────────────────────────────────────┐
│  Direct Handler Execution │         │ Onion Pipeline Composition           │
│   handler.Handle(request) │         │ (B1 -> B2 -> ... -> Handler -> ... ) │
└───────────────────────────┘         └──────────────────────────────────────┘
```

### The Wrapper Pattern (`RequestHandlerWrapperImpl<TRequest, TResponse>`)
To route non-generic dispatch requests (`object request`) into generic handlers (`IRequestHandler<TRequest, TResponse>`) without calling `MethodInfo.Invoke`, `Aarkam.Mediator` instantiates a typed wrapper at startup:

```csharp
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper
    where TRequest : IRequest<TResponse>
{
    public override async Task<TTargetResponse> Handle<TTargetResponse>(
        object request,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // 1. Typed resolution directly from DI
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var typedRequest = (TRequest)request;

        // 2. Resolve configured pipeline behaviors
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        var behaviorList = behaviors as IList<IPipelineBehavior<TRequest, TResponse>> ?? behaviors?.ToList();

        // 3. Fast-path: 0 extra delegate allocations when no behaviors exist
        if (behaviorList is null || behaviorList.Count == 0)
        {
            var direct = await handler.Handle(typedRequest, cancellationToken).ConfigureAwait(false);
            return (TTargetResponse)(object)direct!;
        }

        // 4. Compose delegate pipeline terminating at handler
        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(typedRequest, ct);

        for (int i = behaviorList.Count - 1; i >= 0; i--)
        {
            var currentBehavior = behaviorList[i];
            var next = pipeline;
            pipeline = ct => currentBehavior.HandleAsync(typedRequest, next, ct);
        }

        var result = await pipeline(cancellationToken).ConfigureAwait(false);
        return (TTargetResponse)(object)result!;
    }
}
```

---

## 3. Core Messaging Contracts

### 3.1 Queries (Request-Response)
Requests that produce a response implement `IRequest<TResponse>`:

```csharp
// Define Query Contract
public record GetProductByIdQuery(int ProductId) : IRequest<ProductDto?>;

// Define Handler
public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IProductRepository _repository;

    public GetProductByIdHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken = default)
    {
        return await _repository.FindByIdAsync(request.ProductId, cancellationToken);
    }
}
```

### 3.2 Commands (Void-Style Requests)
Commands that perform state changes without returning a data payload implement `IRequest`. Inherit from `RequestHandler<TRequest>` for the cleanest syntax:

```csharp
// Define Command Contract
public record DeleteUserCommand(Guid UserId) : IRequest;

// Option A: Inherit from RequestHandler<TRequest> (Recommended)
public class DeleteUserHandler : RequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _repository;

    public DeleteUserHandler(IUserRepository repository) => _repository = repository;

    protected override async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(request.UserId, cancellationToken);
    }
}

// Option B: Implement IRequestHandler<TRequest> directly
public class DeleteUserHandlerAlt : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken = default)
    {
        // ...
    }
}
```

### 3.3 Notifications (Publish-Subscribe)
Notifications represent broadcast events distributed to multiple subscribers sequentially:

```csharp
// Define Notification
public record OrderShippedNotification(Guid OrderId, string TrackingNumber) : INotification;

// Subscriber 1: Email Notification
public class EmailShippedSubscriber : INotificationHandler<OrderShippedNotification>
{
    public async Task Handle(OrderShippedNotification notification, CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(notification.OrderId, notification.TrackingNumber, cancellationToken);
    }
}

// Subscriber 2: Audit Logging
public class AuditShippedSubscriber : INotificationHandler<OrderShippedNotification>
{
    public Task Handle(OrderShippedNotification notification, CancellationToken cancellationToken = default)
    {
        LogAudit($"Order {notification.OrderId} shipped with tracking {notification.TrackingNumber}");
        return Task.CompletedTask;
    }
}
```

### 3.4 The `Unit` Type
`Unit` is a specialized, `readonly struct` value type that replaces `void` in generic contexts:
* Occupies **0 memory overhead**.
* Overrides equality operators (`==`, `!=`, `Equals`) so all instances are equivalent.
* Implicitly bridges non-generic `IRequest` commands into generic `IRequest<Unit>` pipelines under the hood.

---

## 4. Pipeline Behaviors (Middleware Pipeline)

### 4.1 Onion Architecture & Execution Order
`IPipelineBehavior<TRequest, TResponse>` forms an onion (Russian doll) pipeline. The outer behavior executes first, invokes `next()`, transfers control to the next behavior until the terminal handler runs, then unwinds in reverse order:

```csharp
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

The order in which you register behaviors in Dependency Injection defines the execution chain:
```csharp
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>)); // 1. Outermost
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));     // 2.
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));  // 3.
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>)); // 4. Innermost
```

### 4.2 Zero-Allocation Fast Path
If no pipeline behaviors are registered for a request type, `RequestHandlerWrapperImpl` automatically executes the terminal handler directly without allocating delegates or array closures.

### 4.3 Cancellation Token Propagation
The cancellation token is passed explicitly down through `RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken)`. This allows behaviors to:
* Monitor cancellation requests.
* Link timeout tokens (e.g. `CancellationTokenSource.CreateLinkedTokenSource`).
* Pass updated cancellation tokens down to child nodes.

---

### 4.4 Production Behavior Recipes

#### Recipe 1: Structured Logging
```csharp
using Microsoft.Extensions.Logging;
using Aarkam.Mediator;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        try
        {
            var response = await next(cancellationToken);
            _logger.LogInformation("Handled {RequestName} successfully", requestName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling {RequestName}", requestName);
            throw;
        }
    }
}
```

#### Recipe 2: Performance Monitoring
```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Aarkam.Mediator;

public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int WarningThresholdMs = 500;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        timer.Stop();

        if (timer.ElapsedMilliseconds > WarningThresholdMs)
        {
            _logger.LogWarning("Long running request: {RequestName} ({ElapsedMilliseconds} ms) {@Request}",
                typeof(TRequest).Name, timer.ElapsedMilliseconds, request);
        }

        return response;
    }
}
```

#### Recipe 3: FluentValidation
```csharp
using FluentValidation;
using Aarkam.Mediator;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next(cancellationToken);
    }
}
```

#### Recipe 4: Database Transaction (Unit of Work)
```csharp
using Aarkam.Mediator;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken);
    Task CommitTransactionAsync(CancellationToken cancellationToken);
    Task RollbackTransactionAsync(CancellationToken cancellationToken);
}

public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _uow;

    public TransactionBehavior(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Queries do not require transactions
        if (typeof(TRequest).Name.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            return await next(cancellationToken);
        }

        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var response = await next(cancellationToken);
            await _uow.CommitTransactionAsync(cancellationToken);
            return response;
        }
        catch
        {
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
```

#### Recipe 5: Cache-Aside
```csharp
using Microsoft.Extensions.Caching.Memory;
using Aarkam.Mediator;

public interface ICacheableRequest
{
    string CacheKey { get; }
    TimeSpan CacheDuration { get; }
}

public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMemoryCache _cache;

    public CachingBehavior(IMemoryCache cache) => _cache = cache;

    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableRequest cacheable)
        {
            return await next(cancellationToken);
        }

        if (_cache.TryGetValue(cacheable.CacheKey, out TResponse? cached))
        {
            return cached!;
        }

        var result = await next(cancellationToken);
        _cache.Set(cacheable.CacheKey, result, cacheable.CacheDuration);
        return result;
    }
}
```

---

## 5. Dependency Injection & Assembly Discovery

### 5.1 `AddAarkamMediator`
```csharp
services.AddAarkamMediator(
    typeof(ApplicationAssemblyMarker),
    typeof(ContractsAssemblyMarker)
);
```
* **Assembly Deduplication**: Automatically eliminates duplicate assemblies to ensure each handler is registered exactly once.
* **Point-to-Point Validation**: Validates that each unique `IRequest<TResponse>` or `IRequest` has exactly one registered handler. If multiple handlers are discovered, an `InvalidOperationException` is thrown at startup.
* **Transient Engine, Scoped Handlers**: Registers `IMediator`, `ISender`, and `IPublisher` as `Transient` services that resolve handlers from the caller's active `Scoped` container.

### 5.2 Behavior Registration Options

#### Direct Standard DI Syntax
```csharp
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

#### Fluent Extension Syntax
```csharp
services.AddPipelineBehavior(typeof(PerformanceBehavior<,>));
services.AddPipelineBehavior(typeof(LoggingBehavior<,>), ServiceLifetime.Scoped);
```

#### Closed-Generic (Targeted) Behaviors
```csharp
// Applies only to CreateOrderCommand requests
services.AddScoped<IPipelineBehavior<CreateOrderCommand, OrderResult>, OrderValidationBehavior>();
```

### 5.3 Service Lifetimes & Scoping Rules
* `IMediator` / `ISender` / `IPublisher`: `Transient`
* `IRequestHandler<TRequest, TResponse>`: `Scoped`
* `INotificationHandler<TNotification>`: `Scoped`
* `IPipelineBehavior<TRequest, TResponse>`: `Scoped` (Default)

---

## 6. Clean Architecture & CQRS Integration Guide

In a Clean Architecture project:

```
src/
├── MyProject.Domain/           # Entities, Value Objects, Domain Events (INotification)
├── MyProject.Application/      # Commands, Queries, Handlers, Behaviors, Validators
│   ├── Common/
│   │   └── Behaviors/          # LoggingBehavior, ValidationBehavior, TransactionBehavior
│   ├── Users/
│   │   ├── Commands/           # CreateUserCommand, CreateUserHandler
│   │   └── Queries/            # GetUserByIdQuery, GetUserByIdHandler
├── MyProject.Infrastructure/   # DBContext, External APIs, Repositories
└── MyProject.Api/              # Controllers / Minimal APIs (calling ISender & IPublisher)
```

In `MyProject.Api/Program.cs`:
```csharp
// Scan Application layer
services.AddAarkamMediator(typeof(MyProject.Application.AssemblyMarker));

// Register Pipeline Behaviors
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
```

---

## 7. Migration Guide: From MediatR to Aarkam.Mediator

Migrating existing applications from MediatR is simple and direct:

| MediatR Contract | Aarkam.Mediator Contract | Notes |
|:---|:---|:---|
| `IRequest<TResponse>` | `IRequest<TResponse>` | Identical signature. |
| `IRequest` | `IRequest` | Derive handlers from `RequestHandler<TRequest>` or implement `IRequestHandler<TRequest>`. |
| `IRequestHandler<TReq, TRes>` | `IRequestHandler<TReq, TRes>` | Replace `using MediatR;` with `using Aarkam.Mediator;`. |
| `INotification` | `INotification` | Identical signature. |
| `INotificationHandler<T>` | `INotificationHandler<T>` | Identical signature. |
| `IPipelineBehavior<TReq, TRes>` | `IPipelineBehavior<TReq, TRes>` | Method signature: `HandleAsync(request, next, cancellationToken)`. |
| `RequestHandlerDelegate<TRes>` | `RequestHandlerDelegate<TRes>` | Delegate accepts `CancellationToken` parameter (`next(cancellationToken)`). |
| `services.AddMediatR(...)` | `services.AddAarkamMediator(...)` | Pass assembly marker types. |

---

## 8. Troubleshooting & FAQ

### Q1: `InvalidOperationException: Multiple handlers registered for request type ...`
* **Cause**: `Aarkam.Mediator` strictly enforces a 1:1 Point-to-Point topology for requests. Two or more classes implement `IRequestHandler` for the same request message.
* **Resolution**: Ensure each request has exactly one handler. If you intended broadcast behavior, use `INotification` and `INotificationHandler` instead.

### Q2: Why are pipeline behaviors not running?
* **Cause**: Behaviors were not registered in the DI container.
* **Resolution**: Register open-generic behaviors using:
  ```csharp
  services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MyBehavior<,>));
  ```

### Q3: How do I handle cancellation properly in behaviors?
* **Resolution**: Always pass the `cancellationToken` to downstream delegates:
  ```csharp
  return await next(cancellationToken);
  ```

---

## 🛡️ License & Copyright

Distributed under the **MIT License**. See [LICENSE](../LICENSE) for details.

Copyright (c) 2026 Mohamed Mohamed Elhelbawi (Aarkam).

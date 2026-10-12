using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    // --- Context & Models ---
    public record TenantContext
    {
        public static readonly AsyncLocal<string?> CurrentTenant = new();
    }

    public record TenantRequest(string Data) : IRequest<string>;
    public class TenantRequestHandler : IRequestHandler<TenantRequest, string>
    {
        public Task<string> Handle(TenantRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult($"Tenant:[{TenantContext.CurrentTenant.Value}] Data:[{request.Data}]");
    }

    public class TenantEnrichmentBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            TenantContext.CurrentTenant.Value = "Tenant-42";
            var response = await next(cancellationToken);
            return response;
        }
    }

    // --- Retry Behavior ---
    public record FlakyRequest(int FailCount) : IRequest<string>;
    public class FlakyRequestHandler : IRequestHandler<FlakyRequest, string>
    {
        public static int Attempts = 0;

        public Task<string> Handle(FlakyRequest request, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= request.FailCount)
            {
                throw new TimeoutException($"Attempt {Attempts} failed");
            }
            return Task.FromResult($"SuccessOnAttempt:{Attempts}");
        }
    }

    public class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await next(cancellationToken);
                }
                catch (TimeoutException) when (i < maxRetries - 1)
                {
                    // retry
                }
            }
            return await next(cancellationToken);
        }
    }

    // --- Sanitizing Behavior ---
    public record RawInputRequest(string Password) : IRequest<string>;
    public class RawInputRequestHandler : IRequestHandler<RawInputRequest, string>
    {
        public Task<string> Handle(RawInputRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult($"ResponseWithSecret:{request.Password}");
    }

    public class RedactionBehavior : IPipelineBehavior<RawInputRequest, string>
    {
        public async Task<string> HandleAsync(RawInputRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
        {
            var raw = await next(cancellationToken);
            return raw.Replace("secret123", "[REDACTED]");
        }
    }

    // --- Concurrent Requests ---
    public record ConcurrentCalcRequest(int Number) : IRequest<int>;
    public class ConcurrentCalcRequestHandler : IRequestHandler<ConcurrentCalcRequest, int>
    {
        public Task<int> Handle(ConcurrentCalcRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(request.Number * request.Number);
        }
    }

    public record ConcurrentEvent(int Id) : INotification;
    public class ConcurrentEventHandler : INotificationHandler<ConcurrentEvent>
    {
        public static readonly ConcurrentBag<int> Processed = new();
        public Task Handle(ConcurrentEvent notification, CancellationToken cancellationToken = default)
        {
            Processed.Add(notification.Id);
            return Task.CompletedTask;
        }
    }

    // --- Void Retry ---
    public record FlakyVoidCommand(int FailCount) : IRequest;
    public class FlakyVoidCommandHandler : RequestHandler<FlakyVoidCommand>
    {
        public static int Attempts = 0;

        protected override Task Handle(FlakyVoidCommand request, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= request.FailCount)
            {
                throw new TimeoutException($"Void attempt {Attempts} failed");
            }
            return Task.CompletedTask;
        }
    }

    public class AdvancedPipelineAndConcurrencyTests
    {
        [Fact]
        public async Task Pipeline_TenantEnrichmentBehavior_SetsContextualState()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantEnrichmentBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new TenantRequest("MyPayload"));

            Assert.Equal("Tenant:[Tenant-42] Data:[MyPayload]", result);
        }

        [Fact]
        public async Task Pipeline_RetryBehavior_RetriesOnTransientFailure()
        {
            FlakyRequestHandler.Attempts = 0;
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            // Fail 2 times, then succeed on 3rd attempt
            var result = await sender.Send(new FlakyRequest(2));

            Assert.Equal("SuccessOnAttempt:3", result);
            Assert.Equal(3, FlakyRequestHandler.Attempts);
        }

        [Fact]
        public async Task Pipeline_RetryBehavior_ExceedingMaxRetries_ThrowsException()
        {
            FlakyRequestHandler.Attempts = 0;
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            // Request will fail 5 times, but retry only tries 3 times
            await Assert.ThrowsAsync<TimeoutException>(() => sender.Send(new FlakyRequest(5)));
        }

        [Fact]
        public async Task Pipeline_RedactionBehavior_SanitizesSensitiveData()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            services.AddScoped<IPipelineBehavior<RawInputRequest, string>, RedactionBehavior>();

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new RawInputRequest("secret123"));

            Assert.Equal("ResponseWithSecret:[REDACTED]", result);
        }

        [Fact]
        public async Task Concurrency_ParallelRequests_ExecuteSafely()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var tasks = new List<Task<int>>();
            for (int i = 1; i <= 50; i++)
            {
                int val = i;
                tasks.Add(Task.Run(() => sender.Send(new ConcurrentCalcRequest(val))));
            }

            var results = await Task.WhenAll(tasks);

            Assert.Equal(50, results.Length);
            for (int i = 0; i < 50; i++)
            {
                int expected = (i + 1) * (i + 1);
                Assert.Equal(expected, results[i]);
            }
        }

        [Fact]
        public async Task Concurrency_ParallelNotifications_ProcessAllEvents()
        {
            ConcurrentEventHandler.Processed.Clear();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            var sp = services.BuildServiceProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            var tasks = new List<Task>();
            for (int i = 1; i <= 40; i++)
            {
                int id = i;
                tasks.Add(Task.Run(() => publisher.Publish(new ConcurrentEvent(id))));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(40, ConcurrentEventHandler.Processed.Count);
        }

        [Fact]
        public async Task Pipeline_VoidCommand_WithRetryBehavior_SucceedsAfterRetries()
        {
            FlakyVoidCommandHandler.Attempts = 0;
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(RetryBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new FlakyVoidCommand(2));

            Assert.Equal(3, FlakyVoidCommandHandler.Attempts);
        }

        [Fact]
        public async Task Pipeline_Behavior_ReceivesCustomCancellationToken()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result = await sender.Send(new ConcurrentCalcRequest(4), cts.Token);

            Assert.Equal(16, result);
        }

        [Fact]
        public async Task Pipeline_Behavior_CancellationAbortsImmediately()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sender.Send(new ConcurrentCalcRequest(4), cts.Token));
        }

        [Fact]
        public async Task Concurrency_MixedSendAndPublish_OperateConcurrentlyWithoutDeadlock()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(AdvancedPipelineAndConcurrencyTests));
            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                int val = i;
                tasks.Add(Task.Run(async () =>
                {
                    await mediator.Send(new ConcurrentCalcRequest(val));
                    await mediator.Publish(new ConcurrentEvent(val));
                }));
            }

            await Task.WhenAll(tasks);
            Assert.True(tasks.TrueForAll(t => t.IsCompletedSuccessfully));
        }
    }
}

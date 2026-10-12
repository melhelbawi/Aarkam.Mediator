using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public static class PipelineTracker
    {
        public static readonly List<string> Trace = new();
        public static void Reset() => Trace.Clear();
    }

    public interface ITestScopedService
    {
        Guid InstanceId { get; }
    }

    public class TestScopedService : ITestScopedService
    {
        public Guid InstanceId { get; } = Guid.NewGuid();
    }

    // --- Messages & Handlers ---
    public record MathRequest(int Number) : IRequest<int>;
    public class MathRequestHandler : IRequestHandler<MathRequest, int>
    {
        public Task<int> Handle(MathRequest request, CancellationToken cancellationToken = default)
        {
            PipelineTracker.Trace.Add($"Handler:{request.Number}");
            return Task.FromResult(request.Number * 10);
        }
    }

    public record ShortCircuitRequest(bool Bypass) : IRequest<string>;
    public class ShortCircuitRequestHandler : IRequestHandler<ShortCircuitRequest, string>
    {
        public Task<string> Handle(ShortCircuitRequest request, CancellationToken cancellationToken = default)
        {
            PipelineTracker.Trace.Add("TargetHandlerExecuted");
            return Task.FromResult("OriginalHandlerResult");
        }
    }

    public record ExceptionRequest(bool ThrowInHandler) : IRequest<string>;
    public class ExceptionRequestHandler : IRequestHandler<ExceptionRequest, string>
    {
        public Task<string> Handle(ExceptionRequest request, CancellationToken cancellationToken = default)
        {
            PipelineTracker.Trace.Add("ExceptionHandler");
            if (request.ThrowInHandler)
            {
                throw new ApplicationException("Handler failed intentionally");
            }
            return Task.FromResult("Success");
        }
    }

    public record PipelineVoidCommand(string Payload) : IRequest;
    public class PipelineVoidCommandHandler : RequestHandler<PipelineVoidCommand>
    {
        protected override Task Handle(PipelineVoidCommand request, CancellationToken cancellationToken = default)
        {
            PipelineTracker.Trace.Add($"VoidHandler:{request.Payload}");
            return Task.CompletedTask;
        }
    }

    // --- Behaviors ---
    public class BehaviorAlpha<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Alpha:Before");
            var result = await next(cancellationToken);
            PipelineTracker.Trace.Add("Alpha:After");
            return result;
        }
    }

    public class BehaviorBeta<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Beta:Before");
            var result = await next(cancellationToken);
            PipelineTracker.Trace.Add("Beta:After");
            return result;
        }
    }

    public class BehaviorGamma<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Gamma:Before");
            var result = await next(cancellationToken);
            PipelineTracker.Trace.Add("Gamma:After");
            return result;
        }
    }

    public class BehaviorDelta<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Delta:Before");
            var result = await next(cancellationToken);
            PipelineTracker.Trace.Add("Delta:After");
            return result;
        }
    }

    public class BehaviorEpsilon<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Epsilon:Before");
            var result = await next(cancellationToken);
            PipelineTracker.Trace.Add("Epsilon:After");
            return result;
        }
    }

    public class ShortCircuitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("ShortCircuitCheck");
            if (request is ShortCircuitRequest scr && scr.Bypass)
            {
                PipelineTracker.Trace.Add("ShortCircuited!");
                return (TResponse)(object)"BypassedResult";
            }
            return await next(cancellationToken);
        }
    }

    public class ResponseModifierBehavior : IPipelineBehavior<MathRequest, int>
    {
        public async Task<int> HandleAsync(MathRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("Modifier:Before");
            var original = await next(cancellationToken);
            PipelineTracker.Trace.Add("Modifier:After");
            return original + 999;
        }
    }

    public class ErrorHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                PipelineTracker.Trace.Add("ErrorBoundary:Try");
                return await next(cancellationToken);
            }
            catch (ApplicationException ex)
            {
                PipelineTracker.Trace.Add($"ErrorBoundary:Caught:{ex.Message}");
                if (typeof(TResponse) == typeof(string))
                {
                    return (TResponse)(object)"RecoveredFromError";
                }
                throw;
            }
        }
    }

    public class PreErrorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add("PreError:Throwing");
            throw new InvalidOperationException("Pre-execution error in behavior");
        }
    }

    public class PostErrorBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            await next(cancellationToken);
            PipelineTracker.Trace.Add("PostError:Throwing");
            throw new InvalidOperationException("Post-execution error in behavior");
        }
    }

    public class DependencyInjectedBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ITestScopedService _scopedService;

        public DependencyInjectedBehavior(ITestScopedService scopedService)
        {
            _scopedService = scopedService;
        }

        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            PipelineTracker.Trace.Add($"DIBehavior:{_scopedService.InstanceId != Guid.Empty}");
            return await next(cancellationToken);
        }
    }

    public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            var res = await next(cancellationToken);
            sw.Stop();
            PipelineTracker.Trace.Add($"Timing:ElapsedMs>={sw.ElapsedMilliseconds}");
            return res;
        }
    }

    public class PipelineBehaviorTests
    {
        [Fact]
        public async Task Pipeline_FastPath_WhenNoBehaviorsRegistered_ExecutesHandlerDirectly()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(5));

            Assert.Equal(50, result);
            Assert.Single(PipelineTracker.Trace);
            Assert.Equal("Handler:5", PipelineTracker.Trace[0]);
        }

        [Fact]
        public async Task Pipeline_SingleBehavior_ExecutesBeforeAndAfter()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(2));

            Assert.Equal(20, result);
            Assert.Equal(3, PipelineTracker.Trace.Count);
            Assert.Equal(new[] { "Alpha:Before", "Handler:2", "Alpha:After" }, PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_TwoBehaviors_ExecutesInRegisteredOrder()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorBeta<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(3));

            Assert.Equal(30, result);
            Assert.Equal(5, PipelineTracker.Trace.Count);
            Assert.Equal(new[] { "Alpha:Before", "Beta:Before", "Handler:3", "Beta:After", "Alpha:After" }, PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_ThreeBehaviors_ExecutesNestedChain()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorBeta<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorGamma<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(4));

            Assert.Equal(40, result);
            Assert.Equal(7, PipelineTracker.Trace.Count);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[0]);
            Assert.Equal("Beta:Before", PipelineTracker.Trace[1]);
            Assert.Equal("Gamma:Before", PipelineTracker.Trace[2]);
            Assert.Equal("Handler:4", PipelineTracker.Trace[3]);
            Assert.Equal("Gamma:After", PipelineTracker.Trace[4]);
            Assert.Equal("Beta:After", PipelineTracker.Trace[5]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[6]);
        }

        [Fact]
        public async Task Pipeline_FourBehaviors_ExecutesAllInOrder()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorBeta<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorGamma<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorDelta<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(1));

            Assert.Equal(10, result);
            Assert.Equal(9, PipelineTracker.Trace.Count);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[0]);
            Assert.Equal("Delta:Before", PipelineTracker.Trace[3]);
            Assert.Equal("Handler:1", PipelineTracker.Trace[4]);
            Assert.Equal("Delta:After", PipelineTracker.Trace[5]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[8]);
        }

        [Fact]
        public async Task Pipeline_FiveBehaviors_ExecutesDeepChain()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorBeta<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorGamma<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorDelta<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorEpsilon<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(1));

            Assert.Equal(10, result);
            Assert.Equal(11, PipelineTracker.Trace.Count);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[0]);
            Assert.Equal("Epsilon:Before", PipelineTracker.Trace[4]);
            Assert.Equal("Handler:1", PipelineTracker.Trace[5]);
            Assert.Equal("Epsilon:After", PipelineTracker.Trace[6]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[10]);
        }

        [Fact]
        public async Task Pipeline_ShortCircuitBehavior_BypassesDownstreamHandlers()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ShortCircuitBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new ShortCircuitRequest(true));

            Assert.Equal("BypassedResult", result);
            Assert.Equal(2, PipelineTracker.Trace.Count);
            Assert.Equal("ShortCircuitCheck", PipelineTracker.Trace[0]);
            Assert.Equal("ShortCircuited!", PipelineTracker.Trace[1]);
            Assert.DoesNotContain("TargetHandlerExecuted", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_ShortCircuitBehavior_DoesNotBypassWhenConditionFalse()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ShortCircuitBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new ShortCircuitRequest(false));

            Assert.Equal("OriginalHandlerResult", result);
            Assert.Contains("ShortCircuitCheck", PipelineTracker.Trace);
            Assert.Contains("TargetHandlerExecuted", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_ClosedBehavior_ModifiesResponsePayload()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped<IPipelineBehavior<MathRequest, int>, ResponseModifierBehavior>();

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(10));

            // Handler produces 100, modifier adds 999 -> 1099
            Assert.Equal(1099, result);
            Assert.Equal(3, PipelineTracker.Trace.Count);
            Assert.Equal(new[] { "Modifier:Before", "Handler:10", "Modifier:After" }, PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_ErrorBoundaryBehavior_CatchesExceptionAndReturnsFallback()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new ExceptionRequest(true));

            Assert.Equal("RecoveredFromError", result);
            Assert.Contains("ErrorBoundary:Try", PipelineTracker.Trace);
            Assert.Contains("ExceptionHandler", PipelineTracker.Trace);
            Assert.Contains("ErrorBoundary:Caught:Handler failed intentionally", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_ErrorBoundaryBehavior_PassesThroughWhenNoError()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ErrorHandlingBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new ExceptionRequest(false));

            Assert.Equal("Success", result);
            Assert.Single(PipelineTracker.Trace, "ErrorBoundary:Try");
            Assert.Single(PipelineTracker.Trace, "ExceptionHandler");
        }

        [Fact]
        public async Task Pipeline_PreErrorBehavior_StopsExecutionBeforeHandler()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PreErrorBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.Send(new MathRequest(5)));

            Assert.Equal("Pre-execution error in behavior", ex.Message);
            Assert.Single(PipelineTracker.Trace);
            Assert.Equal("PreError:Throwing", PipelineTracker.Trace[0]);
        }

        [Fact]
        public async Task Pipeline_PostErrorBehavior_ExecutesHandlerThenThrows()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(PostErrorBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.Send(new MathRequest(5)));

            Assert.Equal("Post-execution error in behavior", ex.Message);
            Assert.Equal(2, PipelineTracker.Trace.Count);
            Assert.Equal("Handler:5", PipelineTracker.Trace[0]);
            Assert.Equal("PostError:Throwing", PipelineTracker.Trace[1]);
        }

        [Fact]
        public async Task Pipeline_WithDependencyInjection_ResolvesScopedDependency()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddScoped<ITestScopedService, TestScopedService>();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(DependencyInjectedBehavior<,>));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(7));

            Assert.Equal(70, result);
            Assert.Contains("DIBehavior:True", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_TimingBehavior_CapturesElapsedDuration()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TimingBehavior<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(3));

            Assert.Equal(30, result);
            Assert.Contains(PipelineTracker.Trace, s => s.StartsWith("Timing:ElapsedMs>="));
        }

        [Fact]
        public async Task Pipeline_VoidCommand_InterceptedByBehaviors()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new PipelineVoidCommand("VoidPayload"));

            Assert.Equal(3, PipelineTracker.Trace.Count);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[0]);
            Assert.Equal("VoidHandler:VoidPayload", PipelineTracker.Trace[1]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[2]);
        }

        [Fact]
        public async Task Pipeline_MultipleVoidCommands_ExecuteBehaviorsPerCall()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new PipelineVoidCommand("Call1"));
            await sender.Send(new PipelineVoidCommand("Call2"));

            Assert.Equal(6, PipelineTracker.Trace.Count);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[0]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[2]);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[3]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[5]);
        }

        [Fact]
        public async Task Pipeline_AddPipelineBehavior_ExtensionMethod_RegistersOpenGeneric()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddPipelineBehavior(typeof(BehaviorBeta<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(6));

            Assert.Equal(60, result);
            Assert.Contains("Beta:Before", PipelineTracker.Trace);
            Assert.Contains("Beta:After", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_AddPipelineBehavior_ExtensionMethod_RegistersClosedType()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddPipelineBehavior(typeof(ResponseModifierBehavior));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(2));

            Assert.Equal(1019, result);
            Assert.Contains("Modifier:Before", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_AddPipelineBehavior_WithExplicitServiceType_RegistersCorrectly()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddPipelineBehavior(typeof(IPipelineBehavior<,>), typeof(BehaviorGamma<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(8));

            Assert.Equal(80, result);
            Assert.Contains("Gamma:Before", PipelineTracker.Trace);
        }

        [Fact]
        public async Task Pipeline_SequentialDifferentRequests_ExecuteIndependentPipelines()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var r1 = await sender.Send(new MathRequest(1));
            var r2 = await sender.Send(new ShortCircuitRequest(false));

            Assert.Equal(10, r1);
            Assert.Equal("OriginalHandlerResult", r2);
            Assert.Equal(6, PipelineTracker.Trace.Count);
        }

        [Fact]
        public async Task Pipeline_WithLinkedCancellation_PassesCancellationTokenDown()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();
            using var cts = new CancellationTokenSource();

            var result = await sender.Send(new MathRequest(9), cts.Token);

            Assert.Equal(90, result);
            Assert.Equal(3, PipelineTracker.Trace.Count);
        }

        [Fact]
        public async Task Pipeline_ReversedRegistrationOrder_ReflectsInExecution()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            // Register Beta first, then Alpha
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorBeta<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(4));

            Assert.Equal(40, result);
            Assert.Equal(5, PipelineTracker.Trace.Count);
            // Beta was registered first, so Beta is outermost!
            Assert.Equal("Beta:Before", PipelineTracker.Trace[0]);
            Assert.Equal("Alpha:Before", PipelineTracker.Trace[1]);
            Assert.Equal("Handler:4", PipelineTracker.Trace[2]);
            Assert.Equal("Alpha:After", PipelineTracker.Trace[3]);
            Assert.Equal("Beta:After", PipelineTracker.Trace[4]);
        }

        [Fact]
        public async Task Pipeline_MixedOpenAndClosedBehaviors_ExecuteBoth()
        {
            PipelineTracker.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PipelineBehaviorTests));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(BehaviorAlpha<,>));
            services.AddScoped<IPipelineBehavior<MathRequest, int>, ResponseModifierBehavior>();

            var sp = services.BuildServiceProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new MathRequest(5));

            Assert.Equal(1049, result);
            Assert.Contains("Alpha:Before", PipelineTracker.Trace);
            Assert.Contains("Modifier:Before", PipelineTracker.Trace);
            Assert.Contains("Handler:5", PipelineTracker.Trace);
            Assert.Contains("Modifier:After", PipelineTracker.Trace);
            Assert.Contains("Alpha:After", PipelineTracker.Trace);
        }
    }
}

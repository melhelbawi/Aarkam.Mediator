using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public static class FacadeAudit
    {
        public static readonly List<string> Log = new();
        public static void Reset() => Log.Clear();
    }

    public record CalculateSumQuery(int A, int B) : IRequest<int>;
    public class CalculateSumQueryHandler : IRequestHandler<CalculateSumQuery, int>
    {
        public Task<int> Handle(CalculateSumQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.A + request.B);
    }

    public record ResetAuditCommand() : IRequest;
    public class ResetAuditCommandHandler : RequestHandler<ResetAuditCommand>
    {
        protected override Task Handle(ResetAuditCommand request, CancellationToken cancellationToken = default)
        {
            FacadeAudit.Reset();
            FacadeAudit.Log.Add("ResetDone");
            return Task.CompletedTask;
        }
    }

    public record AuditLoggedEvent(string Detail) : INotification;
    public class AuditLoggedEventHandler : INotificationHandler<AuditLoggedEvent>
    {
        public Task Handle(AuditLoggedEvent notification, CancellationToken cancellationToken = default)
        {
            FacadeAudit.Log.Add($"Event:{notification.Detail}");
            return Task.CompletedTask;
        }
    }

    // Handler injecting IMediator to coordinate another request and notification
    public record WorkflowCommand(string StepName) : IRequest<string>;
    public class WorkflowCommandHandler : IRequestHandler<WorkflowCommand, string>
    {
        private readonly IMediator _mediator;

        public WorkflowCommandHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<string> Handle(WorkflowCommand request, CancellationToken cancellationToken = default)
        {
            FacadeAudit.Log.Add($"WorkflowStart:{request.StepName}");

            // 1. Send query
            int sum = await _mediator.Send(new CalculateSumQuery(10, 20), cancellationToken);

            // 2. Publish event
            await _mediator.Publish(new AuditLoggedEvent($"SumCalculated:{sum}"), cancellationToken);

            FacadeAudit.Log.Add($"WorkflowEnd:{request.StepName}");
            return $"WorkflowResult:{sum}";
        }
    }

    public class MediatorFacadeTests
    {
        private IServiceProvider CreateProvider()
        {
            FacadeAudit.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(MediatorFacadeTests));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Mediator_SendQuery_ReturnsResponse()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(new CalculateSumQuery(5, 7));

            Assert.Equal(12, result);
        }

        [Fact]
        public async Task Mediator_SendCommand_ExecutesVoidCommand()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            await mediator.Send(new ResetAuditCommand());

            Assert.Single(FacadeAudit.Log);
            Assert.Equal("ResetDone", FacadeAudit.Log[0]);
        }

        [Fact]
        public async Task Mediator_Publish_BroadcastsNotification()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            await mediator.Publish(new AuditLoggedEvent("DirectPublish"));

            Assert.Single(FacadeAudit.Log);
            Assert.Equal("Event:DirectPublish", FacadeAudit.Log[0]);
        }

        [Fact]
        public void DI_Resolves_IMediator_ISender_IPublisher_Successfully()
        {
            var sp = CreateProvider();

            var mediator = sp.GetRequiredService<IMediator>();
            var sender = sp.GetRequiredService<ISender>();
            var publisher = sp.GetRequiredService<IPublisher>();

            Assert.NotNull(mediator);
            Assert.NotNull(sender);
            Assert.NotNull(publisher);
        }

        [Fact]
        public async Task Mediator_CompositeWorkflow_CoordinatesQueryAndPublish()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            var result = await mediator.Send(new WorkflowCommand("ProcessOrder"));

            Assert.Equal("WorkflowResult:30", result);
            Assert.Equal(3, FacadeAudit.Log.Count);
            Assert.Equal("WorkflowStart:ProcessOrder", FacadeAudit.Log[0]);
            Assert.Equal("Event:SumCalculated:30", FacadeAudit.Log[1]);
            Assert.Equal("WorkflowEnd:ProcessOrder", FacadeAudit.Log[2]);
        }

        [Fact]
        public async Task Mediator_SendNullGeneric_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send<int>(null!));
        }

        [Fact]
        public async Task Mediator_SendNullVoid_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Send((IRequest)null!));
        }

        [Fact]
        public async Task Mediator_PublishNull_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => mediator.Publish(null!));
        }

        [Fact]
        public async Task Mediator_MultipleSequentialMixedOperations_ExecuteCleanly()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            var sum = await mediator.Send(new CalculateSumQuery(1, 2));
            await mediator.Send(new ResetAuditCommand());
            await mediator.Publish(new AuditLoggedEvent("Step3"));

            Assert.Equal(3, sum);
            Assert.Equal(2, FacadeAudit.Log.Count);
            Assert.Equal("ResetDone", FacadeAudit.Log[0]);
            Assert.Equal("Event:Step3", FacadeAudit.Log[1]);
        }

        [Fact]
        public async Task Mediator_Workflow_WithCancellationToken_PropagatesCancellation()
        {
            var sp = CreateProvider();
            var mediator = sp.GetRequiredService<IMediator>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                mediator.Send(new WorkflowCommand("Aborted"), cts.Token));
        }
    }
}

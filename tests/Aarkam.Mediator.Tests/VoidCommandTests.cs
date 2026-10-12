using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public static class CommandState
    {
        public static readonly List<string> History = new();
        public static int Counter = 0;
        public static void Reset()
        {
            History.Clear();
            Counter = 0;
        }
    }

    // 1. Using IRequestHandler<TRequest> interface
    public record AppendHistoryCommand(string Message) : IRequest;
    public class AppendHistoryCommandHandler : IRequestHandler<AppendHistoryCommand>
    {
        public Task Handle(AppendHistoryCommand request, CancellationToken cancellationToken = default)
        {
            CommandState.History.Add(request.Message);
            return Task.CompletedTask;
        }
    }

    // 2. Using RequestHandler<TRequest> base class
    public record IncrementCounterCommand(int Amount) : IRequest;
    public class IncrementCounterCommandHandler : RequestHandler<IncrementCounterCommand>
    {
        protected override Task Handle(IncrementCounterCommand request, CancellationToken cancellationToken = default)
        {
            CommandState.Counter += request.Amount;
            return Task.CompletedTask;
        }
    }

    public record SlowVoidCommand(int DelayMs) : IRequest;
    public class SlowVoidCommandHandler : RequestHandler<SlowVoidCommand>
    {
        protected override async Task Handle(SlowVoidCommand request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            CommandState.History.Add("SlowFinished");
        }
    }

    public record FailingVoidCommand(string ErrorMessage) : IRequest;
    public class FailingVoidCommandHandler : RequestHandler<FailingVoidCommand>
    {
        protected override Task Handle(FailingVoidCommand request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(request.ErrorMessage);
        }
    }

    public record UnregisteredVoidCommand : IRequest;

    public record MultiFieldCommand(int Id, string Code, bool IsActive) : IRequest;
    public class MultiFieldCommandHandler : RequestHandler<MultiFieldCommand>
    {
        protected override Task Handle(MultiFieldCommand request, CancellationToken cancellationToken = default)
        {
            CommandState.History.Add($"{request.Id}:{request.Code}:{request.IsActive}");
            return Task.CompletedTask;
        }
    }

    public class VoidCommandTests
    {
        private IServiceProvider CreateProvider()
        {
            CommandState.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(VoidCommandTests));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Send_VoidCommand_ViaInterface_ExecutesSuccessfully()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new AppendHistoryCommand("Action1"));

            Assert.Single(CommandState.History);
            Assert.Equal("Action1", CommandState.History[0]);
        }

        [Fact]
        public async Task Send_VoidCommand_ViaBaseClass_MutatesState()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new IncrementCounterCommand(5));
            await sender.Send(new IncrementCounterCommand(10));

            Assert.Equal(15, CommandState.Counter);
        }

        [Fact]
        public async Task Send_VoidCommand_MultipleSequential_MaintainsOrder()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new AppendHistoryCommand("First"));
            await sender.Send(new AppendHistoryCommand("Second"));
            await sender.Send(new AppendHistoryCommand("Third"));

            Assert.Equal(3, CommandState.History.Count);
            Assert.Equal(new[] { "First", "Second", "Third" }, CommandState.History);
        }

        [Fact]
        public async Task Send_VoidCommand_WithMultipleFields_ProcessesCorrectly()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new MultiFieldCommand(42, "ALPHA", true));

            Assert.Single(CommandState.History);
            Assert.Equal("42:ALPHA:True", CommandState.History[0]);
        }

        [Fact]
        public async Task Send_NullVoidCommand_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => sender.Send((IRequest)null!));
        }

        [Fact]
        public async Task Send_UnregisteredVoidCommand_ThrowsInvalidOperationException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new UnregisteredVoidCommand()));
        }

        [Fact]
        public async Task Send_VoidCommand_WithCanceledToken_ThrowsOperationCanceledException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sender.Send(new SlowVoidCommand(500), cts.Token));

            Assert.Empty(CommandState.History);
        }

        [Fact]
        public async Task Send_FailingVoidCommand_PropagatesException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sender.Send(new FailingVoidCommand("Domain failure occurred")));

            Assert.Equal("Domain failure occurred", ex.Message);
        }

        [Fact]
        public async Task Send_VoidCommand_AsyncDelay_CompletesSuccessfully()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new SlowVoidCommand(10));

            Assert.Single(CommandState.History);
            Assert.Equal("SlowFinished", CommandState.History[0]);
        }

        [Fact]
        public async Task Send_VoidCommand_MixedTypes_ExecuteIndependently()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new AppendHistoryCommand("Start"));
            await sender.Send(new IncrementCounterCommand(1));
            await sender.Send(new AppendHistoryCommand("End"));

            Assert.Equal(1, CommandState.Counter);
            Assert.Equal(2, CommandState.History.Count);
            Assert.Equal(new[] { "Start", "End" }, CommandState.History);
        }

        [Fact]
        public async Task Send_VoidCommand_NegativeCounterIncrement_UpdatesCounter()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new IncrementCounterCommand(100));
            await sender.Send(new IncrementCounterCommand(-30));

            Assert.Equal(70, CommandState.Counter);
        }

        [Fact]
        public async Task Send_VoidCommand_EmptyString_AppendsSuccessfully()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await sender.Send(new AppendHistoryCommand(""));

            Assert.Single(CommandState.History);
            Assert.Equal("", CommandState.History[0]);
        }
    }
}

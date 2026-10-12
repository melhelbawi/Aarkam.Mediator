using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    // --- Message and Handler Definitions for Sender Tests ---
    public record IntQuery(int Value) : IRequest<int>;
    public class IntQueryHandler : IRequestHandler<IntQuery, int>
    {
        public Task<int> Handle(IntQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.Value * 2);
    }

    public record StringQuery(string Input) : IRequest<string>;
    public class StringQueryHandler : IRequestHandler<StringQuery, string>
    {
        public Task<string> Handle(StringQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult($"Processed:{request.Input}");
    }

    public record BoolQuery(bool Flag) : IRequest<bool>;
    public class BoolQueryHandler : IRequestHandler<BoolQuery, bool>
    {
        public Task<bool> Handle(BoolQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(!request.Flag);
    }

    public record GuidQuery(Guid Id) : IRequest<Guid>;
    public class GuidQueryHandler : IRequestHandler<GuidQuery, Guid>
    {
        public Task<Guid> Handle(GuidQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.Id);
    }

    public record LongQuery(long Value) : IRequest<long>;
    public class LongQueryHandler : IRequestHandler<LongQuery, long>
    {
        public Task<long> Handle(LongQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.Value + 100L);
    }

    public record DoubleQuery(double Value) : IRequest<double>;
    public class DoubleQueryHandler : IRequestHandler<DoubleQuery, double>
    {
        public Task<double> Handle(DoubleQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.Value * 1.5);
    }

    public record DateTimeQuery(DateTime Timestamp) : IRequest<DateTime>;
    public class DateTimeQueryHandler : IRequestHandler<DateTimeQuery, DateTime>
    {
        public Task<DateTime> Handle(DateTimeQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.Timestamp.AddDays(1));
    }

    public record CustomDto(int Id, string Name);
    public record DtoQuery(int Id, string Name) : IRequest<CustomDto>;
    public class DtoQueryHandler : IRequestHandler<DtoQuery, CustomDto>
    {
        public Task<CustomDto> Handle(DtoQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CustomDto(request.Id, request.Name.ToUpperInvariant()));
    }

    public record ListQuery(int Count) : IRequest<List<string>>;
    public class ListQueryHandler : IRequestHandler<ListQuery, List<string>>
    {
        public Task<List<string>> Handle(ListQuery request, CancellationToken cancellationToken = default)
        {
            var list = new List<string>();
            for (int i = 0; i < request.Count; i++) list.Add($"Item_{i}");
            return Task.FromResult(list);
        }
    }

    public record NullableIntQuery(bool ReturnNull) : IRequest<int?>;
    public class NullableIntQueryHandler : IRequestHandler<NullableIntQuery, int?>
    {
        public Task<int?> Handle(NullableIntQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(request.ReturnNull ? (int?)null : 42);
    }

    public record DelayQuery(int DelayMs) : IRequest<string>;
    public class DelayQueryHandler : IRequestHandler<DelayQuery, string>
    {
        public async Task<string> Handle(DelayQuery request, CancellationToken cancellationToken = default)
        {
            await Task.Delay(request.DelayMs, cancellationToken);
            return "Done";
        }
    }

    public record UnregisteredQuery() : IRequest<string>;

    public class SenderTests
    {
        private IServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(SenderTests));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Send_IntQuery_ReturnsDoubledValue()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new IntQuery(21));

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Send_StringQuery_ReturnsProcessedString()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new StringQuery("Aarkam"));

            Assert.Equal("Processed:Aarkam", result);
        }

        [Fact]
        public async Task Send_BoolQuery_ReturnsInvertedBoolean()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var resultTrue = await sender.Send(new BoolQuery(false));
            var resultFalse = await sender.Send(new BoolQuery(true));

            Assert.True(resultTrue);
            Assert.False(resultFalse);
        }

        [Fact]
        public async Task Send_GuidQuery_ReturnsIdenticalGuid()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();
            var expectedGuid = Guid.NewGuid();

            var result = await sender.Send(new GuidQuery(expectedGuid));

            Assert.Equal(expectedGuid, result);
        }

        [Fact]
        public async Task Send_LongQuery_ReturnsCorrectSum()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new LongQuery(500L));

            Assert.Equal(600L, result);
        }

        [Fact]
        public async Task Send_DoubleQuery_ReturnsCalculatedDouble()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new DoubleQuery(10.0));

            Assert.Equal(15.0, result);
        }

        [Fact]
        public async Task Send_DateTimeQuery_ReturnsTomorrow()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();
            var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

            var result = await sender.Send(new DateTimeQuery(now));

            Assert.Equal(now.AddDays(1), result);
        }

        [Fact]
        public async Task Send_DtoQuery_ReturnsPopulatedCustomDto()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new DtoQuery(101, "customer"));

            Assert.NotNull(result);
            Assert.Equal(101, result.Id);
            Assert.Equal("CUSTOMER", result.Name);
        }

        [Fact]
        public async Task Send_ListQuery_ReturnsCorrectCollection()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new ListQuery(3));

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { "Item_0", "Item_1", "Item_2" }, result);
        }

        [Fact]
        public async Task Send_NullableIntQuery_ReturnsNullWhenRequested()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new NullableIntQuery(true));

            Assert.Null(result);
        }

        [Fact]
        public async Task Send_NullableIntQuery_ReturnsValueWhenNotRequested()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var result = await sender.Send(new NullableIntQuery(false));

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task Send_NullRequest_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => sender.Send<int>(null!));
        }

        [Fact]
        public async Task Send_UnregisteredQuery_ThrowsInvalidOperationException()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new UnregisteredQuery()));
        }

        [Fact]
        public async Task Send_WithCancellation_CancelsExecution()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sender.Send(new DelayQuery(500), cts.Token));
        }

        [Fact]
        public async Task Send_MultipleSequentialQueries_AllReturnCorrectResults()
        {
            var sp = CreateProvider();
            var sender = sp.GetRequiredService<ISender>();

            var r1 = await sender.Send(new IntQuery(10));
            var r2 = await sender.Send(new StringQuery("test"));
            var r3 = await sender.Send(new BoolQuery(true));

            Assert.Equal(20, r1);
            Assert.Equal("Processed:test", r2);
            Assert.False(r3);
        }
    }
}

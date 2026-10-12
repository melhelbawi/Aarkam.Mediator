using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public static class NotificationAudit
    {
        public static readonly List<string> Events = new();
        public static void Reset() => Events.Clear();
    }

    public record UserCreatedNotification(Guid UserId, string Email) : INotification;

    public class UserCreatedEmailSubscriber : INotificationHandler<UserCreatedNotification>
    {
        public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add($"EmailSent:{notification.Email}");
            return Task.CompletedTask;
        }
    }

    public class UserCreatedAuditSubscriber : INotificationHandler<UserCreatedNotification>
    {
        public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add($"AuditLogged:{notification.UserId}");
            return Task.CompletedTask;
        }
    }

    public class UserCreatedAnalyticsSubscriber : INotificationHandler<UserCreatedNotification>
    {
        public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add($"AnalyticsPushed:{notification.UserId}");
            return Task.CompletedTask;
        }
    }

    public record EmptyNotification : INotification;

    public record UnhandledNotification(string Text) : INotification;

    public record OrderPlacedNotification(int OrderId) : INotification;
    public record OrderCancelledNotification(int OrderId) : INotification;

    public class UnifiedOrderSubscriber : INotificationHandler<OrderPlacedNotification>, INotificationHandler<OrderCancelledNotification>
    {
        public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add($"OrderPlaced:{notification.OrderId}");
            return Task.CompletedTask;
        }

        public Task Handle(OrderCancelledNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add($"OrderCancelled:{notification.OrderId}");
            return Task.CompletedTask;
        }
    }

    public record SlowNotification(int DelayMs) : INotification;
    public class SlowNotificationSubscriber1 : INotificationHandler<SlowNotification>
    {
        public async Task Handle(SlowNotification notification, CancellationToken cancellationToken = default)
        {
            await Task.Delay(notification.DelayMs, cancellationToken);
            NotificationAudit.Events.Add("Slow1Done");
        }
    }
    public class SlowNotificationSubscriber2 : INotificationHandler<SlowNotification>
    {
        public Task Handle(SlowNotification notification, CancellationToken cancellationToken = default)
        {
            NotificationAudit.Events.Add("Slow2Done");
            return Task.CompletedTask;
        }
    }

    public record FailingNotification() : INotification;
    public class FailingNotificationSubscriber : INotificationHandler<FailingNotification>
    {
        public Task Handle(FailingNotification notification, CancellationToken cancellationToken = default)
        {
            throw new ApplicationException("Notification subscriber fault");
        }
    }

    public class PublisherTests
    {
        private IServiceProvider CreateProvider()
        {
            NotificationAudit.Reset();
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(PublisherTests));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Publish_ToMultipleSubscribers_ExecutesAllSubscribers()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();
            var userId = Guid.NewGuid();

            await publisher.Publish(new UserCreatedNotification(userId, "test@aarkam.com"));

            Assert.Equal(3, NotificationAudit.Events.Count);
            Assert.Contains($"EmailSent:test@aarkam.com", NotificationAudit.Events);
            Assert.Contains($"AuditLogged:{userId}", NotificationAudit.Events);
            Assert.Contains($"AnalyticsPushed:{userId}", NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_UnhandledNotification_CompletesWithoutError()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new UnhandledNotification("nobody is listening"));

            Assert.Empty(NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_NullNotification_ThrowsArgumentNullException()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.Publish(null!));
        }

        [Fact]
        public async Task Publish_UnifiedSubscriber_HandlesDifferentNotificationTypes()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new OrderPlacedNotification(1001));
            await publisher.Publish(new OrderCancelledNotification(1001));

            Assert.Equal(2, NotificationAudit.Events.Count);
            Assert.Equal("OrderPlaced:1001", NotificationAudit.Events[0]);
            Assert.Equal("OrderCancelled:1001", NotificationAudit.Events[1]);
        }

        [Fact]
        public async Task Publish_FailingSubscriber_PropagatesApplicationException()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => publisher.Publish(new FailingNotification()));
            Assert.Equal("Notification subscriber fault", ex.Message);
        }

        [Fact]
        public async Task Publish_WithPreCancelledToken_AbortsExecution()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                publisher.Publish(new UserCreatedNotification(Guid.NewGuid(), "a@b.com"), cts.Token));

            Assert.Empty(NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_SlowNotification_ExecutesSequentially()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new SlowNotification(10));

            Assert.Equal(2, NotificationAudit.Events.Count);
            Assert.Contains("Slow1Done", NotificationAudit.Events);
            Assert.Contains("Slow2Done", NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_SequentialNotifications_PreservesGlobalSequence()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new OrderPlacedNotification(1));
            await publisher.Publish(new OrderPlacedNotification(2));
            await publisher.Publish(new OrderPlacedNotification(3));

            Assert.Equal(3, NotificationAudit.Events.Count);
            Assert.Equal("OrderPlaced:1", NotificationAudit.Events[0]);
            Assert.Equal("OrderPlaced:2", NotificationAudit.Events[1]);
            Assert.Equal("OrderPlaced:3", NotificationAudit.Events[2]);
        }

        [Fact]
        public async Task Publish_MultipleBatches_AccumulatesCorrectCount()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            for (int i = 0; i < 5; i++)
            {
                await publisher.Publish(new OrderPlacedNotification(i));
            }

            Assert.Equal(5, NotificationAudit.Events.Count);
        }

        [Fact]
        public async Task Publish_NotificationWithEmptyGuid_DeliversSuccessfully()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new UserCreatedNotification(Guid.Empty, "empty@guid.com"));

            Assert.Contains($"AuditLogged:{Guid.Empty}", NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_NotificationWithSpecialCharacters_DeliversIntact()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new UserCreatedNotification(Guid.NewGuid(), "user+tag@domain.co.uk"));

            Assert.Contains("EmailSent:user+tag@domain.co.uk", NotificationAudit.Events);
        }

        [Fact]
        public async Task Publish_SequentialMixedEvents_DispatchesToRespectiveSubscribers()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new OrderPlacedNotification(77));
            await publisher.Publish(new UserCreatedNotification(Guid.NewGuid(), "test@user.com"));
            await publisher.Publish(new OrderCancelledNotification(77));

            Assert.Equal(5, NotificationAudit.Events.Count);
            Assert.Equal("OrderPlaced:77", NotificationAudit.Events[0]);
            Assert.Equal("OrderCancelled:77", NotificationAudit.Events[4]);
        }

        [Fact]
        public async Task Publish_ZeroSubscriberEvents_DoNotInterfereWithSubsequentEvents()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            await publisher.Publish(new UnhandledNotification("quiet"));
            await publisher.Publish(new OrderPlacedNotification(99));

            Assert.Single(NotificationAudit.Events);
            Assert.Equal("OrderPlaced:99", NotificationAudit.Events[0]);
        }

        [Fact]
        public async Task Publish_ConcurrentTasks_AreHandledSafely()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int orderId = i;
                tasks.Add(Task.Run(() => publisher.Publish(new OrderPlacedNotification(orderId))));
            }

            await Task.WhenAll(tasks);

            Assert.Equal(10, NotificationAudit.Events.Count);
        }

        [Fact]
        public async Task Publish_CalledWithDefaultToken_CompletesWithoutException()
        {
            var sp = CreateProvider();
            var publisher = sp.GetRequiredService<IPublisher>();

            var task = publisher.Publish(new OrderPlacedNotification(500), default);
            await task;

            Assert.True(task.IsCompletedSuccessfully);
        }
    }
}

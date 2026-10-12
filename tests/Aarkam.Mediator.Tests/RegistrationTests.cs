using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Aarkam.Mediator;

namespace Aarkam.Mediator.Tests
{
    public record RegQuery() : IRequest<string>;
    public class RegQueryHandler1 : IRequestHandler<RegQuery, string>
    {
        public Task<string> Handle(RegQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult("Handler1");
    }


    public class ScopedDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public record ScopedQuery() : IRequest<Guid>;
    public class ScopedQueryHandler : IRequestHandler<ScopedQuery, Guid>
    {
        private readonly ScopedDependency _dep;
        public ScopedQueryHandler(ScopedDependency dep) => _dep = dep;

        public Task<Guid> Handle(ScopedQuery request, CancellationToken cancellationToken = default)
            => Task.FromResult(_dep.Id);
    }

    public class RegistrationTests
    {
        [Fact]
        public void AddAarkamMediator_NullMarkers_ThrowsArgumentException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentException>(() => services.AddAarkamMediator(null!));
        }

        [Fact]
        public void AddAarkamMediator_EmptyMarkers_ThrowsArgumentException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentException>(() => services.AddAarkamMediator());
        }

        [Fact]
        public void AddAarkamMediator_DuplicateAssemblyMarkers_SucceedsWithoutError()
        {
            var services = new ServiceCollection();
            // Passing the same assembly multiple times
            services.AddAarkamMediator(typeof(RegistrationTests), typeof(RegistrationTests));

            var sp = services.BuildServiceProvider();
            var mediator = sp.GetRequiredService<IMediator>();

            Assert.NotNull(mediator);
        }

        [Fact]
        public void Mediator_Constructor_NullServiceProvider_ThrowsArgumentNullException()
        {
            var dict1 = FrozenDictionary<Type, RequestHandlerWrapper>.Empty;
            var dict2 = FrozenDictionary<Type, NotificationHandlerWrapper[]>.Empty;

            Assert.Throws<ArgumentNullException>(() => new Mediator(null!, dict1, dict2));
        }

        [Fact]
        public void Mediator_Constructor_NullRequestHandlers_ThrowsArgumentNullException()
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            var dict2 = FrozenDictionary<Type, NotificationHandlerWrapper[]>.Empty;

            Assert.Throws<ArgumentNullException>(() => new Mediator(sp, null!, dict2));
        }

        [Fact]
        public void Mediator_Constructor_NullNotificationHandlers_ThrowsArgumentNullException()
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            var dict1 = FrozenDictionary<Type, RequestHandlerWrapper>.Empty;

            Assert.Throws<ArgumentNullException>(() => new Mediator(sp, dict1, null!));
        }

        [Fact]
        public void AddPipelineBehavior_NullServices_ThrowsArgumentNullException()
        {
            IServiceCollection services = null!;
            Assert.Throws<ArgumentNullException>(() => services.AddPipelineBehavior(typeof(BehaviorAlpha<,>)));
        }

        [Fact]
        public void AddPipelineBehavior_NullType_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentNullException>(() => services.AddPipelineBehavior((Type)null!));
        }

        [Fact]
        public void AddPipelineBehavior_ExplicitNullServiceType_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentNullException>(() => services.AddPipelineBehavior(null!, typeof(BehaviorAlpha<,>)));
        }

        [Fact]
        public void AddPipelineBehavior_ExplicitNullImplementationType_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Assert.Throws<ArgumentNullException>(() => services.AddPipelineBehavior(typeof(IPipelineBehavior<,>), null!));
        }

        [Fact]
        public void AddPipelineBehavior_WithTransientLifetime_RegistersAsTransient()
        {
            var services = new ServiceCollection();
            services.AddPipelineBehavior(typeof(BehaviorAlpha<,>), ServiceLifetime.Transient);

            var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPipelineBehavior<,>));
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }

        [Fact]
        public void AddPipelineBehavior_WithSingletonLifetime_RegistersAsSingleton()
        {
            var services = new ServiceCollection();
            services.AddPipelineBehavior(typeof(BehaviorAlpha<,>), ServiceLifetime.Singleton);

            var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IPipelineBehavior<,>));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        }

        [Fact]
        public async Task ScopedHandlers_ShareScopedDependencies_WithinSameScope()
        {
            var services = new ServiceCollection();
            services.AddScoped<ScopedDependency>();
            services.AddAarkamMediator(typeof(RegistrationTests));

            var sp = services.BuildServiceProvider();

            using var scope1 = sp.CreateScope();
            var sender1 = scope1.ServiceProvider.GetRequiredService<ISender>();
            var id1_a = await sender1.Send(new ScopedQuery());
            var id1_b = await sender1.Send(new ScopedQuery());

            using var scope2 = sp.CreateScope();
            var sender2 = scope2.ServiceProvider.GetRequiredService<ISender>();
            var id2 = await sender2.Send(new ScopedQuery());

            // Within scope1, the instance ID must be identical
            Assert.Equal(id1_a, id1_b);

            // Across scopes, instance IDs must differ
            Assert.NotEqual(id1_a, id2);
        }

        [Fact]
        public void Mediator_IsRegisteredAsTransient()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(RegistrationTests));
            var sp = services.BuildServiceProvider();

            var m1 = sp.GetRequiredService<IMediator>();
            var m2 = sp.GetRequiredService<IMediator>();

            Assert.NotSame(m1, m2);
        }

        [Fact]
        public void ISender_And_IPublisher_ResolveToMediatorInstance()
        {
            var services = new ServiceCollection();
            services.AddAarkamMediator(typeof(RegistrationTests));
            var sp = services.BuildServiceProvider();

            var sender = sp.GetRequiredService<ISender>();
            var publisher = sp.GetRequiredService<IPublisher>();

            Assert.IsAssignableFrom<IMediator>(sender);
            Assert.IsAssignableFrom<IMediator>(publisher);
        }

        [Fact]
        public void ValidateUniqueHandlerConstraint_Throws_WhenDuplicateExists()
        {
            var dict = new Dictionary<Type, RequestHandlerWrapper>
            {
                [typeof(RegQuery)] = null!
            };
            var method = typeof(WhaleMediatorExtensions).GetMethod("ValidateUniqueHandlerConstraint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => method!.Invoke(null, new object[] { typeof(RegQuery), dict }));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }
    }
}

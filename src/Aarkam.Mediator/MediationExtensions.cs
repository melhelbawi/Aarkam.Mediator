using Microsoft.Extensions.DependencyInjection;
using System.Collections.Frozen;

namespace Aarkam.Mediator
{
    /// <summary>
    /// Provides extension methods for registering the high-performance <see cref="Mediator"/> execution engine within Microsoft Dependency Injection container networks.
    /// </summary>
    public static class WhaleMediatorExtensions
    {
        /// <summary>
        /// Registers the high-performance <see cref="Mediator"/> pipeline engine along with automated, reflection-driven assembly handler discovery routines.
        /// </summary>
        /// <param name="services">The active service target container to populate with operational messaging components.</param>
        /// <param name="handlerAssemblyMarkerTypes">One or more structural assembly type indicators utilized to establish reflective discovery boundaries.</param>
        /// <returns>The populated service collection to maintain pipeline method-chaining usability paradigms.</returns>
        /// <exception cref="ArgumentException">Thrown if an empty or null array payload parameter is passed during boot configurations.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a point-to-point request mapping breaks the mandatory singular dependency topology boundary.</exception>
        public static IServiceCollection AddAarkamMediator(this IServiceCollection services, params Type[] handlerAssemblyMarkerTypes)
        {
            if (handlerAssemblyMarkerTypes == null || handlerAssemblyMarkerTypes.Length == 0)
            {
                throw new ArgumentException("At least one assembly marker type must be provided to initiate component tracking.", nameof(handlerAssemblyMarkerTypes));
            }

            var requestHandlers = new Dictionary<Type, RequestHandlerWrapper>();
            var notificationHandlers = new Dictionary<Type, List<NotificationHandlerWrapper>>();

            // Filter out assembly duplicates to prevent redundant type scan iterations
            var assemblies = handlerAssemblyMarkerTypes
                .Select(t => t.Assembly)
                .Distinct()
                .ToList();

            var allConcreteTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
                .ToList();

            foreach (var typeNode in allConcreteTypes)
            {
                RegisterRequestHandlers(services, typeNode, requestHandlers);
                RegisterNotificationHandlers(services, typeNode, notificationHandlers);
            }

            // Convert operational dictionaries into optimized Frozen configurations
            var frozenRequestRegistry = requestHandlers.ToFrozenDictionary();
            var frozenNotificationRegistry = notificationHandlers
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
                .ToFrozenDictionary();

            // Register the immutable Mediator engine core as a Singleton
            services.AddSingleton<IMediator>(sp => new Mediator(sp, frozenRequestRegistry, frozenNotificationRegistry));

            // Bridge convenience interfaces directly to the identical singleton engine instance
            services.AddSingleton<ISender>(sp => sp.GetRequiredService<IMediator>());
            services.AddSingleton<IPublisher>(sp => sp.GetRequiredService<IMediator>());

            return services;
        }

        private static void RegisterRequestHandlers(
            IServiceCollection services,
            Type handlerType,
            Dictionary<Type, RequestHandlerWrapper> requestHandlers)
        {
            var interfaces = handlerType.GetInterfaces();

            foreach (var iface in interfaces)
            {
                if (!iface.IsGenericType) continue;

                var genericDef = iface.GetGenericTypeDefinition();

                // 1. Processing data-returning requests
                if (genericDef == typeof(IRequestHandler<,>))
                {
                    var typeArgs = iface.GetGenericArguments();
                    var requestType = typeArgs[0];
                    var responseType = typeArgs[1];

                    ValidateUniqueHandlerConstraint(requestType, requestHandlers);

                    services.AddScoped(iface, handlerType);

                    // Dynamically build the implementation wrapper type safely
                    var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
                    requestHandlers[requestType] = (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
                }
                // 2. Processing void commands matching the unified interface tree topology
                else if (genericDef == typeof(IRequestHandler<>))
                {
                    var requestType = iface.GetGenericArguments()[0];

                    // Guard against missing definitions or cross-inherited structural duplications
                    if (typeof(IRequest).IsAssignableFrom(requestType))
                    {
                        ValidateUniqueHandlerConstraint(requestType, requestHandlers);

                        services.AddScoped(iface, handlerType);

                        var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(Unit));
                        requestHandlers[requestType] = (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
                    }
                }
            }
        }

        private static void RegisterNotificationHandlers(
            IServiceCollection services,
            Type handlerType,
            Dictionary<Type, List<NotificationHandlerWrapper>> notificationHandlers)
        {
            var matchingNotificationInterfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INotificationHandler<>));

            foreach (var iface in matchingNotificationInterfaces)
            {
                var notificationType = iface.GetGenericArguments()[0];

                // Register standard decoupled scoped interface lookup hooks inside the system container
                services.AddScoped(iface, handlerType);

                // Build a pre-compiled reflection factory invocation delegate to protect runtime execution pathways
                var methodInfo = iface.GetMethod(nameof(INotificationHandler<INotification>.Handle));

                Func<object, IServiceProvider, CancellationToken, Task> factoryDelegate = (notificationInstance, serviceProvider, cancellationToken) =>
                {
                    var resolvedSubscriberInstance = serviceProvider.GetRequiredService(iface);
                    return (Task)methodInfo!.Invoke(resolvedSubscriberInstance, new[] { notificationInstance, cancellationToken })!;
                };

                var wrapperInstance = new NotificationHandlerWrapper(factoryDelegate);

                if (!notificationHandlers.TryGetValue(notificationType, out var subscriberList))
                {
                    subscriberList = new List<NotificationHandlerWrapper>();
                    notificationHandlers[notificationType] = subscriberList;
                }

                subscriberList.Add(wrapperInstance);
            }
        }

        private static void ValidateUniqueHandlerConstraint(Type requestType, Dictionary<Type, RequestHandlerWrapper> requestHandlers)
        {
            if (requestHandlers.ContainsKey(requestType))
            {
                throw new InvalidOperationException(
                    $"Multiple handlers registered for request type: '{requestType.FullName}'. " +
                    "A point-to-point architecture topology enforces exactly one processing handler node per command/query reference.");
            }
        }
    }
}

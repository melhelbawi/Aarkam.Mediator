using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Aarkam.Mediator
{
    /// <summary>
    /// High-performance, lightweight Mediator implementation with full pipeline behavior support.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the core engine class of WhaleMediator. It routes requests cleanly to their pre-compiled 
    /// handlers while delegating downstream pipeline middleware processing entirely to zero-allocation structural wrappers.
    /// </para>
    /// <para>
    /// Key features:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Uses <see cref="FrozenDictionary{TKey,TValue}"/> for optimized lookups.</description></item>
    ///   <item><description>Supports both generic (<see cref="IRequest{TResponse}"/>) and void-style (<see cref="IRequest"/>) requests.</description></item>
    ///   <item><description>Executes pipeline behaviors before invoking the target terminal request handler.</description></item>
    ///   <item><description>Notifications (<see cref="INotification"/>) bypass middleware pipelines entirely for enhanced throughput processing.</description></item>
    /// </list>
    /// </remarks>
    public sealed class Mediator : IMediator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FrozenDictionary<Type, RequestHandlerWrapper> _requestHandlers;
        private readonly FrozenDictionary<Type, NotificationHandlerWrapper[]> _notificationHandlers;

        /// <summary>
        /// Initializes a new instance of the high-performance <see cref="Mediator"/> execution engine.
        /// </summary>
        /// <param name="serviceProvider">The active dependency provider instance used to resolve pipeline middleware on demand.</param>
        /// <param name="requestHandlers">A pre-scanned collection linking request types to their respective execution wrappers.</param>
        /// <param name="notificationHandlers">A pre-scanned collection linking notification event types to active subscriber arrays.</param>
        /// <exception cref="ArgumentNullException">Thrown if any required dependency injection references are null.</exception>
        public Mediator(
            IServiceProvider serviceProvider,
            FrozenDictionary<Type, RequestHandlerWrapper> requestHandlers,
            FrozenDictionary<Type, NotificationHandlerWrapper[]> notificationHandlers)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _requestHandlers = requestHandlers ?? throw new ArgumentNullException(nameof(requestHandlers));
            _notificationHandlers = notificationHandlers ?? throw new ArgumentNullException(nameof(notificationHandlers));
        }

        /// <summary>
        /// Sends a request that expects a response, executing the full pipeline (behaviors + handler).
        /// </summary>
        /// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
        /// <param name="request">The request to send. Cannot be null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the handler after executing all behaviors.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!_requestHandlers.TryGetValue(request.GetType(), out var wrapper))
            {
                ThrowNoHandlerException(request.GetType());
            }

            // High Performance Pass-Through: Middleware assembly loop is handled inside the wrapper class to prevent runtime allocation 
            return wrapper.Handle<TResponse>(request, _serviceProvider, cancellationToken);
        }

        /// <summary>
        /// Sends a void-style (non-generic) request, executing the full pipeline.
        /// Internally handled as <see cref="IRequest{Unit}"/>.
        /// </summary>
        /// <param name="request">The request to send. Cannot be null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no handler is registered for the request type.</exception>
        public Task Send(
            IRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!_requestHandlers.TryGetValue(request.GetType(), out var wrapper))
            {
                ThrowNoHandlerException(request.GetType());
            }

            // Safely maps to Unit processing channels under the hood completely allocation-free
            return wrapper.Handle(request, _serviceProvider, cancellationToken);
        }

        /// <summary>
        /// Publishes a notification to all registered handlers sequentially.
        /// Pipeline behaviors are intentionally not applied to notifications for performance reasons.
        /// </summary>
        /// <param name="notification">The notification to publish. Cannot be null.</param>
        /// <param name="cancellationToken">Cancellation token to abort processing loops early between handlers.</param>
        /// <returns>A task representing the asynchronous publish operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is null.</exception>
        public async Task Publish(
            INotification notification,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(notification);

            if (!_notificationHandlers.TryGetValue(notification.GetType(), out var handlers))
            {
                return; // No handlers registered is a fully valid operational scenario for broadcast notifications
            }

            // Using a raw index loop instead of foreach to bypass structural array enumerator heap allocations
            for (int i = 0; i < handlers.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handlers[i].Handle(notification, _serviceProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Helper method to throw a consistent exception when no handler is found.
        /// </summary>
        [DoesNotReturn]
        private static void ThrowNoHandlerException(Type requestType)
        {
            throw new InvalidOperationException($"No handler registered for request type {requestType.FullName}");
        }
    }
}

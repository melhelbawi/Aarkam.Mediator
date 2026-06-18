namespace Aarkam.Mediator
{
    /// <summary>
    /// Internal non-generic wrapper for notification handlers to avoid reflection and lookup overhead on hot execution paths.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This wrapper acts as a core performance optimization component within the Publish-Subscribe mechanics of WhaleMediator. 
    /// It encapsulates the generic handler resolution and execution flow behind a standardized, pre-compiled delegate wrapper.
    /// </para>
    /// <para>
    /// This design allows the core <see cref="Mediator"/> engine to store and trigger multiple handlers for a single event 
    /// from an optimized collection (such as a <c>FrozenDictionary&lt;Type, NotificationHandlerWrapper[]&gt;</c>) 
    /// completely bypassing the performance overhead of traditional runtime reflection.
    /// </para>
    /// </remarks>
    public sealed class NotificationHandlerWrapper
    {
        private readonly Func<object, IServiceProvider, CancellationToken, Task> _handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationHandlerWrapper"/> class with a pre-compiled execution factory delegate.
        /// </summary>
        /// <param name="handler">The factory execution delegate responsible for type-casting the notification and triggering the handler.</param>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="handler"/> is null.</exception>
        public NotificationHandlerWrapper(Func<object, IServiceProvider, CancellationToken, Task> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Executes the encapsulated notification handler asynchronously using the provided environment dependencies.
        /// </summary>
        /// <param name="notification">The untyped event object to pass to the underlying subscriber handler. Cannot be null.</param>
        /// <param name="sp">The active IoC/DI container instance utilized to resolve necessary handler dependencies.</param>
        /// <param name="ct">A token utilized to monitor and propagate execution cancellation requests to the event subscriber.</param>
        /// <returns>A task representing the completion of the asynchronous subscriber execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="notification"/> or <paramref name="sp"/> is null.</exception>
        public Task Handle(object notification, IServiceProvider sp, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(notification);
            ArgumentNullException.ThrowIfNull(sp);

            return _handler(notification, sp, ct);
        }
    }
}

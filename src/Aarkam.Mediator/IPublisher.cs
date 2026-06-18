namespace Aarkam.Mediator
{
    /// <summary>
    /// Defines the dispatching contract for broadcasting event notifications to all matching subscribers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IPublisher"/> interface implements the Publish-Subscribe pattern (1:N topology) of WhaleMediator.
    /// It decouples publishers from consumers, allowing your domain layer to announce side effects without knowing who processes them.
    /// </para>
    /// <para>
    /// For optimal latency and execution efficiency, cross-cutting pipeline behaviors (middleware) are completely bypassed 
    /// during notification dispatching.
    /// </para>
    /// </remarks>
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a notification message asynchronously to zero or more registered notification handlers.
        /// </summary>
        /// <param name="notification">The event notification object containing context and payload data to broadcast. Cannot be null.</param>
        /// <param name="cancellationToken">A token utilized to signal cancellation requests across active subscriber tasks.</param>
        /// <returns>A task representing the completion of the asynchronous broadcasting operation.</returns>
        /// <remarks>
        /// <para>
        /// If no matching implementations of <see cref="INotificationHandler{TNotification}"/> are registered in the system container, 
        /// this operation will complete successfully without throwing an exception.
        /// </para>
        /// <para>
        /// Depending on your final mediator implementation strategy, handlers can be run either sequentially (where an unhandled exception 
        /// from one subscriber short-circuits subsequent handlers) or concurrently via parallel execution branches.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="notification"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if execution is canceled via the <paramref name="cancellationToken"/>.</exception>
        Task Publish(INotification notification, CancellationToken cancellationToken = default);
    }
}

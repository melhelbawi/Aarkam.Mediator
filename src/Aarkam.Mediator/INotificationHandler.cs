namespace Aarkam.Mediator
{
    /// <summary>
    /// Defines the execution contract for a subscriber that listens to and processes a broadcasted notification message.
    /// </summary>
    /// <typeparam name="TNotification">
    /// The specific type of the incoming event or notification. Must implement the <see cref="INotification"/> marker interface.
    /// The <see langword="in"/> keyword marks this parameter as contravariant, allowing base notification handlers to catch derived event arguments safely.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This interface forms the foundation of the Publish-Subscribe (1:N topology) engine within WhaleMediator.
    /// Unlike requests, a single notification type can be bound to zero, one, or multiple active handlers concurrently.
    /// </para>
    /// <para>
    /// If an exception occurs within one handler during execution, processing behavior depends entirely on the dispatcher implementation 
    /// (e.g., sequential termination versus wrapped aggregate exceptions). 
    /// </para>
    /// <para>
    /// Cross-cutting pipeline behaviors (middleware) are intentionally omitted from notification handlers to achieve 
    /// near-zero allocation overhead and rapid event distribution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public record UserRegisteredEvent(Guid UserId, string Email) : INotification;
    /// 
    /// public class WelcomeEmailHandler : INotificationHandler&lt;UserRegisteredEvent&gt;
    /// {
    ///     public async Task Handle(UserRegisteredEvent notification, CancellationToken ct = default)
    ///     {
    ///         // Send transactional welcome email...
    ///         await Task.CompletedTask;
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface INotificationHandler<in TNotification>
        where TNotification : INotification
    {
        /// <summary>
        /// Handles the broadcasted notification asynchronously.
        /// </summary>
        /// <param name="notification">The incoming event notification object containing context and payload data.</param>
        /// <param name="cancellationToken">A token utilized to signal cancellation requests to the underlying execution task.</param>
        /// <returns>A task representing the completion of the asynchronous notification processing operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the processing operation is canceled via the <paramref name="cancellationToken"/>.</exception>
        Task Handle(TNotification notification, CancellationToken cancellationToken = default);
    }
}

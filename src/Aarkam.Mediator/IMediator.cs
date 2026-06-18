namespace Aarkam.Mediator
{
    /// <summary>
    /// Unified mediator interface that combines both <see cref="ISender"/> and <see cref="IPublisher"/> into a single service engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interface acts as a convenient single unit of implementation (facade) for applications 
    /// that prefer to inject one mediator instance rather than separate sender and publisher references.
    /// </para>
    /// <para>
    /// It provides unified access to both point-to-point request/response handling (<see cref="ISender.Send{TResponse}"/>) 
    /// and broadcast event-style notifications (<see cref="IPublisher.Publish"/>).
    /// </para>
    /// <para>
    /// <strong>Usage Recommendations:</strong>
    /// <list type="bullet">
    ///   <item><description>Use <see cref="IMediator"/> when you need both dispatch capabilities in the same service context.</description></item>
    ///   <item><description>Use <see cref="ISender"/> or <see cref="IPublisher"/> separately for stricter architectural CQRS interface separation.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class OrderService
    /// {
    ///     private readonly IMediator _mediator;
    /// 
    ///     public OrderService(IMediator mediator) => _mediator = mediator;
    /// 
    ///     public async Task CreateOrder(CreateOrderCommand cmd)
    ///     {
    ///         await _mediator.Send(cmd);                          // Point-to-Point Command
    ///         await _mediator.Publish(new OrderCreatedNotify());  // Broadcast Notification
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IMediator : ISender, IPublisher
    {
        // Combined behavioral marker contract interface - no additional member declarations required.
    }
}

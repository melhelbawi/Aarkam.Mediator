namespace Aarkam.Mediator
{
    /// <summary>
    /// Marker interface for notifications representing events that can be broadcast to zero or multiple handlers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike requests (which follow a strict 1:1 routing topology), notifications implement the 
    /// Publish-Subscribe pattern (1:N topology). When a notification is published, the mediator 
    /// locates all registered implementations of its corresponding handler and executes them.
    /// </para>
    /// <para>
    /// If no handlers are registered for a given notification type, the publish operation completes 
    /// successfully without throwing exceptions, making it safe for decoupling cross-cutting events.
    /// </para>
    /// <para>
    /// For performance reasons, pipeline behaviors (like validation or logging middleware) are 
    /// intentionally bypassed during notification routing to ensure ultra-low latency event distribution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Define an event notice
    /// public record OrderPlacedEvent(Guid OrderId, decimal TotalAmount) : INotification;
    /// 
    /// // This event can safely be caught by an email handler, an inventory handler, and a logging handler simultaneously.
    /// </code>
    /// </example>
    public interface INotification { }
}

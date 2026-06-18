namespace Aarkam.Mediator
{
    /// <summary>
    /// Defines the dispatching contract for routing request and command messages directly to their single designated handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="ISender"/> interface represents the Point-to-Point (1:1 routing topology) core component of WhaleMediator. 
    /// It encapsulates execution dispatching logic, hiding the active lookup of handler classes from the calling client application.
    /// </para>
    /// <para>
    /// Any execution initiated via this interface automatically triggers the full cross-cutting pipeline chain 
    /// (e.g., behaviors, validation middlewares, logging filters) in their exact registration order before invoking the actual handler.
    /// </para>
    /// </remarks>
    public interface ISender
    {
        /// <summary>
        /// Sends a generic request asynchronously through the execution pipeline and returns a strongly-typed response.
        /// </summary>
        /// <typeparam name="TResponse">
        /// The expected response type yielded upon processing completion. 
        /// Can be a primitive type, domain object, or DTO.
        /// </typeparam>
        /// <param name="request">The data message object to route. Cannot be null.</param>
        /// <param name="cancellationToken">A token utilized to monitor and signal cancellation requests across the processing pipeline nodes.</param>
        /// <returns>A task representing the asynchronous operation, containing the execution result payload returned from the designated handler.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no valid <see cref="IRequestHandler{TRequest,TResponse}"/> implementation can be resolved for the message type.</exception>
        /// <exception cref="OperationCanceledException">Thrown if execution is stopped early via the <paramref name="cancellationToken"/>.</exception>
        Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a non-generic, command-style request asynchronously through the execution pipeline without returning a data payload.
        /// </summary>
        /// <param name="request">The data message object representing a void side-effect operation. Cannot be null.</param>
        /// <param name="cancellationToken">A token utilized to monitor and signal cancellation requests across the processing pipeline nodes.</param>
        /// <returns>A task representing the completion of the asynchronous execution pipeline.</returns>
        /// <remarks>
        /// This method serves as a developer-friendly shortcut that internally dispatches the message under an 
        /// <c>IRequest&lt;Unit&gt;</c> context, avoiding any manual handling of the underlying <see cref="Unit"/> struct instance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="request"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no valid handler can be resolved for the command type.</exception>
        /// <exception cref="OperationCanceledException">Thrown if execution is stopped early via the <paramref name="cancellationToken"/>.</exception>
        Task Send(IRequest request, CancellationToken cancellationToken = default);
    }
}

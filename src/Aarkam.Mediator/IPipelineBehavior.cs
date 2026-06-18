namespace Aarkam.Mediator
{
    /// <summary>
    /// Represents an asynchronous execution node within the request processing pipeline.
    /// </summary>
    /// <typeparam name="TResponse">The type of response returned by the executing pipeline node.</typeparam>
    /// <param name="cancellationToken">The cancellation token to pass down to subsequent pipeline behaviors or the final handler.</param>
    /// <returns>A task wrapping the strongly-typed response payload.</returns>
    /// <remarks>
    /// Passing the <see cref="CancellationToken"/> explicitly through the delegate signature allows middleware 
    /// components to cleanly pass updated, linked, or timeout-driven cancellation tokens down the execution stack.
    /// </remarks>
    public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);

    /// <summary>
    /// Defines a pipeline behavior (middleware) that intercepts, decorates, or short-circuits 
    /// the processing of a specific request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request message being processed.</typeparam>
    /// <typeparam name="TResponse">The type of response returned upon completion.</typeparam>
    public interface IPipelineBehavior<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Intercepts and executes custom logic before or after the downstream pipeline elements.
        /// </summary>
        /// <param name="request">The strongly-typed incoming request object.</param>
        /// <param name="next">A delegate representing the next behavior in the chain or the terminal request handler.</param>
        /// <param name="cancellationToken">A token to monitor for execution cancellation requests.</param>
        /// <returns>The execution response returned from downstream nodes, which may be inspected or modified.</returns>
        Task<TResponse> HandleAsync(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken);
    }
}

namespace Aarkam.Mediator
{
    /// <summary>
    /// Defines the execution contract for a single handler that processes a specific request and yields a response.
    /// </summary>
    /// <typeparam name="TRequest">
    /// The specific type of the incoming message. Must implement <see cref="IRequest{TResponse}"/>.
    /// </typeparam>
    /// <typeparam name="TResponse">
    /// The data type yielded upon processing completion. This can be a primitive, a custom DTO, 
    /// or <see cref="Unit"/> for void operations.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This is the core functional node of the Request-Response architecture in WhaleMediator. 
    /// There must be exactly one registered implementation of this interface per unique <typeparamref name="TRequest"/> type.
    /// </para>
    /// <para>
    /// The mediator infrastructure routes messages safely to this interface, executing any cross-cutting pipeline 
    /// behaviors before invoking the <see cref="Handle"/> method.
    /// </para>
    /// </remarks>
    public interface IRequestHandler<in TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <summary>
        /// Handles the incoming request asynchronously.
        /// </summary>
        /// <param name="request">The incoming command or query object containing execution data parameters.</param>
        /// <param name="cancellationToken">A token utilized to signal cancellation requests to the underlying execution task.</param>
        /// <returns>A task representing the asynchronous operation, wrapping the typed execution response payload.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the processing operation is canceled via the <paramref name="cancellationToken"/>.</exception>
        Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Defines the execution contract for a command-style handler that processes a request without returning a data payload.
    /// </summary>
    /// <typeparam name="TRequest">The specific type of the incoming message. Must implement the specialized <see cref="IRequest"/> marker interface.</typeparam>
    /// <remarks>
    /// <para>
    /// This interface unifies void-style commands into the underlying generic framework by inheriting directly 
    /// from <c>IRequestHandler&lt;TRequest, Unit&gt;</c>. 
    /// </para>
    /// <para>
    /// This structure guarantees that the pipeline runtime processes all messages using a single, unified execution path 
    /// without code duplication, while hiding the <see cref="Unit"/> return token from consumer implementations.
    /// </para>
    /// </remarks>
    public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
        where TRequest : IRequest
    {
        /// <summary>
        /// Handles the incoming non-generic request asynchronously.
        /// </summary>
        /// <param name="request">The incoming command object containing execution data parameters.</param>
        /// <param name="cancellationToken">A token utilized to signal cancellation requests to the underlying execution task.</param>
        /// <returns>A task representing the completion of the asynchronous void processing operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the processing operation is canceled via the <paramref name="cancellationToken"/>.</exception>
        new Task Handle(TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Explicit default implementation bridging the void command into the unified generic <see cref="Unit"/> pipeline.
        /// </summary>
        async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
        {
            await Handle(request, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }
    }

    /// <summary>
    /// Base class for void-style command handlers that simplifies handling without returning a payload.
    /// </summary>
    /// <typeparam name="TRequest">The request type implementing <see cref="IRequest"/>.</typeparam>
    public abstract class RequestHandler<TRequest> : IRequestHandler<TRequest>
        where TRequest : IRequest
    {
        Task IRequestHandler<TRequest>.Handle(TRequest request, CancellationToken cancellationToken)
            => Handle(request, cancellationToken);

        Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
            => HandleCore(request, cancellationToken);

        private async Task<Unit> HandleCore(TRequest request, CancellationToken cancellationToken)
        {
            await Handle(request, cancellationToken).ConfigureAwait(false);
            return Unit.Value;
        }

        /// <summary>
        /// Handles the void-style command asynchronously.
        /// </summary>
        /// <param name="request">The incoming request command.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing asynchronous execution.</returns>
        protected abstract Task Handle(TRequest request, CancellationToken cancellationToken = default);
    }
}


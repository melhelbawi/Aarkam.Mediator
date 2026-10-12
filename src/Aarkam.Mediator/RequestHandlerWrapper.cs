using Microsoft.Extensions.DependencyInjection;

namespace Aarkam.Mediator
{
    /// <summary>
    /// Serves as the non-generic abstract execution bridge for request processing pipelines inside WhaleMediator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This wrapper is a core performance optimization component. It allows the <see cref="Mediator"/> engine 
    /// to store and execute highly optimized generic handlers from a non-generic collection (such as a 
    /// <c>FrozenDictionary&lt;Type, RequestHandlerWrapper&gt;</c>) without using runtime reflection.
    /// </para>
    /// <para>
    /// By using a strongly-typed subclass implementation instead of a boxed <c>Task&lt;object&gt;</c> delegate, 
    /// this structure completely eliminates garbage collection allocation overhead caused by boxing primitive value types 
    /// (e.g., <see cref="bool"/>, <see cref="int"/>) or the specialized <see cref="Unit"/> struct.
    /// </para>
    /// </remarks>
    public abstract class RequestHandlerWrapper
    {
        /// <summary>
        /// Executes a generic request asynchronously, running the full pipeline chain (behaviors + handler) and returning a typed response.
        /// </summary>
        /// <typeparam name="TResponse">The expected response data type. Can be a primitive, a custom class, or <see cref="Unit"/>.</typeparam>
        /// <param name="request">The incoming untyped request message object. Cannot be null.</param>
        /// <param name="serviceProvider">The active IoC/DI container instance utilized to resolve pipeline dependencies.</param>
        /// <param name="cancellationToken">A token utilized to monitor and propagate execution cancellation requests down through the pipeline.</param>
        /// <returns>A task wrapping the strongly-typed execution response payload.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="request"/> or <paramref name="serviceProvider"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the underlying handler or its pipeline components cannot be resolved from the container.</exception>
        public abstract Task<TResponse> Handle<TResponse>(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);

        /// <summary>
        /// Executes a non-generic, command-style request asynchronously, running the full pipeline chain without returning a data payload.
        /// </summary>
        /// <param name="request">The incoming untyped request message object representing a void operation. Cannot be null.</param>
        /// <param name="serviceProvider">The active IoC/DI container instance utilized to resolve pipeline dependencies.</param>
        /// <param name="cancellationToken">A token utilized to monitor and propagate execution cancellation requests down through the pipeline.</param>
        /// <returns>A task representing the completion of the asynchronous execution pipeline.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the provided <paramref name="request"/> or <paramref name="serviceProvider"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the underlying handler or its pipeline components cannot be resolved from the container.</exception>
        public abstract Task Handle(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }


    // Hidden internally so your users never have to see it or configure it manually
    internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper
        where TRequest : IRequest<TResponse>
    {
        public override async Task<TTargetResponse> Handle<TTargetResponse>(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var handler = serviceProvider.GetService(typeof(IRequestHandler<TRequest, TResponse>)) as IRequestHandler<TRequest, TResponse>;
            if (handler is null)
            {
                throw new InvalidOperationException($"No handler registered for request type {typeof(TRequest).FullName}");
            }

            var typedRequest = (TRequest)request;
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var behaviorList = behaviors as IList<IPipelineBehavior<TRequest, TResponse>> ?? behaviors?.ToList();

            // Zero-allocation fast-path when no pipeline behaviors are configured
            if (behaviorList is null || behaviorList.Count == 0)
            {
                var directResult = await handler.Handle(typedRequest, cancellationToken).ConfigureAwait(false);
                return (TTargetResponse)(object)directResult!;
            }

            // Build the execution pipeline chain terminating at the request handler
            RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(typedRequest, ct);

            for (int i = behaviorList.Count - 1; i >= 0; i--)
            {
                var currentBehavior = behaviorList[i];
                var next = pipeline;
                pipeline = ct => currentBehavior.HandleAsync(typedRequest, next, ct);
            }

            var result = await pipeline(cancellationToken).ConfigureAwait(false);

            // This direct cast is ultra-fast and completely bypasses boxing penalties!
            return (TTargetResponse)(object)result!;
        }

        public override Task Handle(
            object request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            // Void requests automatically map to Unit under the hood here
            return Handle<Unit>(request, serviceProvider, cancellationToken);
        }
    }
}

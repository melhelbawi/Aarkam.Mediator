namespace Aarkam.Mediator
{
    /// <summary>
    /// Marker interface representing a request message that yields a response.
    /// </summary>
    /// <typeparam name="TResponse">
    /// The expected response data type. This can be a primitive type (e.g., <see cref="bool"/>, 
    /// <see cref="int"/>, <see cref="System.Guid"/>), a custom domain class, a data transfer object (DTO), 
    /// or the specialized <see cref="Unit"/> struct for empty payloads.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// This interface forms the foundation of the Request-Response (or Command-Query) pattern 
    /// within WhaleMediator. Every request is routed to a single, specific handler.
    /// </para>
    /// <para>
    /// When using value types or primitives like <see cref="bool"/> as <typeparamref name="TResponse"/>, 
    /// the runtime compiles specialized generic execution paths, completely eliminating runtime boxing overhead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example returning a primitive boolean
    /// public record IsEmailUniqueQuery(string Email) : IRequest&lt;bool&gt;;
    /// 
    /// // Example returning a custom data record
    /// public record GetUserQuery(Guid UserId) : IRequest&lt;UserDto&gt;;
    /// </code>
    /// </example>
    public interface IRequest<out TResponse> { }

    /// <summary>
    /// Marker interface representing a request message that does not return a data payload.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a specialized convenience shortcut interface intended for state-changing commands 
    /// where a return value is unnecessary. It inherits directly from <c>IRequest&lt;Unit&gt;</c>.
    /// </para>
    /// <para>
    /// By deriving from <c>IRequest&lt;Unit&gt;</c>, the internal execution engine can process all 
    /// pipeline requests under a unified generic signature, avoiding code duplication while maintaining 
    /// a clean, language-level "void" experience for the application developer.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // A standard command that performs an action without returning data
    /// public record DeactivateUserCommand(Guid UserId) : IRequest;
    /// </code>
    /// </example>
    public interface IRequest : IRequest<Unit> { }
}

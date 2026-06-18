namespace Aarkam.Mediator
{
    /// <summary>
    /// Represents a void-like value type used as a response for commands/requests 
    /// that do not return any meaningful data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Unit"/> type is a singleton value type that allows commands 
    /// to conform to the generic <see cref="IRequest{TResponse}"/> interface 
    /// without using <see langword="void"/> (which is not valid as a generic type parameter).
    /// </para>
    /// <para>
    /// All instances of <see cref="Unit"/> are considered equal. It is designed to be 
    /// used via the static <see cref="Value"/> property.
    /// </para>
    /// <example>
    /// <code>
    /// public class CreateOrderCommand : IRequest&lt;Unit&gt; { }
    /// 
    /// public class CreateOrderHandler : IRequestHandler&lt;CreateOrderCommand, Unit&gt;
    /// {
    ///     public Task&lt;Unit&gt; Handle(CreateOrderCommand request, CancellationToken ct = default)
    ///     {
    ///         // ... business logic
    ///         return Task.FromResult(Unit.Value);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </remarks>
    public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
    {
        /// <summary>
        /// The singleton instance of the <see cref="Unit"/> type.
        /// </summary>
        private static readonly Unit _value = default;

        /// <summary>
        /// Gets a reference to the singleton <see cref="Unit"/> value.
        /// This is the only value that should be used.
        /// </summary>
        public static ref readonly Unit Value => ref _value;

        /// <summary>
        /// Compares this instance with another <see cref="Unit"/>. 
        /// Always returns 0 because all <see cref="Unit"/> instances are equal.
        /// </summary>
        /// <param name="other">The <see cref="Unit"/> to compare with.</param>
        /// <returns>0</returns>
        public int CompareTo(Unit other) => 0;

        /// <summary>
        /// Compares this instance with another object.
        /// </summary>
        /// <param name="obj">
        /// The object to compare with. Can be null or another <see cref="Unit"/>.
        /// </param>
        /// <returns>
        /// 0 if the object is a <see cref="Unit"/> or null (by convention for value types),
        /// otherwise throws <see cref="ArgumentException"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="obj"/> is not a <see cref="Unit"/>.</exception>
        public int CompareTo(object? obj)
        {
            if (obj is Unit || obj is null)
                return 0;

            throw new ArgumentException($"Object must be of type {nameof(Unit)}.", nameof(obj));
        }

        /// <summary>
        /// Returns a hash code for this instance. Always returns 0 because all 
        /// <see cref="Unit"/> instances are equal.
        /// </summary>
        /// <returns>0</returns>
        public override int GetHashCode() => 0;

        /// <summary>
        /// Determines whether this instance is equal to another <see cref="Unit"/>.
        /// Always returns <see langword="true"/>.
        /// </summary>
        /// <param name="other">The <see cref="Unit"/> to compare with.</param>
        /// <returns><see langword="true"/></returns>
        public bool Equals(Unit other) => true;

        /// <summary>
        /// Determines whether this instance is equal to a specified object.
        /// </summary>
        /// <param name="obj">The object to compare with.</param>
        /// <returns><see langword="true"/> if the object is a <see cref="Unit"/>, otherwise <see langword="false"/>.</returns>
        public override bool Equals(object? obj) => obj is Unit;

        /// <summary>
        /// Returns the string representation of the <see cref="Unit"/> value.
        /// </summary>
        /// <returns>"()" – conventional representation of the Unit type.</returns>
        public override string ToString() => "()";

        /// <summary>
        /// Equality operator. Always returns <see langword="true"/> for any two <see cref="Unit"/> instances.
        /// </summary>
        public static bool operator ==(Unit first, Unit second) => true;

        /// <summary>
        /// Inequality operator. Always returns <see langword="false"/> for any two <see cref="Unit"/> instances.
        /// </summary>
        public static bool operator !=(Unit first, Unit second) => false;
    }
}
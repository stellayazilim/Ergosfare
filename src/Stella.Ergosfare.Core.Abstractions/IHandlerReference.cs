namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A reference to a pipeline participant: the concrete type to instantiate, and a way to
/// obtain an instance for the dispatch in progress.
/// </summary>
/// <remarks>
/// References hold no provider of their own and are shared across dispatches. The instance
/// is obtained per invocation from the provider the dispatcher passes in, so registered DI
/// lifetimes apply as configured. A memoized pipeline caches the resolved instance in the
/// reference and returns it on subsequent calls.
/// </remarks>
/// <typeparam name="THandler">The participant type this reference resolves to.</typeparam>
public interface IHandlerReference<out THandler>
{
    /// <summary>
    /// The concrete type to instantiate. When the participant targets a generic message
    /// type, this type is already closed over the message's generic arguments.
    /// </summary>
    Type HandlerType { get; }

    /// <summary>
    /// Returns an instance for the current dispatch, resolved from
    /// <paramref name="serviceProvider"/>, or the cached instance if this reference is
    /// memoized.
    /// </summary>
    /// <param name="serviceProvider">The provider of the scope the dispatch runs in.</param>
    /// <returns>The participant instance to invoke.</returns>
    THandler Resolve(IServiceProvider serviceProvider);
}

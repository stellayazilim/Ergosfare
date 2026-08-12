namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A resolvable reference to a pipeline handler: its pre-computed concrete type and a way
/// to obtain an instance for the current dispatch.
/// </summary>
/// <remarks>
/// References are provider-independent and shared process-wide; the handler instance is
/// obtained per invocation from the dispatching scope's service provider, which the
/// mediation pipeline passes down explicitly — resolution is the dispatcher's
/// responsibility, never the execution context's. Registered DI lifetimes are honored;
/// memoized pipelines cache the resolved instance inside the reference instead.
/// </remarks>
/// <typeparam name="THandler">The type of the handler.</typeparam>
public interface IHandlerReference<out THandler>
{
    /// <summary>
    /// Gets the concrete handler type to instantiate — already closed over the message's
    /// generic arguments when the handler targets a generic message type.
    /// </summary>
    Type HandlerType { get; }

    /// <summary>
    /// Resolves the handler instance from the given provider — the dispatching scope's
    /// provider, passed down by the mediation pipeline — unless the pipeline is memoized,
    /// in which case the cached instance is returned.
    /// </summary>
    /// <param name="serviceProvider">The provider of the scope the current dispatch runs in.</param>
    THandler Resolve(IServiceProvider serviceProvider);
}

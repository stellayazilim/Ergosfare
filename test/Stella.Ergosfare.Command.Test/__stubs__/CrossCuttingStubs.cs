using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Command.Test.__stubs__;

/// <summary>
/// A cross-cutting pre-interceptor declared over the core <see cref="IMessage"/> contract;
/// the <see cref="ICommand"/> marker is carried directly on the class, opting it into the
/// command module's discovery the same way the module contracts do. Excluded from
/// discovery: a discoverable interceptor reaching <see cref="IMessage"/> would enter every
/// command pipeline in the assembly and keep the generator from planning any of them —
/// the type exists precisely to pin that such a participant stays off the compiled plans.
/// </summary>
[ExcludeFromDiscovery]
public class StubCrossCuttingMessagePreInterceptor : IAsyncPreInterceptor<IMessage>, ICommand
{
    /// <summary>
    /// Indicates whether the interceptor has been called.
    /// </summary>
    public static bool HasCalled;

    /// <summary>
    /// Handles any message asynchronously before its main handler.
    /// </summary>
    /// <param name="message">The message being intercepted.</param>
    /// <param name="context">The execution context for the pipeline.</param>
    /// <returns>The original message as an <see cref="object"/>.</returns>
    public ValueTask<object> HandleAsync(IMessage message, ErgosfareContext context)
    {
        HasCalled = true;
        return ValueTask.FromResult<object>(message);
    }
}

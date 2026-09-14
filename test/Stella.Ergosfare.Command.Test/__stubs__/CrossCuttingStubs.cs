using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Command.Test.__stubs__;

/// <summary>A selected cross-cutting interceptor, isolated to this fixture's group.</summary>
[ExcludeFromDiscovery, Group("cross-cutting-probe")]
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

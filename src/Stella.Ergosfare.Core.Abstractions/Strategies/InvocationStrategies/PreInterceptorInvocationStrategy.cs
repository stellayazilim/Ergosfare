using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Runs a message's pre-interceptor stage, threading the message through each interceptor
/// in turn.
/// </summary>
/// <typeparam name="TMessage">
/// The message type the stage dispatches as; on executor paths this is the message's
/// runtime type.
/// </typeparam>
internal static class PreInterceptorInvocationStrategy<TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// Runs every pre-interceptor of the message, passing each the message the previous one
    /// returned.
    /// </summary>
    /// <param name="messageDependencies">The message's participants; supplies the stage list.</param>
    /// <param name="serviceProvider">The provider interceptors are resolved from.</param>
    /// <param name="message">The message as it enters the stage.</param>
    /// <param name="executionContext">The execution context of this dispatch.</param>
    /// <returns>The message the stage produced, for the main handler to receive.</returns>
    /// <exception cref="NotSupportedException">
    /// An interceptor in the list implements no pre-interceptor contract for
    /// <typeparamref name="TMessage"/>.
    /// </exception>
    public static async ValueTask<object> Invoke(
        IMessageDependencies messageDependencies,
        IServiceProvider serviceProvider,
        TMessage message,
        ErgosfareContext executionContext)
    {
        var interceptors = messageDependencies.PreInterceptors;
        object current = message;

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

            // The asynchronous contract is tested first, so an interceptor implementing
            // both is dispatched as asynchronous. The cast of `current` is what requires
            // each interceptor to return a TMessage.
            current = interceptor switch
            {
                IAsyncPreInterceptor<TMessage> asyncInterceptor =>
                    await asyncInterceptor.HandleAsync((TMessage)current, executionContext),
                IPreInterceptor<TMessage> syncInterceptor =>
                    syncInterceptor.Handle((TMessage)current, executionContext),
                _ => throw new NotSupportedException(
                    $"'{interceptor.GetType()}' does not implement a supported pre-interceptor contract for message '{typeof(TMessage)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
            };
        }

        return current;
    }
}

using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;


/// <summary>
/// Executes the pre-merged pre-interceptor list (direct first, then indirect) for a message,
/// dispatching every interceptor through its typed contract — asynchronous interceptors via
/// <see cref="IAsyncPreInterceptor{TMessage}"/>, synchronous ones via
/// <see cref="IPreInterceptor{TMessage}"/>. There is no object-typed bridge and no boxed
/// awaitable: `in TMessage` variance admits interceptors registered for base message types.
/// </summary>
/// <typeparam name="TMessage">The dispatch message type (the runtime type on executor paths).</typeparam>
internal sealed class PreInterceptorInvocationStrategy<TMessage>(
    IMessageDependencies messageDependencies,
    IServiceProvider serviceProvider)
    where TMessage : notnull
{
    /// <summary>
    /// Executes all pre-interceptors for the specified message.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="executionContext">The execution context for the current pipeline invocation.</param>
    /// <returns>
    /// The transformed message after all pre-interceptors have executed. Interceptors return
    /// the message as <see cref="object"/>; each subsequent interceptor receives it cast back
    /// to <typeparamref name="TMessage"/>.
    /// </returns>
    public async ValueTask<object> Invoke(TMessage message, IExecutionContext executionContext)
    {
        var interceptors = messageDependencies.PreInterceptors;
        object current = message;

        for (var i = 0; i < interceptors.Count; i++)
        {
            var interceptor = interceptors[i].Resolve(serviceProvider);

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

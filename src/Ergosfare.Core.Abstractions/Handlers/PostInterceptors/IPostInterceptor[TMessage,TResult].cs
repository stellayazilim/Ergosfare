using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a post-interceptor for a specific message and result type, executing after the main handler has processed the message.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the main handler.</typeparam>
/// <remarks>
/// This interface allows inspecting, modifying, or replacing the result of a message after it has been processed by its main handler.
/// The <see cref="Handle"/> method returns <see cref="object"/> to remain compatible with runtime-typed pipelines, 
/// while the generic parameters provide compile-time context for IntelliSense and safer casting.
/// 
/// Implementers should ensure that the returned object matches the expected <typeparamref name="TResult"/> type, 
/// otherwise an exception may be thrown at runtime.
/// </remarks>
public interface IPostInterceptor<in TMessage,in TResult>
    : IPostInterceptor 
        where TMessage : notnull 
        where TResult : notnull
{

    /// <inheritdoc cref="IPostInterceptor.Handle"/>
    object IPostInterceptor.Handle(object message, object messageResult, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult) messageResult, AmbientExecutionContext.Current);
    }

    object Handle(TMessage message, TResult messageResult, IExecutionContext context);
}
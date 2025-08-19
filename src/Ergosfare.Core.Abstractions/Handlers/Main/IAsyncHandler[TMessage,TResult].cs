
using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;


/// <summary>
///     Represents an interface for asynchronously handling messages and producing a result of type
///     <typeparamref name="TResult" />.
/// </summary>
/// <typeparam name="TMessage">Specifies the type of messages that this handler is capable of handling.</typeparam>
/// <typeparam name="TResult">
///     Specifies the type of result produced by the handler after successfully processing a
///     message.
/// </typeparam>
/// <remarks>
///     Implementations of this interface should provide the logic for handling messages of type
///     <typeparamref name="TMessage" /> in an asynchronous manner and producing a result of type
///     <typeparamref name="TResult" />. This facilitates asynchronous message processing workflows where the
///     results of the handling process are significant and utilized further.
/// </remarks>
public interface IAsyncHandler<in TMessage,  TResult>: IHandler<TMessage, Task<TResult>>
    where TMessage : notnull
{
    
    
    
    /// <summary>
    ///     Implements the Handle method from the inherited interface by calling the HandleAsync method with the message and
    ///     the current ambient execution context's cancellation token.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="context">Current execution context</param>
    /// <returns>
    ///     A task representing the asynchronous handling operation, which upon completion yields the result of the
    ///     handling process.
    /// </returns>
    /// <remarks>
    ///     This method bridges the synchronous handling method with the asynchronous handling method by internally invoking
    ///     HandleAsync, enabling a unified approach to message handling, where the Handle method can be used while taking
    ///     advantage of asynchronous processing.
    /// </remarks>
    Task<TResult> IHandler<TMessage, Task<TResult>>.Handle(TMessage message, IExecutionContext context)
    {
        return HandleAsync(message, context, AmbientExecutionContext.Current.CancellationToken);
    }

    
    /// <summary>
    ///     Defines a method to handle messages asynchronously and produce a result.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="context">Current execution context</param>
    /// <param name="cancellationToken">
    ///     A cancellation token that can be used to request the cancellation of the handling
    ///     operation, facilitating graceful shutdown scenarios or preventing resource wastage in case of long-running
    ///     operations.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous handling operation. Upon successful completion of the task, it yields the
    ///     result of the handling process, facilitating further operations or workflows based on the result.
    /// </returns>
    /// <remarks>
    ///     Implementers should define the handling logic within this method, providing asynchronous operations to process the
    ///     message effectively and produce a result that can be used in subsequent stages of the workflow.
    /// </remarks>
    Task<TResult> HandleAsync(TMessage message, IExecutionContext context, CancellationToken cancellationToken = default);

}
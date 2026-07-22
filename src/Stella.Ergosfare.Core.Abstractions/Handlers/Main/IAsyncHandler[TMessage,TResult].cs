using System.Threading.Tasks;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


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
///     <typeparamref name="TMessage" /> in an asynchronous manner, producing a
///     <see cref="ValueTask{TResult}"/>. Synchronously completing handlers pay no allocation;
///     implementations that already hold a <see cref="Task{TResult}"/> can wrap it
///     allocation-free via <c>new ValueTask&lt;TResult&gt;(task)</c>.
/// </remarks>
public interface IAsyncHandler<in TMessage,  TResult>: IHandler<TMessage, ValueTask<TResult>>
    where TMessage : notnull
{


    /// <summary>
    ///     Implements the Handle method from the inherited interface by forwarding to the
    ///     strongly-typed <see cref="HandleAsync"/> member.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="context">Current execution context</param>
    /// <returns>
    ///     A task representing the asynchronous handling operation, which upon completion yields the result of the
    ///     handling process.
    /// </returns>
    /// <remarks>
    ///     This method bridges the generic handling member with the asynchronous handling method by internally invoking
    ///     HandleAsync, enabling a unified approach to message handling.
    /// </remarks>
    ValueTask<TResult> IHandler<TMessage, ValueTask<TResult>>.Handle(TMessage message, IExecutionContext context)
    {
        return HandleAsync(message, context);
    }


    /// <summary>
    ///     Defines a method to handle messages asynchronously and produce a result.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="context">Current execution context</param>
    /// <returns>
    ///     A task representing the asynchronous handling operation. Upon successful completion of the task, it yields the
    ///     result of the handling process, facilitating further operations or workflows based on the result.
    /// </returns>
    /// <remarks>
    ///     Implementers should define the handling logic within this method, providing asynchronous operations to process the
    ///     message effectively and produce a result that can be used in subsequent stages of the workflow.
    /// </remarks>
    ValueTask<TResult> HandleAsync(TMessage message, IExecutionContext context);

}

using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Defines an asynchronous command pre-interceptor that can transform an incoming command
/// before it is executed.  
/// </summary>
/// <typeparam name="TCommand">
/// The base command type that this interceptor can process.
/// </typeparam>
/// <typeparam name="TModifiedCommand">
/// A derived command type that replaces the original command after interception.
/// Must inherit from <typeparamref name="TCommand"/>.
/// </typeparam>
public interface IAsyncCommandPreInterceptor<in TCommand, TModifiedCommand>: ICommandPreInterceptor<TCommand> 
    where TCommand : ICommand
    where TModifiedCommand : TCommand
{
    
    /// <summary>
    /// Asynchronously intercepts and optionally transforms a command before execution.  
    /// This is the explicit implementation of <see cref="IAsyncPreInterceptor{TCommand}.HandleAsync"/>  
    /// and delegates to the strongly typed <see cref="HandleAsync(TCommand, IExecutionContext)"/>.
    /// </summary>
    /// <param name="command">The command instance being intercepted.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.  
    /// The result contains the (possibly modified) command object.
    /// </returns>
    async Task<object> IAsyncPreInterceptor<TCommand>
        .HandleAsync(TCommand command, IExecutionContext context)
    {
        return await HandleAsync(command, context);
    }
    
    /// Asynchronously handles interception and transformation of the given command.  
    /// </summary>
    /// <param name="command">The original command instance.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A task that produces a <typeparamref name="TModifiedCommand"/> instance  
    /// which replaces the original command in the execution pipeline.
    /// </returns>
    new Task<TModifiedCommand> HandleAsync(TCommand command, IExecutionContext context);
}
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a type-safe pre-interceptor for commands, allowing modification of the command
/// before it enters the pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of command being intercepted. Must implement <see cref="ICommand"/>.</typeparam>
/// <typeparam name="TModifiedCommand">
/// The type of the command returned after interception. Must be assignable from <typeparamref name="TCommand"/>.
/// This allows modifying or replacing the original command in a type-safe way.
/// </typeparam>
/// <remarks>
/// This interface is the type-safe variant of <see cref="ICommandPreInterceptor"/>.
/// Use this when you want to ensure compile-time type safety while allowing the command
/// to be modified before entering the pipeline.
///
/// The <c>HandleAsync</c> method is called before the command is processed. The returned
/// <typeparamref name="TModifiedCommand"/> will continue through the pipeline as the new command.
/// </remarks>
public interface ICommandPreInterceptor<in TCommand,  TModifiedCommand>: ICommand, IAsyncPreInterceptor<TCommand>
    where TCommand : ICommand
    where TModifiedCommand : TCommand
{
    
    /// <inheritdoc cref="IAsyncPreInterceptor{TMessage}.HandleAsync(TMessage,IExecutionContext)"/>

    async Task<object> IAsyncPreInterceptor<TCommand>
        .HandleAsync(TCommand command, IExecutionContext context)
    {
        return await HandleAsync(command, context);
    }
    
    /// <summary>
    /// Asynchronously handles the pre-processing of the command.
    /// </summary>
    /// <param name="command">The command to intercept.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> producing the modified command of type <typeparamref name="TModifiedCommand"/>.
    /// </returns>
    public new Task<TModifiedCommand> HandleAsync(TCommand command, IExecutionContext context);

}
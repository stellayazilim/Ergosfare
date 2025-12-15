using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a post-processing interceptor for commands that can modify the result.
/// </summary>
/// <typeparam name="TCommand">The type of command this interceptor handles. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
/// <remarks>
/// This interceptor executes after the command has been handled, allowing inspection or modification of the produced <typeparamref name="TResult"/>.
/// 
/// This is a non-type-safe version, meaning that the pipeline internally works with <see cref="object"/>. 
/// If you want full type safety for result modification, use the type-safe version:
/// <see cref="ICommandPostInterceptor{TCommand,TResult,TModifiedResult}"/>.
/// 
/// Use this interface when you want to modify the command result without enforcing compile-time type guarantees.
/// </remarks>
public interface ICommandPostInterceptor<in TCommand, in TResult>: 
    ICommand, 
    IAsyncPostInterceptor<TCommand, TResult> 
        where TCommand : ICommand<TResult> 
        where TResult : notnull;
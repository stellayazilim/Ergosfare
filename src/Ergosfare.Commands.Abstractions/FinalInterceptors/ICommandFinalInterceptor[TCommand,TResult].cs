using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

/// <summary>
/// Represents a final interceptor for commands in the pipeline with a strongly typed result.
/// </summary>
/// <typeparam name="TCommand">The type of command this interceptor handles. Must implement <see cref="ICommand{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
/// <remarks>
/// A final interceptor is executed at the end of the pipeline, after pre-, post-, and exception interceptors.
/// It can observe the command, its result, or any exception thrown, but it should not directly modify the result.
/// 
/// Use this interface to perform logging, cleanup, or any last-step operations while preserving the type safety of the command's result.
/// </remarks>
public interface ICommandFinalInterceptor<in TCommand,in TResult> : 
    ICommand, IAsyncFinalInterceptor<TCommand, TResult>
    where TCommand : ICommand<TResult>;
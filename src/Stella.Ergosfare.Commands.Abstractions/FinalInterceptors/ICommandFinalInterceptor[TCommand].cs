using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Represents a final interceptor for commands in the pipeline.
/// </summary>
/// <typeparam name="TCommand">The type of command this interceptor handles. Must implement <see cref="ICommand"/>.</typeparam>
/// <remarks>
/// A final interceptor is always executed at the end of the pipeline, after pre-, post-, and exception interceptors.
/// It can observe the message, result, or exception, but should not modify the result directly.
/// 
/// Use this interface to perform logging, cleanup, or any last-step operations in the command pipeline.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandFinalInterceptor<in TCommand> :ICommand, IAsyncFinalInterceptor<TCommand>
    where TCommand : ICommand;
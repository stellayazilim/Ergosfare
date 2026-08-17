using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Handles failures raised while dispatching a <typeparamref name="TCommand"/>, without
/// naming the result type.
/// </summary>
/// <typeparam name="TCommand">The command type this interceptor accepts.</typeparam>
/// <remarks>
/// Use this for commands that return nothing, and for work that applies whatever the result
/// is. It must stay result-agnostic to serve a void command: those pipelines carry a
/// <see cref="System.Threading.Tasks.ValueTask"/> in their result slot, which a
/// result-typed contract would not match. For a typed result, implement
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor<in TCommand>: ICommand, IAsyncExceptionInterceptor<TCommand> where TCommand : ICommand;

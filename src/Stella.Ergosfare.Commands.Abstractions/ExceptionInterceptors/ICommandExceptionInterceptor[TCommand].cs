using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Marker interface for asynchronous exception interceptors for commands.
/// Inherits the result-agnostic <see cref="IAsyncExceptionInterceptor{TMessage}"/> and
/// <see cref="ICommand"/> to allow registration within the command module.
/// This interface does not modify the behavior or return type; interception logic
/// is handled by <see cref="IAsyncExceptionInterceptor{TMessage}"/>.
/// </summary>
/// <typeparam name="TCommand">
/// The type of command being intercepted. Must implement <see cref="ICommand"/>
/// </typeparam>
/// <remarks>
/// The result-agnostic base is deliberate: a result-typed base (the previous
/// <c>IAsyncExceptionInterceptor&lt;TCommand, object&gt;</c>) is invisible to the
/// pipeline's pattern match whenever the pipeline result is a value type — void command
/// pipelines carry a <see cref="System.Threading.Tasks.ValueTask"/> result internally, so
/// the exception stage failed with <see cref="System.NotSupportedException"/> the moment
/// it ran. For a strongly-typed result use
/// <see cref="ICommandExceptionInterceptor{TCommand, TResult}"/>.
/// </remarks>
// ReSharper disable once UnusedType.Global
public interface ICommandExceptionInterceptor<in TCommand>: ICommand, IAsyncExceptionInterceptor<TCommand> where TCommand : ICommand;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Represents a final interceptor for commands that can be registered 
/// without specifying a particular command type. 
/// </summary>
/// <remarks>
/// This is a non-type-safe version of a command final interceptor.
/// It allows intercepting the final stage of any command pipeline regardless of the concrete command type.
/// 
/// Use this interface when you want to apply final logic (e.g., logging, cleanup) across multiple command types
/// without requiring a strongly typed command.
/// 
/// For scenarios requiring type safety, prefer using the generic version:
/// <see cref="ICommandFinalInterceptor{TCommand}"/> or
/// <see cref="ICommandFinalInterceptor{TCommand,TResult}"/>.
/// </remarks>
public interface ICommandFinalInterceptor: ICommand, IAsyncFinalInterceptor<ICommand>;
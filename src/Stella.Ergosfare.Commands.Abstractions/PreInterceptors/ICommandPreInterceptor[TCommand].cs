using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Commands.Abstractions;

/// <summary>
/// Defines a pre-interceptor for a command. 
/// This interceptor allows executing logic before the command is handled.
/// </summary>
/// <typeparam name="TCommand">The type of command to intercept.</typeparam>
public interface ICommandPreInterceptor<in TCommand> : ICommand, IAsyncPreInterceptor<TCommand> where TCommand : ICommand;

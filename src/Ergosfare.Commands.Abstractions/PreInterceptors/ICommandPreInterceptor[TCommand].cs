using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Commands.Abstractions;

public interface ICommandPreInterceptor<in TCommand> : ICommand, IAsyncPreInterceptor<TCommand> where TCommand : ICommand;

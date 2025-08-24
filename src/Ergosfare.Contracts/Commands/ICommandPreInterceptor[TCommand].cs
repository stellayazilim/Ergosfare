using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Contracts;

public interface ICommandPreInterceptor<in TCommand> : ICommand, IAsyncPreInterceptor<TCommand> where TCommand : ICommand;

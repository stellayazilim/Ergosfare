using Ergosfare.Core.Abstractions;

namespace Ergosfare.Commands.Abstractions;


/// <summary>
/// Base command and a marker interface that can be registered by command module
/// </summary>
public interface ICommand: IMessage;
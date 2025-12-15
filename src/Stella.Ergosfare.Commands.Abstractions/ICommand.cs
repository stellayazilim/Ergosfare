using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Base command and a marker interface that can be registered by command module
/// </summary>
public interface ICommand: IMessage;
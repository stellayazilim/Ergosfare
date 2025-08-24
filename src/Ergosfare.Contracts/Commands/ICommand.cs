using Ergosfare.Core.Abstractions;

namespace Ergosfare.Contracts;



/// <summary>
/// Base command and a marker interface that can be registered by command module
/// </summary>
public interface ICommand: IMessage;
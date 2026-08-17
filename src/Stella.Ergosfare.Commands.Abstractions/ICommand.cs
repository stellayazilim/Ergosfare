using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Commands.Abstractions;


/// <summary>
/// Marks a type as a command: a message sent to exactly one handler to carry out an
/// operation.
/// </summary>
/// <remarks>
/// The interface declares no members. Implement <see cref="ICommand{TResult}"/> instead
/// when the caller needs a value back.
/// </remarks>
public interface ICommand: IMessage;

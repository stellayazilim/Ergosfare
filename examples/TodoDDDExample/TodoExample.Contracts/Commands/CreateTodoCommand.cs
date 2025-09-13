using Ergosfare.Contracts;

namespace TodoExample.Contracts.Commands;

public record CreateTodoCommand(string Name, bool IsCompleted) : ICommand;



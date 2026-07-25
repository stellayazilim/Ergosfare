using Ergosfare.Contracts;

namespace TodoExample.Contracts.Commands;

public record UpdateTodoCommand(string Name, bool IsCompleted): ICommand;
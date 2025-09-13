using Ergosfare.Contracts;

namespace TodoExample.Contracts.Commands;

public record DeleteTodoCommand(string Name): ICommand;
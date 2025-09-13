
using Ergosfare.Commands.Abstractions;

namespace Ergosfare.Command.Test;

public record TestCommand: ICommand;
public record TestCommandStringResult:ICommand<string>;
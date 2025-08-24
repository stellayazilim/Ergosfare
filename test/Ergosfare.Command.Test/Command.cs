
using Ergosfare.Contracts;

namespace Ergosfare.Command.Test;

public record TestCommand: ICommand;
public record TestCommandStringResult:ICommand<string>;
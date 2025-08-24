
using Ergosfare.Contracts;

namespace Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.Test;

public record TestCommand: ICommand;
public record TestCommandStringResult:ICommand<string>;
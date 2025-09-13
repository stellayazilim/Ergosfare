using Ergosfare.Commands.Abstractions;

namespace Ergosfare.Command.Test.__stubs__;

public record StubNonGenericCommand: ICommand;

public record StubNonGenericCommandStringResult: ICommand<string>;
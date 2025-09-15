using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Test.__stubs__.Messages;

public record StubMessages: IMessage;
public record StubDerivedMessage: StubMessages;
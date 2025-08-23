using Ergosfare.Core.Abstractions;

namespace Ergosfare.Core.Test.__stubs__;

public record StubNonGenericMessage: IMessage;
public record StubNonGenericMessage2 : IMessage;
public record StubNonGenericDerivedMessage: StubNonGenericMessage;
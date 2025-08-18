using Ergosfare.Test.__stubs__;
using Moq;

namespace Ergosfare.Test.__mocks__;

public static class MessageMocks
{
    public static Mock<StubMessages.StubGenericMessage<TArg>> MockStubGenericMessage<TArg>() => new();
}
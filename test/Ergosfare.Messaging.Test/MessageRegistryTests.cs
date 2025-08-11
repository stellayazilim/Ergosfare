using Ergosfare.Messaging.Abstractions.Exceptions;
using Ergosfare.Messaging.Internal.Registry;
using IMessage = Ergosfare.Messaging.Abstractions.IMessage;

namespace Ergosfare.Messaging.Test;

public class MessageRegistryTests
{

    public record InvalidMessage;
    public record ValidMessage: IMessage;
    
   


    [Fact]
    public void Register_ShouldRegisterValidMessage()
    {
        var registry = new MessageRegistry();
        
        registry.Register(typeof(ValidMessage));
        Assert.Single(registry);
    }


    [Fact]
    public void Register_ShouldThrowInvalidMessageException()
    {
        var registry = new MessageRegistry();
        Assert.Throws<InvalidMessageTypeException>(() => registry.Register(typeof(InvalidMessage)));
    }

}

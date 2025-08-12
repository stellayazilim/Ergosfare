using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Internal.Registry;
namespace Ergosfare.Core.Test;

public class MessageRegistryRecordsTests
{
    private record InvalidMessage;

    private record ValidMessage: IMessage;
    
   


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

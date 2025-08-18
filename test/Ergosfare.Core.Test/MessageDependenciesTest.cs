using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test;

public class MessageDependenciesTest
{

    [Fact]
    public void MessageDependenciesFactoryShouldCreateMessageDependencies()
    {
        // arrange 
        var serviceCollection = new ServiceCollection().BuildServiceProvider();
        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        var factory = new MessageDependenciesFactory(serviceCollection);
        // act
        var dependencies = factory.Create(typeof(StubMessages.StubNonGenericMessage), descriptor);
        
        // assert 
        Assert.NotNull(dependencies);
    }
}
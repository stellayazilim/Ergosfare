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
        var descriptor = new MessageDescriptor(typeof(HandlerStubs.StubGenericMessage));
        var factory = new MessageDependenciesFactory(serviceCollection);
        // act
        var dependencies = factory.Create(typeof(HandlerStubs.StubGenericMessage), descriptor);
        
        // assert 
        Assert.NotNull(dependencies);
    }
}
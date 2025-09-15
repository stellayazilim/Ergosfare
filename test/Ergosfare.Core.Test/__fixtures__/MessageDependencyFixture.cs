using System.ComponentModel.Design;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test.__fixtures__;

public class MessageDependencyFixture
{
    private static (IMessageDescriptor, IServiceProvider) CreateMessageDescriptor<TMessageType>(params Type[] handlerTypes)
    {
        var handlers = handlerTypes.ToList();
        var serviceCollection = new ServiceCollection();
        
        handlers.ForEach(x => serviceCollection.AddTransient(x));
        
        var serviceProvider   = serviceCollection.BuildServiceProvider();
        var factory = new HandlerDescriptorBuilderFactory();
        IEnumerable<IHandlerDescriptor> descriptors = new List<IHandlerDescriptor>();
        
        handlers.ForEach(type =>
        {
           descriptors = descriptors.Concat(factory.BuildDescriptors(type));
        });

        var messageDescriptor = new MessageDescriptor(typeof(TMessageType));
        messageDescriptor.AddDescriptors(descriptors);
        
        return (messageDescriptor, serviceProvider);
    }
    
    public static (IMessageDescriptor, IServiceProvider, IMessageDependencies) CreateMessageDependencies<TMessageType>(
        string[] groups,
        params Type[] handlerTypes
        )
    {
        var (messageDescriptor, serviceProvider) = CreateMessageDescriptor<TMessageType>(handlerTypes);
        var dependencies =  new MessageDependencies(typeof(TMessageType), messageDescriptor, serviceProvider, groups);
        ExecutionContextFixture.CreateExecutionContext();
        
        return (messageDescriptor, serviceProvider, dependencies);
    }
    
}
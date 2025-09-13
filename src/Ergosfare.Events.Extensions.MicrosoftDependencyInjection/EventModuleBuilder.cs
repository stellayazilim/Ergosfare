using System.Reflection;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

public class EventModuleBuilder(
    IMessageRegistry messageRegistry)
{

    public EventModuleBuilder Register<TEvent>() where TEvent : IEvent
    {
        Register(typeof(TEvent));
        return this;
    }

    public EventModuleBuilder Register(Type eventType)
    {
        if (!eventType.IsAssignableTo(typeof(IEvent)))
            throw new NotSupportedException($"The given type '{eventType.Name}' is not an event and cannot be registered.");
        
        messageRegistry.Register((eventType));
        return this;
    }


    public EventModuleBuilder RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes()
                     .Where( t => t.IsAssignableTo(typeof(IEvent))))
        {
            messageRegistry.Register(type);
        }
        return this;
    }
    
}
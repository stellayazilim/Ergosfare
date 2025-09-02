using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Factories;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Mediator;

namespace Ergosfare.Core.Internal.Factories;

public sealed class MessageDependenciesFactory(IServiceProvider serviceProvider): IMessageDependenciesFactory
{
    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor, IEnumerable<string> groups)
    {
        return new MessageDependencies(messageType, descriptor, serviceProvider, groups);
    }
}
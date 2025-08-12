using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Contracts;
namespace Ergosfare.Core.Abstractions;

public interface IMessageDependencies
{
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }

}

using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Test.__stubs__;




public static class HandlerStubs
{
    public record StubGenericMessage : IMessage;

    public record StubGenericCommand : ICommand<Task>;
    public record StubGenericDerivedMessage : StubGenericMessage;
    public record StubGenericMessage2 : IMessage;

    public class StubGenericHandler: IHandler<StubGenericMessage, Task>
    {
        public Task Handle(StubGenericMessage message)
        {
            return Task.CompletedTask;
        }
    }


    public class StubGenericHandler2 : IHandler<StubGenericMessage2, Task>
    {
        public Task Handle(StubGenericMessage2 message)
        {
            return Task.CompletedTask;
        }
    }
    
    
    public static LazyHandler<StubGenericHandler, IHandlerDescriptor> StubLazyHandler = new LazyHandler<StubGenericHandler, IHandlerDescriptor>
    {
        Handler = new Lazy<StubGenericHandler>(),
        Descriptor = new MainHandlerDescriptor()
        {
            ResultType = typeof(Task),
            MessageType = typeof(StubGenericMessage),
            HandlerType = typeof(StubGenericHandler)
        }
    };
}

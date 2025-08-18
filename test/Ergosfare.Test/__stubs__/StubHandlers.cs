using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Context;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Test.__stubs__;

public static class StubHandlers
{
    public class StubNonGenericHandler: IHandler<StubMessages.StubNonGenericMessage, Task>
    {
        public Task Handle(StubMessages.StubNonGenericMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    public class StubNonGenericHandlerDuplicate : StubNonGenericHandler;

    public class StubGenericHandler2 : IHandler<StubMessages.StubNonGenericMessage2, Task>
    {
        public Task Handle(StubMessages.StubNonGenericMessage2 message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    
    public static LazyHandler<StubNonGenericHandler, IHandlerDescriptor> StubLazyHandler = new LazyHandler<StubNonGenericHandler, IHandlerDescriptor>
    {
        Handler = new Lazy<StubNonGenericHandler>(),
        Descriptor = new MainHandlerDescriptor()
        {
            ResultType = typeof(Task),
            MessageType = typeof(StubMessages.StubNonGenericMessage),
            HandlerType = typeof(StubNonGenericHandler)
        }
    };
    
    
    public class StubGenericHandler<TArg>: IHandler<StubMessages.StubGenericMessage<TArg>, Task>
    {
        public Task Handle(StubMessages.StubGenericMessage<TArg> message, IExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
    

}
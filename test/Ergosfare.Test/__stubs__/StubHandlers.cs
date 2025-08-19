using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Context;
using Ergosfare.Core.Internal.Registry.Descriptors;

namespace Ergosfare.Test.__stubs__;

public static class StubHandlers
{
    
    public class StubNonGenericPreInterceptor: IPreInterceptor<StubMessages.StubNonGenericMessage>
    {
        public object Handle(StubMessages.StubNonGenericMessage message, IExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }

    public class StubNonGenericHandler: IHandler<StubMessages.StubNonGenericMessage, Task>
    {
        public Task Handle(StubMessages.StubNonGenericMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    public class StubNonGenericPostInterceptor: IPostInterceptor<StubMessages.StubNonGenericMessage,Task>
    {
        public object Handle(StubMessages.StubNonGenericMessage message, Task? messageResult, IExecutionContext context)
        {
            throw new NotImplementedException();
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
            return Task.CompletedTask;
        }
    }
    
    
    public class StubGenericPreInterceptor<TArg>: IPreInterceptor<StubMessages.StubGenericMessage<TArg>>
    {
        public object Handle(StubMessages.StubGenericMessage<TArg> message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    public class StubGenericPostInterceptor<TArg> : IPostInterceptor<StubMessages.StubGenericMessage<TArg>,Task>
    {
        public object Handle(StubMessages.StubGenericMessage<TArg> message, Task? messageResult, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    
    public class StubNonGenericExceptionInterceptor: IExceptionInterceptor<StubMessages.StubNonGenericMessage, Task>
    {
        public object Handle(StubMessages.StubNonGenericMessage message, Task? messageResult, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    public class StubGenericExceptionInterceptor<TArg>: IExceptionInterceptor<StubMessages.StubGenericMessage<TArg>, Task>
    {
        public object Handle(StubMessages.StubGenericMessage<TArg> message, Task? messageResult, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
}
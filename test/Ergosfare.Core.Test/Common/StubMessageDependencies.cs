using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;


internal static class StubHandlerDescriptors
{
    internal static IMainHandlerDescriptor GetDescriptor() => new MainHandlerDescriptor()
    {
        Weight = 1,
        Groups = [],
        MessageType = typeof(StubNonGenericMessage),
        HandlerType = typeof(StubNonGenericStringResultHandler),
        ResultType = typeof(string)
    };


}

internal class StubMessageDependencies : IMessageDependencies
{
    private Type _messageType =  typeof(StubNonGenericMessage);
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; } = new LazyHandlerCollection<IHandler, IMainHandlerDescriptor>(
    [
        new LazyHandler<IHandler, IMainHandlerDescriptor>()
        {
            Handler = new Lazy<IHandler>( () => null! ),
            Descriptor = StubHandlerDescriptors.GetDescriptor()
        }
    ]);

    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; } = new LazyHandlerCollection<IHandler, IMainHandlerDescriptor>([]);
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; } = new LazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor>([]);
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; } = new LazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor>([]);
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; } = new LazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor>([]);
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; } = new LazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor>([]);

    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors
    {
        get;
    } = new LazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor>([]);

    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors
    {
        get;
    } = new LazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor>([]);
    
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors  { get; } = new LazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor>([]);
    public  ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors  { get; } = new LazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor>([]);
}
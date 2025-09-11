using Ergosfare.Core.Events;

namespace Ergosfare.Core.EventHub;

public interface IHasProxyEvents
{
    
    
    // pipeline start event  
    public ProxyEvent<BeginPipelineEvent> BeginPipelineEvent { get; }
    
    // pre intercepting
    public ProxyEvent<BeginPreInterceptingEvent> BeginPreInterceptingEvent { get; }
    public ProxyEvent<BeginPreInterceptorInvocationEvent> BeginPreInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPreInterceptorInvocationEvent> FinishPreInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPreInterceptingWithExceptionEvent> FinishPreInterceptingWithExceptionEvent { get; }
    public ProxyEvent<FinishPreInterceptingEvent>  FinishPreInterceptingEvent { get; }
    
    
    // handler events
    public ProxyEvent<BeginHandlingEvent> BeginHandlingEvent { get; }
    public ProxyEvent<BeginHandlerInvocationEvent> BeginHandlerInvocationEvent { get; }
    public ProxyEvent<FinishHandlerInvocationEvent> FinishHandlerInvocationEvent { get; }
    public ProxyEvent<FinishHandlingWithExceptionEvent> FinishHandlingWithExceptionEvent { get; }
    public ProxyEvent<FinishHandlingEvent>  FinishHandlingEvent { get; }
    
    // post intercepting
    public ProxyEvent<BeginPostInterceptingEvent> BeginPostInterceptingEvent { get; }
    public ProxyEvent<BeginPostInterceptorInvocationEvent> BeginPostInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPostInterceptorInvocationEvent> FinishPostInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishPostInterceptingWithExceptionEvent> FinishPostInterceptingWithExceptionEvent { get; }
    public ProxyEvent<FinishPostInterceptingEvent> FinishPostInterceptingEvent { get; }
    
    // exception intercepting 
    public ProxyEvent<BeginExceptionInterceptingEvent> BeginExceptionInterceptingEvent { get; }
    public ProxyEvent<BeginExceptionInterceptorInvocationEvent> BeginExceptionInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishExceptionInterceptorInvocationEvent> FinishExceptionInterceptorInvocationEvent { get; }
    public ProxyEvent<FinishExceptionInterceptingEvent> FinishExceptionInterceptingEvent { get; }
    
    // finish pipeline
    public ProxyEvent<FinishPipelineEvent> FinishPipelineEvent { get; }
}
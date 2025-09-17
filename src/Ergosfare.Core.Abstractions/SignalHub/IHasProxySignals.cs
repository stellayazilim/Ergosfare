using Ergosfare.Core.Abstractions.SignalHub;
using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub;

public interface IHasProxySignals
{
    
    
    // pipeline start event  
    public ProxySignal<BeginPipelineSignal> BeginPipelineSignal { get; }
    
    // pre intercepting
    public ProxySignal<BeginPreInterceptingSignal> BeginPreInterceptingSignal { get; }
    public ProxySignal<BeginPreInterceptorInvocationSignal> BeginPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptorInvocationSignal> FinishPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptingWithExceptionSignal> FinishPreInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPreInterceptingSignal>  FinishPreInterceptingSignal { get; }
    
    
    // handler events
    public ProxySignal<BeginHandlingSignal> BeginHandlingSignal { get; }
    public ProxySignal<BeginHandlerInvocationSignal> BeginHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlerInvocationSignal> FinishHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlingWithExceptionSignal> FinishHandlingWithExceptionSignal { get; }
    public ProxySignal<FinishHandlingSignal>  FinishHandlingSignal { get; }
    
    // post intercepting
    public ProxySignal<BeginPostInterceptingSignal> BeginPostInterceptingSignal { get; }
    public ProxySignal<BeginPostInterceptorInvocationSignal> BeginPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptorInvocationSignal> FinishPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptingWithExceptionSignal> FinishPostInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPostInterceptingSignal> FinishPostInterceptingSignal { get; }
    
    // exception intercepting 
    public ProxySignal<BeginExceptionInterceptingSignal> BeginExceptionInterceptingSignal { get; }
    public ProxySignal<BeginExceptionInterceptorInvocationSignal> BeginExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptorInvocationSignal> FinishExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptingSignal> FinishExceptionInterceptingSignal { get; }
    
    // finish pipeline
    public ProxySignal<FinishPipelineSignal> FinishPipelineSignal { get; }
}
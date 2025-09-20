using Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Core.Abstractions.SignalHub;

/// <summary>
/// Defines a set of proxy signals for observing various pipeline events.
/// </summary>
public interface IHasProxySignals
{
    /// <summary>Pipeline start event.</summary>
    public ProxySignal<BeginPipelineSignal> BeginPipelineSignal { get; }
    
    /// <summary>Pre-intercepting events.</summary>
    public ProxySignal<BeginPreInterceptingSignal> BeginPreInterceptingSignal { get; }
    public ProxySignal<BeginPreInterceptorInvocationSignal> BeginPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptorInvocationSignal> FinishPreInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPreInterceptingWithExceptionSignal> FinishPreInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPreInterceptingSignal>  FinishPreInterceptingSignal { get; }
    
    
    /// <summary>Handler events.</summary>
    public ProxySignal<BeginHandlingSignal> BeginHandlingSignal { get; }
    public ProxySignal<BeginHandlerInvocationSignal> BeginHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlerInvocationSignal> FinishHandlerInvocationSignal { get; }
    public ProxySignal<FinishHandlingWithExceptionSignal> FinishHandlingWithExceptionSignal { get; }
    public ProxySignal<FinishHandlingSignal>  FinishHandlingSignal { get; }
    
    /// <summary>Post-intercepting events.</summary>
    public ProxySignal<BeginPostInterceptingSignal> BeginPostInterceptingSignal { get; }
    public ProxySignal<BeginPostInterceptorInvocationSignal> BeginPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptorInvocationSignal> FinishPostInterceptorInvocationSignal { get; }
    public ProxySignal<FinishPostInterceptingWithExceptionSignal> FinishPostInterceptingWithExceptionSignal { get; }
    public ProxySignal<FinishPostInterceptingSignal> FinishPostInterceptingSignal { get; }
    
    /// <summary>Exception-intercepting events.</summary>
    public ProxySignal<BeginExceptionInterceptingSignal> BeginExceptionInterceptingSignal { get; }
    public ProxySignal<BeginExceptionInterceptorInvocationSignal> BeginExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptorInvocationSignal> FinishExceptionInterceptorInvocationSignal { get; }
    public ProxySignal<FinishExceptionInterceptingSignal> FinishExceptionInterceptingSignal { get; }
    
    /// <summary>Pipeline finish event.</summary>
    public ProxySignal<FinishPipelineSignal> FinishPipelineSignal { get; }
}
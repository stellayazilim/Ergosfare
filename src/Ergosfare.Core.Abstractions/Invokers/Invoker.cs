
namespace Ergosfare.Core.Abstractions.Invokers;


/// <summary>
/// Provides a common abstract base for all invoker types, ensuring they
/// have access to shared message dependencies.
/// </summary>
internal abstract class AbstractInvoker(
    IMessageDependencies messageDependencies,
    IResultAdapterService? resultAdapterService)
{
    protected IResultAdapterService? ResultAdapterService => resultAdapterService;
    /// <summary>
    /// Gets the total number of pre-interceptors (direct + indirect) for the associated message.
    /// </summary>
    protected ushort PreInterceptorCount = (ushort)(
        messageDependencies.PreInterceptors.Count + messageDependencies.IndirectPreInterceptors.Count);
    
    /// <summary>
    /// Gets the total number of post-interceptors (direct + indirect) for the associated message.
    /// </summary>
    protected ushort PostInterceptorCount =  (ushort)(
        messageDependencies.PostInterceptors.Count + messageDependencies.IndirectPostInterceptors.Count);
    
    /// <summary>
    /// Gets the total number of exception interceptors (direct + indirect) for the associated message.
    /// </summary>
    protected ushort ExceptionInterceptorCount =  (ushort)(
        messageDependencies.ExceptionInterceptors.Count + messageDependencies.IndirectExceptionInterceptors.Count);
    
    /// <summary>
    /// Gets the total number of final interceptors (direct + indirect) for the associated message.
    /// </summary>
    protected ushort FinalInterceptorCount =  (ushort)(
        messageDependencies.FinalInterceptors.Count + messageDependencies.IndirectFinalInterceptors.Count);
    
    
    /// <summary>
    /// Gets the message dependencies that provide access to registered
    /// handlers, interceptors, and related metadata required for invocation.
    /// </summary>
    protected  IMessageDependencies MessageDependencies { get; } = messageDependencies;
}
using System.Collections.Generic;

namespace Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Describes a message type and its associated handler and interceptor descriptors.
/// </summary>
/// <remarks>
/// This interface provides a structured view of all handlers and interceptors 
/// associated with a particular message type, including:
/// <list type="bullet">
/// <item>Main handlers and indirect main handlers.</item>
/// <item>Pre-interceptors and indirect pre-interceptors.</item>
/// <item>Post-interceptors and indirect post-interceptors.</item>
/// <item>Exception interceptors and indirect exception interceptors.</item>
/// <item>Final interceptors and indirect final interceptors.</item>
/// </list>
/// The <see cref="IsGeneric"/> property indicates whether the message type is generic.
/// </remarks>
public interface IMessageDescriptor: IHasMessageType
{
    /// <summary>
    /// Gets a value indicating whether the message type is generic.
    /// </summary>
    bool IsGeneric { get; }

    /// <summary>
    /// Gets the main handlers for the message type.
    /// </summary>
    IReadOnlyCollection<IMainHandlerDescriptor> Handlers  { get; }
    
    /// <summary>
    /// Gets the indirect main handlers for the message type.
    /// These handlers apply to compatible message types (e.g., base types or interfaces).
    /// </summary>
    IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers { get; }
    
    /// <summary>
    /// Gets the pre-interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors { get; }
    
    /// <summary>
    /// Gets the indirect pre-interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    
    /// <summary>
    /// Gets the post-interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors { get; }
    
    /// <summary>
    /// Gets the indirect post-interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
    
    /// <summary>
    /// Gets the exception interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    
    /// <summary>
    /// Gets the indirect exception interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
    
    /// <summary>
    /// Gets the final interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IFinalInterceptorDescriptor> FinalInterceptors { get; }
    
    /// <summary>
    /// Gets the indirect final interceptors for the message type.
    /// </summary>
    IReadOnlyCollection<IFinalInterceptorDescriptor> IndirectFinalInterceptors { get; }
}
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Abstractions;

/// <summary>
/// Represents the collection of handlers and interceptors associated with a specific message type.
/// </summary>
public interface IMessageDependencies
{
    /// <summary>
    /// Gets the main handlers for the message.
    /// </summary>
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; } 

    /// <summary>
    /// Gets the indirect handlers for the message.
    /// </summary>
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }

    /// <summary>
    /// Gets the pre-interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }

    /// <summary>
    /// Gets the indirect pre-interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }

    /// <summary>
    /// Gets the post-interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }

    /// <summary>
    /// Gets the indirect post-interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }

    /// <summary>
    /// Gets the exception interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }

    /// <summary>
    /// Gets the indirect exception interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }

    /// <summary>
    /// Gets the final interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors { get; }

    /// <summary>
    /// Gets the indirect final interceptors for the message.
    /// </summary>
    ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors { get; }
}

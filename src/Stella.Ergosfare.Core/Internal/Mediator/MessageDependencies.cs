using Stella.Ergosfare.Contracts.Attributes;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Internal.Mediator;


/// <summary>
/// Provides lazy resolution and access to all handler dependencies for a given message type,
/// including pre/post interceptors, main handlers, exception interceptors, and final interceptors.
/// </summary>
/// <remarks>
/// This class resolves handlers using an <see cref="IServiceProvider"/> and 
/// filters them by the specified groups. Handlers are returned as lazy collections to avoid
/// unnecessary instantiation until they are required for mediation.
/// </remarks>
internal sealed class MessageDependencies : IMessageDependencies
{
    private readonly Type _messageType;
    
    private readonly IEnumerable<string> _groups;
    
    
    /// <summary>
    /// Gets the lazy collection of pre-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of indirect pre-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of main handlers for the message.
    /// </summary>
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }
    
    /// <summary>
    /// Gets the lazy collection of indirect main handlers for the message.
    /// </summary>
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    
    /// <summary>
    /// Gets the lazy collection of post-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of indirect post-interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
  
    /// <summary>
    /// Gets the lazy collection of exception interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of indirect exception interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of final interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors { get; }
    
    /// <summary>
    /// Gets the lazy collection of indirect final interceptors for the message.
    /// </summary>
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors { get; }
  
    /// <summary>
    /// Initializes a new instance of <see cref="MessageDependencies"/> for the given message type,
    /// descriptor, service provider, and groups.
    /// </summary>
    /// <param name="messageType">The type of the message for which dependencies are resolved.</param>
    /// <param name="descriptor">The message descriptor providing handler metadata.</param>
    /// <param name="serviceProvider">The service provider used to resolve handler instances.</param>
    /// <param name="groups">The groups to filter handlers by; if none provided, the default group is used.</param>
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        _messageType = messageType;
        
        var groupNames = groups.ToList();
        _groups = groupNames.Count == 0 ? [ GroupAttribute.DefaultGroupName ]: groupNames;

        // resolve pre-interceptors
        PreInterceptors = ResolveHandlers(
            descriptor.PreInterceptors, 
            handlerType => (IPreInterceptor) serviceProvider.GetRequiredService(handlerType));
        
        // resolve indirect pre-interceptors
        IndirectPreInterceptors = ResolveHandlers(
            descriptor.IndirectPreInterceptors, 
            handlerType => (IPreInterceptor) serviceProvider.GetRequiredService(handlerType));
        
        // resolve main handlers
        Handlers = ResolveHandlers(descriptor.Handlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));
        
        // resolve indirect main handlers
        IndirectHandlers = ResolveHandlers(descriptor.IndirectHandlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));

        // resolve post-interceptors
        PostInterceptors = ResolveHandlers(
            descriptor.PostInterceptors,
            handlerType => (IPostInterceptor)  serviceProvider.GetRequiredService(handlerType));
        // resolve indirect post-interceptors
        IndirectPostInterceptors = ResolveHandlers(
            descriptor.IndirectPostInterceptors,
            handlerType => (IPostInterceptor)  serviceProvider.GetRequiredService(handlerType));
        
        // resolve exception-interceptors
        ExceptionInterceptors = ResolveHandlers(
            descriptor.ExceptionInterceptors,
            handlerType => (IExceptionInterceptor)  serviceProvider.GetRequiredService(handlerType)
            );
        // resolve indirect exception-interceptors
        IndirectExceptionInterceptors = ResolveHandlers(
            descriptor.IndirectExceptionInterceptors,
            handlerType => (IExceptionInterceptor)  serviceProvider.GetRequiredService(handlerType));
        
        // resolve final-interceptors
        FinalInterceptors = ResolveHandlers(
            descriptor.FinalInterceptors,
            handlerType => (IFinalInterceptor)  serviceProvider.GetRequiredService(handlerType));
        
        // resolve indirect final-interceptors
        IndirectFinalInterceptors = ResolveHandlers(
            descriptor.IndirectFinalInterceptors,
            handlerType => (IFinalInterceptor)  serviceProvider.GetRequiredService(handlerType));
    }

    
    /// <summary>
    /// Resolves a lazy collection of handlers filtered by group and ordered by weight and type name.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TDescriptor">The descriptor type.</typeparam>
    /// <param name="descriptors">The descriptors to resolve.</param>
    /// <param name="resolveFunc">The function to create handler instances from a type.</param>
    /// <returns>A lazy read-only collection of handlers and their descriptors.</returns>
    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
            IEnumerable<TDescriptor> descriptors, 
            Func<Type, THandler> resolveFunc ) where TDescriptor : IHandlerDescriptor
    {
        return descriptors
            .OrderByDescending(d => d.Weight)
            .ThenBy(d => d.HandlerType.FullName, StringComparer.Ordinal)
            .Where(d => d.Groups.Intersect(_groups).Any())
            .Select<TDescriptor, ILazyHandler<THandler, TDescriptor>>(d => new LazyHandler<THandler, TDescriptor>
            {
                Handler = new Lazy<THandler>(() => resolveFunc(GetHandlerType(d))),
                Descriptor = d
            })
            .ToLazyReadOnlyCollection<THandler, TDescriptor>();
    }

    /// <summary>
    /// Returns the correct handler type, making it generic if the message type is generic.
    /// </summary>
    /// <param name="descriptor">The handler descriptor.</param>
    /// <returns>The concrete type to instantiate for the handler.</returns>
    private Type GetHandlerType(IHandlerDescriptor descriptor)
    {
        var handlerType = descriptor.HandlerType;

        if (descriptor.MessageType.IsGenericType)
        {
            handlerType = handlerType.MakeGenericType(_messageType.GetGenericArguments());
        }
        return handlerType;
    }
}

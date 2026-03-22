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
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, Type), Type> GenericTypeCache = new();
    public static readonly string[] DefaultGroupArray = [ GroupAttribute.DefaultGroupName ];

    private readonly Type _messageType;
    private readonly IServiceProvider _serviceProvider;
    private readonly Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor.HandlerDescriptorCache _cached;

    public bool HasInterceptors => _cached.HasInterceptors;
    
    private ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor>? _preInterceptors;
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors =>
        _preInterceptors ??= ResolveHandlers(_cached.PreInterceptors, t => (IPreInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor>? _indirectPreInterceptors;
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors =>
        _indirectPreInterceptors ??= ResolveHandlers(_cached.IndirectPreInterceptors, t => (IPreInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IHandler, IMainHandlerDescriptor>? _handlers;
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers =>
        _handlers ??= ResolveHandlers(_cached.Handlers, t => (IHandler)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IHandler, IMainHandlerDescriptor>? _indirectHandlers;
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers =>
        _indirectHandlers ??= ResolveHandlers(_cached.IndirectHandlers, t => (IHandler)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor>? _postInterceptors;
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors =>
        _postInterceptors ??= ResolveHandlers(_cached.PostInterceptors, t => (IPostInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor>? _indirectPostInterceptors;
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors =>
        _indirectPostInterceptors ??= ResolveHandlers(_cached.IndirectPostInterceptors, t => (IPostInterceptor)_serviceProvider.GetRequiredService(t));
  
    private ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor>? _exceptionInterceptors;
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors =>
        _exceptionInterceptors ??= ResolveHandlers(_cached.ExceptionInterceptors, t => (IExceptionInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor>? _indirectExceptionInterceptors;
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors =>
        _indirectExceptionInterceptors ??= ResolveHandlers(_cached.IndirectExceptionInterceptors, t => (IExceptionInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor>? _finalInterceptors;
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> FinalInterceptors =>
        _finalInterceptors ??= ResolveHandlers(_cached.FinalInterceptors, t => (IFinalInterceptor)_serviceProvider.GetRequiredService(t));
    
    private ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor>? _indirectFinalInterceptors;
    public ILazyHandlerCollection<IFinalInterceptor, IFinalInterceptorDescriptor> IndirectFinalInterceptors =>
        _indirectFinalInterceptors ??= ResolveHandlers(_cached.IndirectFinalInterceptors, t => (IFinalInterceptor)_serviceProvider.GetRequiredService(t));
  
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        _messageType = messageType;
        _serviceProvider = serviceProvider;

        _cached = ((Stella.Ergosfare.Core.Internal.Registry.Descriptors.MessageDescriptor)descriptor).GetCachedDescriptors(groups);
    }

    
    /// <summary>
    /// Resolves a lazy collection of handlers filtered by group.
    /// Descriptors are assumed to be pre-sorted.
    /// </summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <typeparam name="TDescriptor">The descriptor type.</typeparam>
    /// <param name="descriptors">The pre-sorted descriptors to resolve.</param>
    /// <param name="resolveFunc">The function to create handler instances from a type.</param>
    /// <returns>A lazy read-only collection of handlers and their descriptors.</returns>
    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
            TDescriptor[] descriptors,
            Func<Type, THandler> resolveFunc ) where TDescriptor : IHandlerDescriptor
    {
        if (descriptors.Length == 0)
        {
            return EmptyLazyHandlerCollection<THandler, TDescriptor>.Instance;
        }

        if (descriptors.Length == 1)
        {
            var d = descriptors[0];
            return new SingleLazyHandlerCollection<THandler, TDescriptor>(new LazyHandler<THandler, TDescriptor>
            {
                LazyHandlerInstance = new Lazy<THandler>(() => resolveFunc(GetHandlerType(d))),
                Descriptor = d
            });
        }

        var resultList = new List<ILazyHandler<THandler, TDescriptor>>(descriptors.Length);

        foreach (var d in descriptors)
        {
            resultList.Add(new LazyHandler<THandler, TDescriptor>
            {
                LazyHandlerInstance = new Lazy<THandler>(() => resolveFunc(GetHandlerType(d))),
                Descriptor = d
            });
        }

        return resultList.ToLazyReadOnlyCollection();
    }

    private static class EmptyLazyHandlerCollection<THandler, TDescriptor> where TDescriptor : IHandlerDescriptor
    {
        public static readonly ILazyHandlerCollection<THandler, TDescriptor> Instance = new LazyHandlerCollection<THandler, TDescriptor>([]);
    }

    /// <summary>
    /// Returns the correct handler type, making it generic if the message type is generic.
    /// </summary>
    /// <param name="descriptor">The handler descriptor.</param>
    /// <returns>The concrete type to instantiate for the handler.</returns>
    private Type GetHandlerType(IHandlerDescriptor descriptor)
    {
        var handlerType = descriptor.HandlerType;

        if (descriptor.MessageType.IsGenericType && handlerType.IsGenericTypeDefinition)
        {
            return GenericTypeCache.GetOrAdd((handlerType, _messageType), _ =>
                handlerType.MakeGenericType(_messageType.GetGenericArguments()));
        }
        return handlerType;
    }
}
